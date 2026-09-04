using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>
/// Evaluates the product order declared by a domain's authority axes and literals.
/// </summary>
public sealed class AuthorityLattice
{
    private readonly ImmutableDictionary<string, ImmutableDictionary<string, int>> _ranks;
    private readonly ImmutableDictionary<string, AuthorityDescriptor> _authorities;

    /// <summary>
    /// Initializes a validated authority product lattice.
    /// </summary>
    /// <param name="axes">Independent axes whose levels run weakest to strongest.</param>
    /// <param name="authorities">Named literals positioned on every axis.</param>
    public AuthorityLattice(
        IEnumerable<AuthorityAxisDescriptor> axes,
        IEnumerable<AuthorityDescriptor> authorities)
    {
        ArgumentNullException.ThrowIfNull(axes);
        ArgumentNullException.ThrowIfNull(authorities);

        var axisArray = axes.ToImmutableArray();
        var authorityArray = authorities.ToImmutableArray();
        Validate(axisArray, authorityArray);

        Axes = axisArray;
        Authorities = authorityArray;
        _ranks = axisArray.ToImmutableDictionary(
            axis => axis.Name,
            axis => axis.Levels
                .Select((level, rank) => (level, rank))
                .ToImmutableDictionary(item => item.level, item => item.rank, StringComparer.Ordinal),
            StringComparer.Ordinal);
        _authorities = authorityArray.ToImmutableDictionary(
            authority => authority.Name,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the independent axes that define this product order.
    /// </summary>
    public ImmutableArray<AuthorityAxisDescriptor> Axes { get; }

    /// <summary>
    /// Gets the named literals in this lattice.
    /// </summary>
    public ImmutableArray<AuthorityDescriptor> Authorities { get; }

    /// <summary>
    /// Returns whether <paramref name="grantedAuthority"/> is at least as strong
    /// as <paramref name="requiredAuthority"/> on every axis.
    /// </summary>
    public bool Satisfies(string grantedAuthority, string requiredAuthority) =>
        Satisfies(grantedAuthority, Join(requiredAuthority));

    /// <summary>
    /// Returns whether a named grant satisfies a (possibly composite) requirement.
    /// </summary>
    public bool Satisfies(string grantedAuthority, AuthorityRequirement requirement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grantedAuthority);
        ArgumentNullException.ThrowIfNull(requirement);

        var granted = Resolve(grantedAuthority);
        foreach (var axis in Axes)
        {
            if (!requirement.Coordinates.TryGetValue(axis.Name, out var requiredLevel))
            {
                continue;
            }

            var grantedRank = _ranks[axis.Name][granted.Coordinates[axis.Name]];
            var requiredRank = _ranks[axis.Name].GetValueOrDefault(requiredLevel, -1);
            if (requiredRank < 0)
            {
                throw new ArgumentException(
                    $"Requirement names unknown level '{requiredLevel}' on authority axis '{axis.Name}'.",
                    nameof(requirement));
            }

            if (grantedRank < requiredRank)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Computes the least requirement at least as strong as every named authority.
    /// </summary>
    public AuthorityRequirement Join(params string[] authorityNames) =>
        Join((IEnumerable<string>)authorityNames);

    /// <summary>
    /// Computes the least requirement at least as strong as every named authority.
    /// </summary>
    public AuthorityRequirement Join(IEnumerable<string> authorityNames)
    {
        ArgumentNullException.ThrowIfNull(authorityNames);
        var names = authorityNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

        var joined = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var axis in Axes)
        {
            var maximumRank = -1;
            string? maximumLevel = null;
            foreach (var name in names)
            {
                var authority = Resolve(name);
                var level = authority.Coordinates[axis.Name];
                var rank = _ranks[axis.Name][level];
                if (rank > maximumRank)
                {
                    maximumRank = rank;
                    maximumLevel = level;
                }
            }

            if (maximumLevel is not null)
            {
                joined[axis.Name] = maximumLevel;
            }
        }

        return new AuthorityRequirement
        {
            Coordinates = joined.ToImmutable(),
            SourceAuthorities = names,
        };
    }

    /// <summary>
    /// Resolves a named authority literal.
    /// </summary>
    public AuthorityDescriptor Resolve(string authorityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityName);
        return _authorities.TryGetValue(authorityName, out var authority)
            ? authority
            : throw new KeyNotFoundException($"Unknown authority '{authorityName}'.");
    }

    private static void Validate(
        ImmutableArray<AuthorityAxisDescriptor> axes,
        ImmutableArray<AuthorityDescriptor> authorities)
    {
        if (axes.Select(axis => axis.Name).Distinct(StringComparer.Ordinal).Count() != axes.Length)
        {
            throw new ArgumentException("Authority axis names must be unique.", nameof(axes));
        }

        if (authorities.Select(authority => authority.Name).Distinct(StringComparer.Ordinal).Count()
            != authorities.Length)
        {
            throw new ArgumentException("Authority literal names must be unique.", nameof(authorities));
        }

        foreach (var axis in axes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(axis.Name);
            if (axis.Levels.IsDefaultOrEmpty
                || axis.Levels.Any(string.IsNullOrWhiteSpace)
                || axis.Levels.Distinct(StringComparer.Ordinal).Count() != axis.Levels.Length)
            {
                throw new ArgumentException(
                    $"Authority axis '{axis.Name}' must contain distinct, non-empty levels.",
                    nameof(axes));
            }
        }

        var axisNames = axes.Select(axis => axis.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var authority in authorities)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(authority.Name);
            if (!authority.Coordinates.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(axisNames))
            {
                throw new ArgumentException(
                    $"Authority '{authority.Name}' must declare exactly one level on every axis.",
                    nameof(authorities));
            }

            foreach (var axis in axes)
            {
                if (!axis.Levels.Contains(authority.Coordinates[axis.Name], StringComparer.Ordinal))
                {
                    throw new ArgumentException(
                        $"Authority '{authority.Name}' names unknown level "
                        + $"'{authority.Coordinates[axis.Name]}' on axis '{axis.Name}'.",
                        nameof(authorities));
                }
            }
        }
    }
}

using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Immutable facts used to evaluate action predicates. A missing key is
/// unknown; an explicit null literal or <c>false</c> link value is known data.
/// </summary>
public sealed class ActionFacts
{
    /// <summary>Gets an empty, entirely unknown fact set.</summary>
    public static ActionFacts Empty { get; } = new();

    /// <summary>Initializes an immutable fact set.</summary>
    /// <param name="properties">Known property literals, keyed by ontology name.</param>
    /// <param name="links">Known link presence, keyed by ontology name.</param>
    public ActionFacts(
        IEnumerable<KeyValuePair<string, PredicateLiteral>>? properties = null,
        IEnumerable<KeyValuePair<string, bool>>? links = null)
    {
        Properties = Snapshot(properties, nameof(properties));
        Links = Snapshot(links, nameof(links));
    }

    /// <summary>Gets known property values.</summary>
    public ImmutableDictionary<string, PredicateLiteral> Properties { get; }

    /// <summary>Gets known link-presence values.</summary>
    public ImmutableDictionary<string, bool> Links { get; }

    private static ImmutableDictionary<string, TValue> Snapshot<TValue>(
        IEnumerable<KeyValuePair<string, TValue>>? values,
        string parameterName)
    {
        if (values is null)
        {
            return ImmutableDictionary<string, TValue>.Empty.WithComparers(StringComparer.Ordinal);
        }

        var builder = ImmutableDictionary.CreateBuilder<string, TValue>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Action fact names cannot be empty.", parameterName);
            }

            if (pair.Value is null)
            {
                throw new ArgumentException(
                    "Action fact values cannot be null; use PredicateLiteral.Null for an explicit null property.",
                    parameterName);
            }

            builder.Add(pair.Key, pair.Value);
        }

        return builder.ToImmutable();
    }
}

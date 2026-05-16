using System.Diagnostics.CodeAnalysis;

namespace Strategos.Ontology.Descriptors;

/// <summary>
/// Polyglot-capable description of an object type in the ontology.
/// </summary>
/// <remarks>
/// <para>
/// DR-1 (polyglot descriptor schema): identity is no longer CLR-only. A
/// descriptor must carry at least one of <see cref="ClrType"/> (.NET CLR
/// identity) or <see cref="SymbolKey"/> (SCIP moniker for cross-language
/// ingestion). The invariant is enforced at construction time when both
/// fields are explicitly set; ingestion paths always supply a SymbolKey
/// and hand-authored paths always supply a ClrType.
/// </para>
/// <para>
/// The existing positional constructor <c>(name, clrType, domainName)</c>
/// is preserved to keep hand-authored call sites compiling unchanged.
/// </para>
/// </remarks>
public sealed record ObjectTypeDescriptor
{
    private readonly Type? _clrType;
    private readonly string? _symbolKey;

    /// <summary>Canonical descriptor name (per-domain unique).</summary>
    public required string Name { get; init; }

    /// <summary>Owning domain.</summary>
    public required string DomainName { get; init; }

    /// <summary>
    /// CLR type, when known. Null for purely-ingested descriptors whose
    /// language has no loaded .NET type (e.g. TypeScript via SCIP).
    /// </summary>
    /// <remarks>
    /// The DR-1 identity invariant (at least one of <see cref="ClrType"/>
    /// or <see cref="SymbolKey"/> must be non-null) is enforced in the
    /// <see cref="SymbolKey"/> init setter for the explicit-set cases.
    /// The "neither field set" bypass — where the parameterless property-
    /// init constructor runs and neither <see cref="ClrType"/> nor
    /// <see cref="SymbolKey"/> appears in the object initializer — is
    /// caught at <c>OntologyBuilder.ObjectTypeFromDescriptor</c> and
    /// <c>ApplyDelta</c>, the two surfaces through which descriptors
    /// reach the composed graph.
    /// </remarks>
    public Type? ClrType
    {
        get => _clrType;
        init => _clrType = value;
    }

    /// <summary>
    /// SCIP moniker, when known. Mechanical truth for cross-language
    /// identity; wins over <see cref="ClrType"/> at merge time per
    /// basileus ADR §9.2.
    /// </summary>
    /// <remarks>
    /// The DR-1 identity invariant (at least one of <see cref="ClrType"/>
    /// or <see cref="SymbolKey"/> must be non-null) is enforced in the
    /// <see cref="SymbolKey"/> init setter. By convention this property is
    /// the "polyglot escape hatch" — callers using the property-init form
    /// to construct an ingested descriptor always set <see cref="SymbolKey"/>;
    /// callers using the positional <c>(name, clrType, domainName)</c>
    /// constructor satisfy the invariant trivially without touching the
    /// init setters.
    /// </remarks>
    public string? SymbolKey
    {
        get => _symbolKey;
        init
        {
            _symbolKey = value;
            if (_clrType is null && _symbolKey is null)
            {
                throw new InvalidOperationException(
                    "ObjectTypeDescriptor violates the DR-1 identity invariant: "
                    + "at least one of ClrType or SymbolKey must be non-null. "
                    + "Hand-authored descriptors must supply ClrType; ingested descriptors must supply SymbolKey.");
            }
        }
    }

    /// <summary>
    /// Language-formatted fully-qualified name; informational and used
    /// for diagnostic messages.
    /// </summary>
    public string? SymbolFqn { get; init; }

    /// <summary>
    /// Language identifier per SCIP convention (e.g. <c>"dotnet"</c>,
    /// <c>"typescript"</c>). Defaults to <c>"dotnet"</c> for hand-authored
    /// descriptors.
    /// </summary>
    public string LanguageId { get; init; } = "dotnet";

    /// <summary>Record-level provenance — hand-authored vs. ingested.</summary>
    public DescriptorSource Source { get; init; } = DescriptorSource.HandAuthored;

    /// <summary>
    /// Identifier of the <c>IOntologySource</c> that contributed this
    /// descriptor, when <see cref="Source"/> is <see cref="DescriptorSource.Ingested"/>.
    /// </summary>
    public string? SourceId { get; init; }

    /// <summary>Wall-clock instant of ingestion, when applicable.</summary>
    public DateTimeOffset? IngestedAt { get; init; }

    /// <summary>Property-init constructor; identity invariant is enforced via the init setters.</summary>
    public ObjectTypeDescriptor()
    {
    }

    /// <summary>
    /// Backward-compatible positional constructor. Hand-authored
    /// <c>DomainOntology.Define()</c> sites always carry a CLR type, so
    /// the invariant is trivially satisfied.
    /// </summary>
    [SetsRequiredMembers]
    public ObjectTypeDescriptor(string name, Type clrType, string domainName)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        Name = name;
        DomainName = domainName;
        _clrType = clrType;
    }

    public PropertyDescriptor? KeyProperty { get; init; }

    public IReadOnlyList<PropertyDescriptor> Properties { get; init; } = [];

    public IReadOnlyList<LinkDescriptor> Links { get; init; } = [];

    public IReadOnlyList<ActionDescriptor> Actions { get; init; } = [];

    public IReadOnlyList<EventDescriptor> Events { get; init; } = [];

    public IReadOnlyList<InterfaceDescriptor> ImplementedInterfaces { get; init; } = [];

    public LifecycleDescriptor? Lifecycle { get; init; }

    public IReadOnlyList<InterfaceActionMapping> InterfaceActionMappings { get; init; } = [];

    public IReadOnlyList<ExternalLinkExtensionPoint> ExternalLinkExtensionPoints { get; init; } = [];

    public IReadOnlyList<InterfacePropertyMapping> InterfacePropertyMappings { get; init; } = [];

    public ObjectKind Kind { get; init; } = ObjectKind.Entity;

    public Type? ParentType { get; init; }

    public string? ParentTypeName { get; init; }
}

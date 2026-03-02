namespace Strategos.Ontology.Descriptors;

public sealed record ObjectTypeDescriptor(
    string Name,
    Type ClrType,
    string DomainName)
{
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

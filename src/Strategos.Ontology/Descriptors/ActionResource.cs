namespace Strategos.Ontology.Descriptors;

/// <summary>A stable, local resource identifier in an action's declared frame.</summary>
public sealed record ActionResource(ActionResourceKind Kind, string Name)
{
    public static ActionResource Property(string propertyName) => new(ActionResourceKind.Property, propertyName);

    public static ActionResource Link(string linkName) => new(ActionResourceKind.Link, linkName);

    public static ActionResource Event(string eventTypeName) => new(ActionResourceKind.Event, eventTypeName);

    public static ActionResource External(string resourceName) => new(ActionResourceKind.External, resourceName);
}

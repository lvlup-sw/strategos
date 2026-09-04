namespace Strategos.Ontology.Descriptors;

/// <summary>A stable, local resource identifier in an action's declared frame.</summary>
/// <param name="Kind">The resource family.</param>
/// <param name="Name">The resource name.</param>
public sealed record ActionResource(ActionResourceKind Kind, string Name)
{
    /// <summary>Creates a property resource.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The resource identifier.</returns>
    public static ActionResource Property(string propertyName)
    {
        return new ActionResource(ActionResourceKind.Property, propertyName);
    }

    /// <summary>Creates a link resource.</summary>
    /// <param name="linkName">The link name.</param>
    /// <returns>The resource identifier.</returns>
    public static ActionResource Link(string linkName)
    {
        return new ActionResource(ActionResourceKind.Link, linkName);
    }

    /// <summary>Creates an event resource.</summary>
    /// <param name="eventTypeName">The event type name.</param>
    /// <returns>The resource identifier.</returns>
    public static ActionResource Event(string eventTypeName)
    {
        return new ActionResource(ActionResourceKind.Event, eventTypeName);
    }

    /// <summary>Creates an external resource.</summary>
    /// <param name="resourceName">The external resource name.</param>
    /// <returns>The resource identifier.</returns>
    public static ActionResource External(string resourceName)
    {
        return new ActionResource(ActionResourceKind.External, resourceName);
    }
}

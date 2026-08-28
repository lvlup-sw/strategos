using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Internal;

/// <summary>
/// Shared AONT205 field scan. Mechanical ingestion
/// (<see cref="DescriptorSource.Ingested"/>) may not contribute
/// intent-only collections; <see cref="DescriptorSource.HandAuthored"/>
/// and <see cref="DescriptorSource.HandAuthoredContract"/> pass through.
/// </summary>
internal static class IngestedIntentInvariant
{
    /// <summary>
    /// Returns the first intent-only field name that a mechanical
    /// ingester populated, or <c>null</c> when the descriptor is not
    /// ingested or carries no intent.
    /// </summary>
    internal static string? FindOffendingField(ObjectTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.Source != DescriptorSource.Ingested)
        {
            return null;
        }

        if (descriptor.Actions.Count > 0)
        {
            return "Actions";
        }

        if (descriptor.Events.Count > 0)
        {
            return "Events";
        }

        if (descriptor.Lifecycle is not null)
        {
            return "Lifecycle";
        }

        if (descriptor.InterfaceActionMappings.Count > 0)
        {
            return "InterfaceActionMappings";
        }

        if (descriptor.ExternalLinkExtensionPoints.Count > 0)
        {
            return "ExternalLinkExtensionPoints";
        }

        return null;
    }
}

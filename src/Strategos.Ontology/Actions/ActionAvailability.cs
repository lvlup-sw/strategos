namespace Strategos.Ontology.Actions;

/// <summary>The availability of an ontology action under the supplied facts.</summary>
public enum ActionAvailability
{
    /// <summary>Every hard requirement is known to be satisfied.</summary>
    Available,

    /// <summary>At least one hard requirement is known to be unsatisfied.</summary>
    Unavailable,

    /// <summary>No hard requirement is false, but at least one cannot be decided.</summary>
    Indeterminate,
}

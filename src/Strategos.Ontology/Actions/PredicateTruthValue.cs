namespace Strategos.Ontology.Actions;

/// <summary>The three-valued outcome of evaluating an action predicate.</summary>
public enum PredicateTruthValue
{
    /// <summary>The predicate is known to hold.</summary>
    Satisfied,

    /// <summary>The predicate is known not to hold.</summary>
    Unsatisfied,

    /// <summary>The available facts are insufficient to decide the predicate.</summary>
    Indeterminate,
}

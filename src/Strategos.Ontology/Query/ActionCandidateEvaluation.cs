using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Query;

/// <summary>Three-valued discovery result for one ontology action.</summary>
/// <param name="Action">The evaluated action.</param>
/// <param name="Availability">Whether the hard requirements permit the action.</param>
/// <param name="Constraints">Individual hard and soft requirement outcomes.</param>
public sealed record ActionCandidateEvaluation(
    ActionDescriptor Action,
    ActionAvailability Availability,
    IReadOnlyList<ConstraintEvaluation> Constraints);

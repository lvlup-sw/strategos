// Copyright (c) Levelup Software. All rights reserved.

using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Query;

public sealed record ActionConstraintReport(
    ActionDescriptor Action,
    ActionAvailability Availability,
    IReadOnlyList<ConstraintEvaluation> Constraints)
{
    /// <summary>Gets whether every hard constraint is known to be satisfied.</summary>
    public bool IsAvailable => Availability == ActionAvailability.Available;
}

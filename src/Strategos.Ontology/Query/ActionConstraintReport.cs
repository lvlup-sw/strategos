// Copyright (c) Levelup Software. All rights reserved.

using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Query;

public sealed record ActionConstraintReport(
    ActionDescriptor Action,
    bool IsAvailable,
    IReadOnlyList<ConstraintEvaluation> Constraints);

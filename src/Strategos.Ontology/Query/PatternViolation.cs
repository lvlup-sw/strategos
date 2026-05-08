// Copyright (c) Levelup Software. All rights reserved.

namespace Strategos.Ontology.Query;

public sealed record PatternViolation(
    string PatternName,
    string Description,
    OntologyNodeRef Subject,
    ViolationSeverity Severity);

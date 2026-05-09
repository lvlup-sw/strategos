// Copyright (c) Levelup Software. All rights reserved.

namespace Strategos.Ontology.Query;

/// <summary>
/// A structural violation detected against the ontology graph for a design
/// intent (for example, a write to a computed property or a missing
/// extension point).
/// </summary>
/// <param name="PatternName">Stable identifier for the violated pattern.</param>
/// <param name="Description">Human-readable explanation of the violation.</param>
/// <param name="Subject">The ontology node the violation applies to.</param>
/// <param name="Severity">Whether the violation is a warning or an error.</param>
public sealed record PatternViolation(
    string PatternName,
    string Description,
    OntologyNodeRef Subject,
    ViolationSeverity Severity);

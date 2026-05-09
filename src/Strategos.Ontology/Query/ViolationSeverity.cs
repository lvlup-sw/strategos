// Copyright (c) Levelup Software. All rights reserved.

namespace Strategos.Ontology.Query;

/// <summary>
/// Severity classification for a <see cref="PatternViolation"/>.
/// </summary>
public enum ViolationSeverity
{
    /// <summary>
    /// Advisory: the design intent is allowed to proceed but should be reviewed.
    /// </summary>
    Warning,

    /// <summary>
    /// Blocking: the design intent should be rejected until the violation is resolved.
    /// </summary>
    Error,
}

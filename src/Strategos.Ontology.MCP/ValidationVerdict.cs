using Strategos.Ontology.Actions;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Result of validating a <see cref="DesignIntent"/> against the ontology graph.
/// Carries hard violations (blocking) and soft warnings (advisory) split out
/// by <see cref="Strategos.Ontology.Descriptors.ConstraintStrength"/>, plus the
/// blast-radius estimate, structural pattern violations, and an optional
/// coverage report (populated only if an <see cref="IOntologyCoverageProvider"/>
/// is registered).
/// </summary>
public sealed record ValidationVerdict(
    bool Passed,
    IReadOnlyList<ConstraintEvaluation> HardViolations,
    IReadOnlyList<ConstraintEvaluation> SoftWarnings,
    BlastRadius BlastRadius,
    IReadOnlyList<PatternViolation> PatternViolations,
    CoverageReport? Coverage);

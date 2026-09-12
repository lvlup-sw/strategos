import "@typespec/json-schema";
using JsonSchema;
namespace LevelUp.Strategos.Contracts;

/** Invariant axis in the consumer catalog. */
enum InvariantAxis { substrate, authoring }
/** Loading policy for catalog entries. */
enum InvariantLoadCost { `always-load`, `reference-only`, archivable }
/** Integrity boundary guarded by an invariant. */
enum InvariantIntegrityClass { substrate, sdlc, authoring, user }
/** Catalog phase affinity vocabulary. */
enum InvariantPhase { plan, delegate, review, synthesize }
/** Catalog workflow affinity vocabulary. */
enum InvariantWorkflow { feature, debug, refactor, discovery, oneshot }
/** Violation severity, including context overrides. */
enum InvariantSeverityLevel { blocking, advisory }
/** Context keys are consumer-defined; values use the closed severity vocabulary. */
model InvariantSeverityOverrides is Record<InvariantSeverityLevel>;

/** Default severity and optional context-specific overrides; evaluation belongs to the consumer. */
@extension("additionalProperties", false)
model InvariantSeverity {
  default: InvariantSeverityLevel;
  `by-workflow`?: InvariantSeverityOverrides;
  `by-phase`?: InvariantSeverityOverrides;
}

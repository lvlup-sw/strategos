import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Wire-IR step configuration (issue #50). Placeholder shape — the full
 * confidence / retry / compensation / validation sub-definitions land in T16.
 */
@jsonSchema
model StepConfigurationDefinition {
  /** Confidence threshold below which the low-confidence handler fires, if set. */
  confidenceThreshold?: float64;
}

import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Wire-IR step definition (issue #50). Placeholder shape — the 5-kind
 * discriminated union (`skill | handler | gate | delegate | approval`) and the
 * reserved `runtime` slot land in T14.
 */
@jsonSchema
model StepDefinition {
  /** Stable step identifier. */
  stepId: string;

  /** Step name (the builder's `StepDefinition.StepName`). */
  stepName: string;
}

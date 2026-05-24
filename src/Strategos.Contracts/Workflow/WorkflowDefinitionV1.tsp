import "@typespec/json-schema";
import "./StepDefinition.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Wire-IR root for a serialized Strategos workflow (issue #50).
 *
 * This is the **wire contract** — the flat, language-neutral document that
 * cross-product consumers (Exarchos Zod, Basileus, visualization, schema diff)
 * read. It is the export-only projection target of the in-memory builder IR
 * `WorkflowDefinition<TState>` (see `WorkflowDefinitionProjection.ToContract`):
 * the projection drops executable code (LB-1) and reduces CLR `System.Type`
 * handles to simple-name string monikers (LB-2).
 *
 * `schemaVersion` is pinned to the literal `"1.0"`. Future minors are
 * additive-only; a breaking change requires a `WorkflowDefinitionV2` root.
 */
@jsonSchema
model WorkflowDefinitionV1 {
  /** Wire-IR schema version. Pinned literal; additive minors, breaking ⇒ V2. */
  schemaVersion: "1.0";

  /** Workflow name (the IR identity, matches the builder's `WorkflowDefinition.Name`). */
  @minLength(1)
  name: string;

  /** Ordered step definitions (the discriminated `StepDefinition` union). */
  steps: StepDefinition[];

  /** Step id of the workflow entry step, if set. */
  entryStepId?: string;

  /** Step id of the workflow terminal step, if set. */
  terminalStepId?: string;
}

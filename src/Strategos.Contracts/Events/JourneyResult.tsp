import "@typespec/json-schema";
import "./IntentEnvelope.tsp";
import "./MergeGateDecision.tsp";
import "../Workflow/WorkflowRef.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * S1 semantic-merge-queue **JourneyResult** response envelope (#63).
 *
 * Reports the outcome of having run one or more workflow journeys. Like
 * `MergeGateDecision` it composes the shared S2 base blocks — `_meta` (degraded
 * seam) and `_perf` (telemetry) — types its journey outcomes as `WorkflowRef`,
 * and carries the discriminated `next_actions` list.
 *
 * When `_meta.degraded` is true the result is advisory: consumers short-circuit
 * "use the result" logic (the observability seam — see `ResponseMetaV1`).
 */
@jsonSchema
model JourneyResult {
  /** Shared response metadata block (degraded seam). */
  _meta: ResponseMetaV1;

  /** Shared performance telemetry block. */
  _perf: PerfMetaV1;

  /** The journeys whose outcomes this result reports, typed as `WorkflowRef`. */
  journeyOutcomes: WorkflowRef[];

  /** Ordered next actions for the consumer, as a discriminated `NextAction` union. */
  nextActions: NextAction[];
}

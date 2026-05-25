import "@typespec/json-schema";
import "./IntentEnvelope.tsp";
import "./MergeGateDecision.tsp";
import "../Workflow/WorkflowRef.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * The outcome status of a journey run (#63). Member names are PascalCase; wire
 * VALUES are the exact snake_case tokens cross-repo consumers match on. Reused
 * both as the overall `JourneyResult.outcome` and per-journey on `JourneyOutcome`.
 */
@jsonSchema
enum JourneyOutcomeStatus {
  /** Every journey passed. */
  AllPassed: "all_passed",

  /** Some journeys passed and some failed. */
  Partial: "partial",

  /** Every journey failed. */
  AllFailed: "all_failed",

  /** The journeys were not executed. */
  NotExecuted: "not_executed",
}

/**
 * A per-journey outcome record (#63): the catalog identity of the journey that
 * ran, its individual outcome status, and a reference to the evidence it produced.
 */
@jsonSchema
model JourneyOutcome {
  /** Catalog workflow identity of the journey that ran. */
  @minLength(1)
  workflowId: string;

  /** Pinned catalog version of the journey that ran. */
  @minLength(1)
  catalogVersion: string;

  /** The outcome of this individual journey. */
  outcome: JourneyOutcomeStatus;

  /** Reference to the evidence (logs/artifacts) this journey produced. */
  @minLength(1)
  evidenceRef: string;
}

/**
 * Token/cost accounting for a journey run (#63). Token counters are int32 (not
 * int64) to avoid JSON Schema string coercion; `costUsd` is an optional float.
 */
@jsonSchema
model BudgetConsumedV1 {
  /** Input (prompt) tokens consumed across the journey run. */
  @minValue(0)
  inputTokens: int32;

  /** Output (completion) tokens produced across the journey run. */
  @minValue(0)
  outputTokens: int32;

  /** Prompt-cache read tokens served across the journey run. */
  @minValue(0)
  cacheReadTokens: int32;

  /** Optional dollar cost of the journey run. */
  @minValue(0)
  costUsd?: float64;
}

/**
 * S1 semantic-merge-queue **JourneyResult** response envelope (#63).
 *
 * Reports the outcome of having run one or more workflow journeys. Like
 * `MergeGateDecision` it composes the SMQ `_meta` (`MergeQueueMetaV1`) and `_perf`
 * blocks. It carries the overall `outcome`, a per-journey `journeyOutcomes` list
 * (typed `JourneyOutcome`, not bare `WorkflowRef`), token/cost accounting in
 * `budgetConsumed`, a `provenanceRef` to the G3 provenance envelope, and the
 * shared verb-discriminated `next_actions` list.
 *
 * When `_meta.degraded` is true the result is advisory: consumers short-circuit
 * "use the result" logic (the observability seam — see `ResponseMetaV1`).
 */
@jsonSchema
model JourneyResult {
  /** The overall outcome across all journeys. */
  outcome: JourneyOutcomeStatus;

  /** Per-journey outcomes, typed as `JourneyOutcome` records. */
  journeyOutcomes: JourneyOutcome[];

  /** Token/cost accounting for the journey run. */
  budgetConsumed: BudgetConsumedV1;

  /** Reference to the G3 provenance envelope for this run. */
  provenanceRef: string;

  /** Shared SMQ response metadata block (degraded seam + merge context). */
  _meta: MergeQueueMetaV1;

  /** Shared performance telemetry block. */
  _perf: PerfMetaV1;

  /** Ordered next actions for the consumer, as a verb-discriminated `NextAction` union. */
  nextActions: NextAction[];
}

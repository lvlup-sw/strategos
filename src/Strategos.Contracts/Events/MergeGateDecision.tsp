import "@typespec/json-schema";
import "./IntentEnvelope.tsp";
import "../Workflow/WorkflowRef.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * The merge-gate decision verdict (#63). Member names are PascalCase; the wire
 * VALUES are the exact snake_case tokens cross-repo consumers match on.
 */
@jsonSchema
enum MergeDecision {
  /** Skip semantic e2e — the change is low-risk; proceed to merge. */
  Skip: "skip",

  /** Run the full semantic e2e journey set before merging. */
  RunE2e: "run_e2e",

  /** Run a focused subset of the e2e journeys before merging. */
  RunE2eFocused: "run_e2e_focused",

  /** Escalate to a human — the gate cannot decide autonomously. */
  EscalateHuman: "escalate_human",
}

/**
 * How the diff under evaluation was classified (#63). Member names are
 * PascalCase; wire VALUES are the exact snake_case tokens.
 */
@jsonSchema
enum DiffClassification {
  /** Documentation-only change. */
  Docs: "docs",

  /** Test-only change. */
  TestOnly: "test_only",

  /** A refactor with no behavioral change. */
  Refactor: "refactor",

  /** A user-facing feature change. */
  Feature: "feature",

  /** Infrastructure / build / CI change. */
  Infra: "infra",

  /** A schema / contract change. */
  Schema: "schema",

  /** A configuration change. */
  Config: "config",

  /** A mixed change spanning multiple classifications. */
  Mixed: "mixed",
}

/**
 * Why the gate fell back to a non-autonomous decision (#63). A subset of the
 * model-side degradation reasons. Member names are PascalCase; wire VALUES are
 * the exact snake_case tokens.
 */
@jsonSchema
enum FallbackReason {
  /** The model did not return a response within its deadline. */
  ModelTimeout: "model_timeout",

  /** The model endpoint returned a 5xx server error. */
  Model5xx: "model_5xx",

  /** The producer was rate-limited by the model endpoint. */
  RateLimit: "rate_limit",

  /** The model returned output that could not be parsed into a decision. */
  MalformedOutput: "malformed_output",

  /** The model's confidence was below the actionable threshold. */
  LowConfidence: "low_confidence",

  /** The semantic judge could not be reached. */
  JudgeUnavailable: "judge_unavailable",
}

/**
 * Typed params for the `run_buildkite_pipeline` next-action (#63): the journeys
 * to run, each a typed `WorkflowRef` (no string indirection).
 */
model BuildkitePipelineParams {
  /** The journeys to run in the pipeline, typed as `WorkflowRef` (INV-8). */
  journeys: WorkflowRef[];
}

/**
 * `next_actions` branch (#63): run a Buildkite pipeline over a set of journeys.
 * Verb-discriminated; carries its typed params directly.
 */
model RunBuildkitePipelineAction {
  /** Discriminator: run a Buildkite pipeline. */
  verb: "run_buildkite_pipeline";

  /** The pipeline params (the journeys to run). */
  params: BuildkitePipelineParams;
}

/**
 * `next_actions` branch (#63): escalate to a human. Mirrors the `escalate_human`
 * decision — terminal, no journey to run, carries a human-readable reason.
 */
model EscalateHumanAction {
  /** Discriminator: escalate to a human. */
  verb: "escalate_human";

  /** Why the action escalates to a human. */
  @minLength(1)
  reason: string;
}

/**
 * The discriminated `next_actions` pattern (#63): a union of TYPED action
 * branches keyed on `verb`. Each branch carries its own structured payload rather
 * than a string-typed indirection, so a consumer routes on the discriminator and
 * gets the branch's fields directly.
 */
@jsonSchema
union NextAction {
  run_buildkite_pipeline: RunBuildkitePipelineAction,
  escalate_human: EscalateHumanAction,
}

/**
 * S1 semantic-merge-queue **MergeGateDecision** response envelope (#63).
 *
 * The decision the CI coordinator (Exarchos) receives from the semantic judge
 * (Basileus AgentHost). It composes the SMQ `_meta` (`MergeQueueMetaV1`, degraded
 * seam + merge context) and `_perf` (telemetry) blocks, classifies the diff,
 * types its suggested journeys as `WorkflowRef`, and carries the verb-discriminated
 * `next_actions` list.
 *
 * When `_meta.degraded` is true the decision is advisory: consumers short-circuit
 * "use the decision" logic (the observability seam — see `ResponseMetaV1`).
 */
@jsonSchema
model MergeGateDecision {
  /** Wire schema version literal. */
  schemaVersion: "merge-gate.v1";

  /** The gate's verdict. */
  decision: MergeDecision;

  /** Model confidence in the decision, in the unit interval. */
  @minValue(0)
  @maxValue(1)
  confidence: float64;

  /** Human-readable rationale for the decision. */
  rationale: string;

  /** How the evaluated diff was classified. */
  diffClassification: DiffClassification;

  /** Risk signals the gate surfaced (free-form monikers). */
  riskSignals: string[];

  /** Journeys the gate suggests running, typed as `WorkflowRef` (INV-8). */
  suggestedJourneys: WorkflowRef[];

  /** Why the gate fell back to a non-autonomous decision (present only on fallback). */
  fallbackReason?: FallbackReason;

  /** The prompt that produced this decision. */
  promptId: string;

  /** The model that produced this decision. */
  modelId: string;

  /** Shared SMQ response metadata block (degraded seam + merge context). */
  _meta: MergeQueueMetaV1;

  /** Shared performance telemetry block. */
  _perf: PerfMetaV1;

  /** Ordered next actions for the consumer, as a verb-discriminated `NextAction` union. */
  nextActions: NextAction[];
}

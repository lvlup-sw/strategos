import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Closed enum of reasons a semantic-merge-queue response is **degraded** (#64).
 *
 * Member names are PascalCase; the wire VALUES are the exact snake_case tokens
 * cross-repo consumers match on. Exarchos's TypeScript enum and Basileus's C#
 * enum round-trip against these **by value** (INV-8 polyglot identity) — never by
 * ordinal. Adding a reason is an additive minor; renaming or removing one is a
 * breaking major.
 */
@jsonSchema
enum DegradedReason {
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

  /** The token/cost budget for the evaluation was exhausted. */
  BudgetExhausted: "budget_exhausted",

  /** The execution sandbox could not be reached or provisioned. */
  SandboxUnavailable: "sandbox_unavailable",
}

/**
 * Shared `_meta` block for every S-series response envelope (#64).
 *
 * `degraded == true` is the OBSERVABILITY SEAM: it instructs downstream consumers
 * to **short-circuit any "use the decision" logic** and fall back. The fallback
 * must be visible, never silent — a degraded response is still a well-formed
 * envelope, but its decision payload is advisory at best. `degradedReason` names
 * *why* the response degraded when `degraded` is true; it is absent otherwise.
 */
@jsonSchema
model ResponseMetaV1 {
  /**
   * When true, the producer fell back: downstream consumers MUST NOT treat the
   * decision as authoritative and MUST short-circuit "use the decision" logic.
   */
  degraded: boolean;

  /** Why the response degraded (present only when `degraded` is true). */
  degradedReason?: DegradedReason;
}

/**
 * The semantic-merge-queue `_meta` block (#63). Spreads the universal
 * `ResponseMetaV1` degraded seam and adds the merge-context fields that pin a
 * response to a specific merge attempt: the head/base SHAs, the merge group, and
 * the evaluator tier (Tier 1 merge-gate / Tier 2 e2e) that produced it.
 */
@jsonSchema
model MergeQueueMetaV1 {
  ...ResponseMetaV1;

  /** The commit SHA at the head of the change under evaluation. */
  @minLength(1)
  headSha: string;

  /** The base commit SHA the change merges into. */
  @minLength(1)
  baseSha: string;

  /** The merge group / queue batch this evaluation belongs to. */
  @minLength(1)
  mergeGroupId: string;

  /** The evaluator tier that produced the response (e.g. Tier 1 merge-gate, Tier 2 e2e). */
  @minLength(1)
  evaluatorTier: string;
}

/**
 * Shared `_perf` block for every S-series response envelope (#63/#64).
 *
 * Carries coarse performance telemetry for the response: wall-clock `ms` and the
 * token accounting the producer measured. All counters are int32 (not int64) to
 * avoid JSON Schema string coercion.
 */
@jsonSchema
model PerfMetaV1 {
  /** Wall-clock duration the producer took to compute the response, milliseconds. */
  @minValue(0)
  ms: int32;

  /** Input (prompt) tokens consumed while computing the response. */
  @minValue(0)
  inputTokens: int32;

  /** Output (completion) tokens produced while computing the response. */
  @minValue(0)
  outputTokens: int32;

  /** Prompt-cache read tokens served while computing the response. */
  @minValue(0)
  cacheReadTokens: int32;
}

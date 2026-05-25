import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Closed enum of reasons a semantic-merge-queue response is **degraded** (#64).
 *
 * Member names are the contract Exarchos's TypeScript enum and Basileus's C# enum
 * round-trip against **by name** (INV-8 polyglot identity) — never by ordinal. The
 * wire monikers are kebab-case. Adding a reason is an additive minor; renaming or
 * removing one is a breaking major.
 */
@jsonSchema
enum DegradedReason {
  /** A downstream dependency (judge, catalog, AgentHost) could not be reached. */
  DownstreamUnreachable: "downstream-unreachable",

  /** The semantic judge did not return a decision within its deadline. */
  JudgeTimeout: "judge-timeout",

  /** The judge returned output that could not be parsed into a decision. */
  MalformedOutput: "malformed-output",
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
 * Shared `_perf` block for every S-series response envelope (#64).
 *
 * Carries coarse performance telemetry for the response: wall-clock `ms`, and
 * optional `bytes`/`tokens` accounting where the producer measured them. All
 * counters are int32 (not int64) to avoid JSON Schema string coercion.
 */
@jsonSchema
model PerfMetaV1 {
  /** Wall-clock duration the producer took to compute the response, milliseconds. */
  @minValue(0)
  ms: int32;

  /** Optional byte count processed while computing the response. */
  @minValue(0)
  bytes?: int32;

  /** Optional token count consumed while computing the response. */
  @minValue(0)
  tokens?: int32;
}

import "@typespec/json-schema";
import "./IntentEnvelope.tsp";
import "../Workflow/WorkflowRef.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * `next_actions` branch (#63): run a catalog/authored journey. Carries a TYPED
 * `WorkflowRef` — no string indirection. The consumer (Basileus AgentHost)
 * resolves the ref and runs the journey.
 */
model RunJourneyAction {
  /** Discriminator: run a workflow journey. */
  kind: "run-journey";

  /** The journey to run, typed as a WorkflowRef (catalog or authored). */
  journey: WorkflowRef;
}

/**
 * `next_actions` branch (#63): block the merge. Carries a human-readable reason
 * (not a typed ref — blocking is terminal, there is no journey to run).
 */
model BlockAction {
  /** Discriminator: block the merge. */
  kind: "block";

  /** Why the merge is blocked. */
  @minLength(1)
  reason: string;
}

/**
 * The discriminated `next_actions` pattern (#63): a union of TYPED action
 * branches on `kind`. Each branch carries its own structured payload rather than
 * a string-typed indirection, so a consumer routes on the discriminator and gets
 * the branch's fields directly.
 */
@jsonSchema
union NextAction {
  `run-journey`: RunJourneyAction,
  block: BlockAction,
}

/**
 * S1 semantic-merge-queue **MergeGateDecision** response envelope (#63).
 *
 * The decision the CI coordinator (Exarchos) receives from the semantic judge
 * (Basileus AgentHost). It composes the shared S2 base blocks — `_meta`
 * (degraded seam) and `_perf` (telemetry) — types its suggested journeys as
 * `WorkflowRef`, and carries the discriminated `next_actions` list.
 *
 * When `_meta.degraded` is true the decision is advisory: consumers short-circuit
 * "use the decision" logic (the observability seam — see `ResponseMetaV1`).
 */
@jsonSchema
model MergeGateDecision {
  /** Shared response metadata block (degraded seam). */
  _meta: ResponseMetaV1;

  /** Shared performance telemetry block. */
  _perf: PerfMetaV1;

  /** Journeys the gate suggests running, typed as `WorkflowRef` (INV-8). */
  suggestedJourneys: WorkflowRef[];

  /** Ordered next actions for the consumer, as a discriminated `NextAction` union. */
  nextActions: NextAction[];
}

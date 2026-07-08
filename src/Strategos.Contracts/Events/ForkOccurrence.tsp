import "@typespec/json-schema";
import "../Workflow/ForkTrigger.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * The runtime evidence VALUES a fork occurrence carries to justify itself
 * (#151, DR-8) — a field-name → value map. Where the declaration side
 * (`DiagnosticForkDefinition` / `PermittedForkTrigger`, DR-10) names WHICH
 * evidence fields a permitted trigger requires (`requiredEvidenceFields`), this
 * is the concrete evidence captured when a fork actually happens, keyed by
 * those same field names.
 *
 * A map (not a fixed pair of fields) is the polyglot shape that lets ANY
 * trigger's declared evidence be represented: a `ratification_failure` fork
 * carries `{ provisionalStampEventId, taints }`, a `gate_contradiction` fork
 * carries `{ leftGateId, rightGateId }`, and a future trigger carries whatever
 * fields its edge declares — all without a payload redesign. It is the
 * occurrence-side companion of the declaration-side `requiredEvidenceFields`
 * list, subsuming the earlier monomorphic ratification-only shape.
 *
 * `@minProperties(1)` is the only schema-level floor: an occurrence with an
 * empty evidence map carries no justification and fails validation. WHICH
 * fields a given trigger requires is not knowable at the contract level (the
 * fields are author-declared per workflow edge, not fixed in the closed
 * `ForkTrigger` enum), so PER-TRIGGER completeness is enforced where
 * occurrences are born — the generated guard (DR-9) — which reads each edge's
 * declared `requiredEvidenceFields` and refuses a fork whose map omits any of
 * them. That is the only place occurrences are produced, so an unjustified fork
 * is unrepresentable.
 */
alias ForkEvidence = Record<string>;

/**
 * Runtime payload of a workflow fork (#151, DR-8): the `{Pascal}WorkflowForked`
 * event body and the occurrence-side sibling of exarchos's `workflow.forked`.
 * The generated saga emits exactly one of these at the single fork decision
 * site (DR-9); it is never hand-authored.
 *
 * It carries the closed trigger (ForkTrigger) that fired plus the runtime
 * evidence VALUES that justify it — a field-name → value map (`ForkEvidence`)
 * keyed by the trigger's declared `requiredEvidenceFields`. Both are REQUIRED,
 * and the evidence map is non-empty (`@minProperties(1)`), so an occurrence
 * missing its trigger or carrying no evidence fails schema validation.
 * Per-trigger completeness — that the map carries EXACTLY the declared fields
 * for the trigger that fired — is enforced by the generated guard (DR-9) at the
 * single decision site, so an unjustified fork is unrepresentable there.
 *
 * Additive-evolution posture (DR-18): schemaVersion is a pinned literal version
 * marker. A future budget-bounded trigger (the planned exploratory member) is
 * admitted by appending it to ForkTrigger — a NON-BREAKING additive minor under
 * the enum-evolution policy — and its evidence is representable in the same map
 * with no payload change. A breaking change to this shape would bump the marker
 * to fork.v2, never mutate fork.v1 in place.
 */
@jsonSchema
model ForkOccurrence {
  /** Wire schema version literal — the DR-18 version marker (additive minors; breaking ⇒ fork.v2). */
  schemaVersion: "fork.v1";

  /** The closed trigger that fired this fork (DR-8 — the shared trigger identity). */
  trigger: ForkTrigger;

  /**
   * The runtime evidence values that justify the fork — a non-empty field-name →
   * value map (`ForkEvidence`) keyed by the trigger's declared evidence fields.
   * REQUIRED and non-empty (no unjustified fork); per-trigger completeness is
   * enforced by the generated guard (DR-9).
   */
  @minProperties(1)
  evidence: ForkEvidence;
}

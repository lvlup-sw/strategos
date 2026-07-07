import "@typespec/json-schema";
import "../Workflow/ForkTrigger.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * The runtime evidence VALUES a fork occurrence must carry to justify itself
 * (#151, DR-8). Where the declaration side (DiagnosticForkDefinition, DR-10)
 * names WHICH evidence fields a permitted trigger requires, this is the
 * concrete evidence captured when a fork actually happens.
 *
 * Both fields are REQUIRED, so an occurrence that omits them fails schema
 * validation — there is no unjustified fork. For a ratification_failure fork
 * the evidence is the provisional-stamp event id that failed plus the non-empty
 * taint set it carried. Per-trigger completeness (which evidence each trigger
 * requires) is enforced where occurrences are born — the generated guard
 * (DR-9) — never at authoring time; this model pins the schema-level floor.
 */
@jsonSchema
model ForkEvidence {
  /**
   * The provisional-stamp event id that seeded the fork's justification.
   * REQUIRED and non-empty — a fork with no anchoring event is unrepresentable.
   */
  @minLength(1)
  provisionalStampEventId: string;

  /**
   * The taint set carried at fork time. REQUIRED and NON-EMPTY (at least one
   * taint): a ratification_failure fork with an empty taint set carries no
   * evidence and is rejected at the schema boundary.
   */
  @minItems(1)
  taints: string[];
}

/**
 * Runtime payload of a workflow fork (#151, DR-8): the `{Pascal}WorkflowForked`
 * event body and the occurrence-side sibling of exarchos's `workflow.forked`.
 * The generated saga emits exactly one of these at the single fork decision
 * site (DR-9); it is never hand-authored.
 *
 * It carries the closed trigger (ForkTrigger) that fired plus the runtime
 * evidence VALUES that justify it. Both are REQUIRED — an occurrence missing
 * its trigger or its evidence fails schema validation, so an unjustified fork
 * is unrepresentable on the wire.
 *
 * Additive-evolution posture (DR-18): schemaVersion is a pinned literal version
 * marker. A future budget-bounded trigger (the planned exploratory member) is
 * admitted by appending it to ForkTrigger — a NON-BREAKING additive minor under
 * the enum-evolution policy, not a payload redesign. A breaking change to this
 * shape would bump the marker to fork.v2, never mutate fork.v1 in place.
 */
@jsonSchema
model ForkOccurrence {
  /** Wire schema version literal — the DR-18 version marker (additive minors; breaking ⇒ fork.v2). */
  schemaVersion: "fork.v1";

  /** The closed trigger that fired this fork (DR-8 — the shared trigger identity). */
  trigger: ForkTrigger;

  /** The runtime evidence values that justify the fork — REQUIRED (no unjustified fork). */
  evidence: ForkEvidence;
}

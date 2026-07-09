import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Closed enum of the TRIGGERS that may fork a workflow's diagnostic path
 * (#151, DR-8) — the declaration-side trigger vocabulary shared across the
 * contract boundary so both runtimes fork on ONE trigger identity.
 *
 * Member names are PascalCase; the wire VALUES are the exact snake_case tokens
 * cross-repo consumers match on. Exarchos's TypeScript enum and Basileus's C#
 * enum round-trip against these by VALUE (INV-8 polyglot identity) — never by
 * ordinal.
 *
 * Additive-evolution posture (DR-18): the set is CLOSED, but a future
 * budget-bounded trigger (the planned exploratory member, basileus PR #401
 * rec L-4) is admitted by APPENDING a member — a NON-BREAKING additive minor,
 * not a redesign (the ForkOccurrence payload carries an explicit schemaVersion
 * marker so this evolution is versioned, not implicit). Because emitted
 * converters stay strict, an added member is flagged NOTICE: consumers upgrade
 * before producers emit it. Removing or renaming a member is a breaking major.
 */
@jsonSchema
enum ForkTrigger {
  /** A ratification step failed — the workflow forks to remediate. */
  RatificationFailure: "ratification_failure",

  /** Two gates reached contradictory verdicts — the workflow forks to resolve. */
  GateContradiction: "gate_contradiction",

  /** An operator explicitly requested the fork. */
  OperatorExplicit: "operator_explicit",
}

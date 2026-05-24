import "@typespec/json-schema";
import "./CheckNode.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Enforcement arm: `check` — the invariant is enforced by evaluating a
 * declarative `CheckNode` combinator tree. Carries no executable code (LB-1 /
 * INV-4); the `check` is the inert combinator tree.
 */
model CheckEnforcement {
  mode: "check";

  /** The declarative check tree evaluated to enforce the invariant. */
  check: CheckNode;
}

/**
 * Enforcement arm: `audit` — the invariant is enforced by a natural-language
 * audit prompt (an LLM/agent reviews against it). The `audit-prompt` is inert
 * prose, never executable code.
 */
model AuditEnforcement {
  mode: "audit";

  /** Natural-language audit prompt (kebab-case wire name); inert prose. */
  `audit-prompt`: string;
}

/**
 * How an invariant is enforced (#98): a discriminated union on `mode` —
 * `check` (a declarative `CheckNode`) or `audit` (a natural-language prompt).
 * Reuses the discriminated-union emitter path; the discriminator property is
 * `mode` (not `kind`), which the emitter resolves generically.
 */
@jsonSchema
union Enforcement {
  check: CheckEnforcement,
  audit: AuditEnforcement,
}

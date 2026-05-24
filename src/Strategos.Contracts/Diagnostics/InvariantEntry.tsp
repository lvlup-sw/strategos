import "@typespec/json-schema";
import "./Enforcement.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * A single invariant-catalog entry (#98), v3. Carries the v2 fields plus the
 * additive v3 fields. **Every v3 field is optional** so a v2 catalog parses
 * unchanged (back-compat).
 *
 * Wire names are preserved verbatim — kebab-case (`cost-of-load`, `applies-to`,
 * the affinities, `integrity-class`) and snake_case (`axiom_overlap`) — via
 * quoted TypeSpec identifiers, so the generated decoder validates Exarchos's
 * YAML frontmatter without re-casing. Declarative-only (LB-1 / INV-4): the only
 * enforcement payload is the `Enforcement` union, which itself carries no
 * executable code.
 */
@jsonSchema
model InvariantEntry {
  // --- v2 fields ---

  /** Stable invariant identifier (e.g. `INV-4`). */
  id: string;

  /** Design dimension the invariant belongs to (e.g. `DIM-3`). */
  dimension: string;

  /** Axis within the dimension. */
  axis: string;

  /** Relative cost of loading/checking the invariant (kebab-case wire name). */
  `cost-of-load`: string;

  /** Scopes the invariant applies to (kebab-case wire name). */
  `applies-to`: string[];

  /** Human-readable summary of the invariant. */
  summary: string;

  /** Overlapping axioms (snake_case wire name preserved verbatim). */
  axiom_overlap: string[];

  /** Supporting citations. */
  citations: string[];

  /** Cross-references to related material. */
  references: string[];

  // --- v3 additive fields (all OPTIONAL for back-compat) ---

  /** SDLC phases the invariant is most relevant to (kebab-case wire name). */
  `phase-affinity`?: string[];

  /** Workflows the invariant is most relevant to (kebab-case wire name). */
  `workflow-affinity`?: string[];

  /** Ontological states the invariant is most relevant to (kebab-case wire name). */
  `state-affinity`?: string[];

  /** How the invariant is enforced (check tree or audit prompt). */
  enforcement?: Enforcement;

  /** Severity of a violation. */
  severity?: string;

  /** Integrity class the invariant guards (kebab-case wire name). */
  `integrity-class`?: string;
}

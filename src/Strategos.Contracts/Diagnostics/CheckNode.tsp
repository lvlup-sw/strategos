import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Leaf: a `grep` check — a literal/regex pattern match over files selected by an
 * optional glob. Declarative-only (LB-1 / INV-4): `pattern` and `file-glob` are
 * inert match data, never an executable command. There is no field on this arm
 * that can carry code.
 */
model GrepLeaf {
  kind: "grep";

  /** The pattern to match (regex/literal); inert match data, not a command. */
  pattern: string;

  /** Optional file glob scoping the match (kebab-case wire name). */
  `file-glob`?: string;
}

/**
 * Leaf: a `structural` check — a pattern match interpreted structurally (e.g.
 * over an AST or symbol table) rather than line-by-line. Declarative-only: the
 * `pattern`/`file-glob` are match data, never executable code (LB-1 / INV-4).
 */
model StructuralLeaf {
  kind: "structural";

  /** The structural pattern to match; inert match data, not a command. */
  pattern: string;

  /** Optional file glob scoping the match (kebab-case wire name). */
  `file-glob`?: string;
}

/**
 * Leaf: a `heuristic` check — a bounded numeric heuristic gated by a threshold.
 * Declarative-only: the only payload is the numeric `threshold`; there is no
 * member admitting code or a command (LB-1 / INV-4).
 */
model HeuristicLeaf {
  kind: "heuristic";

  /** Numeric gate the heuristic compares against (e.g. a ratio/count). */
  threshold: float64;
}

/**
 * Combinator: `all-of` — the conjunction of its child checks. Recurses back into
 * `CheckNode` via the `children` array (the self-referential arm of the tree).
 */
model AllOfNode {
  kind: "all-of";

  /** Child checks; all must hold (recurses into CheckNode). */
  children: CheckNode[];
}

/**
 * Combinator: `any-of` — the disjunction of its child checks. Recurses back into
 * `CheckNode` via the `children` array.
 */
model AnyOfNode {
  kind: "any-of";

  /** Child checks; at least one must hold (recurses into CheckNode). */
  children: CheckNode[];
}

/**
 * Combinator: `not` — the negation of a single child check. Recurses into
 * `CheckNode` via `child`.
 */
model NotNode {
  kind: "not";

  /** The single child check to negate (recurses into CheckNode). */
  child: CheckNode;
}

/**
 * Combinator: `scope` — narrows a child check to files matching a glob. Recurses
 * into `CheckNode` via `child`.
 */
model ScopeNode {
  kind: "scope";

  /** File glob the child check is scoped to (kebab-case wire name). */
  `file-glob`: string;

  /** The child check evaluated within the scope (recurses into CheckNode). */
  child: CheckNode;
}

/**
 * Recursive combinator tree (#98) describing a declarative check. A
 * discriminated union over three leaf kinds (`grep` / `structural` / `heuristic`)
 * and four combinator arms (`all-of` / `any-of` / `not` / `scope`) whose children
 * recurse back into `CheckNode`.
 *
 * Declarative-only (LB-1 / INV-4 — the sandbox guarantee): every arm carries only
 * inert match data (`pattern`, `file-glob`, `threshold`) or child `CheckNode`s.
 * **No arm admits an arbitrary-code / command / exec member.** The schema is
 * therefore structurally incapable of expressing an embedded executable — the
 * sandbox guarantee is a property of the shape, not of downstream validation.
 */
@jsonSchema
union CheckNode {
  grep: GrepLeaf,
  structural: StructuralLeaf,
  heuristic: HeuristicLeaf,
  allOf: AllOfNode,
  anyOf: AnyOfNode,
  not: NotNode,
  scope: ScopeNode,
}

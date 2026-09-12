import "@typespec/json-schema";
using JsonSchema;
namespace LevelUp.Strategos.Contracts;

/** Declarative grep match. Pattern and glob are inert data; threshold is consumer-interpreted. */
@extension("additionalProperties", false)
model GrepLeaf {
  kind: "grep";
  pattern: string;
  `file-glob`?: string;
  threshold?: float64;
}

/** Declarative structural match. Pattern and glob are inert data; threshold is consumer-interpreted. */
@extension("additionalProperties", false)
model StructuralLeaf {
  kind: "structural";
  pattern: string;
  `file-glob`?: string;
  threshold?: float64;
}

/** Declarative heuristic match. Pattern and glob are inert data; threshold is consumer-interpreted. */
@extension("additionalProperties", false)
model HeuristicLeaf {
  kind: "heuristic";
  pattern: string;
  `file-glob`?: string;
  threshold?: float64;
}

/** Conjunction of child checks; an empty list is permitted. */
@extension("additionalProperties", false)
model AllOfNode { `all-of`: CheckNode[]; }

/** Disjunction of child checks; an empty list is permitted. */
@extension("additionalProperties", false)
model AnyOfNode { `any-of`: CheckNode[]; }

/** Negation of a child check. */
@extension("additionalProperties", false)
model NotNode { not: CheckNode; }

/** Optional restrictions on a subtree; an empty scope is permitted. */
@extension("additionalProperties", false)
model CheckScope {
  `file-glob`?: string;
  phase?: string;
}

/** A child check evaluated within the declared scope. */
@extension("additionalProperties", false)
model ScopeNode {
  scope: CheckScope;
  node: CheckNode;
}

/** Recursive, declarative-only check tree. Closed arms forbid embedded executable fields. */
@jsonSchema
@oneOf
union CheckNode {
  grep: GrepLeaf,
  structural: StructuralLeaf,
  heuristic: HeuristicLeaf,
  allOf: AllOfNode,
  anyOf: AnyOfNode,
  not: NotNode,
  scope: ScopeNode,
}

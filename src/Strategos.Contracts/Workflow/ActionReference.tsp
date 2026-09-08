import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Language-neutral identity of an ontology action performed by a workflow step
 * occurrence. Every component is a name; no runtime or CLR type identity crosses
 * the wire boundary.
 */
@jsonSchema
model ActionReferenceV1 {
  /** Ontology domain name. */
  @minLength(1)
  @pattern(".*\\S.*")
  domainName: string;

  /** Ontology object type name. */
  @minLength(1)
  @pattern(".*\\S.*")
  objectTypeName: string;

  /** Ontology action name. */
  @minLength(1)
  @pattern(".*\\S.*")
  actionName: string;
}

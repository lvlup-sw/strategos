import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * A reference to a workflow journey (#66) — the language-neutral wire contract
 * that crosses from Exarchos's CI coordinator (TypeScript) into Basileus's
 * AgentHost (C#). It is a discriminated union on `kind` carrying ONLY string
 * monikers: never a CLR type name, assembly-qualified handle, or executable
 * (INV-8 polyglot identity).
 *
 * `catalog` references a workflow pinned in a `WorkflowCatalog` by its
 * `(workflowId, catalogVersion)` pair (the operational path). `authored`
 * references a free-form journey description.
 */
model CatalogWorkflowRef {
  /** Discriminator: a reference into a published `WorkflowCatalog`. */
  kind: "catalog";

  /** Catalog workflow identity (resolves against `WorkflowCatalogEntry.workflowId`). */
  @minLength(1)
  workflowId: string;

  /** Pinned catalog version (resolves against `WorkflowCatalog.catalogVersion`). */
  @minLength(1)
  catalogVersion: string;
}

/**
 * Authored journey reference (#66) — a free-form description rather than a
 * catalog lookup.
 *
 * OPERATIONALLY DEFERRED: the TYPE is defined now so the wire contract is
 * complete, but no consumer exercises it in this milestone. The execution path
 * for authored journeys ships later (basileus#179 / exarchos#1255). Until then
 * producers emit only the `catalog` variant.
 */
model AuthoredWorkflowRef {
  /** Discriminator: a free-form authored journey (not a catalog lookup). */
  kind: "authored";

  /** Natural-language description of the journey to author/run. */
  @minLength(1)
  journeyDescription: string;
}

/**
 * Discriminated union over the two workflow-reference kinds (#66), discriminated
 * on `kind`. Wire-neutral: string monikers only, no CLR identity.
 */
@jsonSchema
union WorkflowRef {
  catalog: CatalogWorkflowRef,
  authored: AuthoredWorkflowRef,
}

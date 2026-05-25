import "@typespec/json-schema";
import "./WorkflowDefinitionV1.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * A single published catalog entry (#65): a `(workflowId, catalogVersion)`
 * identity paired with its `WorkflowDefinitionV1` IR payload. A catalog
 * `WorkflowRef` resolves to exactly one entry whose identity pair matches.
 */
@jsonSchema
model WorkflowCatalogEntry {
  /** Catalog workflow identity (the lookup key for a catalog `WorkflowRef`). */
  @minLength(1)
  workflowId: string;

  /** Catalog version this entry was published at (the second half of the key). */
  @minLength(1)
  catalogVersion: string;

  /** The serialized workflow-IR payload for this catalog entry. */
  definition: WorkflowDefinitionV1;
}

/**
 * The published workflow catalog manifest (#65) — the set of workflow journeys a
 * semantic-merge-queue producer may reference via a catalog `WorkflowRef`. It
 * pins a `catalogVersion` (the manifest's own version) and the list of entries.
 *
 * A catalog `WorkflowRef` is valid against a manifest iff its
 * `(workflowId, catalogVersion)` pair matches some entry (see
 * `WorkflowCatalogResolver`).
 */
@jsonSchema
model WorkflowCatalog {
  /** Version of this catalog manifest. */
  @minLength(1)
  catalogVersion: string;

  /** The published catalog entries. */
  entries: WorkflowCatalogEntry[];
}

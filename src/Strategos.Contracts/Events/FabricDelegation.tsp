import "@typespec/json-schema";
import "./Enums.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

// ─── Fabric query audit (ADR §4.2) ───────────────────────────────────────────

/**
 * Data payload for `fabric.query` audit events — records a query against the
 * data fabric / ontology, including whether the result was served degraded.
 */
@jsonSchema
model FabricQueryData {
  /** Kind of fabric query that was executed. */
  queryType: FabricQueryType;

  /** Object type the query targeted, if applicable. */
  objectType?: string;

  /** Number of results returned. */
  resultCount: int32;

  /** True if the query was served from a degraded / partial fabric. */
  degraded: boolean;

  /** Ontology graph version the query resolved against. */
  ontologyVersion: string;
}

// ─── Remote delegation (ADR §4.3) ────────────────────────────────────────────

/**
 * Data payload for `task.delegated.remote` events — a task was routed to the
 * Basileus tier rather than executed locally.
 */
@jsonSchema
model TaskDelegatedRemoteData {
  taskId: string;
  featureId: string;

  /** Delegation target — Basileus is the only remote tier in 0.2.0. */
  target: "basileus";

  agentRole: string;

  /** Why the router chose remote placement. */
  @maxLength(500)
  reason: string;

  /** Blast-radius scope that biased the placement decision (Local | CrossDomain | Global). */
  blastRadiusScope: "Local" | "CrossDomain" | "Global";
}

/**
 * Data payload for `dependency.cross-tier.resolved` events — a dependency was
 * satisfied by work that completed on another tier.
 */
@jsonSchema
model CrossTierDependencyResolvedData {
  dependentTaskId: string;
  resolvedByTaskId: string;

  /** Tier that resolved the dependency. */
  resolvedByTier: "exarchos" | "basileus";

  branch: string;
  commitSha: string;
}

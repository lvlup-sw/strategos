import "@typespec/json-schema";
import "./Enums.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

// ─── Ontological-record layers (ADR §2.5) ────────────────────────────────────
//
// The deeper validation/snapshot sub-types (OntologyNodeRef, ValidationVerdict,
// BlastRadius, TaskSpec, QualityGateSpec, …) are ADR §4.4 contracts that land
// with the consumer-migration issues (basileus#152 / exarchos#1125). This family
// models the named in-scope fields concretely and carries the not-yet-modelled
// collections as open objects so the lifecycle round-trips without prematurely
// pinning shapes owned by a later wave.

/**
 * Process layer of an ontological record — authored by Exarchos at `/plan`.
 */
@jsonSchema
model ProcessLayerData {
  /** Reference to the design document the process layer derives from. */
  designRef: string;

  /** Ordered task ledger (TaskSpec shapes land with §4.4). */
  taskLedger: Record<unknown>[];

  /** Quality gates the work must clear (QualityGateSpec shapes land with §4.4). */
  qualityGates: Record<unknown>[];

  /** Cross-task dependencies (DependencySpec shapes land with §4.4). */
  dependencies: Record<unknown>[];

  /** Governs local-vs-remote task placement. */
  delegationPolicy: DelegationPolicy;

  /** Optional budget constraints. */
  budgetConstraints?: Record<unknown>;
}

/**
 * Domain layer of an ontological record — enriched by Basileus on
 * `intent.proposed` via ontology validation.
 */
@jsonSchema
model DomainLayerData {
  /** Ontology nodes the change touches (OntologyNodeRef lands with §4.4). */
  affectedNodes: Record<unknown>[];

  /** Validation verdict (ValidationVerdict lands with §4.4). */
  validationVerdict?: Record<unknown>;

  /** Blast radius of the change (BlastRadius lands with §4.4). */
  blastRadius?: Record<unknown>;

  /** Pattern violations surfaced by validation. */
  patternViolations?: Record<unknown>[];

  /** Existing patterns the change should follow. */
  existingPatterns?: Record<unknown>[];
}

/**
 * Composite ontological record (ADR §2.5) — a Marten event-sourced aggregate.
 * Carries the process layer (Exarchos) and, once enriched, the domain layer
 * (Basileus).
 */
@jsonSchema
model OntologicalRecordData {
  id: string;
  featureId: string;
  status: RecordStatus;
  createdAt: utcDateTime;
  updatedAt: utcDateTime;

  /** Pins the ontology graph version the record was created against. */
  ontologyVersion: string;

  /** VCS branch the record is scoped to. */
  branch?: string;

  /** Correlated GitHub PR number (null until a PR exists). */
  prNumber?: int32;

  processLayer: ProcessLayerData;
  domainLayer?: DomainLayerData;
}

// ─── Intent lifecycle events (ADR §4.1) ──────────────────────────────────────

/** Data payload for `intent.proposed` events. */
@jsonSchema
model IntentProposedData {
  recordId: string;
  featureId: string;
  processLayer: ProcessLayerData;
  delegationPolicy: DelegationPolicy;
  ontologyVersion: string;
  branch?: string;
  prNumber?: int32;
}

/** Data payload for `intent.enriched` events. */
@jsonSchema
model IntentEnrichedData {
  recordId: string;
  featureId: string;
  domainLayer: DomainLayerData;
}

/** Data payload for `intent.completed` events. */
@jsonSchema
model IntentCompletedData {
  recordId: string;
  featureId: string;
  tasksCompleted: int32;
  tasksFailed: int32;
  duration?: float64;
}

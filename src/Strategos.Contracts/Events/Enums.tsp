import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Source system that emitted an SDLC event. Constrains the envelope's
 * `source` field to the two products on the contract boundary.
 */
@jsonSchema
enum EventSource {
  exarchos,
  basileus,
}

/**
 * TDD cycle phase carried by `task.progressed` events.
 */
@jsonSchema
enum TddPhase {
  red,
  green,
  refactor,
}

/**
 * Lifecycle status of an ontological record (ADR §2.5). The Wolverine saga
 * transitions a record through these states as intent events are applied.
 */
@jsonSchema
enum RecordStatus {
  proposed,
  validated,
  enriched,
  executing,
  completed,
  failed,
}

/**
 * Delegation policy carried by an ontological record's process layer (ADR §2.5,
 * §2.6). Governs whether the router may place tasks locally or remotely.
 */
@jsonSchema
enum DelegationPolicy {
  /** Tasks run on the developer's machine (full repo resolved). */
  local,
  /** Tasks may be delegated to the Basileus tier. */
  remote,
  /** Router decides per task using blast-radius scope. */
  auto,
}

/**
 * Kind of fabric query recorded by a `fabric.query` audit event (ADR §4.2).
 */
@jsonSchema
enum FabricQueryType {
  ontologyQuery,
  designValidation,
  domainStateResolution,
  intentRegister,
}

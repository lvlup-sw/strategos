import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * The #193 kernel's `authority { … }` block and its typed-contract slot.
 *
 * **Carried, not proved.** Strategos serializes every member below and checks
 * NONE of them in 3.0. No analyzer, no emitter and no runtime path reads this
 * block. It exists so the semantic plane has one vocabulary for the authority
 * frame from birth, rather than each consumer inventing its own and converging
 * later. A consumer that acts on this block acts on unproved data.
 */

/**
 * One statement in the authority frame.
 *
 * A statement is a model rather than a bare string on purpose. The frame's
 * members are unproved in 3.0, so the fields a proved statement will need
 * (a provenance ref, a check id, a severity) are not known yet. Adding a
 * field to this model later is additive. Replacing `string[]` with a model
 * later would be a narrowing, and a narrowing here would be the design error
 * #209 exists to avoid.
 */
model WorkflowAuthorityStatementV1 {
  /**
   * Stable identifier, when the statement has one (for example `U-1` from the
   * repository invariants catalog). Omitted for a free statement.
   */
  id?: string;

  /** The statement itself, in prose. */
  @minLength(1)
  @pattern(".*\\S.*")
  statement: string;
}

/**
 * The authority frame of a workflow: what it must preserve, what it is for,
 * what it takes for granted, what it may decide alone, and where it must stop.
 *
 * Every member is OPTIONAL, following the `InvariantEntry` v3 precedent, so a
 * definition that declares no frame omits the block entirely and a definition
 * that declares one member carries only that member.
 */
model WorkflowAuthorityV1 {
  /** What the workflow must preserve. */
  invariants?: WorkflowAuthorityStatementV1[];

  /** What the workflow is for. */
  goals?: WorkflowAuthorityStatementV1[];

  /** What the workflow takes for granted about its environment. */
  assumptions?: WorkflowAuthorityStatementV1[];

  /** Decisions the workflow may make without escalating. */
  delegatedDecisions?: WorkflowAuthorityStatementV1[];

  /** The boundaries past which the workflow must escalate. */
  escalationBoundaries?: WorkflowAuthorityStatementV1[];
}

/**
 * The kernel's `inputs` / `outputs: TypedContract` slot — FROZEN AS A SLOT,
 * not as a type system.
 *
 * A step's accepted and returned CLR types are deliberately not projected
 * (LB-2): the wire carries simple-name monikers, never assembly-qualified
 * handles. Rather than invent a second type system on the wire, this slot
 * carries a reference to a schema that already has an identity — the `$id` of
 * a JSON Schema document.
 *
 * The reference is never resolved by Strategos. Whether the id resolves, and
 * to what, is the resolving consumer's concern.
 */
model TypedContractRefV1 {
  /**
   * The `$id` of the schema describing this contract. An absolute URI, matching
   * the `$id` the emitter stamps on every emitted schema document.
   */
  @minLength(1)
  @pattern(".*\\S.*")
  schemaId: string;
}

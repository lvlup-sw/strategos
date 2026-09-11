import "@typespec/json-schema";
import "./StepDefinition.tsp";
import "./WorkflowAuthority.tsp";
import "./Structural.tsp";
import "../Gates/GateDeclaration.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Wire-IR root for a serialized Strategos workflow (issue #50).
 *
 * This is the **wire contract** — the flat, language-neutral document that
 * cross-product consumers (Exarchos Zod, Basileus, visualization, schema diff)
 * read. It is the export-only projection target of the in-memory builder IR
 * `WorkflowDefinition<TState>` (see `WorkflowDefinitionProjection.ToContract`):
 * the projection drops executable code (LB-1) and reduces CLR `System.Type`
 * handles to simple-name string monikers (LB-2).
 *
 * `schemaVersion` is a pinned literal `1.0`. While the Contracts package is
 * pre-1.0 a minor may narrow this document in place, and every narrowing must
 * be listed in `schemas/breaking-changes.allowlist.json`; after 1.0 a breaking
 * change requires a V2 root.
 */
@jsonSchema
model WorkflowDefinitionV1 {
  /**
   * Wire-IR schema version: pinned literal `1.0`. While the Contracts package
   * is pre-1.0 a minor may narrow this document in place and every narrowing
   * must be listed in `schemas/breaking-changes.allowlist.json`; after 1.0 a
   * breaking change requires a V2 root.
   */
  schemaVersion: "1.0";

  /** Workflow name (the IR identity, matches the builder's `WorkflowDefinition.Name`). */
  @minLength(1)
  @pattern(".*\\S.*")
  name: string;

  /** Ordered step definitions (the discriminated `StepDefinition` union). */
  steps: StepDefinition[];

  /** Directed transitions between steps. */
  transitions: TransitionDefinition[];

  /** Branch points (conditional fan-out). */
  branchPoints: BranchPointDefinition[];

  /** Loops (RepeatUntil constructs). */
  loops: LoopDefinition[];

  /** Fork points (concurrent fan-out / join). */
  forkPoints: ForkPointDefinition[];

  /** Workflow-scoped failure handlers. */
  failureHandlers: FailureHandlerDefinition[];

  /** Approval points (human-approval pauses). */
  approvalPoints: ApprovalDefinition[];

  /**
   * Gate declarations for this workflow (#150, DR-3). Consumer-plane data: the
   * generated saga does NOT consume gates — they flow through the shared wire IR
   * so both runtimes see the same gate vocabulary from birth. OPTIONAL and
   * additive; a workflow with no declared gates omits the slot entirely.
   *
   * A GateStep may back-reference a declaration here by its `gateId`. That a
   * `gateId` must name an id present in this list is a semantic rule JSON Schema
   * cannot express (see README, "Dangling gateId"); the build-time import
   * front-end enforces it (DR-13/DR-15), not this schema.
   */
  gates?: GateDeclaration[];

  /**
   * Diagnostic fork edges for this workflow (#151, DR-10). The DECLARATIVE half of
   * the fork/compensation edge (DR-7): where the workflow may fork, the closed
   * triggers permitted with their evidence-ref schema, the `maxForks` bound, and
   * the compensation seed. OPTIONAL and additive — a workflow with no fork edge
   * omits the slot entirely, so this is a NON-BREAKING wire change.
   *
   * INV-8: every reference inside a DiagnosticForkDefinition is a string moniker,
   * never a CLR type. The generated saga lowers the edge (DR-9); the runtime
   * companion is ForkOccurrence (DR-8) in the Events family.
   */
  diagnosticForks?: DiagnosticForkDefinition[];

  /**
   * SHA-256 over this definition's STRUCTURAL fields, lowercase hex (#193
   * kernel `identity.contentHash`). Prose is excluded, on the same rules
   * `OntologyGraphHasher` already applies: documentation churn must not change
   * the hash, because the hash exists to say when the shape changed.
   *
   * OPTIONAL and additive. A producer that does not stamp one omits the slot,
   * and no Strategos path requires it. #204 reads it for tamper and skew
   * detection across an assembly boundary.
   */
  @pattern("^[0-9a-f]{64}$")
  contentHash?: string;

  /**
   * The #193 kernel's authority frame — invariants, goals, assumptions,
   * delegated decisions and escalation boundaries (#193, DR-kernel).
   *
   * **Carried, not proved in 3.0.** Strategos serializes this block and checks
   * none of it. See `WorkflowAuthorityV1`.
   */
  authority?: WorkflowAuthorityV1;

  /** Step id of the workflow entry step, if set. */
  entryStepId?: string;

  /** Step id of the workflow terminal step, if set. */
  terminalStepId?: string;
}

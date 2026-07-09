import "@typespec/json-schema";
import "./StepDefinition.tsp";
import "./ApprovalFailureConfig.tsp";
import "./ForkTrigger.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Wire-IR transition — a directed edge between two steps (mirrors the builder's
 * `TransitionDefinition`).
 */
@jsonSchema
model TransitionDefinition {
  /** Stable transition identifier. */
  transitionId: string;

  /** Step id this transition originates from. */
  fromStepId: string;

  /** Step id this transition targets. */
  toStepId: string;

  /** Whether this is the default (fallthrough) transition. */
  isDefault: boolean;
}

/**
 * Wire-IR branch path — one route through a conditional branch (mirrors the
 * builder's `BranchPathDefinition`).
 */
@jsonSchema
model BranchPathDefinition {
  /** Stable branch-path identifier. */
  pathId: string;

  /** Human-readable condition description (for visualization / diff). */
  conditionDescription: string;

  /** Steps executed on this path. */
  steps: StepDefinition[];

  /** Whether this path terminates without rejoining. */
  isTerminal: boolean;

  /** Optional approval gate guarding this path. */
  approval?: ApprovalDefinition;
}

/**
 * Wire-IR branch point — a decision point fanning out to branch paths (mirrors
 * the builder's `BranchPointDefinition`).
 */
@jsonSchema
model BranchPointDefinition {
  /** Stable branch-point identifier. */
  branchPointId: string;

  /** Step id where the branch originates. */
  fromStepId: string;

  /** Available branch paths. */
  paths: BranchPathDefinition[];

  /** Step id where branches rejoin (absent if they do not rejoin). */
  rejoinStepId?: string;
}

/**
 * Wire-IR loop — a RepeatUntil construct (mirrors the builder's
 * `LoopDefinition`).
 */
@jsonSchema
model LoopDefinition {
  /** Stable loop identifier. */
  loopId: string;

  /** Loop name (used for phase enum prefixing). */
  loopName: string;

  /** Step id where the loop originates. */
  fromStepId: string;

  /** Maximum iterations (infinite-loop guard). */
  maxIterations: int32;

  /** Steps in the loop body. */
  bodySteps: StepDefinition[];

  /** Step id to continue to after the loop exits, if set. */
  continuationStepId?: string;
}

/**
 * Wire-IR fork path — one concurrent branch of a fork (mirrors the builder's
 * `ForkPathDefinition`).
 */
@jsonSchema
model ForkPathDefinition {
  /** Stable fork-path identifier. */
  pathId: string;

  /** Zero-based path index within the fork. */
  pathIndex: int32;

  /** Steps executed on this concurrent path. */
  steps: StepDefinition[];

  /** Optional per-path failure handler. */
  failureHandler?: FailureHandlerDefinition;
}

/**
 * Wire-IR fork point — a fan-out into concurrent paths that rejoin at a join
 * step (mirrors the builder's `ForkPointDefinition`).
 */
@jsonSchema
model ForkPointDefinition {
  /** Stable fork-point identifier. */
  forkPointId: string;

  /** Step id where the fork originates. */
  fromStepId: string;

  /** Concurrent fork paths. */
  paths: ForkPathDefinition[];

  /** Step id where the concurrent paths rejoin. */
  joinStepId: string;
}

/**
 * Wire-IR permitted fork trigger with its DECLARATION-side evidence-ref schema
 * (#151, DR-10 / DR-8). Pairs one closed ForkTrigger the workflow may fork on
 * with the NAMES of the evidence fields a future fork occurrence must carry to
 * justify that trigger.
 *
 * This is the declaration half: field-NAME declarations, never runtime VALUES —
 * those exist only when a fork actually happens (see the occurrence side,
 * ForkOccurrence / ForkEvidence, in the Events family). Every entry is a plain
 * string moniker (INV-8): a field name, never a CLR type or a runtime payload.
 */
@jsonSchema
model PermittedForkTrigger {
  /** The closed trigger this entry permits (DR-8 — the shared trigger identity). */
  trigger: ForkTrigger;

  /**
   * The evidence FIELD NAMES a future fork occurrence must carry for this trigger
   * (declaration side — NOT runtime values). Each is a plain string moniker
   * (INV-8): the name of a field on the occurrence's evidence (for example
   * `provisionalStampEventId`), never a CLR type. At least one field: a permitted
   * trigger that names no evidence declares no justification schema. Per-trigger
   * runtime completeness is enforced where occurrences are born — the generated
   * guard (DR-9) — never at authoring time; this shape pins the declared floor.
   */
  @minItems(1)
  requiredEvidenceFields: string[];
}

/**
 * Wire-IR diagnostic fork edge (#151, DR-10 → #100): the DECLARATIVE half of the
 * basileus #394 rollback-and-fork verb, and the shared combinator exarchos #1258
 * lowers to. Names WHERE a workflow may fork (anchor step monikers), WHICH closed
 * triggers may fork it paired with the evidence fields each requires
 * (PermittedForkTrigger — the declaration side), a `maxForks` upper bound the
 * generated guard enforces (DR-9; the LoopDefinition.maxIterations forced-exit
 * precedent), and the compensation seed the fork routes to.
 *
 * INV-8: EVERY step/type reference on this shape is a string moniker — the anchor
 * step ids, the evidence field names, and the compensation seed are all plain
 * strings; the only typed reference is the closed ForkTrigger enum (a snake_case
 * wire vocabulary, not a CLR type). Nothing here carries a runtime `Type`, an
 * assembly-qualified name, or executable code.
 *
 * OPTIONAL and additive on WorkflowDefinitionV1 (the `diagnosticForks?` slot): a
 * workflow that declares no fork edge omits the slot entirely, so introducing it
 * is a NON-BREAKING wire change. The generated saga lowers it (DR-9); the
 * runtime-occurrence companion is ForkOccurrence (DR-8), in the Events family.
 */
@jsonSchema
model DiagnosticForkDefinition {
  /**
   * The anchor step monikers — the step ids where this workflow may fork (INV-8:
   * simple-name step id monikers, never CLR types). At least one anchor: a fork
   * edge with nowhere to fork is unrepresentable.
   */
  @minItems(1)
  anchorStepIds: string[];

  /**
   * The closed triggers permitted to fork this workflow, each paired with its
   * declaration-side evidence-ref schema (PermittedForkTrigger). At least one:
   * the edge is inexpressible without declaring a permitted trigger (DR-7).
   */
  @minItems(1)
  permittedTriggers: PermittedForkTrigger[];

  /**
   * Upper bound on the forks this edge may spawn. The generated guard enforces it
   * (DR-9; the LoopDefinition.maxIterations forced-exit precedent) — exceeding it
   * routes to a blocked / human-escalation terminal. At least 1: a bound of 0
   * forbids the very fork the edge exists to permit.
   */
  @minValue(1)
  maxForks: int32;

  /**
   * The compensation seed moniker (INV-8: a plain string moniker, never a CLR
   * type) — the seed the fork routes compensation to, composing with the existing
   * Compensate / OnFailure merged trigger site (DR-9). REQUIRED and non-empty.
   */
  @minLength(1)
  compensationSeed: string;
}

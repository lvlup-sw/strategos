import "@typespec/json-schema";
import "./StepDefinition.tsp";
import "./ApprovalFailureConfig.tsp";

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

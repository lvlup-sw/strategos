import "@typespec/json-schema";
import "./StepDefinition.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Scope of a failure handler (mirrors the builder's `FailureHandlerScope`):
 * the whole workflow, a single step, or one fork path.
 */
@jsonSchema
enum FailureHandlerScope {
  workflow,
  step,
  forkPath,
}

/**
 * Wire-IR approval-escalation handler — steps run when an approval escalates
 * (mirrors the builder's `ApprovalEscalationDefinition`).
 */
@jsonSchema
model ApprovalEscalationDefinition {
  /** Stable escalation identifier. */
  escalationId: string;

  /** Steps run on escalation. */
  steps: StepDefinition[];

  /** Nested approval gates within the escalation, if any. */
  nestedApprovals: ApprovalDefinition[];

  /** Whether escalation terminates the workflow. */
  isTerminal: boolean;
}

/**
 * Wire-IR approval-rejection handler — steps run when an approval is rejected
 * (mirrors the builder's `ApprovalRejectionDefinition`).
 */
@jsonSchema
model ApprovalRejectionDefinition {
  /** Stable rejection-handler identifier. */
  rejectionHandlerId: string;

  /** Steps run on rejection. */
  steps: StepDefinition[];

  /** Whether rejection terminates the workflow. */
  isTerminal: boolean;
}

/**
 * Wire-IR approval point — a human-approval pause (mirrors the builder's
 * `ApprovalDefinition`). The approver CLR type reduces to a simple-name moniker
 * (LB-2); the approval configuration's runtime context factory (a CLR
 * expression) is intentionally **not** carried on the wire (declarative-only,
 * LB-1).
 */
@jsonSchema
model ApprovalDefinition {
  /** Stable approval-point identifier. */
  approvalPointId: string;

  /** Simple-name CLR moniker of the approver type (LB-2). */
  approverType: string;

  /** Step id immediately preceding this approval gate. */
  precedingStepId: string;

  /** Optional escalation handler. */
  escalationHandler?: ApprovalEscalationDefinition;

  /** Optional rejection handler. */
  rejectionHandler?: ApprovalRejectionDefinition;
}

/**
 * Wire-IR failure handler — scoped recovery steps (mirrors the builder's
 * `FailureHandlerDefinition`).
 */
@jsonSchema
model FailureHandlerDefinition {
  /** Stable handler identifier. */
  handlerId: string;

  /** Handler scope (workflow | step | forkPath). */
  scope: FailureHandlerScope;

  /** Step id that triggers this handler, if step-scoped. */
  triggerStepId?: string;

  /** Recovery steps. */
  steps: StepDefinition[];

  /** Whether the handler terminates the workflow. */
  isTerminal: boolean;
}

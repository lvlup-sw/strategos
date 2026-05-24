import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Wire-IR approval point (issue #50). Placeholder shape — the full approval /
 * escalation / rejection sub-definitions land in T16.
 */
@jsonSchema
model ApprovalDefinition {
  /** Stable approval-point identifier. */
  approvalPointId: string;

  /** Simple-name CLR moniker of the approver type (LB-2). */
  approverType: string;

  /** Step id immediately preceding this approval gate. */
  precedingStepId: string;
}

/**
 * Wire-IR failure handler (issue #50). Placeholder shape — the full failure /
 * configuration sub-definitions land in T16.
 */
@jsonSchema
model FailureHandlerDefinition {
  /** Stable handler identifier. */
  handlerId: string;
}

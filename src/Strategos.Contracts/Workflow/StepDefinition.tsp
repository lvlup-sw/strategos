import "@typespec/json-schema";
import "./StepConfiguration.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Reserved runtime placement for a step (v3.3.0 federation forward-compat slot).
 * Optional on every step kind; absence means the default `exarchos`. No behavior
 * is attached in 0.2.0 — the slot exists so a future federation release is an
 * additive minor, not a breaking V2.
 */
@jsonSchema
enum StepRuntime {
  exarchos,
  strategos,
  remote,
}

/**
 * Fields common to every wire step kind. Spread (`...StepCommon`) into each arm
 * of the `StepDefinition` discriminated union so the shared shape stays single-
 * sourced while each arm pins its own `kind` discriminator.
 */
alias StepCommon = {
  /** Stable step identifier (the builder's `StepDefinition.StepId`). */
  stepId: string;

  /** Step name (the builder's `StepDefinition.StepName`). */
  stepName: string;

  /** Optional instance name distinguishing the same step type across contexts. */
  instanceName?: string;

  /** Whether this step terminates the workflow. */
  isTerminal: boolean;

  /** Reserved federation placement (default `exarchos`); no behavior in 0.2.0. */
  runtime?: StepRuntime;

  /** Optional step configuration (confidence / retry / compensation / …). */
  configuration?: StepConfigurationDefinition;
};

/**
 * Skill step — a CLR step type implementing the skill protocol. `stepType` is
 * the **simple type name** moniker (LB-2): language-neutral, not assembly- or
 * namespace-qualified.
 */
model SkillStep {
  kind: "skill";
  ...StepCommon;

  /** Simple-name CLR moniker of the step type (LB-2). */
  stepType: string;
}

/**
 * Handler step — a CLR step type. Same wire shape as a skill step; the kind is
 * carried so consumers can route on the builder's semantic intent.
 */
model HandlerStep {
  kind: "handler";
  ...StepCommon;

  /** Simple-name CLR moniker of the step type (LB-2). */
  stepType: string;
}

/**
 * Gate step — a CLR step type acting as a structural gate.
 */
model GateStep {
  kind: "gate";
  ...StepCommon;

  /** Simple-name CLR moniker of the step type (LB-2). */
  stepType: string;
}

/**
 * Delegate (lambda) step — built via `StepDefinition.CreateFromLambda` (LB-1).
 * The wire contract **never carries executable code**: the `Delegate` body is
 * dropped and its loss made visible by the `lambda: true` marker. There is no
 * field on this arm that can hold a delegate or executable.
 */
model DelegateStep {
  kind: "delegate";
  ...StepCommon;

  /** Declares this step was a lambda whose body was dropped (LB-1). Always true. */
  lambda: true;
}

/**
 * Approval step — a human-approval pause point. `approverType` is the simple-name
 * moniker of the approver CLR type (LB-2).
 */
model ApprovalStep {
  kind: "approval";
  ...StepCommon;

  /** Simple-name CLR moniker of the approver type (LB-2). */
  approverType: string;
}

/**
 * Wire-IR step definition (issue #50): a discriminated union over the five step
 * kinds. Each arm pins its `kind` discriminator const and spreads the shared
 * `StepCommon` fields. This is the wire shape both consumers (Exarchos Zod,
 * Basileus) derive from.
 */
@jsonSchema
union StepDefinition {
  skill: SkillStep,
  handler: HandlerStep,
  gate: GateStep,
  delegate: DelegateStep,
  approval: ApprovalStep,
}

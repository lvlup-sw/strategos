// -----------------------------------------------------------------------
// <copyright file="WireDtos.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Import;

// =============================================================================
// DR-12 (#100) — internal wire-DTO twins for the JSON import front-end.
//
// Strategos.Generators is an isolated netstandard2.0 analyzer: it CANNOT
// reference the Strategos.Contracts assembly or its System.Text.Json-attributed
// records (the documented AgwfCodes.g.cs isolated-analyzer posture). So the
// import reader cannot deserialize onto the real contract types. These twins are
// hand-authored netstandard2.0-safe mirrors of the WorkflowDefinitionV1 wire
// contract, populated by the vendored MinimalJsonReader (zero package deps).
//
// The twins are PINNED to the Contracts-emitted JSON Schema by
// WireDtoSchemaConformanceTests (in the net-current test project, which CAN
// reference Contracts): drift in either direction — a missing field, an extra
// field, or a wrong type — fails that test. That is the mechanical-parity
// guarantee (same pattern as DR-16) that lets these hand-authored twins stand in
// for the generated contract types safely.
//
// INV-8: every step/type reference on these shapes is a plain string moniker,
// never a CLR Type. The wire enums (GateClass, ForkTrigger, StepRuntime,
// FailureHandlerScope) are carried as their snake_case string VALUES — the
// polyglot identity both runtimes match on — not as CLR enum handles.
// =============================================================================

/// <summary>
/// Marker for a wire-contract DTO twin. Tags the hand-authored mirrors so the
/// schema-conformance test can discover them by reflection (and exclude the
/// reader plumbing that shares the namespace).
/// </summary>
internal interface IWireContractDto
{
}

/// <summary>
/// Wire-IR root twin for a serialized Strategos workflow (mirrors
/// <c>WorkflowDefinitionV1</c>). Carries every top-level slot the schema
/// declares — including the constructs the import front-end will REJECT per
/// DR-14 (branch points, loops) — so the bridge can observe and reject them
/// rather than silently drop them.
/// </summary>
internal sealed class WorkflowDefinitionV1 : IWireContractDto
{
    /// <summary>Gets or sets the pinned wire-IR schema version (the literal <c>"1.0"</c>).</summary>
    public string? SchemaVersion { get; set; }

    /// <summary>Gets or sets the workflow name (the IR identity).</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the ordered step definitions (the discriminated step union).</summary>
    public List<StepDefinition> Steps { get; set; } = new List<StepDefinition>();

    /// <summary>Gets or sets the directed transitions between steps.</summary>
    public List<TransitionDefinition> Transitions { get; set; } = new List<TransitionDefinition>();

    /// <summary>Gets or sets the branch points (conditional fan-out; rejected on import per DR-14).</summary>
    public List<BranchPointDefinition> BranchPoints { get; set; } = new List<BranchPointDefinition>();

    /// <summary>Gets or sets the loops (RepeatUntil constructs; rejected on import per DR-14).</summary>
    public List<LoopDefinition> Loops { get; set; } = new List<LoopDefinition>();

    /// <summary>Gets or sets the fork points (concurrent fan-out / join).</summary>
    public List<ForkPointDefinition> ForkPoints { get; set; } = new List<ForkPointDefinition>();

    /// <summary>Gets or sets the workflow-scoped failure handlers.</summary>
    public List<FailureHandlerDefinition> FailureHandlers { get; set; } = new List<FailureHandlerDefinition>();

    /// <summary>Gets or sets the approval points (human-approval pauses).</summary>
    public List<ApprovalDefinition> ApprovalPoints { get; set; } = new List<ApprovalDefinition>();

    /// <summary>Gets or sets the gate declarations for this workflow (#150, DR-3). Optional/additive.</summary>
    public List<GateDeclaration> Gates { get; set; } = new List<GateDeclaration>();

    /// <summary>Gets or sets the diagnostic fork edges for this workflow (#151, DR-10). Optional/additive.</summary>
    public List<DiagnosticForkDefinition> DiagnosticForks { get; set; } = new List<DiagnosticForkDefinition>();

    /// <summary>Gets or sets the step id of the workflow entry step, if set.</summary>
    public string? EntryStepId { get; set; }

    /// <summary>Gets or sets the step id of the workflow terminal step, if set.</summary>
    public string? TerminalStepId { get; set; }
}

/// <summary>
/// Base twin for a wire-IR step definition — the shared fields spread across
/// every arm of the <c>StepDefinition</c> discriminated union. The concrete arm
/// is chosen by the <see cref="Kind"/> discriminator.
/// </summary>
internal abstract class StepDefinition : IWireContractDto
{
    /// <summary>Gets or sets the discriminator naming the step arm (e.g. <c>skill</c>).</summary>
    public string? Kind { get; set; }

    /// <summary>Gets or sets the stable step identifier.</summary>
    public string? StepId { get; set; }

    /// <summary>Gets or sets the step name.</summary>
    public string? StepName { get; set; }

    /// <summary>Gets or sets the optional instance name distinguishing the same step type across contexts.</summary>
    public string? InstanceName { get; set; }

    /// <summary>Gets or sets a value indicating whether this step terminates the workflow.</summary>
    public bool IsTerminal { get; set; }

    /// <summary>Gets or sets the reserved federation placement moniker (default <c>exarchos</c>).</summary>
    public string? Runtime { get; set; }

    /// <summary>Gets or sets the optional step configuration (confidence / retry / compensation / …).</summary>
    public StepConfigurationDefinition? Configuration { get; set; }
}

/// <summary>Skill-step twin — a CLR step type implementing the skill protocol.</summary>
internal sealed class SkillStep : StepDefinition
{
    /// <summary>Gets or sets the simple-name CLR moniker of the step type (LB-2).</summary>
    public string? StepType { get; set; }
}

/// <summary>Handler-step twin — a CLR step type (same wire shape as a skill step).</summary>
internal sealed class HandlerStep : StepDefinition
{
    /// <summary>Gets or sets the simple-name CLR moniker of the step type (LB-2).</summary>
    public string? StepType { get; set; }
}

/// <summary>Gate-step twin — a CLR step type acting as a structural gate.</summary>
internal sealed class GateStep : StepDefinition
{
    /// <summary>Gets or sets the simple-name CLR moniker of the step type (LB-2).</summary>
    public string? StepType { get; set; }

    /// <summary>Gets or sets the optional back-reference to a gate declaration id on the workflow root (#150, DR-3).</summary>
    public string? GateId { get; set; }
}

/// <summary>
/// Delegate (lambda) step twin — built from a lambda whose body is dropped
/// (LB-1). The <see cref="Lambda"/> marker makes the loss visible; the import
/// front-end rejects this arm per DR-14.
/// </summary>
internal sealed class DelegateStep : StepDefinition
{
    /// <summary>Gets or sets the lambda-lossiness marker (always <c>true</c> when present).</summary>
    public bool Lambda { get; set; }
}

/// <summary>Approval-step twin — a human-approval pause point.</summary>
internal sealed class ApprovalStep : StepDefinition
{
    /// <summary>Gets or sets the simple-name CLR moniker of the approver type (LB-2).</summary>
    public string? ApproverType { get; set; }
}

/// <summary>
/// Wire-IR step configuration tree twin (mirrors <c>StepConfigurationDefinition</c>).
/// All members are optional — an unconfigured step omits the whole object.
/// </summary>
internal sealed class StepConfigurationDefinition : IWireContractDto
{
    /// <summary>Gets or sets the confidence threshold below which the low-confidence handler fires.</summary>
    public double? ConfidenceThreshold { get; set; }

    /// <summary>Gets or sets the low-confidence handler.</summary>
    public LowConfidenceHandlerDefinition? OnLowConfidence { get; set; }

    /// <summary>Gets or sets the compensation configuration.</summary>
    public CompensationConfiguration? Compensation { get; set; }

    /// <summary>Gets or sets the retry configuration.</summary>
    public RetryConfiguration? Retry { get; set; }

    /// <summary>Gets or sets the step timeout (ISO-8601 duration), if set.</summary>
    public string? Timeout { get; set; }

    /// <summary>Gets or sets the validation guard.</summary>
    public ValidationDefinition? Validation { get; set; }
}

/// <summary>
/// Wire-IR retry configuration twin (mirrors <c>RetryConfiguration</c>).
/// Durations are ISO-8601 duration strings (language-neutral; not a .NET <c>TimeSpan</c>).
/// </summary>
internal sealed class RetryConfiguration : IWireContractDto
{
    /// <summary>Gets or sets the maximum retry attempts.</summary>
    public int MaxAttempts { get; set; }

    /// <summary>Gets or sets the initial backoff delay (ISO-8601 duration).</summary>
    public string? InitialDelay { get; set; }

    /// <summary>Gets or sets the exponential backoff multiplier.</summary>
    public double? BackoffMultiplier { get; set; }

    /// <summary>Gets or sets the maximum backoff delay (ISO-8601 duration).</summary>
    public string? MaxDelay { get; set; }

    /// <summary>Gets or sets a value indicating whether jitter is applied to backoff.</summary>
    public bool? UseJitter { get; set; }
}

/// <summary>
/// Wire-IR compensation configuration twin (mirrors <c>CompensationConfiguration</c>).
/// The compensation step CLR type reduces to a simple-name moniker (LB-2).
/// </summary>
internal sealed class CompensationConfiguration : IWireContractDto
{
    /// <summary>Gets or sets the simple-name CLR moniker of the compensation step type (LB-2).</summary>
    public string? CompensationStepType { get; set; }

    /// <summary>Gets or sets a value indicating whether compensation is required on failure.</summary>
    public bool? RequiredOnFailure { get; set; }

    /// <summary>Gets or sets the compensation timeout (ISO-8601 duration), if set.</summary>
    public string? Timeout { get; set; }
}

/// <summary>
/// Wire-IR validation definition twin (mirrors <c>ValidationDefinition</c>).
/// <see cref="PredicateExpression"/> is a declarative description, not executable
/// code (LB-1).
/// </summary>
internal sealed class ValidationDefinition : IWireContractDto
{
    /// <summary>Gets or sets the declarative predicate description (not executable code, LB-1).</summary>
    public string? PredicateExpression { get; set; }

    /// <summary>Gets or sets the error message surfaced when validation fails.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Wire-IR low-confidence handler twin (mirrors <c>LowConfidenceHandlerDefinition</c>).</summary>
internal sealed class LowConfidenceHandlerDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable handler identifier.</summary>
    public string? HandlerId { get; set; }

    /// <summary>Gets or sets the steps run when confidence is below threshold.</summary>
    public List<StepDefinition> HandlerSteps { get; set; } = new List<StepDefinition>();

    /// <summary>Gets or sets a value indicating whether the handler terminates the workflow.</summary>
    public bool IsTerminal { get; set; }

    /// <summary>Gets or sets the step id where the handler rejoins the main flow, if any.</summary>
    public string? RejoinStepId { get; set; }
}

/// <summary>Wire-IR transition twin — a directed edge between two steps.</summary>
internal sealed class TransitionDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable transition identifier.</summary>
    public string? TransitionId { get; set; }

    /// <summary>Gets or sets the step id this transition originates from.</summary>
    public string? FromStepId { get; set; }

    /// <summary>Gets or sets the step id this transition targets.</summary>
    public string? ToStepId { get; set; }

    /// <summary>Gets or sets a value indicating whether this is the default (fallthrough) transition.</summary>
    public bool IsDefault { get; set; }
}

/// <summary>Wire-IR branch point twin — a decision point fanning out to branch paths.</summary>
internal sealed class BranchPointDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable branch-point identifier.</summary>
    public string? BranchPointId { get; set; }

    /// <summary>Gets or sets the step id where the branch originates.</summary>
    public string? FromStepId { get; set; }

    /// <summary>Gets or sets the available branch paths.</summary>
    public List<BranchPathDefinition> Paths { get; set; } = new List<BranchPathDefinition>();

    /// <summary>Gets or sets the step id where branches rejoin (absent if they do not rejoin).</summary>
    public string? RejoinStepId { get; set; }
}

/// <summary>Wire-IR branch path twin — one route through a conditional branch.</summary>
internal sealed class BranchPathDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable branch-path identifier.</summary>
    public string? PathId { get; set; }

    /// <summary>Gets or sets the human-readable condition description (for visualization / diff).</summary>
    public string? ConditionDescription { get; set; }

    /// <summary>Gets or sets the steps executed on this path.</summary>
    public List<StepDefinition> Steps { get; set; } = new List<StepDefinition>();

    /// <summary>Gets or sets a value indicating whether this path terminates without rejoining.</summary>
    public bool IsTerminal { get; set; }

    /// <summary>Gets or sets the optional approval gate guarding this path.</summary>
    public ApprovalDefinition? Approval { get; set; }
}

/// <summary>Wire-IR loop twin — a RepeatUntil construct (mirrors <c>LoopDefinition</c>).</summary>
internal sealed class LoopDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable loop identifier.</summary>
    public string? LoopId { get; set; }

    /// <summary>Gets or sets the loop name (used for phase enum prefixing).</summary>
    public string? LoopName { get; set; }

    /// <summary>Gets or sets the step id where the loop originates.</summary>
    public string? FromStepId { get; set; }

    /// <summary>Gets or sets the maximum iterations (infinite-loop guard).</summary>
    public int MaxIterations { get; set; }

    /// <summary>Gets or sets the steps in the loop body.</summary>
    public List<StepDefinition> BodySteps { get; set; } = new List<StepDefinition>();

    /// <summary>Gets or sets the step id to continue to after the loop exits, if set.</summary>
    public string? ContinuationStepId { get; set; }
}

/// <summary>Wire-IR fork point twin — a fan-out into concurrent paths that rejoin at a join step.</summary>
internal sealed class ForkPointDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable fork-point identifier.</summary>
    public string? ForkPointId { get; set; }

    /// <summary>Gets or sets the step id where the fork originates.</summary>
    public string? FromStepId { get; set; }

    /// <summary>Gets or sets the concurrent fork paths.</summary>
    public List<ForkPathDefinition> Paths { get; set; } = new List<ForkPathDefinition>();

    /// <summary>Gets or sets the step id where the concurrent paths rejoin.</summary>
    public string? JoinStepId { get; set; }
}

/// <summary>Wire-IR fork path twin — one concurrent branch of a fork.</summary>
internal sealed class ForkPathDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable fork-path identifier.</summary>
    public string? PathId { get; set; }

    /// <summary>Gets or sets the zero-based path index within the fork.</summary>
    public int PathIndex { get; set; }

    /// <summary>Gets or sets the steps executed on this concurrent path.</summary>
    public List<StepDefinition> Steps { get; set; } = new List<StepDefinition>();

    /// <summary>Gets or sets the optional per-path failure handler.</summary>
    public FailureHandlerDefinition? FailureHandler { get; set; }
}

/// <summary>Wire-IR failure handler twin — scoped recovery steps.</summary>
internal sealed class FailureHandlerDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable handler identifier.</summary>
    public string? HandlerId { get; set; }

    /// <summary>Gets or sets the handler scope value (<c>workflow</c> | <c>step</c> | <c>forkPath</c>).</summary>
    public string? Scope { get; set; }

    /// <summary>Gets or sets the step id that triggers this handler, if step-scoped.</summary>
    public string? TriggerStepId { get; set; }

    /// <summary>Gets or sets the recovery steps.</summary>
    public List<StepDefinition> Steps { get; set; } = new List<StepDefinition>();

    /// <summary>Gets or sets a value indicating whether the handler terminates the workflow.</summary>
    public bool IsTerminal { get; set; }
}

/// <summary>
/// Wire-IR approval point twin — a human-approval pause (mirrors <c>ApprovalDefinition</c>).
/// The context body (a static message or a runtime context factory) is dropped
/// from the wire (LB-1); its presence is marked by <see cref="HasContext"/> so
/// the loss is visible in the data rather than silent (DR-14).
/// </summary>
internal sealed class ApprovalDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable approval-point identifier.</summary>
    public string? ApprovalPointId { get; set; }

    /// <summary>Gets or sets the simple-name CLR moniker of the approver type (LB-2).</summary>
    public string? ApproverType { get; set; }

    /// <summary>Gets or sets the step id immediately preceding this approval gate.</summary>
    public string? PrecedingStepId { get; set; }

    /// <summary>Gets or sets the optional escalation handler.</summary>
    public ApprovalEscalationDefinition? EscalationHandler { get; set; }

    /// <summary>Gets or sets the optional rejection handler.</summary>
    public ApprovalRejectionDefinition? RejectionHandler { get; set; }

    /// <summary>
    /// Gets or sets the declarative-only context lossiness marker (DR-14, task 024):
    /// present and <c>true</c> only when approval context was configured; omitted
    /// for a context-free approval point.
    /// </summary>
    public bool HasContext { get; set; }
}

/// <summary>Wire-IR approval-escalation handler twin — steps run when an approval escalates.</summary>
internal sealed class ApprovalEscalationDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable escalation identifier.</summary>
    public string? EscalationId { get; set; }

    /// <summary>Gets or sets the steps run on escalation.</summary>
    public List<StepDefinition> Steps { get; set; } = new List<StepDefinition>();

    /// <summary>Gets or sets the nested approval gates within the escalation, if any.</summary>
    public List<ApprovalDefinition> NestedApprovals { get; set; } = new List<ApprovalDefinition>();

    /// <summary>Gets or sets a value indicating whether escalation terminates the workflow.</summary>
    public bool IsTerminal { get; set; }
}

/// <summary>Wire-IR approval-rejection handler twin — steps run when an approval is rejected.</summary>
internal sealed class ApprovalRejectionDefinition : IWireContractDto
{
    /// <summary>Gets or sets the stable rejection-handler identifier.</summary>
    public string? RejectionHandlerId { get; set; }

    /// <summary>Gets or sets the steps run on rejection.</summary>
    public List<StepDefinition> Steps { get; set; } = new List<StepDefinition>();

    /// <summary>Gets or sets a value indicating whether rejection terminates the workflow.</summary>
    public bool IsTerminal { get; set; }
}

/// <summary>
/// Wire-IR gate declaration twin (#150, DR-2): the typed gate CLASS it evaluates,
/// a stable id, and — only when telemetry-measured — its reliability block.
/// </summary>
internal sealed class GateDeclaration : IWireContractDto
{
    /// <summary>Gets or sets the typed gate class value this declaration evaluates (DR-1).</summary>
    public string? Class { get; set; }

    /// <summary>Gets or sets the stable identifier for this gate declaration.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the measured-reliability telemetry — present only when measured, never hand-authored.</summary>
    public GateReliability? Reliability { get; set; }
}

/// <summary>
/// Measured-reliability telemetry twin for a gate (#150, DR-2). Never
/// hand-authored — <see cref="Source"/> (the provenance) is required.
/// </summary>
internal sealed class GateReliability : IWireContractDto
{
    /// <summary>Gets or sets the measured false-positive rate (a fraction in the closed unit interval).</summary>
    public double Fpr { get; set; }

    /// <summary>Gets or sets the number of observations the measurement was computed over.</summary>
    public int SampleSize { get; set; }

    /// <summary>Gets or sets when the measurement was taken (UTC, ISO-8601 date-time).</summary>
    public string? AsOf { get; set; }

    /// <summary>Gets or sets the provenance of the measurement (required).</summary>
    public string? Source { get; set; }
}

/// <summary>
/// Wire-IR diagnostic fork edge twin (#151, DR-10): the declarative half of the
/// fork/compensation edge — where a workflow may fork, the closed triggers
/// permitted with their evidence-ref schema, the <c>maxForks</c> bound, and the
/// compensation seed.
/// </summary>
internal sealed class DiagnosticForkDefinition : IWireContractDto
{
    /// <summary>Gets or sets the anchor step monikers — the step ids where this workflow may fork (INV-8).</summary>
    public List<string> AnchorStepIds { get; set; } = new List<string>();

    /// <summary>Gets or sets the closed triggers permitted to fork this workflow, each with its evidence-ref schema.</summary>
    public List<PermittedForkTrigger> PermittedTriggers { get; set; } = new List<PermittedForkTrigger>();

    /// <summary>Gets or sets the upper bound on the forks this edge may spawn (the generated guard enforces it).</summary>
    public int MaxForks { get; set; }

    /// <summary>Gets or sets the compensation seed moniker the fork routes compensation to (INV-8; required, non-empty).</summary>
    public string? CompensationSeed { get; set; }
}

/// <summary>
/// Wire-IR permitted fork trigger twin (#151, DR-10 / DR-8): one closed trigger
/// the workflow may fork on, paired with the NAMES of the evidence fields a
/// future fork occurrence must carry to justify it (declaration side, INV-8).
/// </summary>
internal sealed class PermittedForkTrigger : IWireContractDto
{
    /// <summary>Gets or sets the closed trigger value this entry permits (DR-8).</summary>
    public string? Trigger { get; set; }

    /// <summary>Gets or sets the evidence field NAMES a future fork occurrence must carry for this trigger (INV-8).</summary>
    public List<string> RequiredEvidenceFields { get; set; } = new List<string>();
}

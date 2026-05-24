// =============================================================================
// <copyright file="WorkflowDefinitionProjection.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;
using Wire = Strategos.Contracts.Generated;

namespace Strategos.Contracts;

/// <summary>
/// Projects the in-memory builder IR (<see cref="WorkflowDefinition{TState}"/>)
/// to the generated wire contract (<see cref="Wire.WorkflowDefinitionV1"/>).
/// </summary>
/// <remarks>
/// <para>
/// This is the single seam between the two workflow representations: the
/// behavioral builder IR (generic, CLR-typed, lambda-bearing — the build and
/// execution authority) and the serializable wire IR (a flat, language-neutral
/// document the cross-product consumers derive from). It realizes the design's
/// two load-bearing decisions:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>LB-1 (declarative-only):</b> a lambda step (built via
///     <see cref="StepDefinition.CreateFromLambda"/>) projects to a
///     <c>delegate</c>-kind wire step carrying a <c>lambda: true</c> marker. The
///     <see cref="StepDefinition.LambdaDelegate"/> body is <b>dropped</b> — the
///     wire contract never serializes executable code, and the loss is made
///     visible in the data rather than silently elided.
///   </description></item>
///   <item><description>
///     <b>LB-2 (export-only, language-neutral moniker):</b> a step's
///     <see cref="StepDefinition.StepType"/> (<see cref="System.Type"/>)
///     projects to a <c>stepType</c> string using the <b>simple type name</b>
///     (<see cref="System.Reflection.MemberInfo.Name"/>) — never assembly- or
///     namespace-qualified. The projection is <b>one-way</b>: there is
///     deliberately no <c>FromContract</c> / rehydrate-to-runnable-workflow API
///     in 0.2.0. Rehydration is deferred to a future bidirectional V-next; the
///     simple-name moniker and the <c>lambda</c> marker are forward-compatible
///     with it. See the design's "LB-2 — Identity &amp; round-trip" section.
///   </description></item>
/// </list>
/// </remarks>
public static class WorkflowDefinitionProjection
{
    /// <summary>The wire-IR schema version this projection targets.</summary>
    public const string SchemaVersion = "1.0";

    /// <summary>
    /// Projects a builder <see cref="WorkflowDefinition{TState}"/> to its
    /// serializable wire contract (<see cref="Wire.WorkflowDefinitionV1"/>).
    /// </summary>
    /// <typeparam name="TState">The workflow state type.</typeparam>
    /// <param name="workflow">The in-memory builder workflow definition.</param>
    /// <returns>The wire-IR projection of <paramref name="workflow"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="workflow"/> is null.</exception>
    public static Wire.WorkflowDefinitionV1 ToContract<TState>(this WorkflowDefinition<TState> workflow)
        where TState : class, IWorkflowState
    {
        ArgumentNullException.ThrowIfNull(workflow, nameof(workflow));

        return new Wire.WorkflowDefinitionV1
        {
            SchemaVersion = SchemaVersion,
            Name = workflow.Name,
            Steps = [.. workflow.Steps.Select(ProjectStep)],
            Transitions = [.. workflow.Transitions.Select(ProjectTransition)],
            BranchPoints = [.. workflow.BranchPoints.Select(ProjectBranchPoint)],
            Loops = [.. workflow.Loops.Select(ProjectLoop)],
            ForkPoints = [.. workflow.ForkPoints.Select(ProjectForkPoint)],
            FailureHandlers = [.. workflow.FailureHandlers.Select(ProjectFailureHandler)],
            ApprovalPoints = [.. workflow.ApprovalPoints.Select(ProjectApproval)],
            EntryStepId = workflow.EntryStep?.StepId,
            TerminalStepId = workflow.TerminalStep?.StepId,
        };
    }

    /// <summary>
    /// The builder-IR step classifications this projection knows how to map onto
    /// a wire step arm. The builder IR exposes <b>no richer kind discriminator
    /// today</b> than <see cref="StepDefinition.IsLambdaStep"/>: a step is either
    /// a lambda step (LB-1 → <c>delegate</c> arm) or a typed/CLR step (LB-2 →
    /// <c>skill</c> arm). The wire union additionally defines <c>handler</c>,
    /// <c>gate</c>, and <c>approval</c> arms; those are <b>deliberately not
    /// produced</b> in 0.2.0 because the builder cannot yet distinguish them. The
    /// 2-of-5 subset is therefore an <i>asserted decision</i>, not an accident —
    /// see <c>ProjectionStepKindMappingTests</c>.
    /// </summary>
    private enum BuilderStepKind
    {
        /// <summary>A typed/CLR step → wire <c>skill</c> arm (LB-2).</summary>
        Skill,

        /// <summary>A lambda step → wire <c>delegate</c> arm (LB-1).</summary>
        Delegate,
    }

    /// <summary>
    /// Classifies a builder step into one of the recognized
    /// <see cref="BuilderStepKind"/> values. This is the single place where the
    /// builder's discriminator is read; growing the builder with a richer kind
    /// signal means extending this method <b>and</b> the <see cref="ProjectStep"/>
    /// switch (whose <c>default</c> arm throws so a new, unmapped classification
    /// can never silently fall through to <c>skill</c>).
    /// </summary>
    private static BuilderStepKind ClassifyStep(StepDefinition step) =>
        step.IsLambdaStep ? BuilderStepKind.Delegate : BuilderStepKind.Skill;

    /// <summary>
    /// Projects a builder step to its discriminated wire arm. A lambda step
    /// (<see cref="StepDefinition.IsLambdaStep"/>) becomes a <c>delegate</c> arm
    /// with the body dropped (LB-1); a typed step becomes a <c>skill</c> arm
    /// carrying the simple-name CLR moniker (LB-2). The mapping is
    /// <b>explicit and non-silent</b>: an unrecognized classification hits the
    /// <c>default</c> arm and throws rather than defaulting to <c>skill</c>, so a
    /// future builder kind that is added to <see cref="BuilderStepKind"/> but not
    /// wired here fails loudly instead of being silently mis-projected.
    /// </summary>
    private static Wire.StepDefinition ProjectStep(StepDefinition step) =>
        ProjectMappedKind(ClassifyStep(step), step);

    /// <summary>
    /// Maps an explicitly classified <see cref="BuilderStepKind"/> to its wire
    /// arm. This is the single, non-silent dispatch point: every recognized kind
    /// has an explicit arm, and the <c>default</c> arm <b>throws</b>
    /// <see cref="NotSupportedException"/> naming the unmapped kind. A future kind
    /// added to <see cref="BuilderStepKind"/> without a matching arm here fails
    /// loudly instead of being silently mis-projected to <c>skill</c>.
    /// </summary>
    /// <param name="kind">The classified builder step kind.</param>
    /// <param name="step">The builder step being projected.</param>
    /// <returns>The wire step arm for <paramref name="kind"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="kind"/> has no explicit wire mapping.
    /// </exception>
    private static Wire.StepDefinition ProjectMappedKind(BuilderStepKind kind, StepDefinition step) =>
        kind switch
        {
            // LB-1: the Delegate body is intentionally not carried — only the
            // structure and a lambda marker survive to the wire.
            BuilderStepKind.Delegate => new Wire.DelegateStep
            {
                StepId = step.StepId,
                StepName = step.StepName,
                InstanceName = step.InstanceName,
                IsTerminal = step.IsTerminal,
                Configuration = ProjectConfiguration(step.Configuration),
                Lambda = true,
            },

            BuilderStepKind.Skill => new Wire.SkillStep
            {
                StepId = step.StepId,
                StepName = step.StepName,
                InstanceName = step.InstanceName,
                IsTerminal = step.IsTerminal,
                Configuration = ProjectConfiguration(step.Configuration),

                // LB-2: simple type name, not assembly- or namespace-qualified.
                StepType = step.StepType.Name,
            },

            // Non-silent guard: any BuilderStepKind not explicitly mapped above
            // is a projection gap, not a skill step. Throw naming the unmapped
            // kind rather than silently emitting the wrong wire arm.
            _ => throw new NotSupportedException(
                $"No wire-step mapping for builder step kind '{(int)kind}'. " +
                "Add an explicit arm to WorkflowDefinitionProjection.ProjectMappedKind; " +
                "the projection must never silently default an unrecognized kind."),
        };

    private static Wire.TransitionDefinition ProjectTransition(TransitionDefinition t) => new()
    {
        TransitionId = t.TransitionId,
        FromStepId = t.FromStepId,
        ToStepId = t.ToStepId,
        IsDefault = t.IsDefault,
    };

    private static Wire.BranchPointDefinition ProjectBranchPoint(BranchPointDefinition b) => new()
    {
        BranchPointId = b.BranchPointId,
        FromStepId = b.FromStepId,
        Paths = [.. b.Paths.Select(ProjectBranchPath)],
        RejoinStepId = b.RejoinStepId,
    };

    private static Wire.BranchPathDefinition ProjectBranchPath(BranchPathDefinition p) => new()
    {
        PathId = p.PathId,
        ConditionDescription = p.ConditionDescription,
        Steps = [.. p.Steps.Select(ProjectStep)],
        IsTerminal = p.IsTerminal,
        Approval = p.Approval is null ? null : ProjectApproval(p.Approval),
    };

    private static Wire.LoopDefinition ProjectLoop(LoopDefinition l) => new()
    {
        LoopId = l.LoopId,
        LoopName = l.LoopName,
        FromStepId = l.FromStepId,
        MaxIterations = l.MaxIterations,
        BodySteps = [.. l.BodySteps.Select(ProjectStep)],
        ContinuationStepId = l.ContinuationStepId,
    };

    private static Wire.ForkPointDefinition ProjectForkPoint(ForkPointDefinition f) => new()
    {
        ForkPointId = f.ForkPointId,
        FromStepId = f.FromStepId,
        Paths = [.. f.Paths.Select(ProjectForkPath)],
        JoinStepId = f.JoinStepId,
    };

    private static Wire.ForkPathDefinition ProjectForkPath(ForkPathDefinition p) => new()
    {
        PathId = p.PathId,
        PathIndex = p.PathIndex,
        Steps = [.. p.Steps.Select(ProjectStep)],
        FailureHandler = p.FailureHandler is null ? null : ProjectFailureHandler(p.FailureHandler),
    };

    private static Wire.FailureHandlerDefinition ProjectFailureHandler(FailureHandlerDefinition h) => new()
    {
        HandlerId = h.HandlerId,
        Scope = h.Scope switch
        {
            FailureHandlerScope.Workflow => Wire.FailureHandlerScope.Workflow,
            FailureHandlerScope.Step => Wire.FailureHandlerScope.Step,
            FailureHandlerScope.ForkPath => Wire.FailureHandlerScope.ForkPath,
            _ => Wire.FailureHandlerScope.Workflow,
        },
        TriggerStepId = h.TriggerStepId,
        Steps = [.. h.Steps.Select(ProjectStep)],
        IsTerminal = h.IsTerminal,
    };

    private static Wire.ApprovalDefinition ProjectApproval(ApprovalDefinition a) => new()
    {
        ApprovalPointId = a.ApprovalPointId,

        // LB-2: simple type name moniker for the approver.
        ApproverType = a.ApproverType.Name,
        PrecedingStepId = a.PrecedingStepId,
        EscalationHandler = a.EscalationHandler is null ? null : ProjectEscalation(a.EscalationHandler),
        RejectionHandler = a.RejectionHandler is null ? null : ProjectRejection(a.RejectionHandler),
    };

    private static Wire.ApprovalEscalationDefinition ProjectEscalation(ApprovalEscalationDefinition e) => new()
    {
        EscalationId = e.EscalationId,
        Steps = [.. e.Steps.Select(ProjectStep)],
        NestedApprovals = [.. e.NestedApprovals.Select(ProjectApproval)],
        IsTerminal = e.IsTerminal,
    };

    private static Wire.ApprovalRejectionDefinition ProjectRejection(ApprovalRejectionDefinition r) => new()
    {
        RejectionHandlerId = r.RejectionHandlerId,
        Steps = [.. r.Steps.Select(ProjectStep)],
        IsTerminal = r.IsTerminal,
    };

    private static Wire.StepConfigurationDefinition? ProjectConfiguration(StepConfigurationDefinition? c)
    {
        if (c is null)
        {
            return null;
        }

        return new Wire.StepConfigurationDefinition
        {
            ConfidenceThreshold = c.ConfidenceThreshold,
            OnLowConfidence = c.OnLowConfidence is null ? null : ProjectLowConfidence(c.OnLowConfidence),
            Compensation = c.Compensation is null ? null : ProjectCompensation(c.Compensation),
            Retry = c.Retry is null ? null : ProjectRetry(c.Retry),
            Timeout = c.Timeout is { } t ? System.Xml.XmlConvert.ToString(t) : null,
            Validation = c.Validation is null ? null : ProjectValidation(c.Validation),
        };
    }

    private static Wire.LowConfidenceHandlerDefinition ProjectLowConfidence(LowConfidenceHandlerDefinition h) => new()
    {
        HandlerId = h.HandlerId,
        HandlerSteps = [.. h.HandlerSteps.Select(ProjectStep)],
        IsTerminal = h.IsTerminal,
        RejoinStepId = h.RejoinStepId,
    };

    private static Wire.CompensationConfiguration ProjectCompensation(CompensationConfiguration c) => new()
    {
        // LB-2: simple type name moniker for the compensation step.
        CompensationStepType = c.CompensationStepType.Name,
        RequiredOnFailure = c.RequiredOnFailure,
        Timeout = c.Timeout is { } t ? System.Xml.XmlConvert.ToString(t) : null,
    };

    private static Wire.RetryConfiguration ProjectRetry(RetryConfiguration r) => new()
    {
        MaxAttempts = r.MaxAttempts,
        InitialDelay = System.Xml.XmlConvert.ToString(r.InitialDelay),
        BackoffMultiplier = r.BackoffMultiplier,
        MaxDelay = System.Xml.XmlConvert.ToString(r.MaxDelay),
        UseJitter = r.UseJitter,
    };

    private static Wire.ValidationDefinition ProjectValidation(ValidationDefinition v) => new()
    {
        PredicateExpression = v.PredicateExpression,
        ErrorMessage = v.ErrorMessage,
    };
}

// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkLoweringSourceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Shape/regression tests for lowering a declared <c>AllowDiagnosticFork(...)</c> edge
/// (DR-9, #151) into the generated Wolverine+Marten saga: the single fork decision-site
/// handler with its anchor, permitted-trigger + evidence, and maxForks guards; the
/// <c>{Pascal}WorkflowForked</c> audit event (event-sourced); the <c>ForkBlocked</c>
/// terminal phase; the <c>DiagnosticForkCount</c> saga property; and the compensation
/// seeding that composes with the merged Compensate/OnFailure trigger site (#140).
/// </summary>
/// <remarks>
/// These run the FULL generator pipeline and assert on the generated SOURCE, so they are
/// the Postgres-free semantic guard for the fork lowering (the runtime behavioral proofs
/// live in <c>Strategos.Generators.Behavioral.Tests</c>). A workflow WITHOUT a fork edge
/// must lower none of these artifacts.
/// </remarks>
[Property("Category", "Integration")]
public class DiagnosticForkLoweringSourceTests
{
    /// <summary>
    /// An event-sourced workflow that declares a diagnostic-fork edge anchored at its
    /// start step, permitting two triggers (each with evidence refs), routing compensation
    /// to the compensated <c>StampStep</c>, and bounding forks at 2. The compensated step
    /// gives the fork's seed a real Compensate/OnFailure merged trigger site to route into.
    /// </summary>
    private const string EventSourcedForkWorkflow = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ForkTrigger
        {
            RatificationFailure,
            GateContradiction,
            OperatorExplicit,
        }

        [WorkflowState]
        public record RatifyState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class RatifyStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        public class StampStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        public class RollbackStampStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        public class CompleteStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        [Workflow("ratify-deploy", Persistence = PersistenceMode.EventSourced)]
        public static partial class RatifyDeployWorkflow
        {
            public static WorkflowDefinition<RatifyState> Definition => Workflow<RatifyState>
                .Create("ratify-deploy")
                .StartWith<RatifyStep>()
                .Then<StampStep>(step => step.Compensate<RollbackStampStep>())
                .AllowDiagnosticFork(fork => fork
                    .Anchor("RatifyStep")
                    .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId", "taints")
                    .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
                    .WithCompensationSeed("StampStep")
                    .MaxForks(2))
                .Finally<CompleteStep>();
        }
        """;

    /// <summary>
    /// The SagaDocument-mode counterpart of <see cref="EventSourcedForkWorkflow"/>: the
    /// fork decision-site handler and its guards still lower (they are control flow, not
    /// audit events), but the <c>{Pascal}WorkflowForked</c> STREAM event and its append
    /// are event-sourced-only and must NOT appear.
    /// </summary>
    private const string DocumentModeForkWorkflow = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ForkTrigger
        {
            RatificationFailure,
            GateContradiction,
            OperatorExplicit,
        }

        [WorkflowState]
        public record RatifyState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class RatifyStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        public class StampStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        public class RollbackStampStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        public class CompleteStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        [Workflow("ratify-deploy")]
        public static partial class RatifyDeployWorkflow
        {
            public static WorkflowDefinition<RatifyState> Definition => Workflow<RatifyState>
                .Create("ratify-deploy")
                .StartWith<RatifyStep>()
                .Then<StampStep>(step => step.Compensate<RollbackStampStep>())
                .AllowDiagnosticFork(fork => fork
                    .Anchor("RatifyStep")
                    .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId", "taints")
                    .WithCompensationSeed("StampStep")
                    .MaxForks(2))
                .Finally<CompleteStep>();
        }
        """;

    /// <summary>
    /// The fork decision command lowers with the occurrence shape: a saga-identity
    /// workflow id plus the anchor, trigger, and evidence (provisional-stamp event id +
    /// taint set) the guard needs to admit or refuse the fork.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_ForkDecisionCommand_IsGenerated()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedForkWorkflow);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeployCommands.g.cs");

        await Assert.That(commandsSource).Contains("public sealed partial record ForkRatifyDeployCommand(");
        await Assert.That(commandsSource).Contains("string Anchor,");
        await Assert.That(commandsSource).Contains("string Trigger,");
        await Assert.That(commandsSource).Contains("string ProvisionalStampEventId,");
        await Assert.That(commandsSource).Contains("System.Collections.Generic.IReadOnlyList<string> Taints);");
    }

    /// <summary>
    /// The anchor guard (<c>AnchorStepIds</c> lowering) admits the fork only at a declared
    /// anchor moniker; the fork blocked terminal phase and the fork-count property lower too.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_AnchorGuardAndForkBlockedPhase_AreGenerated()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedForkWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeployPhase.g.cs");

        // Single decision-site handler over the fork command.
        await Assert.That(sagaSource).Contains("ForkRatifyDeployCommand cmd,");

        // Anchor guard: admissible only at the declared anchor step moniker.
        await Assert.That(sagaSource).Contains("cmd.Anchor == \"RatifyStep\"");

        // Workflow-scoped fork tally + the blocked/human-escalation terminal.
        await Assert.That(sagaSource).Contains("public int DiagnosticForkCount");
        await Assert.That(phaseSource).Contains("ForkBlocked,");
    }

    /// <summary>
    /// The permitted-trigger + evidence guard (<c>PermittedTriggers</c> lowering): each
    /// permitted trigger lowers to its snake_case wire value and the guard requires the
    /// occurrence evidence (a non-empty provisional-stamp event id and a non-empty taint
    /// set) — the DR-8 occurrence-completeness chokepoint.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_PermittedTriggerAndEvidenceGuard_IsGenerated()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedForkWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");

        // Trigger enum member names lower to their snake_case wire values.
        await Assert.That(sagaSource).Contains("cmd.Trigger == \"ratification_failure\"");
        await Assert.That(sagaSource).Contains("cmd.Trigger == \"gate_contradiction\"");

        // Evidence-completeness floor (mirrors the contract ForkEvidence required fields).
        await Assert.That(sagaSource).Contains("!string.IsNullOrWhiteSpace(cmd.ProvisionalStampEventId)");
        await Assert.That(sagaSource).Contains("cmd.Taints.Count > 0");

        // A refused fork short-circuits without seeding compensation.
        await Assert.That(sagaSource).Contains("yield break;");
    }

    /// <summary>
    /// The maxForks guard (<c>MaxForks</c> lowering): once the declared bound is reached,
    /// an overflowing fork routes to the blocked/human-escalation terminal phase (the loop
    /// MaxIterations forced-exit precedent).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_MaxForksGuard_RoutesToBlockedTerminal()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedForkWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");

        await Assert.That(sagaSource).Contains("if (DiagnosticForkCount >= 2)");
        await Assert.That(sagaSource).Contains("Phase = RatifyDeployPhase.ForkBlocked;");
        await Assert.That(sagaSource).Contains("DiagnosticForkCount++;");
    }

    /// <summary>
    /// The compensation seed (<c>CompensationSeed</c> lowering): a valid fork seeds
    /// compensation by yielding the merged <c>Trigger{Pascal}FailureHandlerCommand</c>
    /// whose failed-step name is the declared seed — composing with the #140 site.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_CompensationSeed_RoutesIntoMergedTriggerSite()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedForkWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");

        await Assert.That(sagaSource).Contains("yield return new TriggerRatifyDeployFailureHandlerCommand(");
        await Assert.That(sagaSource).Contains("\"StampStep\"");
    }

    /// <summary>
    /// The <c>{Pascal}WorkflowForked</c> audit event (event-sourced) mirrors the contract
    /// fork-occurrence shape and is appended at the single decision site.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_EventSourced_AppendsWorkflowForkedEvent()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedForkWorkflow);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeployEvents.g.cs");
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");

        // The audit event record mirrors ForkOccurrence: schema marker + trigger + evidence.
        await Assert.That(eventsSource).Contains("public sealed partial record RatifyDeployWorkflowForked(");
        await Assert.That(eventsSource).Contains("string SchemaVersion,");
        await Assert.That(eventsSource).Contains("string Trigger,");
        await Assert.That(eventsSource).Contains("System.Collections.Generic.IReadOnlyList<string> Taints,");

        // Appended at the decision site with the pinned schema-version marker.
        await Assert.That(sagaSource).Contains("session.Events.Append(");
        await Assert.That(sagaSource).Contains("new RatifyDeployWorkflowForked(");
        await Assert.That(sagaSource).Contains("\"fork.v1\"");
    }

    /// <summary>
    /// SagaDocument mode: the fork decision-site handler and its guards still lower, but
    /// the WorkflowForked STREAM event (record + append) is event-sourced-only and absent.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_DocumentMode_HandlerLowersButNoWorkflowForkedEvent()
    {
        var result = GeneratorTestHelper.RunGenerator(DocumentModeForkWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeployEvents.g.cs");

        // The control-flow guards lower in document mode too.
        await Assert.That(sagaSource).Contains("ForkRatifyDeployCommand cmd,");
        await Assert.That(sagaSource).Contains("if (DiagnosticForkCount >= 2)");

        // But the audit stream event record is event-sourced-only ...
        await Assert.That(eventsSource).DoesNotContain("WorkflowForked");

        // ... and the decision site appends no stream event in document mode.
        await Assert.That(sagaSource).DoesNotContain("new RatifyDeployWorkflowForked(");
        await Assert.That(sagaSource).DoesNotContain("session.Events.Append");
    }
}

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
    /// workflow id plus the anchor, trigger, and the per-trigger evidence MAP (field-name
    /// -> value) the guard needs to admit or refuse the fork — no hardcoded ratification
    /// evidence fields.
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
        await Assert.That(commandsSource).Contains("System.Collections.Generic.IReadOnlyDictionary<string, string> Evidence);");

        // The old monomorphic ratification-only fields are gone — evidence is a map now.
        await Assert.That(commandsSource).DoesNotContain("string ProvisionalStampEventId,");
        await Assert.That(commandsSource).DoesNotContain("IReadOnlyList<string> Taints);");
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

        // Per-edge fork tally (edge 0) + the blocked/human-escalation terminal.
        await Assert.That(sagaSource).Contains("public int DiagnosticForkCount_0");
        await Assert.That(phaseSource).Contains("ForkBlocked,");
    }

    /// <summary>
    /// The permitted-trigger + PER-TRIGGER evidence guard (<c>PermittedTriggers</c> +
    /// <c>RequiredEvidenceFields</c> lowering): each permitted trigger lowers to its
    /// snake_case wire value, and the evidence guard is a per-trigger switch that requires
    /// EXACTLY that trigger's declared evidence fields (via the shared
    /// <c>ForkEvidenceComplete</c> helper) — the DR-8 occurrence-completeness chokepoint.
    /// A <c>gate_contradiction</c> fork must carry ITS OWN <c>leftGateId</c>/
    /// <c>rightGateId</c>, never the hardcoded ratification evidence.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_PermittedTriggerAndEvidenceGuard_IsGenerated()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedForkWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");

        // Trigger enum member names lower to their snake_case wire values in the switch.
        await Assert.That(sagaSource).Contains("cmd.Trigger == \"ratification_failure\"");
        await Assert.That(sagaSource).Contains("cmd.Trigger == \"gate_contradiction\"");

        // Per-trigger evidence switch: each arm requires exactly the trigger's declared
        // fields through the shared completeness helper.
        await Assert.That(sagaSource).Contains("cmd.Trigger switch");
        await Assert.That(sagaSource)
            .Contains("\"ratification_failure\" => ForkEvidenceComplete(cmd.Evidence, \"provisionalStampEventId\", \"taints\")");
        await Assert.That(sagaSource)
            .Contains("\"gate_contradiction\" => ForkEvidenceComplete(cmd.Evidence, \"leftGateId\", \"rightGateId\")");

        // The completeness helper is the field-driven per-trigger check (present + non-empty).
        await Assert.That(sagaSource).Contains("private static bool ForkEvidenceComplete(");
        await Assert.That(sagaSource).Contains("evidence.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value)");

        // The old hardcoded ratification-only guard is gone.
        await Assert.That(sagaSource).DoesNotContain("cmd.ProvisionalStampEventId");
        await Assert.That(sagaSource).DoesNotContain("cmd.Taints");

        // A refused fork short-circuits without seeding compensation.
        await Assert.That(sagaSource).Contains("yield break;");
    }

    /// <summary>
    /// The per-trigger evidence chokepoint for a NON-ratification trigger: the
    /// <c>gate_contradiction</c> arm requires its OWN declared evidence fields
    /// (<c>leftGateId</c>/<c>rightGateId</c>) and never the ratification fields — proving
    /// each trigger's <c>RequiredEvidenceFields</c> actually drive the generated guard
    /// rather than a single hardcoded shape.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_NonRatificationTrigger_RequiresItsOwnEvidenceFields()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedForkWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");

        // The gate_contradiction arm demands its declared gate ids ...
        await Assert.That(sagaSource)
            .Contains("\"gate_contradiction\" => ForkEvidenceComplete(cmd.Evidence, \"leftGateId\", \"rightGateId\")");

        // ... and does NOT check them against the ratification trigger's fields.
        await Assert.That(sagaSource)
            .DoesNotContain("\"gate_contradiction\" => ForkEvidenceComplete(cmd.Evidence, \"provisionalStampEventId\"");
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

        await Assert.That(sagaSource).Contains("if (DiagnosticForkCount_0 >= 2)");
        await Assert.That(sagaSource).Contains("Phase = RatifyDeployPhase.ForkBlocked;");
        await Assert.That(sagaSource).Contains("DiagnosticForkCount_0++;");
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

        // The audit event record mirrors ForkOccurrence: schema marker + trigger + the
        // per-trigger evidence MAP (no hardcoded ratification fields).
        await Assert.That(eventsSource).Contains("public sealed partial record RatifyDeployWorkflowForked(");
        await Assert.That(eventsSource).Contains("string SchemaVersion,");
        await Assert.That(eventsSource).Contains("string Trigger,");
        await Assert.That(eventsSource).Contains("System.Collections.Generic.IReadOnlyDictionary<string, string> Evidence,");
        await Assert.That(eventsSource).DoesNotContain("IReadOnlyList<string> Taints,");

        // Appended at the decision site with the pinned schema-version marker and the
        // fired trigger's own evidence map.
        await Assert.That(sagaSource).Contains("session.Events.Append(");
        await Assert.That(sagaSource).Contains("new RatifyDeployWorkflowForked(");
        await Assert.That(sagaSource).Contains("\"fork.v1\"");
        await Assert.That(sagaSource).Contains("cmd.Evidence,");
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
        await Assert.That(sagaSource).Contains("if (DiagnosticForkCount_0 >= 2)");

        // But the audit stream event record is event-sourced-only ...
        await Assert.That(eventsSource).DoesNotContain("WorkflowForked");

        // ... and the decision site appends no stream event in document mode.
        await Assert.That(sagaSource).DoesNotContain("new RatifyDeployWorkflowForked(");
        await Assert.That(sagaSource).DoesNotContain("session.Events.Append");
    }

    /// <summary>
    /// L3 multi-edge maxForks: a workflow declaring TWO fork edges with DIFFERENT bounds
    /// lowers a SEPARATE per-edge counter for each, and each edge's maxForks guard checks
    /// its OWN counter. A single shared workflow-scoped tally would let a high-bound edge
    /// starve a low-bound edge; per-edge counters make the two edges independent.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_MultipleEdges_HavePerEdgeIndependentCounters()
    {
        var result = GeneratorTestHelper.RunGenerator(MultiEdgeForkWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");

        // Two independent per-edge counters are declared.
        await Assert.That(sagaSource).Contains("public int DiagnosticForkCount_0");
        await Assert.That(sagaSource).Contains("public int DiagnosticForkCount_1");

        // Each edge's bound check is against its OWN counter (edge 0 bound 5, edge 1 bound 1).
        await Assert.That(sagaSource).Contains("if (DiagnosticForkCount_0 >= 5)");
        await Assert.That(sagaSource).Contains("if (DiagnosticForkCount_1 >= 1)");
        await Assert.That(sagaSource).Contains("DiagnosticForkCount_0++;");
        await Assert.That(sagaSource).Contains("DiagnosticForkCount_1++;");

        // No shared workflow-scoped counter survives to be starved.
        await Assert.That(sagaSource).DoesNotContain("if (DiagnosticForkCount >=");
        await Assert.That(sagaSource).DoesNotContain("DiagnosticForkCount++;");
    }

    /// <summary>
    /// M5 codegen safety: an anchor moniker and compensation seed carrying a double-quote
    /// are emitted as SymbolDisplay-escaped C# literals, so the generated saga source stays
    /// syntactically valid (no broken-string-literal lexer errors). A raw interpolation
    /// would produce <c>cmd.Anchor == "Rat"ify"</c> and fail to compile.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkLowering_HostileAnchorAndSeedLiterals_StayEscapedAndCompile()
    {
        var result = GeneratorTestHelper.RunGenerator(HostileLiteralForkWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "RatifyDeploySaga.g.cs");

        // The double-quote in the anchor/seed is escaped in the emitted literal.
        await Assert.That(sagaSource).Contains("cmd.Anchor == \"Rat\\\"ify\"");
        await Assert.That(sagaSource).Contains("\"Stamp\\\"Step\"");

        // The unescaped forms (a raw interpolation) must NOT appear.
        await Assert.That(sagaSource).DoesNotContain("cmd.Anchor == \"Rat\"ify\"");

        // The generated output is free of the lexer/parser errors an unescaped literal
        // would produce (CS1010 newline-in-constant, CS1002/CS1003/CS1026 token errors).
        var diagnostics = GeneratorTestHelper.GetCompilationDiagnostics(HostileLiteralForkWorkflow);
        var syntaxErrors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => BrokenLiteralSyntaxErrorIds.Contains(d.Id))
            .ToList();

        await Assert.That(syntaxErrors)
            .IsEmpty()
            .Because(
                "escaped anchor/seed literals must keep the generated saga syntactically valid; got: "
                + string.Join(", ", syntaxErrors.Select(d => $"{d.Id} {d.GetMessage()}")));
    }

    /// <summary>
    /// The lexer/parser diagnostic ids a broken (unescaped) string literal produces — a
    /// stray double-quote splits the literal and the tail runs to end-of-line.
    /// </summary>
    private static readonly HashSet<string> BrokenLiteralSyntaxErrorIds =
        new(StringComparer.Ordinal) { "CS1002", "CS1003", "CS1010", "CS1026", "CS1039", "CS1513", "CS1525" };

    /// <summary>
    /// An event-sourced workflow declaring TWO diagnostic-fork edges with distinct anchors
    /// and DIFFERENT maxForks bounds (edge 0 at <c>RatifyStep</c> bound 5; edge 1 at
    /// <c>StampStep</c> bound 1), used to prove per-edge counter independence (L3).
    /// </summary>
    private const string MultiEdgeForkWorkflow = """
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
                    .WithCompensationSeed("StampStep")
                    .MaxForks(5))
                .AllowDiagnosticFork(fork => fork
                    .Anchor("StampStep")
                    .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
                    .WithCompensationSeed("StampStep")
                    .MaxForks(1))
                .Finally<CompleteStep>();
        }
        """;

    /// <summary>
    /// A fork workflow whose anchor moniker (<c>Rat"ify</c>) and compensation seed
    /// (<c>Stamp"Step</c>) carry an embedded double-quote — the M5 codegen-safety fixture
    /// proving the emitted literals are escaped so the generated saga still compiles.
    /// </summary>
    private const string HostileLiteralForkWorkflow = """"
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
                    .Anchor("Rat\"ify")
                    .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId", "taints")
                    .WithCompensationSeed("Stamp\"Step")
                    .MaxForks(2))
                .Finally<CompleteStep>();
        }
        """";
}

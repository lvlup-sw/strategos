// -----------------------------------------------------------------------
// <copyright file="BranchBoolDiscriminatorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Emission and compile proofs that a bool-discriminated branch with both
/// <see langword="true"/> and <see langword="false"/> arms is exhaustive: the saga
/// switch must not emit a discarded <c>_ =&gt;</c> (CS8510 / #179).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BranchTerminalCaseTests"/> keeps an enum discriminator so its mixed
/// fixture stays green while this class owns the bool shape that fixture avoided.
/// An enum branch with no <c>Otherwise</c> still emits the discarded default arm.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class BranchBoolDiscriminatorTests
{
    /// <summary>
    /// The generated hint name of the bool-discriminated fixture's saga.
    /// </summary>
    private const string SagaHintName = "EscalateIfCrisisSaga.g.cs";

    /// <summary>
    /// A bool-discriminated branch with both <see langword="true"/> and
    /// <see langword="false"/> cases, ahead of a declared terminal.
    /// </summary>
    private const string BoolBranchWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public sealed record IncidentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool IsCrisis { get; init; }
        }

        public sealed class AssessIncident : IWorkflowStep<IncidentState>
        {
            public Task<StepResult<IncidentState>> ExecuteAsync(
                IncidentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<IncidentState>.FromState(state));
        }

        public sealed class PageOnCall : IWorkflowStep<IncidentState>
        {
            public Task<StepResult<IncidentState>> ExecuteAsync(
                IncidentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<IncidentState>.FromState(state));
        }

        public sealed class FileTicket : IWorkflowStep<IncidentState>
        {
            public Task<StepResult<IncidentState>> ExecuteAsync(
                IncidentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<IncidentState>.FromState(state));
        }

        public sealed class CloseIncident : IWorkflowStep<IncidentState>
        {
            public Task<StepResult<IncidentState>> ExecuteAsync(
                IncidentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<IncidentState>.FromState(state));
        }

        [Workflow("escalate-if-crisis")]
        public static partial class EscalateIfCrisisWorkflow
        {
            public static WorkflowDefinition<IncidentState> Definition => Workflow<IncidentState>
                .Create("escalate-if-crisis")
                .StartWith<AssessIncident>()
                .Branch(state => state.IsCrisis,
                    BranchCase<IncidentState, bool>.When(
                        true, path => path.Then<PageOnCall>()),
                    BranchCase<IncidentState, bool>.When(
                        false, path => path.Then<FileTicket>()))
                .Finally<CloseIncident>();
        }
        """;

    /// <summary>
    /// A bool-discriminated branch saga compiles: the emitted switch is exhaustive,
    /// so CS8510 is absent.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BoolDiscriminatedBranch_CompilesWithoutUnreachableDefaultArm()
    {
        var diagnostics = GeneratorTestHelper.GetCompilationDiagnostics(BoolBranchWorkflow);
        var unreachable = diagnostics
            .Where(d => string.Equals(d.Id, "CS8510", StringComparison.Ordinal))
            .ToList();

        await Assert.That(unreachable).IsEmpty()
            .Because("true + false on a bool discriminator is exhaustive; the discarded _ => arm is CS8510 (#179).");
    }

    /// <summary>
    /// The emitted routing switch carries both bool arms and no discarded default.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BoolDiscriminatedBranch_EmittedSwitchHasNoDiscardArm()
    {
        var routingSwitch = RoutingSwitchFromSaga();

        await Assert.That(routingSwitch).Contains("true =>")
            .Because("the crisis path must remain a named true arm.");
        await Assert.That(routingSwitch).Contains("false =>")
            .Because("the non-crisis path must remain a named false arm.");
        await Assert.That(routingSwitch).DoesNotContain("_ =>")
            .Because("an exhaustive bool switch must not emit a discarded default arm (#179).");
    }

    /// <summary>
    /// <see cref="BranchHandlerEmitter"/> omits <c>_ =&gt;</c> when the discriminator
    /// is bool and both literals are present.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Property("Category", "Unit")]
    public async Task BranchHandlerEmitter_ExhaustiveBool_OmitsDiscardArm()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBoolBranch(includeFalse: true);

        emitter.EmitRoutingHandler(sb, model, "AssessIncident", branch);
        var result = sb.ToString();

        await Assert.That(result).Contains("true => new StartPageOnCallCommand(WorkflowId)");
        await Assert.That(result).Contains("false => new StartFileTicketCommand(WorkflowId)");
        await Assert.That(result).DoesNotContain("_ =>")
            .Because("reverting the bool-exhaustiveness skip reintroduces the unreachable default.");
    }

    /// <summary>
    /// A bool branch that only names <see langword="true"/> is not exhaustive and
    /// still emits the discarded default (consecutive-branch / throw fallback).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Property("Category", "Unit")]
    public async Task BranchHandlerEmitter_BoolTrueOnly_KeepsDiscardArm()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBoolBranch(includeFalse: false);

        emitter.EmitRoutingHandler(sb, model, "AssessIncident", branch);
        var result = sb.ToString();

        await Assert.That(result).Contains("true => new StartPageOnCallCommand(WorkflowId)");
        await Assert.That(result).Contains("_ => throw new InvalidOperationException")
            .Because("a single true arm is not exhaustive; the default fallback must remain.");
    }

    /// <summary>
    /// Enum branches are unchanged: missing <c>Otherwise</c> still emits a discarded default.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Property("Category", "Unit")]
    public async Task BranchHandlerEmitter_EnumWithoutOtherwise_KeepsDiscardArm()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approved = BranchCaseModel.Create(
            caseValueLiteral: "ReviewOutcome.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["ProcessApprovedOrder"],
            isTerminal: false);
        var rejected = BranchCaseModel.Create(
            caseValueLiteral: "ReviewOutcome.Rejected",
            branchPathPrefix: "Rejected",
            stepNames: ["RejectOrder"],
            isTerminal: false);
        var branch = BranchModel.Create(
            branchId: "Review-Outcome",
            previousStepName: "ReviewOrder",
            discriminatorPropertyPath: "Outcome",
            discriminatorTypeName: "ReviewOutcome",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases: [approved, rejected]);

        emitter.EmitRoutingHandler(sb, model, "ReviewOrder", branch);
        var result = sb.ToString();

        await Assert.That(result).Contains("_ => throw new InvalidOperationException")
            .Because("enum branches stay non-exhaustive unless Otherwise is declared.");
    }

    /// <summary>
    /// <see cref="LoopCompletedHandlerEmitter"/> omits <c>_ =&gt;</c> on a bool
    /// <c>BranchOnExit</c> that names both literals.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Property("Category", "Unit")]
    public async Task LoopCompletedHandlerEmitter_ExhaustiveBoolBranchOnExit_OmitsDiscardArm()
    {
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBoolBranch(includeFalse: true);
        var loop = LoopModel.Create(
            loopName: "Triage",
            conditionId: "EscalateIfCrisis-Triage",
            maxIterations: 3,
            bodySteps: [StepModel.Create("AssessIncident", "TestNamespace.AssessIncident")],
            continuationStepName: "CloseIncident",
            branchOnExit: branch);
        var context = new HandlerContext(
            StepIndex: 0,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: "CloseIncident",
            StepModel: null,
            LoopsAtStep: [loop],
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        emitter.EmitHandler(sb, model, "AssessIncident", context);
        var result = sb.ToString();

        await Assert.That(result).Contains("true => new StartPageOnCallCommand(WorkflowId)");
        await Assert.That(result).Contains("false => new StartFileTicketCommand(WorkflowId)");
        await Assert.That(result).DoesNotContain("_ =>")
            .Because("loop-exit bool routing has the same CS8510 discarded-arm defect as the main branch switch.");
    }

    /// <summary>
    /// Extracts the discriminator switch expression from the emitted saga.
    /// </summary>
    /// <returns>The switch expression source, from <c>switch</c> through its closing brace.</returns>
    private static string RoutingSwitchFromSaga()
    {
        var result = GeneratorTestHelper.RunGenerator(BoolBranchWorkflow);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, SagaHintName);
        if (string.IsNullOrEmpty(saga))
        {
            throw new InvalidOperationException(
                $"The generator emitted no saga named '{SagaHintName}'.");
        }

        var switchIndex = saga.IndexOf("switch", StringComparison.Ordinal);
        if (switchIndex < 0)
        {
            throw new InvalidOperationException(
                $"The emitted saga has no switch expression. Emitted source:{Environment.NewLine}{saga}");
        }

        var openBrace = saga.IndexOf('{', switchIndex);
        var closeBrace = saga.IndexOf($"{Environment.NewLine}        }}", openBrace, StringComparison.Ordinal);
        if (openBrace < 0 || closeBrace < 0)
        {
            throw new InvalidOperationException(
                $"Could not delimit the routing switch. Emitted source:{Environment.NewLine}{saga}");
        }

        return saga.Substring(switchIndex, closeBrace - switchIndex);
    }

    private static WorkflowModel CreateMinimalModel()
    {
        return new WorkflowModel(
            WorkflowName: "escalate-if-crisis",
            PascalName: "EscalateIfCrisis",
            Namespace: "TestNamespace",
            StepNames: ["AssessIncident", "PageOnCall", "FileTicket", "CloseIncident"],
            StateTypeName: "IncidentState",
            Loops: null);
    }

    private static BranchModel CreateBoolBranch(bool includeFalse)
    {
        var crisis = BranchCaseModel.Create(
            caseValueLiteral: "true",
            branchPathPrefix: "Crisis",
            stepNames: ["PageOnCall"],
            isTerminal: false);

        IReadOnlyList<BranchCaseModel> cases = includeFalse
            ?
            [
                crisis,
                BranchCaseModel.Create(
                    caseValueLiteral: "false",
                    branchPathPrefix: "Routine",
                    stepNames: ["FileTicket"],
                    isTerminal: false),
            ]
            : [crisis];

        return BranchModel.Create(
            branchId: "Escalate-IsCrisis",
            previousStepName: "AssessIncident",
            discriminatorPropertyPath: "IsCrisis",
            discriminatorTypeName: "Boolean",
            isEnumDiscriminator: false,
            isMethodDiscriminator: false,
            cases: cases);
    }
}

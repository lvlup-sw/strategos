// -----------------------------------------------------------------------
// <copyright file="SagaApprovalConstructTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Helpers;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Construct coverage for <c>AwaitApproval</c>: last-on-flow rejection, resume
/// immediately before a fork, and the <see cref="ForkModel.PreviousStepName"/>
/// lookup across an intervening checkpoint.
/// </summary>
[Property("Category", "Unit")]
public sealed class SagaApprovalConstructTests
{
    /// <summary>
    /// <see cref="ForkExtractor"/> walks through an intervening <c>AwaitApproval</c>
    /// when recording <see cref="ForkModel.PreviousStepName"/>, so the fork is keyed
    /// by the gated step — the same name as <see cref="ApprovalModel.PrecedingStepName"/>.
    /// The resume defect is still named by the JOIN: the next main-flow step after that
    /// gated step is the join, and that is the lookup that must change the dispatch.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkExtractor_AwaitApprovalBeforeFork_PreviousStepNameIsTheGatedStep()
    {
        const string code = """
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<ReceiveLoanApplication>()
                        .AwaitApproval<UnderwriterApprover>(approval => approval)
                        .Fork(
                            path => path.Then<ScoreCredit>(),
                            path => path.Then<VerifyIncome>())
                        .Join<MergeAssessment>()
                        .Finally<IssueLoan>();
                }
            }
            """;

        var forks = ForkExtractor.Extract(CreateParseContext(code, "loan-origination"));

        await Assert.That(forks.Count).IsEqualTo(1);
        await Assert.That(forks[0].PreviousStepName)
            .IsEqualTo("ReceiveLoanApplication")
            .Because(
                "AwaitApproval is not a step, so FindPreviousStepName walks through it to the "
                + "gated step. ForksByPreviousStep therefore shares a key with the approval's "
                + "PrecedingStepName; the join-step lookup is still the one that names #182");
        await Assert.That(forks[0].JoinStepName).IsEqualTo("MergeAssessment");
    }

    /// <summary>
    /// When the next main-flow step after an approval is a fork's join, the resume
    /// handler dispatches every fork path and never publishes <c>Start{Join}</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ApprovalResume_ImmediatelyBeforeFork_DispatchesForkNotJoin()
    {
        var sb = new StringBuilder();
        new SagaApprovalComponentEmitter().Emit(sb, CreateApprovalBeforeForkModel());
        var result = sb.ToString();

        await Assert.That(result)
            .Contains("StartScoreCreditCommand")
            .Because("path 0 is reached only through the fork dispatch the resume must emit");

        await Assert.That(result)
            .Contains("StartVerifyIncomeCommand")
            .Because("path 1 must start with path 0; starting only one path hangs the join");

        await Assert.That(result)
            .Contains("Forking_loan_origination_Fork0")
            .Because("the resume must enter the same forking phase the preceding-step dispatch would");

        await Assert.That(result)
            .DoesNotContain("StartMergeAssessmentCommand")
            .Because(
                "the join is the next main-flow step, and resuming onto it skips the fork "
                + "and parks every path at Pending (#182)");
    }

    /// <summary>
    /// An approval whose next main-flow step is an ordinary step (not a join) still
    /// resumes onto that step — the join rewrite must not fire on every successor.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ApprovalResume_LinearSuccessor_StillStartsTheNextMainFlowStep()
    {
        var approval = ApprovalModel.Create(
            approvalPointName: "CreditOfficer",
            approverTypeName: "TestNamespace.CreditOfficerApprover",
            precedingStepName: "AssessCreditRisk");
        var model = new WorkflowModel(
            WorkflowName: "credit-limit-review",
            PascalName: "CreditLimitReview",
            Namespace: "TestNamespace",
            StepNames: ["AssessCreditRisk", "IssueCreditLine", "RecordCreditDecision"],
            StateTypeName: "CreditLimitReviewState",
            ApprovalPoints: [approval]);

        var sb = new StringBuilder();
        new SagaApprovalComponentEmitter().Emit(sb, model);
        var result = sb.ToString();

        await Assert.That(result)
            .Contains("StartIssueCreditLineCommand")
            .Because("a linear successor is not a join, so the resume stays a next-step start");

        await Assert.That(result)
            .DoesNotContain("IEnumerable<object>? Handle(")
            .Because("only the fork-dispatch resume needs to return multiple start commands");
    }

    /// <summary>
    /// The generated saga for <c>Then → AwaitApproval → Fork → Join → Finally</c> resumes
    /// onto the fork dispatch, not <c>Start{Join}</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_ApprovalImmediatelyBeforeFork_ResumeDispatchesFork()
    {
        var result = GeneratorTestHelper.RunGenerator(ApprovalBeforeForkWorkflow);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "LoanOriginationSaga.g.cs");
        var resume = ResumeHandlerBodyFor(saga, "Underwriter");

        await Assert.That(saga)
            .IsNotEmpty()
            .Because("the fixture must compile far enough to emit a saga");

        await Assert.That(resume)
            .Contains("StartScoreCreditCommand")
            .Because("the generated resume must start path 0");

        await Assert.That(resume)
            .Contains("StartVerifyIncomeCommand")
            .Because("the generated resume must start path 1");

        await Assert.That(resume)
            .DoesNotContain("StartMergeAssessmentCommand")
            .Because("Start{Join} is the #182 hang: the join runs with every path still Pending");
    }

    /// <summary>
    /// A last-on-flow approval with a two-step rejection chain emits the chain's first
    /// start command from the resume handler.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_LastOnFlowRejection_ResumeDispatchesFirstRejectionStep()
    {
        var result = GeneratorTestHelper.RunGenerator(LastOnFlowRejectionWorkflow);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "ExpenseReportReviewSaga.g.cs");
        var resume = ResumeHandlerBodyFor(saga, "FinanceController");

        await Assert.That(saga)
            .IsNotEmpty()
            .Because("the fixture must compile far enough to emit a saga");

        await Assert.That(resume)
            .Contains("StartRecordExpenseRefusalCommand")
            .Because("last-on-flow rejection must publish the chain's first start command (#186)");

        await Assert.That(resume)
            .DoesNotContain("subsequent handler")
            .Because("that comment described a handoff that had no trigger");
    }

    /// <summary>
    /// An approval with <c>OnTimeout</c> emits the set-pending and timeout
    /// handlers onto the saga, so a host can inject the timeout command.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_OnTimeout_EmitsSetPendingAndTimeoutHandlers()
    {
        var result = GeneratorTestHelper.RunGenerator(TimeoutEscalationWorkflow);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "WireTransferReviewSaga.g.cs");

        await Assert.That(saga)
            .Contains("SetComplianceOfficerPendingApprovalCommand")
            .Because("the timeout race guard needs PendingApprovalRequestId set on the saga");

        await Assert.That(saga)
            .Contains("ComplianceOfficerApprovalTimeoutCommand")
            .Because("the host injects this command; it must have a saga handler");

        await Assert.That(saga)
            .Contains("StartEscalateToComplianceLeadCommand")
            .Because("the timeout handler must dispatch the first escalation step");
    }

    private static string ResumeHandlerBodyFor(string source, string approvalPointName)
    {
        var marker = $"Resume{approvalPointName}ApprovalCommand cmd";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var brace = source.IndexOf('{', start);
        if (brace < 0)
        {
            return source[start..];
        }

        var depth = 0;
        for (var i = brace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[start..(i + 1)];
                }
            }
        }

        return source[start..];
    }

    private static WorkflowModel CreateApprovalBeforeForkModel()
    {
        var fork = ForkModel.Create(
            forkId: "loan-origination-Fork0",
            previousStepName: "ReceiveLoanApplication",
            paths:
            [
                ForkPathModel.Create(
                    pathIndex: 0,
                    steps: [StepModel.Create("ScoreCredit", "TestNamespace.ScoreCredit")],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
                ForkPathModel.Create(
                    pathIndex: 1,
                    steps: [StepModel.Create("VerifyIncome", "TestNamespace.VerifyIncome")],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
            ],
            joinStepName: "MergeAssessment");

        var approval = ApprovalModel.Create(
            approvalPointName: "Underwriter",
            approverTypeName: "TestNamespace.UnderwriterApprover",
            precedingStepName: "ReceiveLoanApplication");

        return new WorkflowModel(
            WorkflowName: "loan-origination",
            PascalName: "LoanOrigination",
            Namespace: "TestNamespace",
            StepNames:
            [
                "ReceiveLoanApplication",
                "ScoreCredit",
                "VerifyIncome",
                "MergeAssessment",
                "IssueLoan",
            ],
            StateTypeName: "LoanOriginationState",
            ApprovalPoints: [approval],
            Forks: [fork]);
    }

    private static FluentDslParseContext CreateParseContext(string source, string workflowName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return FluentDslParseContext.Create(
            syntaxTree.GetRoot(),
            compilation.GetSemanticModel(syntaxTree),
            workflowName,
            CancellationToken.None);
    }

    private const string ApprovalBeforeForkWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public sealed record LoanOriginationState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public sealed class ReceiveLoanApplication : IWorkflowStep<LoanOriginationState>
        {
            public Task<StepResult<LoanOriginationState>> ExecuteAsync(
                LoanOriginationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanOriginationState>.FromState(state));
        }

        public sealed class ScoreCredit : IWorkflowStep<LoanOriginationState>
        {
            public Task<StepResult<LoanOriginationState>> ExecuteAsync(
                LoanOriginationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanOriginationState>.FromState(state));
        }

        public sealed class VerifyIncome : IWorkflowStep<LoanOriginationState>
        {
            public Task<StepResult<LoanOriginationState>> ExecuteAsync(
                LoanOriginationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanOriginationState>.FromState(state));
        }

        public sealed class MergeAssessment : IWorkflowStep<LoanOriginationState>
        {
            public Task<StepResult<LoanOriginationState>> ExecuteAsync(
                LoanOriginationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanOriginationState>.FromState(state));
        }

        public sealed class IssueLoan : IWorkflowStep<LoanOriginationState>
        {
            public Task<StepResult<LoanOriginationState>> ExecuteAsync(
                LoanOriginationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanOriginationState>.FromState(state));
        }

        public sealed class UnderwriterApprover
        {
        }

        [Workflow("loan-origination")]
        public static partial class LoanOriginationWorkflow
        {
            public static WorkflowDefinition<LoanOriginationState> Definition => Workflow<LoanOriginationState>
                .Create("loan-origination")
                .StartWith<ReceiveLoanApplication>()
                .AwaitApproval<UnderwriterApprover>(approval => approval
                    .WithContext("An underwriter must release the application before scoring.")
                    .WithOption("release", "Release", "Release the application to scoring.", isDefault: true))
                .Fork(
                    path => path.Then<ScoreCredit>(),
                    path => path.Then<VerifyIncome>())
                .Join<MergeAssessment>()
                .Finally<IssueLoan>();
        }
        """;

    private const string LastOnFlowRejectionWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public sealed record ExpenseReportState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public sealed class SubmitExpenseReport : IWorkflowStep<ExpenseReportState>
        {
            public Task<StepResult<ExpenseReportState>> ExecuteAsync(
                ExpenseReportState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ExpenseReportState>.FromState(state));
        }

        public sealed class AttachReceipts : IWorkflowStep<ExpenseReportState>
        {
            public Task<StepResult<ExpenseReportState>> ExecuteAsync(
                ExpenseReportState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ExpenseReportState>.FromState(state));
        }

        public sealed class RecordExpenseRefusal : IWorkflowStep<ExpenseReportState>
        {
            public Task<StepResult<ExpenseReportState>> ExecuteAsync(
                ExpenseReportState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ExpenseReportState>.FromState(state));
        }

        public sealed class NotifyExpenseSubmitter : IWorkflowStep<ExpenseReportState>
        {
            public Task<StepResult<ExpenseReportState>> ExecuteAsync(
                ExpenseReportState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ExpenseReportState>.FromState(state));
        }

        public sealed class FinanceControllerApprover
        {
        }

        [Workflow("expense-report-review")]
        public static partial class ExpenseReportReviewWorkflow
        {
            public static WorkflowDefinition<ExpenseReportState> Definition => Workflow<ExpenseReportState>
                .Create("expense-report-review")
                .StartWith<SubmitExpenseReport>()
                .Then<AttachReceipts>()
                .AwaitApproval<FinanceControllerApprover>(approval => approval
                    .WithContext("A finance controller must accept the expense report.")
                    .WithOption("accept", "Accept", "Accept the expense report.", isDefault: true)
                    .WithOption("refuse", "Refuse", "Refuse the expense report.")
                    .OnRejection(rejection => rejection
                        .Then<RecordExpenseRefusal>()
                        .Then<NotifyExpenseSubmitter>()
                        .Complete()));
        }
        """;

    private const string TimeoutEscalationWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public sealed record WireTransferState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public sealed class SubmitWireTransfer : IWorkflowStep<WireTransferState>
        {
            public Task<StepResult<WireTransferState>> ExecuteAsync(
                WireTransferState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<WireTransferState>.FromState(state));
        }

        public sealed class ReleaseWireTransfer : IWorkflowStep<WireTransferState>
        {
            public Task<StepResult<WireTransferState>> ExecuteAsync(
                WireTransferState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<WireTransferState>.FromState(state));
        }

        public sealed class RecordWireTransfer : IWorkflowStep<WireTransferState>
        {
            public Task<StepResult<WireTransferState>> ExecuteAsync(
                WireTransferState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<WireTransferState>.FromState(state));
        }

        public sealed class EscalateToComplianceLead : IWorkflowStep<WireTransferState>
        {
            public Task<StepResult<WireTransferState>> ExecuteAsync(
                WireTransferState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<WireTransferState>.FromState(state));
        }

        public sealed class ComplianceOfficerApprover
        {
        }

        [Workflow("wire-transfer-review")]
        public static partial class WireTransferReviewWorkflow
        {
            public static WorkflowDefinition<WireTransferState> Definition => Workflow<WireTransferState>
                .Create("wire-transfer-review")
                .StartWith<SubmitWireTransfer>()
                .AwaitApproval<ComplianceOfficerApprover>(approval => approval
                    .WithContext("A compliance officer must release the wire transfer.")
                    .WithOption("release", "Release", "Release the wire transfer.", isDefault: true)
                    .OnTimeout(escalation => escalation
                        .Then<EscalateToComplianceLead>()
                        .Complete()))
                .Then<ReleaseWireTransfer>()
                .Finally<RecordWireTransfer>();
        }
        """;
}

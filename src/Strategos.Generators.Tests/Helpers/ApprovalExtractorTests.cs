// =============================================================================
// <copyright file="ApprovalExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="ApprovalExtractor"/> class.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>AwaitApproval calls are detected and parsed</description></item>
///   <item><description>Approver type names are extracted correctly</description></item>
///   <item><description>Preceding step names are identified correctly</description></item>
///   <item><description>Approval point names are generated correctly</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class ApprovalExtractorTests
{
    // =============================================================================
    // A. Basic Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns empty list when no AwaitApproval calls.
    /// </summary>
    [Test]
    public async Task Extract_NoAwaitApproval_ReturnsEmptyList()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result).Count().IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Extract finds a single AwaitApproval call.
    /// </summary>
    [Test]
    public async Task Extract_WithSingleAwaitApproval_ReturnsOne()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a.WithContext(""Please review""))
        .Then<ProcessStep>()
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class ProcessStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that Extract finds multiple AwaitApproval calls.
    /// </summary>
    [Test]
    public async Task Extract_WithMultipleAwaitApprovals_ReturnsAll()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a.WithContext(""First review""))
        .Then<ProcessStep>()
        .AwaitApproval<DirectorApprover>(a => a.WithContext(""Final review""))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class ProcessStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
public class DirectorApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
    }

    // =============================================================================
    // B. Approver Type Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract captures the approver type name.
    /// </summary>
    [Test]
    public async Task Extract_CapturesApproverTypeName()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a.WithContext(""Please review""))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].ApproverTypeName).IsEqualTo("ManagerApprover");
    }

    // =============================================================================
    // C. Preceding Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract identifies the preceding step name.
    /// </summary>
    [Test]
    public async Task Extract_IdentifiesPrecedingStepName()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a.WithContext(""Please review""))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].PrecedingStepName).IsEqualTo("ValidateStep");
    }

    /// <summary>
    /// Verifies that Extract identifies preceding step when approval follows Then.
    /// </summary>
    [Test]
    public async Task Extract_AfterThen_IdentifiesCorrectPrecedingStep()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .Then<ProcessStep>()
        .AwaitApproval<ManagerApprover>(a => a.WithContext(""Please review""))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class ProcessStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].PrecedingStepName).IsEqualTo("ProcessStep");
    }

    // =============================================================================
    // D. Approval Point Name Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract generates approval point name based on approver.
    /// </summary>
    [Test]
    public async Task Extract_GeneratesApprovalPointName()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a.WithContext(""Please review""))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].ApprovalPointName).IsNotNull().And.IsNotEqualTo(string.Empty);
    }

    /// <summary>
    /// Verifies that PhaseName is correctly derived from ApprovalPointName.
    /// </summary>
    [Test]
    public async Task Extract_PhaseNameHasAwaitApprovalPrefix()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a.WithContext(""Please review""))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].PhaseName).StartsWith("AwaitApproval_");
    }

    // =============================================================================
    // E. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract throws when context is null.
    /// </summary>
    [Test]
    public async Task Extract_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalExtractor.Extract(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // F. OnRejection Parsing Tests (Phase 2)
    // =============================================================================

    /// <summary>
    /// Verifies that Extract captures rejection steps from OnRejection handler.
    /// </summary>
    [Test]
    public async Task Extract_WithOnRejection_CapturesRejectionSteps()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a
            .WithContext(""Please review"")
            .OnRejection(r => r.Then<LogRejectionStep>()))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class LogRejectionStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].HasRejection).IsTrue();
        await Assert.That(result[0].RejectionSteps).IsNotNull();
        await Assert.That(result[0].RejectionSteps!).Count().IsEqualTo(1);
        await Assert.That(result[0].RejectionSteps![0].StepName).IsEqualTo("LogRejectionStep");
    }

    /// <summary>
    /// Verifies that Extract captures multiple rejection steps in order.
    /// </summary>
    [Test]
    public async Task Extract_WithMultipleRejectionSteps_CapturesAllStepsInOrder()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a
            .OnRejection(r => r
                .Then<LogRejectionStep>()
                .Then<NotifyRequesterStep>()))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class LogRejectionStep : IWorkflowStep<TestState> { }
public class NotifyRequesterStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].RejectionSteps!).Count().IsEqualTo(2);
        await Assert.That(result[0].RejectionSteps![0].StepName).IsEqualTo("LogRejectionStep");
        await Assert.That(result[0].RejectionSteps![1].StepName).IsEqualTo("NotifyRequesterStep");
    }

    /// <summary>
    /// Verifies that Extract sets IsRejectionTerminal when Complete() is called.
    /// </summary>
    [Test]
    public async Task Extract_WithOnRejectionTerminal_SetsIsRejectionTerminalTrue()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a
            .OnRejection(r => r
                .Then<LogRejectionStep>()
                .Complete()))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class LogRejectionStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].IsRejectionTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that Extract sets IsRejectionTerminal to false when Complete() is not called.
    /// </summary>
    [Test]
    public async Task Extract_WithOnRejectionNonTerminal_SetsIsRejectionTerminalFalse()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a
            .OnRejection(r => r.Then<LogRejectionStep>()))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class LogRejectionStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].IsRejectionTerminal).IsFalse();
    }

    // =============================================================================
    // G. OnTimeout/Escalation Parsing Tests (Phase 2)
    // =============================================================================

    /// <summary>
    /// Verifies that Extract captures escalation steps from OnTimeout handler.
    /// </summary>
    [Test]
    public async Task Extract_WithOnTimeout_CapturesEscalationSteps()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a
            .WithTimeout(System.TimeSpan.FromHours(4))
            .OnTimeout(e => e.Then<NotifyEscalationStep>()))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class NotifyEscalationStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].HasEscalation).IsTrue();
        await Assert.That(result[0].EscalationSteps).IsNotNull();
        await Assert.That(result[0].EscalationSteps!).Count().IsEqualTo(1);
        await Assert.That(result[0].EscalationSteps![0].StepName).IsEqualTo("NotifyEscalationStep");
    }

    /// <summary>
    /// Verifies that Extract captures nested approval from EscalateTo.
    /// </summary>
    [Test]
    public async Task Extract_WithEscalateTo_CapturesNestedApproval()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a
            .OnTimeout(e => e.EscalateTo<DirectorApprover>(d => d.WithContext(""Escalated""))))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
public class DirectorApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].HasEscalation).IsTrue();
        await Assert.That(result[0].NestedEscalationApprovals).IsNotNull();
        await Assert.That(result[0].NestedEscalationApprovals!).Count().IsEqualTo(1);
        await Assert.That(result[0].NestedEscalationApprovals![0].ApproverTypeName).IsEqualTo("DirectorApprover");
    }

    /// <summary>
    /// Verifies that Extract sets IsEscalationTerminal when Complete() is called.
    /// </summary>
    [Test]
    public async Task Extract_WithEscalationTerminal_SetsIsEscalationTerminalTrue()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a
            .OnTimeout(e => e
                .Then<NotifyEscalationStep>()
                .Complete()))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class NotifyEscalationStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].IsEscalationTerminal).IsTrue();
    }

    // =============================================================================
    // H. Combined Configuration Tests (Phase 2)
    // =============================================================================

    /// <summary>
    /// Verifies that Extract captures both OnTimeout and OnRejection configurations.
    /// </summary>
    [Test]
    public async Task Extract_WithBothOnTimeoutAndOnRejection_CapturesBoth()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a
            .OnTimeout(e => e
                .Then<NotifyEscalationStep>()
                .EscalateTo<DirectorApprover>(d => d.WithContext(""Escalated"")))
            .OnRejection(r => r
                .Then<LogRejectionStep>()
                .Complete()))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class NotifyEscalationStep : IWorkflowStep<TestState> { }
public class LogRejectionStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
public class DirectorApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result).Count().IsEqualTo(1);

        // Verify escalation
        await Assert.That(result[0].HasEscalation).IsTrue();
        await Assert.That(result[0].EscalationSteps!).Count().IsEqualTo(1);
        await Assert.That(result[0].NestedEscalationApprovals!).Count().IsEqualTo(1);
        await Assert.That(result[0].IsEscalationTerminal).IsFalse();

        // Verify rejection
        await Assert.That(result[0].HasRejection).IsTrue();
        await Assert.That(result[0].RejectionSteps!).Count().IsEqualTo(1);
        await Assert.That(result[0].IsRejectionTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that Extract captures nested approval with correct approver type name.
    /// </summary>
    [Test]
    public async Task Extract_WithNestedApprovalAndSteps_CapturesBothInEscalation()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .AwaitApproval<ManagerApprover>(a => a
            .OnTimeout(e => e
                .Then<NotifyEscalationStep>()
                .Then<PrepareEscalationStep>()
                .EscalateTo<DirectorApprover>(d => d.WithContext(""Escalated""))))
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class NotifyEscalationStep : IWorkflowStep<TestState> { }
public class PrepareEscalationStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
public class DirectorApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].EscalationSteps!).Count().IsEqualTo(2);
        await Assert.That(result[0].EscalationSteps![0].StepName).IsEqualTo("NotifyEscalationStep");
        await Assert.That(result[0].EscalationSteps![1].StepName).IsEqualTo("PrepareEscalationStep");
        await Assert.That(result[0].NestedEscalationApprovals!).Count().IsEqualTo(1);
        await Assert.That(result[0].NestedEscalationApprovals![0].ApprovalPointName).IsEqualTo("Director");
    }

    // =============================================================================
    // I. Approval Inside Branch Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract finds approvals nested inside BranchCase lambda expressions.
    /// This is critical for workflows that have conditional approval paths.
    /// </summary>
    [Test]
    public async Task Extract_WithApprovalInsideBranch_FindsApproval()
    {
        // Arrange - Simulates the Orchestrator workflow pattern
        var source = @"
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;

[Workflow(""test-workflow"")]
public static partial class TestWorkflowDefinition
{
    public static WorkflowDefinition<TestState> Definition =>
        Workflow<TestState>
            .Create(""test"")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ShouldEscalate ? 1 : 0,
                BranchCase<TestState, int>.When(1, path => path
                    .AwaitApproval<ManagerApprover>(a => a
                        .WithContext(""Needs approval"")
                        .OnTimeout(e => e.Then<AutoFailStep>())
                        .OnRejection(r => r.Then<TerminateStep>()))
                    .Then<CompleteStep>()
                    .Complete()),
                BranchCase<TestState, int>.Otherwise(path => path
                    .Then<CompleteStep>()
                    .Complete()))
            .Finally<CompleteStep>();
}

public class TestState : IWorkflowState
{
    public bool ShouldEscalate { get; set; }
}
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class AutoFailStep : IWorkflowStep<TestState> { }
public class TerminateStep : IWorkflowStep<TestState> { }
public class ManagerApprover { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = ApprovalExtractor.Extract(context);

        // Assert
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].ApprovalPointName).IsEqualTo("Manager");

        // Verify rejection steps
        await Assert.That(result[0].HasRejection).IsTrue();
        await Assert.That(result[0].RejectionSteps!).Count().IsEqualTo(1);
        await Assert.That(result[0].RejectionSteps![0].StepName).IsEqualTo("TerminateStep");

        // Verify escalation steps
        await Assert.That(result[0].HasEscalation).IsTrue();
        await Assert.That(result[0].EscalationSteps!).Count().IsEqualTo(1);
        await Assert.That(result[0].EscalationSteps![0].StepName).IsEqualTo("AutoFailStep");
    }
}

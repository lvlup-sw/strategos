// -----------------------------------------------------------------------
// <copyright file="TransitionsEmitterUnitTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Unit tests for the <see cref="TransitionsEmitter"/> class.
/// </summary>
/// <remarks>
/// These tests verify transition table generation in isolation, independent of the source generator.
/// </remarks>
[Property("Category", "Unit")]
public class TransitionsEmitterUnitTests
{
    // =============================================================================
    // A. Transition Class Structure Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter generates a transitions class.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_GeneratesTransitionsClass()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public static partial class ProcessOrderTransitions");
    }

    /// <summary>
    /// Verifies that the transitions class has a ValidTransitions dictionary.
    /// </summary>
    [Test]
    public async Task Emit_TransitionsClass_HasValidTransitionsDictionary()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("IReadOnlyDictionary<ProcessOrderPhase, ProcessOrderPhase[]>");
        await Assert.That(source).Contains("ValidTransitions");
    }

    // =============================================================================
    // B. Transition Entries Tests
    // =============================================================================

    /// <summary>
    /// Verifies that NotStarted transitions to the first step.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_NotStartedToFirstStep()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderPhase.NotStarted, [ProcessOrderPhase.ValidateOrder]");
    }

    /// <summary>
    /// Verifies that steps transition sequentially.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_StepsTransitionSequentially()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderPhase.ValidateOrder, [ProcessOrderPhase.ProcessPayment, ProcessOrderPhase.Failed]");
        await Assert.That(source).Contains("ProcessOrderPhase.ProcessPayment, [ProcessOrderPhase.SendConfirmation, ProcessOrderPhase.Failed]");
    }

    /// <summary>
    /// Verifies that the last step transitions to Completed.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_LastStepToCompleted()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderPhase.SendConfirmation, [ProcessOrderPhase.Completed, ProcessOrderPhase.Failed]");
    }

    /// <summary>
    /// Verifies that terminal phases have no transitions.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_TerminalPhasesHaveNoTransitions()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderPhase.Completed, []");
        await Assert.That(source).Contains("ProcessOrderPhase.Failed, []");
    }

    /// <summary>
    /// Verifies that IsValidTransition helper method is generated.
    /// </summary>
    [Test]
    public async Task Emit_Transitions_GeneratesIsValidTransitionMethod()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public static bool IsValidTransition(ProcessOrderPhase from, ProcessOrderPhase to)");
    }

    /// <summary>
    /// Verifies IsValidTransition uses ValidTransitions dictionary.
    /// </summary>
    [Test]
    public async Task Emit_IsValidTransition_UsesValidTransitionsDictionary()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ValidTransitions.TryGetValue(from, out var validTargets)");
    }

    // =============================================================================
    // B2. Real-Graph Entry Tests
    // =============================================================================

    /// <summary>
    /// Verifies that each fork path's last step reaches the join rather than the entry that
    /// happens to follow it in the step-name list.
    /// </summary>
    [Test]
    public async Task Emit_ForkWorkflow_PathLastStepReachesJoin()
    {
        // Arrange
        var model = CreateForkModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert - both parallel paths converge on the join
        await Assert.That(source).Contains("ParallelOrderPhase.ProcessPayment, [ParallelOrderPhase.SynthesizeResults, ParallelOrderPhase.Failed]");
        await Assert.That(source).Contains("ParallelOrderPhase.ReserveInventory, [ParallelOrderPhase.SynthesizeResults, ParallelOrderPhase.Failed]");

        // Assert - the fork's predecessor dispatches both paths at once
        await Assert.That(source).Contains("ParallelOrderPhase.ValidateOrder, [ParallelOrderPhase.ProcessPayment, ParallelOrderPhase.ReserveInventory, ParallelOrderPhase.Failed]");
    }

    /// <summary>
    /// Verifies that a branch case which ends the workflow completes at its own last step, and
    /// that its non-terminal sibling still converges on the rejoin step.
    /// </summary>
    [Test]
    public async Task Emit_TerminalBranchCase_CompletesInsteadOfChaining()
    {
        // Arrange
        var model = CreateTerminalBranchModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ValidateOrderPhase.RejectOrder, [ValidateOrderPhase.Completed, ValidateOrderPhase.Failed]");
        await Assert.That(source).Contains("ValidateOrderPhase.ProcessOrder, [ValidateOrderPhase.ShipOrder, ValidateOrderPhase.Failed]");
        await Assert.That(source).DoesNotContain("ValidateOrderPhase.RejectOrder, [ValidateOrderPhase.ShipOrder");
    }

    // =============================================================================
    // C. Header and Namespace Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the auto-generated header is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesAutoGeneratedHeader()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("// <auto-generated/>");
    }

    /// <summary>
    /// Verifies that nullable enable directive is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesNullableEnable()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("#nullable enable");
    }

    /// <summary>
    /// Verifies that the emitter uses the correct namespace.
    /// </summary>
    [Test]
    public async Task Emit_Transitions_UsesCorrectNamespace()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("namespace TestNamespace;");
    }

    // =============================================================================
    // C. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that null model throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Emit_WithNullModel_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => TransitionsEmitter.Emit(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateTestModel()
    {
        return new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment", "SendConfirmation"],
            StateTypeName: "OrderState");
    }

    /// <summary>
    /// Mirrors what the fork extractor produces for
    /// <c>.StartWith&lt;ValidateOrder&gt;().Fork(ProcessPayment, ReserveInventory)
    /// .Join&lt;SynthesizeResults&gt;().Finally&lt;SendConfirmation&gt;()</c>: both path steps and the
    /// join step appear in the step-name list, so consecutive entries chain two parallel paths.
    /// </summary>
    private static WorkflowModel CreateForkModel()
    {
        var forks = new List<ForkModel>
        {
            new(
                ForkId: "ParallelOrder-Fork0",
                PreviousStepName: "ValidateOrder",
                Paths:
                [
                    new(
                        PathIndex: 0,
                        Steps: [StepModel.Create("ProcessPayment", "TestNamespace.ProcessPayment")],
                        HasFailureHandler: false,
                        IsTerminalOnFailure: false),
                    new(
                        PathIndex: 1,
                        Steps: [StepModel.Create("ReserveInventory", "TestNamespace.ReserveInventory")],
                        HasFailureHandler: false,
                        IsTerminalOnFailure: false),
                ],
                JoinStepName: "SynthesizeResults"),
        };

        return new WorkflowModel(
            WorkflowName: "parallel-order",
            PascalName: "ParallelOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment", "ReserveInventory", "SynthesizeResults", "SendConfirmation"],
            StateTypeName: "OrderState",
            Forks: forks);
    }

    /// <summary>
    /// Mirrors what the branch extractor produces for a two-case branch whose second case calls
    /// <c>.Complete()</c>. Case step names are the bare step names — the branch path prefix is a
    /// separate field on the case, never folded into the step name.
    /// </summary>
    private static WorkflowModel CreateTerminalBranchModel()
    {
        var branches = new List<BranchModel>
        {
            new(
                BranchId: "validate-order-Branch0-IsValid",
                PreviousStepName: "ValidateOrder",
                DiscriminatorPropertyPath: "IsValid",
                DiscriminatorTypeName: "bool",
                IsEnumDiscriminator: false,
                IsMethodDiscriminator: false,
                Cases:
                [
                    new(
                        CaseValueLiteral: "true",
                        BranchPathPrefix: "IsValid_true",
                        StepNames: ["ProcessOrder"],
                        IsTerminal: false),
                    new(
                        CaseValueLiteral: "false",
                        BranchPathPrefix: "IsValid_false",
                        StepNames: ["RejectOrder"],
                        IsTerminal: true),
                ],
                RejoinStepName: "ShipOrder"),
        };

        return new WorkflowModel(
            WorkflowName: "validate-order",
            PascalName: "ValidateOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessOrder", "RejectOrder", "ShipOrder"],
            StateTypeName: "OrderState",
            Branches: branches);
    }
}

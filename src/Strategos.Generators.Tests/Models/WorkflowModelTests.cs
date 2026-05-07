// -----------------------------------------------------------------------
// <copyright file="WorkflowModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for the <see cref="WorkflowModel"/> record.
/// </summary>
[Property("Category", "Unit")]
public class WorkflowModelTests
{
    // =============================================================================
    // A. Version Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Version defaults to 1 when not specified.
    /// </summary>
    [Test]
    public async Task Version_WhenNotSpecified_DefaultsToOne()
    {
        // Arrange & Act
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1", "Step2"]);

        // Assert
        await Assert.That(model.Version).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that Version can be set explicitly.
    /// </summary>
    [Test]
    public async Task Version_WhenSpecified_RetainsValue()
    {
        // Arrange & Act
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            Version: 3);

        // Assert
        await Assert.That(model.Version).IsEqualTo(3);
    }

    // =============================================================================
    // B. SagaClassName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that SagaClassName for version 1 does not include version suffix.
    /// </summary>
    [Test]
    public async Task SagaClassName_WhenVersionIsOne_ReturnsNameWithoutSuffix()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            Version: 1);

        // Act & Assert
        await Assert.That(model.SagaClassName).IsEqualTo("ProcessOrderSaga");
    }

    /// <summary>
    /// Verifies that SagaClassName for version 2 includes version suffix.
    /// </summary>
    [Test]
    public async Task SagaClassName_WhenVersionIsTwo_ReturnsNameWithV2Suffix()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            Version: 2);

        // Act & Assert
        await Assert.That(model.SagaClassName).IsEqualTo("ProcessOrderSagaV2");
    }

    /// <summary>
    /// Verifies that SagaClassName for higher versions includes correct suffix.
    /// </summary>
    [Test]
    public async Task SagaClassName_WhenVersionIsHigher_ReturnsNameWithCorrectSuffix()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "iterative-refinement",
            PascalName: "IterativeRefinement",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            Version: 5);

        // Act & Assert
        await Assert.That(model.SagaClassName).IsEqualTo("IterativeRefinementSagaV5");
    }

    // =============================================================================
    // C. PhaseEnumName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that PhaseEnumName is derived correctly from PascalName.
    /// </summary>
    [Test]
    public async Task PhaseEnumName_ReturnsCorrectDerivedName()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"]);

        // Act & Assert
        await Assert.That(model.PhaseEnumName).IsEqualTo("ProcessOrderPhase");
    }

    // =============================================================================
    // D. Record Properties Tests
    // =============================================================================

    /// <summary>
    /// Verifies that all properties are correctly set.
    /// </summary>
    [Test]
    public async Task Constructor_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var stepNames = new List<string> { "ValidateOrder", "ProcessPayment" };

        // Act
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: stepNames,
            StateTypeName: "OrderState",
            Version: 2);

        // Assert
        await Assert.That(model.WorkflowName).IsEqualTo("process-order");
        await Assert.That(model.PascalName).IsEqualTo("ProcessOrder");
        await Assert.That(model.Namespace).IsEqualTo("TestNamespace");
        await Assert.That(model.StepNames).IsEquivalentTo(stepNames);
        await Assert.That(model.StateTypeName).IsEqualTo("OrderState");
        await Assert.That(model.Version).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that StateTypeName defaults to null.
    /// </summary>
    [Test]
    public async Task StateTypeName_WhenNotSpecified_DefaultsToNull()
    {
        // Arrange & Act
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"]);

        // Assert
        await Assert.That(model.StateTypeName).IsNull();
    }

    // =============================================================================
    // E. ReducerTypeName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ReducerTypeName is computed from StateTypeName.
    /// </summary>
    [Test]
    public async Task ReducerTypeName_WhenStateTypeNameIsSet_ReturnsReducerName()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            StateTypeName: "OrderState");

        // Act & Assert
        await Assert.That(model.ReducerTypeName).IsEqualTo("OrderStateReducer");
    }

    /// <summary>
    /// Verifies that ReducerTypeName returns null when StateTypeName is null.
    /// </summary>
    [Test]
    public async Task ReducerTypeName_WhenStateTypeNameIsNull_ReturnsNull()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"]);

        // Act & Assert
        await Assert.That(model.ReducerTypeName).IsNull();
    }

    // =============================================================================
    // F. Steps Collection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Steps collection defaults to null when not specified.
    /// </summary>
    [Test]
    public async Task Steps_WhenNotSpecified_DefaultsToNull()
    {
        // Arrange & Act
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"]);

        // Assert
        await Assert.That(model.Steps).IsNull();
    }

    /// <summary>
    /// Verifies that Steps collection retains provided StepModels.
    /// </summary>
    [Test]
    public async Task Steps_WhenSpecified_ContainsStepModels()
    {
        // Arrange
        var steps = new List<StepModel>
        {
            new("ValidateOrder", "TestNamespace.ValidateOrder"),
            new("ProcessPayment", "TestNamespace.ProcessPayment"),
        };

        // Act
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            Steps: steps);

        // Assert
        await Assert.That(model.Steps).IsNotNull();
        await Assert.That(model.Steps!.Count).IsEqualTo(2);
        await Assert.That(model.Steps[0].StepName).IsEqualTo("ValidateOrder");
        await Assert.That(model.Steps[0].StepTypeName).IsEqualTo("TestNamespace.ValidateOrder");
        await Assert.That(model.Steps[1].StepName).IsEqualTo("ProcessPayment");
        await Assert.That(model.Steps[1].StepTypeName).IsEqualTo("TestNamespace.ProcessPayment");
    }

    /// <summary>
    /// Verifies that Steps collection preserves step type names correctly.
    /// </summary>
    [Test]
    public async Task Steps_PreservesFullyQualifiedTypeNames()
    {
        // Arrange
        var steps = new List<StepModel>
        {
            new("SendConfirmation", "MyApp.Workflows.Steps.SendConfirmation"),
        };

        // Act
        var model = new WorkflowModel(
            WorkflowName: "notify",
            PascalName: "Notify",
            Namespace: "TestNamespace",
            StepNames: ["SendConfirmation"],
            Steps: steps);

        // Assert
        await Assert.That(model.Steps![0].StepTypeName)
            .IsEqualTo("MyApp.Workflows.Steps.SendConfirmation");
    }

    // =============================================================================
    // G. Loops Collection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Loops collection defaults to null when not specified.
    /// </summary>
    [Test]
    public async Task Loops_WhenNotSpecified_DefaultsToNull()
    {
        // Arrange & Act
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"]);

        // Assert
        await Assert.That(model.Loops).IsNull();
    }

    /// <summary>
    /// Verifies that Loops collection retains provided LoopModels.
    /// </summary>
    [Test]
    public async Task Loops_WhenSpecified_ContainsLoopModels()
    {
        // Arrange
        var loops = new List<LoopModel>
        {
            new(
                LoopName: "Refinement",
                ConditionId: "ProcessClaim-Refinement",
                MaxIterations: 5,
                FirstBodyStepName: "Refinement_CritiqueStep",
                LastBodyStepName: "Refinement_RefineStep",
                ContinuationStepName: "PublishResult",
                ParentLoopName: null),
        };

        // Act
        var model = new WorkflowModel(
            WorkflowName: "process-claim",
            PascalName: "ProcessClaim",
            Namespace: "TestNamespace",
            StepNames: ["Refinement_CritiqueStep", "Refinement_RefineStep", "PublishResult"],
            Loops: loops);

        // Assert
        await Assert.That(model.Loops).IsNotNull();
        await Assert.That(model.Loops!.Count).IsEqualTo(1);
        await Assert.That(model.Loops[0].LoopName).IsEqualTo("Refinement");
        await Assert.That(model.Loops[0].MaxIterations).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that HasLoops returns true when Loops collection is non-empty.
    /// </summary>
    [Test]
    public async Task HasLoops_WhenLoopsExist_ReturnsTrue()
    {
        // Arrange
        var loops = new List<LoopModel>
        {
            new("Refinement", "ProcessClaim-Refinement", 5, "Refinement_Step", "Refinement_Step", "Done", null),
        };

        var model = new WorkflowModel(
            WorkflowName: "process-claim",
            PascalName: "ProcessClaim",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            Loops: loops);

        // Assert
        await Assert.That(model.HasLoops).IsTrue();
    }

    /// <summary>
    /// Verifies that HasLoops returns false when Loops is null.
    /// </summary>
    [Test]
    public async Task HasLoops_WhenLoopsNull_ReturnsFalse()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"]);

        // Assert
        await Assert.That(model.HasLoops).IsFalse();
    }

    /// <summary>
    /// Verifies that HasLoops returns false when Loops is empty.
    /// </summary>
    [Test]
    public async Task HasLoops_WhenLoopsEmpty_ReturnsFalse()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            Loops: []);

        // Assert
        await Assert.That(model.HasLoops).IsFalse();
    }

    // =============================================================================
    // H. Branches Collection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Branches collection defaults to null when not specified.
    /// </summary>
    [Test]
    public async Task Branches_WhenNotSpecified_DefaultsToNull()
    {
        // Arrange & Act
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"]);

        // Assert
        await Assert.That(model.Branches).IsNull();
    }

    /// <summary>
    /// Verifies that Branches collection retains provided BranchModels.
    /// </summary>
    [Test]
    public async Task Branches_WhenSpecified_ContainsBranchModels()
    {
        // Arrange
        var cases = new List<BranchCaseModel>
        {
            new("OrderStatus.Approved", "Approved", ["ProcessPayment"], false),
            new("OrderStatus.Rejected", "Rejected", ["NotifyRejection"], true),
        };

        var branches = new List<BranchModel>
        {
            new(
                BranchId: "ProcessOrder-Status",
                PreviousStepName: "ValidateOrder",
                DiscriminatorPropertyPath: "Status",
                DiscriminatorTypeName: "OrderStatus",
                IsEnumDiscriminator: true,
                IsMethodDiscriminator: false,
                Cases: cases,
                RejoinStepName: "Complete"),
        };

        // Act
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "Approved_ProcessPayment", "Rejected_NotifyRejection", "Complete"],
            Branches: branches);

        // Assert
        await Assert.That(model.Branches).IsNotNull();
        await Assert.That(model.Branches!.Count).IsEqualTo(1);
        await Assert.That(model.Branches[0].BranchId).IsEqualTo("ProcessOrder-Status");
        await Assert.That(model.Branches[0].Cases).Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that HasBranches returns true when Branches collection is non-empty.
    /// </summary>
    [Test]
    public async Task HasBranches_WhenBranchesExist_ReturnsTrue()
    {
        // Arrange
        var branches = new List<BranchModel>
        {
            new("ProcessOrder-Status", "ValidateOrder", "Status", "OrderStatus", true, false, [], "Complete"),
        };

        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            Branches: branches);

        // Assert
        await Assert.That(model.HasBranches).IsTrue();
    }

    /// <summary>
    /// Verifies that HasBranches returns false when Branches is null.
    /// </summary>
    [Test]
    public async Task HasBranches_WhenBranchesNull_ReturnsFalse()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"]);

        // Assert
        await Assert.That(model.HasBranches).IsFalse();
    }

    /// <summary>
    /// Verifies that HasBranches returns false when Branches is empty.
    /// </summary>
    [Test]
    public async Task HasBranches_WhenBranchesEmpty_ReturnsFalse()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            Branches: []);

        // Assert
        await Assert.That(model.HasBranches).IsFalse();
    }

    // =============================================================================
    // I. Combined Loops and Branches Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WorkflowModel can have both loops and branches.
    /// </summary>
    [Test]
    public async Task Constructor_WithLoopsAndBranches_RetainsBoth()
    {
        // Arrange
        var loops = new List<LoopModel>
        {
            new("Refinement", "Workflow-Refinement", 3, "Refinement_Step", "Refinement_Step", "Done", null),
        };

        var branches = new List<BranchModel>
        {
            new("Workflow-Status", "ValidateOrder", "Status", "OrderStatus", true, false, [], "Complete"),
        };

        // Act
        var model = new WorkflowModel(
            WorkflowName: "complex-workflow",
            PascalName: "ComplexWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["Step1"],
            Loops: loops,
            Branches: branches);

        // Assert
        await Assert.That(model.HasLoops).IsTrue();
        await Assert.That(model.HasBranches).IsTrue();
        await Assert.That(model.Loops!.Count).IsEqualTo(1);
        await Assert.That(model.Branches!.Count).IsEqualTo(1);
    }

    // =============================================================================
    // J. HasAnyValidation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that HasAnyValidation returns true when at least one step has validation.
    /// </summary>
    [Test]
    public async Task HasAnyValidation_WithValidatedStep_ReturnsTrue()
    {
        // Arrange
        var steps = new List<StepModel>
        {
            new("ValidateOrder", "TestNamespace.ValidateOrder"),
            new("ProcessPayment", "TestNamespace.ProcessPayment", ValidationPredicate: "state.Total > 0", ValidationErrorMessage: "Total must be positive"),
        };

        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            Steps: steps);

        // Assert
        await Assert.That(model.HasAnyValidation).IsTrue();
    }

    /// <summary>
    /// Verifies that HasAnyValidation returns false when no steps have validation.
    /// </summary>
    [Test]
    public async Task HasAnyValidation_WithoutValidation_ReturnsFalse()
    {
        // Arrange
        var steps = new List<StepModel>
        {
            new("ValidateOrder", "TestNamespace.ValidateOrder"),
            new("ProcessPayment", "TestNamespace.ProcessPayment"),
        };

        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            Steps: steps);

        // Assert
        await Assert.That(model.HasAnyValidation).IsFalse();
    }
}

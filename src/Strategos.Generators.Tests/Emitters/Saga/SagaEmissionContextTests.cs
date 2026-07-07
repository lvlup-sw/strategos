// -----------------------------------------------------------------------
// <copyright file="SagaEmissionContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

using TUnit.Core;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for <see cref="SagaEmissionContext"/>.
/// </summary>
[Property("Category", "Unit")]
public class SagaEmissionContextTests
{
    // ====================================================================
    // Section A: Guard Clause Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create throws ArgumentNullException when model is null.
    /// </summary>
    [Test]
    public async Task Create_NullModel_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => SagaEmissionContext.Create(null!))
            .Throws<ArgumentNullException>();
    }

    // ====================================================================
    // Section B: Basic Creation Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create returns a non-null context for a valid model.
    /// </summary>
    [Test]
    public async Task Create_ValidModel_ReturnsNonNullContext()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context).IsNotNull();
    }

    /// <summary>
    /// Verifies that Create stores the model.
    /// </summary>
    [Test]
    public async Task Create_ValidModel_StoresModel()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(ReferenceEquals(context.Model, model)).IsTrue();
    }

    /// <summary>
    /// Verifies that Create computes the saga class name.
    /// </summary>
    [Test]
    public async Task Create_ValidModel_ComputesSagaClassName()
    {
        // Arrange
        var model = CreateMinimalModel(pascalName: "ProcessOrder");

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.SagaClassName).IsEqualTo("ProcessOrderSaga");
    }

    /// <summary>
    /// Verifies that Create includes version suffix when version > 1.
    /// </summary>
    [Test]
    public async Task Create_ModelWithVersion_StoresSagaClassNameWithVersionSuffix()
    {
        // Arrange
        var model = CreateMinimalModel(pascalName: "ProcessOrder", version: 2);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.SagaClassName).IsEqualTo("ProcessOrderSagaV2");
    }

    // ====================================================================
    // Section C: Loop Lookup Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create returns empty loops dictionary when model has no loops.
    /// </summary>
    [Test]
    public async Task Create_ModelWithoutLoops_ReturnsEmptyLoopsDictionary()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.LoopsByLastStep).IsNotNull();
        await Assert.That(context.LoopsByLastStep.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Create builds loops lookup keyed by last step name.
    /// </summary>
    [Test]
    public async Task Create_ModelWithLoops_BuildsLoopsByLastStepLookup()
    {
        // Arrange
        var loop = CreateLoop("Refinement", lastBodyStepName: "Refine");
        var model = CreateMinimalModel(loops: [loop]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.LoopsByLastStep.ContainsKey("Refine")).IsTrue();
        await Assert.That(context.LoopsByLastStep["Refine"].Count).IsEqualTo(1);
        await Assert.That(context.LoopsByLastStep["Refine"][0].LoopName).IsEqualTo("Refinement");
    }

    /// <summary>
    /// Verifies that nested loops are ordered innermost first.
    /// </summary>
    [Test]
    public async Task Create_ModelWithNestedLoops_OrdersLoopsInnermostFirst()
    {
        // Arrange
        // Inner loop has parent, so its FullPrefix has more underscores (deeper nesting)
        var outerLoop = CreateLoop("Outer", lastBodyStepName: "SharedStep", parentLoopName: null);
        var innerLoop = CreateLoop("Inner", lastBodyStepName: "SharedStep", parentLoopName: "Outer");
        var model = CreateMinimalModel(loops: [outerLoop, innerLoop]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.LoopsByLastStep.ContainsKey("SharedStep")).IsTrue();
        var loops = context.LoopsByLastStep["SharedStep"];
        await Assert.That(loops.Count).IsEqualTo(2);
        await Assert.That(loops[0].LoopName).IsEqualTo("Inner"); // Innermost first
        await Assert.That(loops[1].LoopName).IsEqualTo("Outer"); // Outermost second
    }

    // ====================================================================
    // Section D: Branch Lookup Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create returns empty branches dictionary when model has no branches.
    /// </summary>
    [Test]
    public async Task Create_ModelWithoutBranches_ReturnsEmptyBranchesDictionary()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.BranchesByPreviousStep).IsNotNull();
        await Assert.That(context.BranchesByPreviousStep.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Create builds branches lookup keyed by previous step name.
    /// </summary>
    [Test]
    public async Task Create_ModelWithBranches_BuildsBranchesByPreviousStepLookup()
    {
        // Arrange
        var branch = CreateBranch("Status", previousStepName: "Validate");
        var model = CreateMinimalModel(branches: [branch]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.BranchesByPreviousStep.ContainsKey("Validate")).IsTrue();
        await Assert.That(context.BranchesByPreviousStep["Validate"].BranchId).IsEqualTo("Status");
    }

    /// <summary>
    /// Verifies that Create builds branch path info for non-terminal branch cases.
    /// </summary>
    [Test]
    public async Task Create_ModelWithBranchPaths_BuildsBranchPathInfoLookup()
    {
        // Arrange
        var branchCase = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["Approved_Process", "Approved_Complete"],
            isTerminal: false);
        var branch = BranchModel.Create(
            branchId: "Status",
            previousStepName: "Validate",
            discriminatorPropertyPath: "Status",
            discriminatorTypeName: "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            rejoinStepName: "Finalize",
            cases: [branchCase]);
        var model = CreateMinimalModel(branches: [branch]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.BranchPathInfo.ContainsKey("Approved_Complete")).IsTrue();
        var pathInfo = context.BranchPathInfo["Approved_Complete"];
        await Assert.That(pathInfo.Branch.BranchId).IsEqualTo("Status");
        await Assert.That(pathInfo.Case.BranchPathPrefix).IsEqualTo("Approved");
    }

    /// <summary>
    /// Verifies that terminal branch cases are not included in path info.
    /// </summary>
    [Test]
    public async Task Create_ModelWithTerminalBranch_ExcludesFromPathInfo()
    {
        // Arrange
        var terminalCase = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Rejected",
            branchPathPrefix: "Rejected",
            stepNames: ["Rejected_Handle"],
            isTerminal: true);
        var branch = BranchModel.Create(
            branchId: "Status",
            previousStepName: "Validate",
            discriminatorPropertyPath: "Status",
            discriminatorTypeName: "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            rejoinStepName: null,
            cases: [terminalCase]);
        var model = CreateMinimalModel(branches: [branch]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.BranchPathInfo.ContainsKey("Rejected_Handle")).IsFalse();
    }

    // ====================================================================
    // Section E: Step Lookup Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create returns empty steps dictionary when model has no steps.
    /// </summary>
    [Test]
    public async Task Create_ModelWithoutSteps_ReturnsEmptyStepsDictionary()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.StepsByName).IsNotNull();
        await Assert.That(context.StepsByName.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Create builds steps lookup keyed by step name.
    /// </summary>
    [Test]
    public async Task Create_ModelWithSteps_BuildsStepsByNameLookup()
    {
        // Arrange
        var step = StepModel.Create(
            stepName: "Validate",
            stepTypeName: "Test.ValidateStep",
            validationPredicate: "state.IsValid",
            validationErrorMessage: "State is not valid");
        var model = CreateMinimalModel(steps: [step]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.StepsByName.ContainsKey("Validate")).IsTrue();
        await Assert.That(context.StepsByName["Validate"].HasValidation).IsTrue();
    }

    // ====================================================================
    // Helper Methods
    // ====================================================================

    private static WorkflowModel CreateMinimalModel(
        string? pascalName = null,
        int version = 1,
        IReadOnlyList<LoopModel>? loops = null,
        IReadOnlyList<BranchModel>? branches = null,
        IReadOnlyList<StepModel>? steps = null)
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: pascalName ?? "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["Step1", "Step2"],
            StateTypeName: "TestState",
            Version: version,
            Loops: loops,
            Branches: branches,
            Steps: steps);
    }

    private static LoopModel CreateLoop(
        string loopName,
        string? lastBodyStepName = null,
        string? parentLoopName = null)
    {
        var lastName = lastBodyStepName ?? $"{loopName}_End";
        return LoopModel.Create(
            loopName: loopName,
            conditionId: $"TestWorkflow-{loopName}",
            maxIterations: 5,
            bodySteps:
            [
                StepModel.Create($"{loopName}_Start", $"TestNamespace.{loopName}_Start"),
                StepModel.Create(lastName, $"TestNamespace.{lastName}"),
            ],
            continuationStepName: null,
            parentLoopName: parentLoopName);
    }

    private static BranchModel CreateBranch(
        string branchId,
        string previousStepName)
    {
        var branchCase = BranchCaseModel.Create(
            caseValueLiteral: "TestValue",
            branchPathPrefix: "TestPath",
            stepNames: ["TestPath_Step"],
            isTerminal: false);

        return BranchModel.Create(
            branchId: branchId,
            previousStepName: previousStepName,
            discriminatorPropertyPath: "Property",
            discriminatorTypeName: "TestType",
            isEnumDiscriminator: false,
            isMethodDiscriminator: false,
            rejoinStepName: "RejoinStep",
            cases: [branchCase]);
    }
}

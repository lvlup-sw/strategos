// -----------------------------------------------------------------------
// <copyright file="LoopModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="LoopModel"/> record.
/// </summary>
[Property("Category", "Unit")]
public class LoopModelTests
{
    /// <summary>
    /// Builds a bare configured step whose <see cref="StepModel.PhaseName"/> equals the supplied
    /// (already loop-prefixed) name, so the loop's <c>FirstBodyStepName</c>/<c>LastBodyStepName</c>
    /// projections resolve to the expected prefixed names.
    /// </summary>
    private static StepModel Step(string phaseName) => StepModel.Create(phaseName, $"TestNamespace.{phaseName}");

    // =============================================================================
    // A. Constructor Tests
    // =============================================================================

    /// <summary>
    /// Verifies that LoopModel can be created with valid parameters.
    /// </summary>
    [Test]
    public async Task Constructor_WithValidParameters_CreatesModel()
    {
        // Arrange & Act
        var model = new LoopModel(
            LoopName: "Refinement",
            ConditionId: "ProcessClaim-Refinement",
            MaxIterations: 5,
            BodySteps: [Step("Refinement_CritiqueStep"), Step("Refinement_RefineStep")],
            ContinuationStepName: "PublishResult",
            ParentLoopName: null);

        // Assert
        await Assert.That(model.LoopName).IsEqualTo("Refinement");
        await Assert.That(model.ConditionId).IsEqualTo("ProcessClaim-Refinement");
        await Assert.That(model.MaxIterations).IsEqualTo(5);
        await Assert.That(model.FirstBodyStepName).IsEqualTo("Refinement_CritiqueStep");
        await Assert.That(model.LastBodyStepName).IsEqualTo("Refinement_RefineStep");
        await Assert.That(model.ContinuationStepName).IsEqualTo("PublishResult");
        await Assert.That(model.ParentLoopName).IsNull();
    }

    /// <summary>
    /// Verifies that LoopModel with null continuation step is valid (terminal loop).
    /// </summary>
    [Test]
    public async Task Constructor_WithNullContinuationStep_IsValid()
    {
        // Arrange & Act
        var model = new LoopModel(
            LoopName: "FinalLoop",
            ConditionId: "Workflow-FinalLoop",
            MaxIterations: 3,
            BodySteps: [Step("FinalLoop_Step")],
            ContinuationStepName: null,
            ParentLoopName: null);

        // Assert
        await Assert.That(model.ContinuationStepName).IsNull();
    }

    // =============================================================================
    // B. Nested Loop Tests
    // =============================================================================

    /// <summary>
    /// Verifies that nested loop has ParentLoopName set.
    /// </summary>
    [Test]
    public async Task Constructor_WithNestedLoop_SetsParentLoopName()
    {
        // Arrange & Act
        var model = new LoopModel(
            LoopName: "Inner",
            ConditionId: "Workflow-Outer_Inner",
            MaxIterations: 3,
            BodySteps: [Step("Outer_Inner_InnerStep")],
            ContinuationStepName: "Outer_OuterStep2",
            ParentLoopName: "Outer");

        // Assert
        await Assert.That(model.ParentLoopName).IsEqualTo("Outer");
    }

    // =============================================================================
    // C. Computed Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that FullPrefix returns LoopName for top-level loops.
    /// </summary>
    [Test]
    public async Task FullPrefix_TopLevelLoop_ReturnsLoopName()
    {
        // Arrange
        var model = new LoopModel(
            LoopName: "Refinement",
            ConditionId: "Workflow-Refinement",
            MaxIterations: 5,
            BodySteps: [Step("Refinement_Step")],
            ContinuationStepName: "NextStep",
            ParentLoopName: null);

        // Act & Assert
        await Assert.That(model.FullPrefix).IsEqualTo("Refinement");
    }

    /// <summary>
    /// Verifies that FullPrefix returns hierarchical prefix for nested loops.
    /// </summary>
    [Test]
    public async Task FullPrefix_NestedLoop_ReturnsHierarchicalPrefix()
    {
        // Arrange
        var model = new LoopModel(
            LoopName: "Inner",
            ConditionId: "Workflow-Outer_Inner",
            MaxIterations: 3,
            BodySteps: [Step("Outer_Inner_Step")],
            ContinuationStepName: "Outer_NextStep",
            ParentLoopName: "Outer");

        // Act & Assert
        await Assert.That(model.FullPrefix).IsEqualTo("Outer_Inner");
    }

    /// <summary>
    /// Verifies that IterationPropertyName is derived from FullPrefix without underscores.
    /// </summary>
    [Test]
    public async Task IterationPropertyName_TopLevelLoop_ReturnsCorrectName()
    {
        // Arrange
        var model = new LoopModel(
            LoopName: "Refinement",
            ConditionId: "Workflow-Refinement",
            MaxIterations: 5,
            BodySteps: [Step("Refinement_Step")],
            ContinuationStepName: "NextStep",
            ParentLoopName: null);

        // Act & Assert
        await Assert.That(model.IterationPropertyName).IsEqualTo("RefinementIterationCount");
    }

    /// <summary>
    /// Verifies that IterationPropertyName for nested loops removes underscores.
    /// </summary>
    [Test]
    public async Task IterationPropertyName_NestedLoop_RemovesUnderscores()
    {
        // Arrange
        var model = new LoopModel(
            LoopName: "Inner",
            ConditionId: "Workflow-Outer_Inner",
            MaxIterations: 3,
            BodySteps: [Step("Outer_Inner_Step")],
            ContinuationStepName: "Outer_NextStep",
            ParentLoopName: "Outer");

        // Act & Assert
        await Assert.That(model.IterationPropertyName).IsEqualTo("OuterInnerIterationCount");
    }

    /// <summary>
    /// Verifies that ConditionMethodName is derived correctly.
    /// </summary>
    [Test]
    public async Task ConditionMethodName_ReturnsCorrectName()
    {
        // Arrange
        var model = new LoopModel(
            LoopName: "Refinement",
            ConditionId: "Workflow-Refinement",
            MaxIterations: 5,
            BodySteps: [Step("Refinement_Step")],
            ContinuationStepName: "NextStep",
            ParentLoopName: null);

        // Act & Assert
        await Assert.That(model.ConditionMethodName).IsEqualTo("ShouldExitRefinementLoop");
    }

    /// <summary>
    /// Verifies that ConditionMethodName for nested loops removes underscores.
    /// </summary>
    [Test]
    public async Task ConditionMethodName_NestedLoop_RemovesUnderscores()
    {
        // Arrange
        var model = new LoopModel(
            LoopName: "Inner",
            ConditionId: "Workflow-Outer_Inner",
            MaxIterations: 3,
            BodySteps: [Step("Outer_Inner_Step")],
            ContinuationStepName: "Outer_NextStep",
            ParentLoopName: "Outer");

        // Act & Assert
        await Assert.That(model.ConditionMethodName).IsEqualTo("ShouldExitOuterInnerLoop");
    }

    // =============================================================================
    // D. Body-Step Projection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the loop carries configured <see cref="StepModel"/> body steps and that
    /// <c>FirstBodyStepName</c>/<c>LastBodyStepName</c> are computed projections over them
    /// (mirroring <see cref="ForkPathModel"/>), so per-step configuration is preserved on the
    /// loop body rather than collapsed to bare names.
    /// </summary>
    [Test]
    public async Task LoopModel_CarriesConfiguredBodySteps_WithComputedFirstLastNames()
    {
        // Arrange - a loop body whose first step declares a confidence gate
        var gatedStep = StepModel.Create(
            "CritiqueStep",
            "TestNamespace.CritiqueStep",
            loopName: "Refinement",
            confidence: new ConfidenceModel(0.85));
        var plainStep = StepModel.Create("RefineStep", "TestNamespace.RefineStep", loopName: "Refinement");

        // Act
        var model = new LoopModel(
            LoopName: "Refinement",
            ConditionId: "Workflow-Refinement",
            MaxIterations: 5,
            BodySteps: [gatedStep, plainStep],
            ContinuationStepName: "PublishResult",
            ParentLoopName: null);

        // Assert - the body is carried as configured StepModels, not just names
        await Assert.That(model.BodySteps.Count).IsEqualTo(2);
        await Assert.That(model.BodySteps[0].Confidence).IsNotNull();
        await Assert.That(model.BodySteps[0].Confidence!.Threshold).IsEqualTo(0.85);

        // Assert - first/last names are computed projections over the body step phase names
        await Assert.That(model.FirstBodyStepName).IsEqualTo("Refinement_CritiqueStep");
        await Assert.That(model.LastBodyStepName).IsEqualTo("Refinement_RefineStep");
    }

    // =============================================================================
    // E. Record Equality Tests
    // =============================================================================

    /// <summary>
    /// Verifies that LoopModel is a record with value equality.
    /// </summary>
    [Test]
    public async Task LoopModel_IsValueEqual()
    {
        // Arrange - share the body-step list instance so the collection member compares equal
        // (records compare IReadOnlyList members by reference, matching ForkModel/ForkPathModel).
        var bodySteps = new List<StepModel> { Step("Refinement_Step") };

        var model1 = new LoopModel(
            LoopName: "Refinement",
            ConditionId: "Workflow-Refinement",
            MaxIterations: 5,
            BodySteps: bodySteps,
            ContinuationStepName: "NextStep",
            ParentLoopName: null);

        var model2 = new LoopModel(
            LoopName: "Refinement",
            ConditionId: "Workflow-Refinement",
            MaxIterations: 5,
            BodySteps: bodySteps,
            ContinuationStepName: "NextStep",
            ParentLoopName: null);

        // Assert
        await Assert.That(model1).IsEqualTo(model2);
    }

    /// <summary>
    /// Verifies that LoopModels with different values are not equal.
    /// </summary>
    [Test]
    public async Task LoopModel_DifferentValues_AreNotEqual()
    {
        // Arrange
        var model1 = new LoopModel(
            LoopName: "Refinement",
            ConditionId: "Workflow-Refinement",
            MaxIterations: 5,
            BodySteps: [Step("Refinement_Step")],
            ContinuationStepName: "NextStep",
            ParentLoopName: null);

        var model2 = new LoopModel(
            LoopName: "Validation",
            ConditionId: "Workflow-Validation",
            MaxIterations: 3,
            BodySteps: [Step("Validation_Step")],
            ContinuationStepName: "NextStep",
            ParentLoopName: null);

        // Assert
        await Assert.That(model1).IsNotEqualTo(model2);
    }
}

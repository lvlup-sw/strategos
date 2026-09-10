// -----------------------------------------------------------------------
// <copyright file="LoopConditionEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="LoopConditionEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class LoopConditionEmitterTests
{
    // =============================================================================
    // A. Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitConditionMethod throws for null StringBuilder.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var model = CreateModel();
        var loop = CreateLoop("Refinement");

        // Act & Assert
        await Assert.That(() => emitter.EmitConditionMethod(null!, model, loop))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitConditionMethod throws for null model.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var loop = CreateLoop("Refinement");

        // Act & Assert
        await Assert.That(() => emitter.EmitConditionMethod(sb, null!, loop))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitConditionMethod throws for null loop.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_NullLoop_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var model = CreateModel();

        // Act & Assert
        await Assert.That(() => emitter.EmitConditionMethod(sb, model, null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Method Signature Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitConditionMethod generates protected virtual method.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_ValidLoop_GeneratesProtectedVirtualMethod()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var model = CreateModel();
        var loop = CreateLoop("Refinement");

        // Act
        emitter.EmitConditionMethod(sb, model, loop);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("protected virtual bool ShouldExitRefinementLoop()");
    }

    /// <summary>
    /// Verifies that method name includes loop name.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_ValidLoop_MethodNameIncludesLoopName()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var model = CreateModel();
        var loop = CreateLoop("Processing");

        // Act
        emitter.EmitConditionMethod(sb, model, loop);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("ShouldExitProcessingLoop");
    }

    // =============================================================================
    // C. Method Body Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitConditionMethod generates registry call.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_ValidLoop_GeneratesRegistryCall()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var model = CreateModel();
        var loop = CreateLoop("Refinement");

        // Act
        emitter.EmitConditionMethod(sb, model, loop);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("WorkflowConditionRegistry.Evaluate<TestState>(\"TestWorkflow-Refinement\", State)");
    }

    /// <summary>
    /// Verifies that EmitConditionMethod generates registry reference comment.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_ValidLoop_GeneratesRegistryComment()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var model = CreateModel();
        var loop = CreateLoop("Refinement");

        // Act
        emitter.EmitConditionMethod(sb, model, loop);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("WorkflowConditionRegistry");
    }

    // =============================================================================
    // D. XML Documentation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitConditionMethod generates XML documentation.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_ValidLoop_GeneratesXmlDocumentation()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var model = CreateModel();
        var loop = CreateLoop("Refinement");

        // Act
        emitter.EmitConditionMethod(sb, model, loop);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <summary>");
        await Assert.That(result).Contains("/// </summary>");
    }

    /// <summary>
    /// Verifies that EmitConditionMethod generates remarks with condition ID.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_ValidLoop_GeneratesRemarksWithConditionId()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var model = CreateModel();
        var loop = CreateLoop("Refinement");

        // Act
        emitter.EmitConditionMethod(sb, model, loop);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Condition ID:");
        await Assert.That(result).Contains("TestWorkflow-Refinement");
    }

    /// <summary>
    /// Verifies that EmitConditionMethod generates returns documentation.
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_ValidLoop_GeneratesReturnsDoc()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var model = CreateModel();
        var loop = CreateLoop("Refinement");

        // Act
        emitter.EmitConditionMethod(sb, model, loop);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <returns>");
    }

    // =============================================================================
    // E. Nested Loop Tests
    // =============================================================================

    /// <summary>
    /// Verifies that nested loops use hierarchical method names.
    /// For a loop with ParentLoopName="Outer" and LoopName="Inner",
    /// the method should be "ShouldExitOuterInnerLoop", not "ShouldExitInnerLoop".
    /// </summary>
    [Test]
    public async Task EmitConditionMethod_NestedLoop_UsesHierarchicalMethodName()
    {
        // Arrange
        var emitter = new LoopConditionEmitter();
        var sb = new StringBuilder();
        var model = CreateModel();
        var nestedLoop = LoopModel.Create(
            loopName: "Inner",
            conditionId: "TestWorkflow-Outer-Inner",
            maxIterations: 3,
            bodySteps: [StepModel.Create("Outer_Inner_InnerStep", "TestNamespace.Outer_Inner_InnerStep")],
            parentLoopName: "Outer");

        // Act
        emitter.EmitConditionMethod(sb, model, nestedLoop);
        var result = sb.ToString();

        // Assert - Should use hierarchical method name (ShouldExitOuterInnerLoop)
        await Assert.That(result).Contains("protected virtual bool ShouldExitOuterInnerLoop()");
        // Should NOT use just the inner loop name (ShouldExitInnerLoop)
        await Assert.That(result).DoesNotContain("ShouldExitInnerLoop()");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateModel()
    {
        return new WorkflowModel(
            WorkflowName: "TestWorkflow",
            PascalName: "TestWorkflow",
            Namespace: "Test.Namespace",
            StepNames: ["Step1", "Step2"],
            StateTypeName: "TestState",
            Version: 1,
            Steps: null,
            Loops: null,
            Branches: null,
            FailureHandlers: null,
            Forks: null);
    }

    private static LoopModel CreateLoop(string loopName)
    {
        return LoopModel.Create(
            loopName: loopName,
            conditionId: $"TestWorkflow-{loopName}",
            maxIterations: 5,
            bodySteps:
            [
                StepModel.Create($"{loopName}_Start", $"TestNamespace.{loopName}_Start"),
                StepModel.Create($"{loopName}_End", $"TestNamespace.{loopName}_End"),
            ]);
    }
}

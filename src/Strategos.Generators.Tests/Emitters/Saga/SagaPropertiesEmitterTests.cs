// -----------------------------------------------------------------------
// <copyright file="SagaPropertiesEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="SagaPropertiesEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class SagaPropertiesEmitterTests
{
    // =============================================================================
    // A. Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit throws for null StringBuilder.
    /// </summary>
    [Test]
    public async Task Emit_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var model = CreateMinimalModel();

        // Act & Assert
        await Assert.That(() => emitter.Emit(null!, model))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Emit throws for null model.
    /// </summary>
    [Test]
    public async Task Emit_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();

        // Act & Assert
        await Assert.That(() => emitter.Emit(sb, null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. WorkflowId Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates WorkflowId property.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesWorkflowIdProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public Guid WorkflowId { get; set; }");
    }

    /// <summary>
    /// Verifies that WorkflowId has SagaIdentity attribute.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_WorkflowIdHasSagaIdentityAttribute()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("[SagaIdentity]");
    }

    /// <summary>
    /// Verifies that WorkflowId has Identity attribute.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_WorkflowIdHasIdentityAttribute()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        // Fully qualified [JasperFx.Identity] (the Marten document-identity
        // attribute) to avoid a CS0616 collision with the Strategos.Identity
        // namespace in consumers that reference Strategos.Identity.Abstractions.
        await Assert.That(result).Contains("[JasperFx.Identity]");
    }

    // =============================================================================
    // C. Version Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates Version property.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesVersionProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        // long (not int): Marten 9 widened numeric document revisions to long
        // and rejects an int [Version] property at document-mapping time.
        await Assert.That(result).Contains("public new long Version { get; set; }");
    }

    /// <summary>
    /// Verifies that Version has Version attribute.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_VersionHasVersionAttribute()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("[Version]");
    }

    // =============================================================================
    // D. Phase Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates Phase property with correct enum type.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesPhaseProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public TestWorkflowPhase Phase { get; set; }");
    }

    /// <summary>
    /// Verifies that Phase property has NotStarted default.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_PhaseHasNotStartedDefault()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("TestWorkflowPhase.NotStarted");
    }

    // =============================================================================
    // E. State Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates State property when StateTypeName is specified.
    /// </summary>
    [Test]
    public async Task Emit_WithStateType_GeneratesStateProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(stateTypeName: "OrderState");

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public OrderState State { get; set; }");
    }

    /// <summary>
    /// Verifies that Emit does NOT generate State property when no StateTypeName.
    /// </summary>
    [Test]
    public async Task Emit_WithoutStateType_DoesNotGenerateStateProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(stateTypeName: null);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).DoesNotContain("State { get; set; }");
    }

    // =============================================================================
    // F. Loop Iteration Count Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates iteration count property for loops.
    /// </summary>
    [Test]
    public async Task Emit_WithLoops_GeneratesIterationCountProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var loop = LoopModel.Create(
            loopName: "Refinement",
            conditionId: "TestWorkflow-Refinement",
            maxIterations: 5,
            firstBodyStepName: "Refinement_Analyze",
            lastBodyStepName: "Refinement_Refine");
        var model = CreateMinimalModel(loops: [loop]);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public int RefinementIterationCount { get; set; }");
    }

    /// <summary>
    /// Verifies that Emit does NOT generate iteration count when no loops.
    /// </summary>
    [Test]
    public async Task Emit_WithoutLoops_DoesNotGenerateIterationCountProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(loops: null);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).DoesNotContain("IterationCount");
    }

    /// <summary>
    /// Verifies that nested loops use hierarchical iteration property names.
    /// For a loop with ParentLoopName="Outer" and LoopName="Inner",
    /// the property should be "OuterInnerIterationCount", not "InnerIterationCount".
    /// </summary>
    [Test]
    public async Task Emit_NestedLoop_UsesHierarchicalIterationPropertyName()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var nestedLoop = LoopModel.Create(
            loopName: "Inner",
            conditionId: "TestWorkflow-Outer-Inner",
            maxIterations: 3,
            firstBodyStepName: "Outer_Inner_InnerStep",
            lastBodyStepName: "Outer_Inner_InnerStep",
            parentLoopName: "Outer");
        var model = CreateMinimalModel(loops: [nestedLoop]);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert - Should use hierarchical property name (OuterInnerIterationCount)
        await Assert.That(result).Contains("public int OuterInnerIterationCount { get; set; }");
        // Should NOT use just the inner loop name (InnerIterationCount)
        await Assert.That(result).DoesNotContain("public int InnerIterationCount { get; set; }");
    }

    // =============================================================================
    // G. StartedAt Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates StartedAt property.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesStartedAtProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public DateTimeOffset StartedAt { get; set; }");
    }

    // =============================================================================
    // H. XML Documentation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates XML documentation for properties.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesXmlDocumentation()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <summary>");
        await Assert.That(result).Contains("/// </summary>");
    }

    // =============================================================================
    // I. Interface Implementation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that SagaPropertiesEmitter implements ISagaComponentEmitter.
    /// </summary>
    [Test]
    public async Task Class_ImplementsISagaComponentEmitter()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();

        // Assert
        ISagaComponentEmitter componentEmitter = emitter;
        await Assert.That(componentEmitter).IsNotNull();
    }

    // =============================================================================
    // J. Approval State Tracking Properties Tests (Phase 2)
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates PendingApprovalRequestId property when approvals exist.
    /// </summary>
    [Test]
    public async Task Emit_WithApprovalPoints_GeneratesPendingApprovalRequestIdProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateStep");
        var model = CreateMinimalModel(approvalPoints: [approval]);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public string? PendingApprovalRequestId { get; set; }");
    }

    /// <summary>
    /// Verifies that Emit generates ApprovalInstructions property when approvals exist.
    /// </summary>
    [Test]
    public async Task Emit_WithApprovalPoints_GeneratesApprovalInstructionsProperty()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateStep");
        var model = CreateMinimalModel(approvalPoints: [approval]);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public string? ApprovalInstructions { get; set; }");
    }

    /// <summary>
    /// Verifies that approval properties are NOT generated when no approvals exist.
    /// </summary>
    [Test]
    public async Task Emit_WithoutApprovalPoints_DoesNotGenerateApprovalProperties()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(approvalPoints: null);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).DoesNotContain("PendingApprovalRequestId");
        await Assert.That(result).DoesNotContain("ApprovalInstructions");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateMinimalModel(
        string? stateTypeName = "TestState",
        IReadOnlyList<LoopModel>? loops = null,
        IReadOnlyList<ApprovalModel>? approvalPoints = null)
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateStep", "ProcessStep"],
            StateTypeName: stateTypeName,
            Loops: loops,
            ApprovalPoints: approvalPoints);
    }
}

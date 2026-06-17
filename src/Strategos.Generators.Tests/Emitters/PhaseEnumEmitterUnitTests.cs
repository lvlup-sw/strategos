// -----------------------------------------------------------------------
// <copyright file="PhaseEnumEmitterUnitTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Unit tests for the <see cref="PhaseEnumEmitter"/> class.
/// </summary>
/// <remarks>
/// These tests verify the emitter in isolation, independent of the source generator.
/// </remarks>
[Property("Category", "Unit")]
public class PhaseEnumEmitterUnitTests
{
    // =============================================================================
    // A. Basic Emission Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter returns valid, non-empty source code.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_ReturnsNonEmptySource()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).IsNotNull();
        await Assert.That(source.Length).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that the emitter generates the correct enum name.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_GeneratesCorrectEnumName()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public enum ProcessOrderPhase");
    }

    /// <summary>
    /// Verifies that the emitter uses the correct namespace.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_UsesCorrectNamespace()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("namespace TestNamespace;");
    }

    // =============================================================================
    // B. Standard Phases Tests
    // =============================================================================

    /// <summary>
    /// Verifies that NotStarted phase is always included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesNotStarted()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("NotStarted,");
    }

    /// <summary>
    /// Verifies that Completed phase is always included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesCompleted()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Completed,");
    }

    /// <summary>
    /// Verifies that Failed phase is always included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesFailed()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Failed,");
    }

    // =============================================================================
    // C. Step Phases Tests
    // =============================================================================

    /// <summary>
    /// Verifies that step names are included as enum values.
    /// </summary>
    [Test]
    public async Task Emit_WithSteps_IncludesStepPhases()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ValidateOrder,");
        await Assert.That(source).Contains("ProcessPayment,");
        await Assert.That(source).Contains("SendConfirmation,");
    }

    /// <summary>
    /// Verifies that loop-prefixed step names are preserved.
    /// </summary>
    [Test]
    public async Task Emit_WithLoopSteps_PreservesLoopPrefix()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "iterative-refinement",
            PascalName: "IterativeRefinement",
            Namespace: "TestNamespace",
            StepNames: ["ValidateInput", "Refinement_CritiqueStep", "Refinement_RefineStep", "PublishResult"]);

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Refinement_CritiqueStep,");
        await Assert.That(source).Contains("Refinement_RefineStep,");
    }

    // =============================================================================
    // D. Attributes and Header Tests
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
        var source = PhaseEnumEmitter.Emit(model);

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
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("#nullable enable");
    }

    /// <summary>
    /// Verifies that GeneratedCode attribute is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesGeneratedCodeAttribute()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("[GeneratedCode(\"Strategos.Generators\"");
    }

    /// <summary>
    /// Verifies that JsonConverter attribute is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesJsonConverterAttribute()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("[JsonConverter(typeof(JsonStringEnumConverter))]");
    }

    /// <summary>
    /// Verifies that XML documentation is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesXmlDocumentation()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("/// <summary>");
        await Assert.That(source).Contains("Phase enumeration for the process-order workflow");
    }

    // =============================================================================
    // E. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that null model throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Emit_WithNullModel_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => PhaseEnumEmitter.Emit(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // F. ValidationFailed Phase Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ValidationFailed phase is included when workflow has validation.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithValidation_IncludesValidationFailedPhase()
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

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ValidationFailed,");
    }

    /// <summary>
    /// Verifies that ValidationFailed phase is NOT included when workflow has no validation.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithoutValidation_DoesNotIncludeValidationFailedPhase()
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

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).DoesNotContain("ValidationFailed");
    }

    // =============================================================================
    // G. Approval Phase Tests (Phase 2 - Rejection/Escalation Step Phases)
    // =============================================================================

    /// <summary>
    /// Verifies that rejection step phases are generated when approval has rejection path.
    /// </summary>
    [Test]
    public async Task Emit_WithRejectionSteps_GeneratesRejectionStepPhases()
    {
        // Arrange
        var rejectionSteps = new List<StepModel>
        {
            StepModel.Create("LogRejection", "TestNamespace.LogRejection"),
            StepModel.Create("NotifyStakeholders", "TestNamespace.NotifyStakeholders"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            rejectionSteps: rejectionSteps);
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("LogRejection,");
        await Assert.That(source).Contains("NotifyStakeholders,");
    }

    /// <summary>
    /// Verifies that escalation step phases are generated when approval has timeout escalation.
    /// </summary>
    [Test]
    public async Task Emit_WithEscalationSteps_GeneratesEscalationStepPhases()
    {
        // Arrange
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyEscalation", "TestNamespace.NotifyEscalation"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("NotifyEscalation,");
    }

    /// <summary>
    /// Verifies that nested approval phases are generated for escalation chains.
    /// </summary>
    [Test]
    public async Task Emit_WithNestedApproval_GeneratesEscalationApprovalPhase()
    {
        // Arrange
        var nestedApproval = ApprovalModel.Create(
            approvalPointName: "DirectorReview",
            approverTypeName: "TestNamespace.DirectorApprover",
            precedingStepName: "ManagerReview");
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            nestedEscalationApprovals: [nestedApproval]);
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("AwaitApproval_ManagerReview,");
        await Assert.That(source).Contains("AwaitApproval_DirectorReview,");
    }

    // =============================================================================
    // H. Fork Phase Tests
    // =============================================================================

    /// <summary>
    /// Verifies that fork phase is generated when workflow has forks.
    /// </summary>
    [Test]
    public async Task Emit_WithFork_GeneratesForkingPhase()
    {
        // Arrange
        var model = CreateTestModelWithFork();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Forking_");
    }

    /// <summary>
    /// Verifies that join phase is generated when workflow has forks.
    /// </summary>
    [Test]
    public async Task Emit_WithFork_GeneratesJoiningPhase()
    {
        // Arrange
        var model = CreateTestModelWithFork();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Joining_");
    }

    /// <summary>
    /// Verifies that path-specific phases are generated for each fork path.
    /// </summary>
    [Test]
    public async Task Emit_WithFork_GeneratesPathPhases()
    {
        // Arrange
        var model = CreateTestModelWithFork();

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Fork0_Path0_");
        await Assert.That(source).Contains("Fork0_Path1_");
    }

    // =============================================================================
    // I. Instance Name Tests (Phase 2 - Step Reuse Support)
    // =============================================================================

    /// <summary>
    /// Verifies that phases use instance names (EffectiveName) when provided.
    /// Same step type with different instance names should generate distinct phases.
    /// </summary>
    [Test]
    public async Task Emit_WithInstanceNames_GeneratesInstanceNamePhases()
    {
        // Arrange - Two uses of AnalyzeStep with different instance names
        var steps = new List<StepModel>
        {
            StepModel.Create("PrepareData", "TestNamespace.PrepareData"),
            StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "Technical"),
            StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "Fundamental"),
            StepModel.Create("SynthesizeResults", "TestNamespace.SynthesizeResults"),
        };

        var model = new WorkflowModel(
            WorkflowName: "multi-analysis",
            PascalName: "MultiAnalysis",
            Namespace: "TestNamespace",
            StepNames: ["PrepareData", "Technical", "Fundamental", "SynthesizeResults"],
            StateTypeName: "AnalysisState",
            Steps: steps);

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert - Should use instance names (Technical, Fundamental), not step type name (AnalyzeStep)
        await Assert.That(source).Contains("Technical,");
        await Assert.That(source).Contains("Fundamental,");
        // Should NOT contain duplicate AnalyzeStep phases
        await Assert.That(source).DoesNotContain("AnalyzeStep,");
    }

    /// <summary>
    /// Verifies that steps without instance names use their step name in the phase.
    /// </summary>
    [Test]
    public async Task Emit_WithoutInstanceNames_UsesStepName()
    {
        // Arrange - Steps without instance names
        var steps = new List<StepModel>
        {
            StepModel.Create("ValidateInput", "TestNamespace.ValidateInput"),
            StepModel.Create("ProcessData", "TestNamespace.ProcessData"),
        };

        var model = new WorkflowModel(
            WorkflowName: "simple-workflow",
            PascalName: "SimpleWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateInput", "ProcessData"],
            StateTypeName: "SimpleState",
            Steps: steps);

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert - Should use step names directly
        await Assert.That(source).Contains("ValidateInput,");
        await Assert.That(source).Contains("ProcessData,");
    }

    /// <summary>
    /// Verifies that steps with instance names inside loops combine loop prefix with instance name.
    /// </summary>
    [Test]
    public async Task Emit_WithInstanceNamesInLoop_CombinesLoopPrefixWithInstanceName()
    {
        // Arrange - Instance-named steps inside a loop
        var steps = new List<StepModel>
        {
            StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "Technical", loopName: "Refinement"),
            StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "Fundamental", loopName: "Refinement"),
        };

        var model = new WorkflowModel(
            WorkflowName: "loop-analysis",
            PascalName: "LoopAnalysis",
            Namespace: "TestNamespace",
            StepNames: ["Refinement_Technical", "Refinement_Fundamental"],
            StateTypeName: "LoopState",
            Steps: steps);

        // Act
        var source = PhaseEnumEmitter.Emit(model);

        // Assert - Should combine loop prefix with instance name
        await Assert.That(source).Contains("Refinement_Technical,");
        await Assert.That(source).Contains("Refinement_Fundamental,");
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

    private static WorkflowModel CreateTestModelWithApproval(ApprovalModel approval)
    {
        return new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment", "SendConfirmation"],
            StateTypeName: "OrderState",
            ApprovalPoints: [approval]);
    }

    private static WorkflowModel CreateTestModelWithFork()
    {
        var path0 = ForkPathModel.Create(
            pathIndex: 0,
            steps:
            [
                StepModel.Create("ProcessPayment", "TestNamespace.ProcessPayment"),
                StepModel.Create("ChargeCard", "TestNamespace.ChargeCard"),
            ],
            hasFailureHandler: false,
            isTerminalOnFailure: false);

        var path1 = ForkPathModel.Create(
            pathIndex: 1,
            steps:
            [
                StepModel.Create("ReserveInventory", "TestNamespace.ReserveInventory"),
                StepModel.Create("PickItems", "TestNamespace.PickItems"),
            ],
            hasFailureHandler: false,
            isTerminalOnFailure: false);

        var fork = ForkModel.Create(
            forkId: "OrderWorkflow-Fork0",
            previousStepName: "ValidateOrder",
            paths: [path0, path1],
            joinStepName: "SynthesizeResults");

        return new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "Fork0_Path0_ProcessPayment", "Fork0_Path0_ChargeCard", "Fork0_Path1_ReserveInventory", "Fork0_Path1_PickItems", "SynthesizeResults", "SendConfirmation"],
            StateTypeName: "OrderState",
            Forks: [fork]);
    }
}

// -----------------------------------------------------------------------
// <copyright file="StepStartHandlerEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="StepStartHandlerEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class StepStartHandlerEmitterTests
{
    // =============================================================================
    // A. Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler throws for null StringBuilder.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var model = CreateMinimalModel();
        var context = CreateContext(stepIndex: 0, stepModel: null);

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(null!, model, "ValidateStep", context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitHandler throws for null model.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var context = CreateContext(stepIndex: 0, stepModel: null);

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(sb, null!, "ValidateStep", context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitHandler throws for null stepName.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullStepName_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(stepIndex: 0, stepModel: null);

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(sb, model, null!, context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitHandler throws for null context.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(sb, model, "ValidateStep", null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Standard Handler Tests (No Validation)
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates standard handler signature.
    /// </summary>
    [Test]
    public async Task EmitHandler_NoValidation_GeneratesStandardSignature()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(stepIndex: 0, stepModel: null);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Handler now includes ILogger method injection
        await Assert.That(result).Contains("public ExecuteValidateStepWorkerCommand Handle(");
        await Assert.That(result).Contains("StartValidateStepCommand command,");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    /// <summary>
    /// Verifies that EmitHandler generates guard clause.
    /// </summary>
    [Test]
    public async Task EmitHandler_NoValidation_GeneratesGuardClause()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(stepIndex: 0, stepModel: null);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("ArgumentNullException.ThrowIfNull(command, nameof(command))");
    }

    /// <summary>
    /// Verifies that EmitHandler sets phase.
    /// </summary>
    [Test]
    public async Task EmitHandler_NoValidation_SetsPhase()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(stepIndex: 0, stepModel: null);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.ValidateStep");
    }

    /// <summary>
    /// Verifies that EmitHandler returns worker command.
    /// </summary>
    [Test]
    public async Task EmitHandler_NoValidation_ReturnsWorkerCommand()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(stepIndex: 0, stepModel: null);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("return new ExecuteValidateStepWorkerCommand(WorkflowId, Guid.NewGuid(), State)");
    }

    // =============================================================================
    // C. Validation Handler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates yield-based signature for validation steps.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithValidation_GeneratesYieldSignature()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var stepModel = CreateStepModelWithValidation();
        var context = CreateContext(stepIndex: 0, stepModel: stepModel);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Handler now includes ILogger method injection
        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(result).Contains("StartValidateStepCommand command,");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    /// <summary>
    /// Verifies that EmitHandler generates validation guard check.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithValidation_GeneratesGuardCheck()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var stepModel = CreateStepModelWithValidation();
        var context = CreateContext(stepIndex: 0, stepModel: stepModel);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("if (!(State.IsValid))");
    }

    /// <summary>
    /// Verifies that EmitHandler generates validation failed event.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithValidation_GeneratesValidationFailedEvent()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var stepModel = CreateStepModelWithValidation();
        var context = CreateContext(stepIndex: 0, stepModel: stepModel);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("yield return new TestWorkflowValidationFailed(");
    }

    /// <summary>
    /// Verifies that EmitHandler sets ValidationFailed phase on failure.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithValidation_SetsValidationFailedPhase()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var stepModel = CreateStepModelWithValidation();
        var context = CreateContext(stepIndex: 0, stepModel: stepModel);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.ValidationFailed");
    }

    /// <summary>
    /// Verifies that EmitHandler includes yield break after validation failure.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithValidation_YieldsBreakOnFailure()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var stepModel = CreateStepModelWithValidation();
        var context = CreateContext(stepIndex: 0, stepModel: stepModel);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("yield break;");
    }

    /// <summary>
    /// Verifies that EmitHandler yields worker command on validation success.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithValidation_YieldsWorkerCommand()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var stepModel = CreateStepModelWithValidation();
        var context = CreateContext(stepIndex: 0, stepModel: stepModel);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("yield return new ExecuteValidateStepWorkerCommand(WorkflowId, Guid.NewGuid(), State)");
    }

    // =============================================================================
    // D. XML Documentation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates XML documentation.
    /// </summary>
    [Test]
    public async Task EmitHandler_ValidInput_GeneratesXmlDocumentation()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(stepIndex: 0, stepModel: null);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <summary>");
        await Assert.That(result).Contains("/// </summary>");
        await Assert.That(result).Contains("/// <param name=\"command\">");
        await Assert.That(result).Contains("/// <returns>");
    }

    // =============================================================================
    // E.0 Validation Error Message Literal Escaping Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a validation error message containing a double-quote and a
    /// backslash is emitted as a properly escaped C# string literal, so the
    /// generated start-handler source compiles instead of producing a broken
    /// literal. Regression for CL-1 (#142): the raw <c>Token.ValueText</c> was
    /// interpolated directly into the literal, so a <c>"</c> or <c>\</c> in the
    /// message terminated/corrupted the emitted string.
    /// </summary>
    [Test]
    public async Task StepStartHandlerEmitter_ValidationMessageWithQuoteAndBackslash_EmitsCompilableLiteral()
    {
        // Arrange
        const string message = "He said \"go\" \\ stop";
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var stepModel = StepModel.Create(
            stepName: "ValidateStep",
            stepTypeName: "Test.ValidateStep",
            validationPredicate: "state.IsValid",
            validationErrorMessage: message);
        var context = CreateContext(stepIndex: 0, stepModel: stepModel);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - the message must appear as a fully escaped, quote-wrapped literal.
        // SymbolDisplay.FormatLiteral(message, quote: true) is the canonical, compilable form.
        var expectedLiteral = SymbolDisplay.FormatLiteral(message, quote: true);
        await Assert.That(result).Contains(expectedLiteral);

        // And the broken raw form (verbatim message wrapped in plain quotes) must NOT appear -
        // that is the unescaped interpolation that fails to compile.
        await Assert.That(result).DoesNotContain("\"" + message + "\"");
    }

    // =============================================================================
    // E. State Parameter Replacement Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler replaces state parameter with State property.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithStatePredicate_ReplacesStateParameter()
    {
        // Arrange
        var emitter = new StepStartHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var stepModel = StepModel.Create(
            stepName: "ValidateStep",
            stepTypeName: "Test.ValidateStep",
            validationPredicate: "state.Amount > 0",
            validationErrorMessage: "Amount must be positive");
        var context = CreateContext(stepIndex: 0, stepModel: stepModel);

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("State.Amount > 0");
        await Assert.That(result).DoesNotContain("state.Amount");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateMinimalModel()
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateStep", "ProcessStep"],
            StateTypeName: "TestState",
            Loops: null);
    }

    private static StepModel CreateStepModelWithValidation()
    {
        return StepModel.Create(
            stepName: "ValidateStep",
            stepTypeName: "Test.ValidateStep",
            validationPredicate: "state.IsValid",
            validationErrorMessage: "State is not valid");
    }

    private static HandlerContext CreateContext(int stepIndex, StepModel? stepModel)
    {
        return new HandlerContext(
            StepIndex: stepIndex,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: "ProcessStep",
            StepModel: stepModel,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);
    }
}

// -----------------------------------------------------------------------
// <copyright file="SagaStartMethodEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="SagaStartMethodEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class SagaStartMethodEmitterTests
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
        var emitter = new SagaStartMethodEmitter();
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
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();

        // Act & Assert
        await Assert.That(() => emitter.Emit(sb, null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Method Signature Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates static Start method.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesStaticStartMethod()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public static (");
    }

    /// <summary>
    /// Verifies that Emit generates correct return type with saga and command.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesCorrectReturnType()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("TestWorkflowSaga Saga, StartValidateStepCommand Command");
    }

    /// <summary>
    /// Verifies that Emit generates Start method name.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesStartMethodName()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains(") Start(");
    }

    /// <summary>
    /// Verifies that Emit generates correct command parameter.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesCommandParameter()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("StartTestWorkflowCommand command");
    }

    // =============================================================================
    // C. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates guard clause for command parameter.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesGuardClause()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("ArgumentNullException.ThrowIfNull(command, nameof(command))");
    }

    // =============================================================================
    // D. Saga Initialization Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates saga instantiation.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesSagaInstantiation()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("var saga = new TestWorkflowSaga");
    }

    /// <summary>
    /// Verifies that Emit sets WorkflowId from command.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_SetsWorkflowIdFromCommand()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("WorkflowId = command.WorkflowId");
    }

    /// <summary>
    /// Verifies that Emit sets Phase to NotStarted.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_SetsPhaseToNotStarted()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.NotStarted");
    }

    /// <summary>
    /// Verifies that Emit sets StartedAt to UtcNow.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_SetsStartedAtToUtcNow()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("StartedAt = DateTimeOffset.UtcNow");
    }

    // =============================================================================
    // E. State Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit initializes State when StateTypeName is specified.
    /// </summary>
    [Test]
    public async Task Emit_WithStateType_InitializesState()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(stateTypeName: "OrderState");

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("State = command.InitialState");
    }

    /// <summary>
    /// Verifies that Emit does NOT initialize State when no StateTypeName.
    /// </summary>
    [Test]
    public async Task Emit_WithoutStateType_DoesNotInitializeState()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(stateTypeName: null);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).DoesNotContain("State = command.InitialState");
    }

    // =============================================================================
    // F. First Step Command Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates first step command.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesFirstStepCommand()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("var stepCommand = new StartValidateStepCommand(command.WorkflowId)");
    }

    /// <summary>
    /// Verifies that Emit generates return statement.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesReturnStatement()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("return (saga, stepCommand);");
    }

    // =============================================================================
    // G. XML Documentation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates XML documentation.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesXmlDocumentation()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <summary>");
        await Assert.That(result).Contains("/// </summary>");
    }

    /// <summary>
    /// Verifies that Emit generates param documentation.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesParamDocumentation()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <param name=\"command\">");
    }

    /// <summary>
    /// Verifies that Emit generates returns documentation.
    /// </summary>
    [Test]
    public async Task Emit_ValidModel_GeneratesReturnsDocumentation()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <returns>");
    }

    // =============================================================================
    // H. Interface Implementation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that SagaStartMethodEmitter implements ISagaComponentEmitter.
    /// </summary>
    [Test]
    public async Task Class_ImplementsISagaComponentEmitter()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();

        // Assert
        ISagaComponentEmitter componentEmitter = emitter;
        await Assert.That(componentEmitter).IsNotNull();
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateMinimalModel(string? stateTypeName = "TestState")
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateStep", "ProcessStep"],
            StateTypeName: stateTypeName,
            Loops: null);
    }
}

// -----------------------------------------------------------------------
// <copyright file="WorkerHandlerIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Integration tests for the WorkerHandlerEmitter through the full generator pipeline.
/// </summary>
[Property("Category", "Integration")]
public class WorkerHandlerIntegrationTests
{
    // =============================================================================
    // A. Worker Handler File Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces a Handlers file for linear workflows.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_GeneratesHandlersFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderHandlers.g.cs");

        // Assert
        await Assert.That(handlersSource).IsNotNull().And.IsNotEmpty();
    }

    /// <summary>
    /// Verifies that handlers for each step are generated.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_GeneratesAllStepHandlers()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderHandlers.g.cs");

        // Assert
        await Assert.That(handlersSource).Contains("ValidateOrderHandler");
        await Assert.That(handlersSource).Contains("ProcessPaymentHandler");
        await Assert.That(handlersSource).Contains("SendConfirmationHandler");
    }

    /// <summary>
    /// Verifies that generated handlers inject step types via constructor.
    /// </summary>
    [Test]
    public async Task Generator_Handlers_InjectStepsViaConstructor()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderHandlers.g.cs");

        // Assert - new signature includes ILogger injection
        await Assert.That(handlersSource).Contains("ValidateOrderHandler(");
        await Assert.That(handlersSource).Contains("ValidateOrder step,");
        await Assert.That(handlersSource).Contains("ILogger<ValidateOrderHandler> logger)");
    }

    /// <summary>
    /// Verifies that generated handlers have Handle methods for worker commands.
    /// </summary>
    [Test]
    public async Task Generator_Handlers_HaveHandleMethods()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderHandlers.g.cs");

        // Assert
        await Assert.That(handlersSource).Contains("ExecuteValidateOrderWorkerCommand command");
        await Assert.That(handlersSource).Contains("ExecuteProcessPaymentWorkerCommand command");
        await Assert.That(handlersSource).Contains("ExecuteSendConfirmationWorkerCommand command");
    }

    /// <summary>
    /// Verifies that generated handlers return completion events.
    /// </summary>
    [Test]
    public async Task Generator_Handlers_ReturnCompletionEvents()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderHandlers.g.cs");

        // Assert
        await Assert.That(handlersSource).Contains("ValidateOrderCompleted");
        await Assert.That(handlersSource).Contains("ProcessPaymentCompleted");
        await Assert.That(handlersSource).Contains("SendConfirmationCompleted");
    }
}

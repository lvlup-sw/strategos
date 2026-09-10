// -----------------------------------------------------------------------
// <copyright file="ExtensionsIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Integration tests for the ExtensionsEmitter through the full generator pipeline.
/// </summary>
[Property("Category", "Integration")]
public class ExtensionsIntegrationTests
{
    // =============================================================================
    // A. Extensions File Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces an Extensions file for linear workflows.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_GeneratesExtensionsFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderExtensions.g.cs");

        // Assert
        await Assert.That(extensionsSource).IsNotNull().And.IsNotEmpty();
    }

    /// <summary>
    /// Verifies that the extensions class is generated with correct name.
    /// </summary>
    [Test]
    public async Task Generator_Extensions_HasCorrectClassName()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderExtensions.g.cs");

        // Assert
        await Assert.That(extensionsSource).Contains("ProcessOrderWorkflowExtensions");
    }

    /// <summary>
    /// Verifies that the Add extension method is generated.
    /// </summary>
    [Test]
    public async Task Generator_Extensions_HasAddMethod()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderExtensions.g.cs");

        // Assert
        await Assert.That(extensionsSource).Contains("AddProcessOrderWorkflow");
    }

    /// <summary>
    /// Verifies that step types are registered.
    /// </summary>
    [Test]
    public async Task Generator_Extensions_RegistersStepTypes()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderExtensions.g.cs");

        // Assert
        await Assert.That(extensionsSource).Contains("services.AddTransient<ValidateOrder>();");
        await Assert.That(extensionsSource).Contains("services.AddTransient<ProcessPayment>();");
        await Assert.That(extensionsSource).Contains("services.AddTransient<SendConfirmation>();");
    }

    /// <summary>
    /// Verifies that worker handlers are registered.
    /// </summary>
    [Test]
    public async Task Generator_Extensions_RegistersHandlers()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderExtensions.g.cs");

        // Assert
        await Assert.That(extensionsSource).Contains("services.AddTransient<ValidateOrderHandler>();");
        await Assert.That(extensionsSource).Contains("services.AddTransient<ProcessPaymentHandler>();");
        await Assert.That(extensionsSource).Contains("services.AddTransient<SendConfirmationHandler>();");
    }

    /// <summary>
    /// Verifies that extensions use IServiceCollection.
    /// </summary>
    [Test]
    public async Task Generator_Extensions_UsesIServiceCollection()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderExtensions.g.cs");

        // Assert
        await Assert.That(extensionsSource).Contains("IServiceCollection services");
    }
}

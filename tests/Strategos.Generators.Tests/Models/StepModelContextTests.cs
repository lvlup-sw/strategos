// -----------------------------------------------------------------------
// <copyright file="StepModelContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for the <see cref="StepModel"/> Context property extension.
/// </summary>
[Property("Category", "Unit")]
public class StepModelContextTests
{
    // =============================================================================
    // A. StepModel Context Property Tests (Task A4)
    // =============================================================================

    /// <summary>
    /// Verifies that StepModel with context stores the ContextModel.
    /// </summary>
    [Test]
    public async Task StepModel_WithContext_StoresContextModel()
    {
        // Arrange
        var contextSources = new ContextSourceModel[]
        {
            new LiteralContextSourceModel("You are a helpful assistant."),
        };
        var context = new ContextModel(contextSources);

        // Act
        var model = StepModel.Create(
            stepName: "ProcessQuery",
            stepTypeName: "TestNamespace.ProcessQuery",
            context: context);

        // Assert
        await Assert.That(model.Context).IsNotNull();
        await Assert.That(model.Context!.Sources).HasCount(1);
    }

    /// <summary>
    /// Verifies that StepModel without context has null Context property.
    /// </summary>
    [Test]
    public async Task StepModel_WithoutContext_ContextIsNull()
    {
        // Arrange & Act
        var model = StepModel.Create(
            stepName: "ProcessQuery",
            stepTypeName: "TestNamespace.ProcessQuery");

        // Assert
        await Assert.That(model.Context).IsNull();
    }

    /// <summary>
    /// Verifies that ContextModel with sources stores the sources list.
    /// </summary>
    [Test]
    public async Task ContextModel_WithSources_StoresSourcesList()
    {
        // Arrange
        var sources = new ContextSourceModel[]
        {
            new LiteralContextSourceModel("System prompt"),
            new StateContextSourceModel("CustomerName", "string", "state.CustomerName"),
        };

        // Act
        var context = new ContextModel(sources);

        // Assert
        await Assert.That(context.Sources).HasCount(2);
        await Assert.That(context.Sources[0]).IsTypeOf<LiteralContextSourceModel>();
        await Assert.That(context.Sources[1]).IsTypeOf<StateContextSourceModel>();
    }
}

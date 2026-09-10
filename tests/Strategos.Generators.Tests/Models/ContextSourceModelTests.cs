// -----------------------------------------------------------------------
// <copyright file="ContextSourceModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for the <see cref="ContextSourceModel"/> hierarchy.
/// </summary>
[Property("Category", "Unit")]
public class ContextSourceModelTests
{
    // =============================================================================
    // A. ContextSourceModel Base Type Tests (Task A1)
    // =============================================================================

    /// <summary>
    /// Verifies that ContextSourceModel is an abstract record and cannot be instantiated directly.
    /// </summary>
    [Test]
    public async Task ContextSourceModel_IsAbstractRecord_CannotInstantiate()
    {
        // Arrange & Act
        var type = typeof(ContextSourceModel);

        // Assert
        await Assert.That(type.IsAbstract).IsTrue();
    }

    // =============================================================================
    // B. StateContextSourceModel Tests (Task A2)
    // =============================================================================

    /// <summary>
    /// Verifies that StateContextSourceModel stores the property path.
    /// </summary>
    [Test]
    public async Task StateContextSourceModel_WithPropertyPath_StoresPath()
    {
        // Arrange
        const string propertyPath = "state.CustomerName";

        // Act
        var model = new StateContextSourceModel(propertyPath, "string", "state.CustomerName");

        // Assert
        await Assert.That(model.PropertyPath).IsEqualTo(propertyPath);
    }

    /// <summary>
    /// Verifies that StateContextSourceModel stores the access expression.
    /// </summary>
    [Test]
    public async Task StateContextSourceModel_WithAccessExpression_StoresExpression()
    {
        // Arrange
        const string accessExpression = "state.Order.Summary";

        // Act
        var model = new StateContextSourceModel("Order.Summary", "string", accessExpression);

        // Assert
        await Assert.That(model.AccessExpression).IsEqualTo(accessExpression);
    }

    // =============================================================================
    // C. RetrievalContextSourceModel Tests (Task A3)
    // =============================================================================

    /// <summary>
    /// Verifies that RetrievalContextSourceModel stores the collection type name.
    /// </summary>
    [Test]
    public async Task RetrievalContextSourceModel_WithCollectionType_StoresType()
    {
        // Arrange
        const string collectionTypeName = "ProductCatalog";

        // Act
        var model = new RetrievalContextSourceModel(
            collectionTypeName,
            QueryExpression: null,
            LiteralQuery: "product info",
            TopK: 5,
            MinRelevance: 0.7m,
            Filters: []);

        // Assert
        await Assert.That(model.CollectionTypeName).IsEqualTo(collectionTypeName);
    }

    /// <summary>
    /// Verifies that RetrievalContextSourceModel stores the TopK value.
    /// </summary>
    [Test]
    public async Task RetrievalContextSourceModel_WithTopK_StoresValue()
    {
        // Arrange & Act
        var model = new RetrievalContextSourceModel(
            "ProductCatalog",
            QueryExpression: null,
            LiteralQuery: "query",
            TopK: 10,
            MinRelevance: 0.8m,
            Filters: []);

        // Assert
        await Assert.That(model.TopK).IsEqualTo(10);
    }

    /// <summary>
    /// Verifies that RetrievalFilterModel with StaticValue returns IsStatic true.
    /// </summary>
    [Test]
    public async Task RetrievalFilterModel_WithStaticValue_IsStaticReturnsTrue()
    {
        // Arrange & Act
        var filter = new RetrievalFilterModel("category", StaticValue: "electronics", ValueExpression: null);

        // Assert
        await Assert.That(filter.IsStatic).IsTrue();
    }

    /// <summary>
    /// Verifies that RetrievalFilterModel with ValueExpression returns IsStatic false.
    /// </summary>
    [Test]
    public async Task RetrievalFilterModel_WithValueExpression_IsStaticReturnsFalse()
    {
        // Arrange & Act
        var filter = new RetrievalFilterModel("category", StaticValue: null, ValueExpression: "state.Category");

        // Assert
        await Assert.That(filter.IsStatic).IsFalse();
    }

    /// <summary>
    /// Verifies that LiteralContextSourceModel stores the literal value.
    /// </summary>
    [Test]
    public async Task LiteralContextSourceModel_WithValue_StoresValue()
    {
        // Arrange
        const string literalValue = "You are a helpful assistant.";

        // Act
        var model = new LiteralContextSourceModel(literalValue);

        // Assert
        await Assert.That(model.Value).IsEqualTo(literalValue);
    }
}

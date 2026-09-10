// =============================================================================
// <copyright file="InMemoryVectorSearchAdapterGenericTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Rag.Adapters;

namespace Strategos.Rag.Tests.Adapters;

/// <summary>
/// Unit tests for the <see cref="InMemoryVectorSearchAdapter{TCollection}"/> class.
/// </summary>
[Property("Category", "Unit")]
public class InMemoryVectorSearchAdapterGenericTests
{
    [Test]
    public async Task SearchAsync_WithNoDocuments_ReturnsEmptyList()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter<TestRagCollection>();

        // Act
        var results = await adapter.SearchAsync("test query");

        // Assert
        await Assert.That(results).IsNotNull();
        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SearchAsync_WithSeededDocuments_ReturnsMatches()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter<TestRagCollection>();
        adapter.Seed("Document about artificial intelligence", 0.9, "doc1");
        adapter.Seed("Document about machine learning", 0.85, "doc2");

        // Act
        var results = await adapter.SearchAsync("AI and ML", minRelevance: 0.8);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Score).IsGreaterThanOrEqualTo(results[1].Score);
    }

    [Test]
    public async Task SearchAsync_WithMinRelevance_FiltersBelowThreshold()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter<TestRagCollection>();
        adapter.Seed("High relevance document", 0.95, "high");
        adapter.Seed("Low relevance document", 0.5, "low");

        // Act
        var results = await adapter.SearchAsync("query", minRelevance: 0.7);

        // Assert
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo("high");
    }

    [Test]
    public async Task SearchAsync_WithTopK_LimitsResults()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter<TestRagCollection>();
        adapter.Seed("Doc 1", 0.95, "doc1");
        adapter.Seed("Doc 2", 0.90, "doc2");
        adapter.Seed("Doc 3", 0.85, "doc3");

        // Act
        var results = await adapter.SearchAsync("query", topK: 2, minRelevance: 0.8);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Seed_WithMetadata_StoresMetadata()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter<TestRagCollection>();
        var metadata = new Dictionary<string, object?> { { "category", "test" } };

        // Act
        adapter.Seed("Document with metadata", 0.9, "doc1", metadata);
        var results = await adapter.SearchAsync("query", minRelevance: 0.8);

        // Assert
        await Assert.That(results[0].Metadata).ContainsKey("category");
        await Assert.That(results[0].Metadata["category"]).IsEqualTo("test");
    }

    [Test]
    public async Task InMemoryVectorSearchAdapter_ImplementsGenericInterface()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter<TestRagCollection>();

        // Assert
        await Assert.That(adapter).IsAssignableTo<IVectorSearchAdapter<TestRagCollection>>();
    }

    /// <summary>
    /// Test implementation of IRagCollection for testing purposes.
    /// </summary>
    private sealed class TestRagCollection : IRagCollection
    {
    }
}

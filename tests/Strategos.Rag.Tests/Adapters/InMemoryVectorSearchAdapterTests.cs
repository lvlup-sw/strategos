// =============================================================================
// <copyright file="InMemoryVectorSearchAdapterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Rag.Adapters;

namespace Strategos.Rag.Tests.Adapters;

/// <summary>
/// Unit tests for the <see cref="InMemoryVectorSearchAdapter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class InMemoryVectorSearchAdapterTests
{
    [Test]
    public async Task SearchAsync_WithValidQuery_ReturnsMatchingResults()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("Document about artificial intelligence and machine learning");
        adapter.AddDocument("Document about natural language processing");

        // Act
        var results = await adapter.SearchAsync("artificial intelligence", minRelevance: 0.5);

        // Assert
        await Assert.That(results).IsNotNull();
        await Assert.That(results.Count).IsGreaterThan(0);
        await Assert.That(results[0].Content).Contains("artificial intelligence");
    }

    [Test]
    public async Task SearchAsync_WithEmptyCorpus_ReturnsEmptyList()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();

        // Act
        var results = await adapter.SearchAsync("test query");

        // Assert
        await Assert.That(results).IsNotNull();
        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SearchAsync_RespectsTopKParameter()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("Document one about testing");
        adapter.AddDocument("Document two about testing");
        adapter.AddDocument("Document three about testing");
        adapter.AddDocument("Document four about testing");

        // Act
        var results = await adapter.SearchAsync("testing", topK: 2, minRelevance: 0.5);

        // Assert
        await Assert.That(results.Count).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task SearchAsync_RespectsMinRelevanceFilter()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("exact match for test query");
        adapter.AddDocument("completely unrelated content about something else");

        // Act
        var results = await adapter.SearchAsync("test query", minRelevance: 0.9);

        // Assert
        await Assert.That(results.All(r => r.Score >= 0.9)).IsTrue();
    }

    [Test]
    public async Task SearchAsync_ReturnsResultsInDescendingRelevanceOrder()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("document with test");
        adapter.AddDocument("document with test and query");
        adapter.AddDocument("document with test query words");

        // Act
        var results = await adapter.SearchAsync("test query words", minRelevance: 0.1);

        // Assert
        await Assert.That(results.Count).IsGreaterThan(0);
        for (int i = 0; i < results.Count - 1; i++)
        {
            await Assert.That(results[i].Score).IsGreaterThanOrEqualTo(results[i + 1].Score);
        }
    }

    [Test]
    public async Task AddDocument_AddsDocumentSuccessfully()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();

        // Act
        adapter.AddDocument("New document content");
        var results = await adapter.SearchAsync("document content", minRelevance: 0.5);

        // Assert
        await Assert.That(results.Count).IsGreaterThan(0);
        await Assert.That(results[0].Content).IsEqualTo("New document content");
    }

    [Test]
    public async Task AddDocument_WithCustomId_SetsId()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        const string customId = "custom-doc-123";

        // Act
        adapter.AddDocument("Document with custom ID", id: customId);
        var results = await adapter.SearchAsync("custom ID", minRelevance: 0.5);

        // Assert
        await Assert.That(results.Count).IsGreaterThan(0);
        await Assert.That(results[0].Id).IsEqualTo(customId);
    }

    [Test]
    public async Task AddDocument_WithoutId_GeneratesId()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();

        // Act
        adapter.AddDocument("Document without explicit ID");
        var results = await adapter.SearchAsync("explicit ID", minRelevance: 0.5);

        // Assert
        await Assert.That(results.Count).IsGreaterThan(0);
        await Assert.That(results[0].Id).IsNotNull();
        await Assert.That(results[0].Id).IsNotEmpty();
    }

    [Test]
    public async Task AddDocument_WithMetadata_StoresMetadata()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        var metadata = new Dictionary<string, object?>
        {
            { "category", "technical" },
            { "author", "test-author" },
            { "tags", new[] { "testing", "unit-test" } },
        };

        // Act
        adapter.AddDocument("Document with metadata", metadata: metadata);
        var results = await adapter.SearchAsync("metadata", minRelevance: 0.5);

        // Assert
        await Assert.That(results.Count).IsGreaterThan(0);
        await Assert.That(results[0].Metadata).ContainsKey("category");
        await Assert.That(results[0].Metadata["category"]).IsEqualTo("technical");
        await Assert.That(results[0].Metadata["author"]).IsEqualTo("test-author");
    }

    [Test]
    public async Task AddDocument_WithNullMetadata_UsesEmptyDictionary()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();

        // Act
        adapter.AddDocument("Document without metadata", metadata: null);
        var results = await adapter.SearchAsync("without metadata", minRelevance: 0.5);

        // Assert
        await Assert.That(results.Count).IsGreaterThan(0);
        await Assert.That(results[0].Metadata).IsNotNull();
    }

    [Test]
    public async Task SearchAsync_WithSpecialCharactersInQuery_HandlesGracefully()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("Document with special characters: @#$%^&*()");

        // Act
        var results = await adapter.SearchAsync("@#$%^&*()", minRelevance: 0.1);

        // Assert
        await Assert.That(results).IsNotNull();
    }

    [Test]
    public async Task SearchAsync_WithEmptyQuery_ReturnsZeroScoreResults()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("Some document content");

        // Act
        // Empty query results in 0.0 score, which passes minRelevance: 0.0
        var results = await adapter.SearchAsync(string.Empty, minRelevance: 0.0);

        // Assert - Document is returned with score 0.0
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Score).IsEqualTo(0.0);
    }

    [Test]
    public async Task SearchAsync_WithWhitespaceOnlyQuery_ReturnsZeroScoreResults()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("Some document content");

        // Act
        // Whitespace-only query results in 0.0 score, which passes minRelevance: 0.0
        var results = await adapter.SearchAsync("   ", minRelevance: 0.0);

        // Assert - Document is returned with score 0.0
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Score).IsEqualTo(0.0);
    }

    [Test]
    public async Task SearchAsync_LargeCorpus_ReturnsResultsEfficiently()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        for (int i = 0; i < 1000; i++)
        {
            adapter.AddDocument($"Document number {i} with search term content", id: $"doc-{i}");
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await adapter.SearchAsync("search term", topK: 10, minRelevance: 0.3);
        stopwatch.Stop();

        // Assert
        await Assert.That(results.Count).IsLessThanOrEqualTo(10);
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(1000);
    }

    [Test]
    public async Task SearchAsync_ConcurrentOperations_HandlesThreadSafety()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        for (int i = 0; i < 100; i++)
        {
            adapter.AddDocument($"Document {i} with concurrent content", id: $"doc-{i}");
        }

        // Act - Run multiple searches concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => adapter.SearchAsync("concurrent content", topK: 5, minRelevance: 0.3))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        await Assert.That(results.All(r => r.Count > 0)).IsTrue();
    }

    [Test]
    public async Task SearchAsync_CaseInsensitiveMatching_FindsResults()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("Document with UPPERCASE content");

        // Act
        var results = await adapter.SearchAsync("uppercase", minRelevance: 0.5);

        // Assert
        await Assert.That(results.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task SearchAsync_PartialWordMatch_CalculatesScoreCorrectly()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("testing test tester");

        // Act - Query with "test" which appears as part of other words
        var results = await adapter.SearchAsync("test", minRelevance: 0.5);

        // Assert
        await Assert.That(results.Count).IsGreaterThan(0);
        await Assert.That(results[0].Score).IsEqualTo(1.0);
    }

    [Test]
    public async Task SearchAsync_MultipleTerms_CalculatesAverageScore()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("Document containing only first term");
        adapter.AddDocument("Document containing first and second terms");

        // Act - Query with multiple terms
        var results = await adapter.SearchAsync("first second", minRelevance: 0.4);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);

        // Document with both terms should score higher
        await Assert.That(results[0].Score).IsGreaterThan(results[1].Score);
    }

    [Test]
    public async Task SearchAsync_WithCancellationToken_DoesNotThrow()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();
        adapter.AddDocument("Test document");
        using var cts = new CancellationTokenSource();

        // Act
        var results = await adapter.SearchAsync("test", cancellationToken: cts.Token);

        // Assert
        await Assert.That(results).IsNotNull();
    }

    [Test]
    public async Task InMemoryVectorSearchAdapter_ImplementsIVectorSearchAdapter()
    {
        // Arrange
        var adapter = new InMemoryVectorSearchAdapter();

        // Assert
        await Assert.That(adapter).IsAssignableTo<IVectorSearchAdapter>();
    }
}


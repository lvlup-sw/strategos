// =============================================================================
// <copyright file="InMemoryArtifactStoreTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Infrastructure.Tests.ArtifactStores;

/// <summary>
/// Tests for the <see cref="InMemoryArtifactStore"/> class.
/// </summary>
/// <remarks>
/// Tests verify the in-memory implementation of the artifact store contract.
/// This implementation is suitable for testing and development scenarios.
/// </remarks>
[Property("Category", "Unit")]
public sealed class InMemoryArtifactStoreTests
{
    // =========================================================================
    // A. StoreAsync Tests
    // =========================================================================

    /// <summary>
    /// Verifies that StoreAsync returns a valid URI with memory scheme.
    /// </summary>
    [Test]
    public async Task StoreAsync_WithValidArtifact_ReturnsUriWithMemoryScheme()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };

        // Act
        var result = await store.StoreAsync(artifact, "test-category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Scheme).IsEqualTo("memory");
    }

    /// <summary>
    /// Verifies that StoreAsync throws ArgumentNullException when artifact is null.
    /// </summary>
    [Test]
    public async Task StoreAsync_WithNullArtifact_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryArtifactStore();

        // Act & Assert
        await Assert.That(async () => await store.StoreAsync<TestArtifact>(null!, "category", CancellationToken.None))
            .Throws<ArgumentNullException>()
            .WithParameterName("artifact");
    }

    /// <summary>
    /// Verifies that StoreAsync throws ArgumentException when category is null, empty, or whitespace.
    /// </summary>
    /// <param name="category">The invalid category value.</param>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task StoreAsync_WithInvalidCategory_ThrowsArgumentException(string? category)
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };

        // Act & Assert
        await Assert.That(async () => await store.StoreAsync(artifact, category!, CancellationToken.None))
            .Throws<ArgumentException>()
            .WithParameterName("category");
    }

    /// <summary>
    /// Verifies that the returned URI contains the category in its path.
    /// </summary>
    [Test]
    public async Task StoreAsync_ReturnedUri_ContainsCategory()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };

        // Act
        var result = await store.StoreAsync(artifact, "my-category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result.ToString()).Contains("my-category");
    }

    /// <summary>
    /// Verifies that multiple store operations return unique URIs.
    /// </summary>
    [Test]
    public async Task StoreAsync_CalledMultipleTimes_ReturnsUniqueUris()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact1 = new TestArtifact { Data = "data-1" };
        var artifact2 = new TestArtifact { Data = "data-2" };

        // Act
        var uri1 = await store.StoreAsync(artifact1, "category", CancellationToken.None).ConfigureAwait(false);
        var uri2 = await store.StoreAsync(artifact2, "category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(uri1).IsNotEqualTo(uri2);
    }

    // =========================================================================
    // B. RetrieveAsync Tests
    // =========================================================================

    /// <summary>
    /// Verifies that RetrieveAsync returns the previously stored artifact.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WithValidReference_ReturnsStoredArtifact()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Act
        var result = await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Data).IsEqualTo("test-data");
    }

    /// <summary>
    /// Verifies that RetrieveAsync throws ArgumentNullException when reference is null.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WithNullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryArtifactStore();

        // Act & Assert
        await Assert.That(async () => await store.RetrieveAsync<TestArtifact>(null!, CancellationToken.None))
            .Throws<ArgumentNullException>()
            .WithParameterName("reference");
    }

    /// <summary>
    /// Verifies that RetrieveAsync throws KeyNotFoundException when artifact not found.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WithNonExistentReference_ThrowsKeyNotFoundException()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var nonExistentUri = new Uri("memory://artifacts/nonexistent/12345");

        // Act & Assert
        await Assert.That(async () => await store.RetrieveAsync<TestArtifact>(nonExistentUri, CancellationToken.None))
            .Throws<KeyNotFoundException>();
    }

    /// <summary>
    /// Verifies that RetrieveAsync throws KeyNotFoundException after artifact is deleted.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_AfterDelete_ThrowsKeyNotFoundException()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);
        await store.DeleteAsync(uri, CancellationToken.None).ConfigureAwait(false);

        // Act & Assert
        await Assert.That(async () => await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None))
            .Throws<KeyNotFoundException>();
    }

    // =========================================================================
    // C. DeleteAsync Tests
    // =========================================================================

    /// <summary>
    /// Verifies that DeleteAsync removes the artifact from the store.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithValidReference_RemovesArtifact()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Act
        await store.DeleteAsync(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert - Verify artifact is gone
        await Assert.That(async () => await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None))
            .Throws<KeyNotFoundException>();
    }

    /// <summary>
    /// Verifies that DeleteAsync throws ArgumentNullException when reference is null.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithNullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryArtifactStore();

        // Act & Assert
        await Assert.That(async () => await store.DeleteAsync(null!, CancellationToken.None))
            .Throws<ArgumentNullException>()
            .WithParameterName("reference");
    }

    /// <summary>
    /// Verifies that DeleteAsync is idempotent - succeeds silently for non-existent artifact.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithNonExistentReference_SucceedsSilently()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var nonExistentUri = new Uri("memory://artifacts/nonexistent/12345");

        // Act & Assert - Should not throw (idempotent)
        await store.DeleteAsync(nonExistentUri, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that DeleteAsync is idempotent - calling twice succeeds silently.
    /// </summary>
    [Test]
    public async Task DeleteAsync_CalledTwice_SucceedsSilently()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Act - Delete twice
        await store.DeleteAsync(uri, CancellationToken.None).ConfigureAwait(false);
        await store.DeleteAsync(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert - No exception means success (implicit assertion)
    }

    // =========================================================================
    // D. Concurrency Tests
    // =========================================================================

    /// <summary>
    /// Verifies that concurrent store operations all succeed with unique URIs.
    /// </summary>
    [Test]
    public async Task StoreAsync_ConcurrentCalls_AllSucceedWithUniqueUris()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var tasks = new List<Task<Uri>>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var artifact = new TestArtifact { Data = $"data-{i}" };
            tasks.Add(store.StoreAsync(artifact, "category", CancellationToken.None).AsTask());
        }

        var uris = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - All URIs are unique
        var uniqueUris = uris.Distinct().ToList();
        await Assert.That(uniqueUris.Count).IsEqualTo(100);
    }

    /// <summary>
    /// Verifies that concurrent retrieve operations on the same URI work correctly.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_ConcurrentReads_AllReturnCorrectData()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "shared-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        var tasks = new List<Task<TestArtifact>>();

        // Act - Read same artifact 50 times concurrently
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None).AsTask());
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - All reads return the same data
        foreach (var result in results)
        {
            await Assert.That(result.Data).IsEqualTo("shared-data");
        }
    }

    /// <summary>
    /// Verifies that concurrent mixed operations (store, retrieve, delete) work correctly.
    /// </summary>
    [Test]
    public async Task ConcurrentMixedOperations_AllCompleteSuccessfully()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var storeTasks = new List<Task<Uri>>();
        var deleteTasks = new List<Task>();

        // Act - Store 20 artifacts
        for (int i = 0; i < 20; i++)
        {
            var artifact = new TestArtifact { Data = $"data-{i}" };
            storeTasks.Add(store.StoreAsync(artifact, "category", CancellationToken.None).AsTask());
        }

        var uris = await Task.WhenAll(storeTasks).ConfigureAwait(false);

        // Delete first 10 while reading last 10
        var retrieveTasks = new List<Task<TestArtifact>>();
        for (int i = 0; i < 10; i++)
        {
            deleteTasks.Add(store.DeleteAsync(uris[i], CancellationToken.None).AsTask());
            retrieveTasks.Add(store.RetrieveAsync<TestArtifact>(uris[i + 10], CancellationToken.None).AsTask());
        }

        await Task.WhenAll(deleteTasks).ConfigureAwait(false);
        var retrieved = await Task.WhenAll(retrieveTasks).ConfigureAwait(false);

        // Assert - Retrieved artifacts have correct data
        for (int i = 0; i < 10; i++)
        {
            await Assert.That(retrieved[i].Data).IsEqualTo($"data-{i + 10}");
        }
    }

    // =========================================================================
    // E. Complex Object Tests
    // =========================================================================

    /// <summary>
    /// Verifies round-trip storage and retrieval of complex nested objects.
    /// </summary>
    [Test]
    public async Task RoundTrip_WithComplexNestedObject_PreservesAllData()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new ComplexArtifact
        {
            Id = 42,
            Name = "Test Artifact",
            Tags = ["tag1", "tag2", "tag3"],
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2",
            },
            Nested = new NestedData
            {
                Level = 1,
                Description = "Nested description",
                Timestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            },
        };

        // Act
        var uri = await store.StoreAsync(artifact, "complex", CancellationToken.None).ConfigureAwait(false);
        var result = await store.RetrieveAsync<ComplexArtifact>(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result.Id).IsEqualTo(42);
        await Assert.That(result.Name).IsEqualTo("Test Artifact");
        await Assert.That(result.Tags).HasCount(3);
        await Assert.That(result.Metadata).ContainsKey("key1");
        await Assert.That(result.Nested).IsNotNull();
        await Assert.That(result.Nested!.Level).IsEqualTo(1);
        await Assert.That(result.Nested.Description).IsEqualTo("Nested description");
    }

    /// <summary>
    /// Verifies storage and retrieval of large artifacts.
    /// </summary>
    [Test]
    public async Task RoundTrip_WithLargeArtifact_SucceedsAndPreservesData()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var largeData = new string('x', 100_000); // 100KB of data
        var artifact = new TestArtifact { Data = largeData };

        // Act
        var uri = await store.StoreAsync(artifact, "large", CancellationToken.None).ConfigureAwait(false);
        var result = await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result.Data).IsEqualTo(largeData);
        await Assert.That(result.Data.Length).IsEqualTo(100_000);
    }

    /// <summary>
    /// Verifies storage of artifact with special characters in data.
    /// </summary>
    [Test]
    public async Task RoundTrip_WithSpecialCharacters_PreservesData()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var specialData = "Special chars: <>&\"'\\n\\t\u00e9\u00f1\u00fc emoji: \ud83d\ude00";
        var artifact = new TestArtifact { Data = specialData };

        // Act
        var uri = await store.StoreAsync(artifact, "special", CancellationToken.None).ConfigureAwait(false);
        var result = await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result.Data).IsEqualTo(specialData);
    }

    // =========================================================================
    // F. Category and URI Tests
    // =========================================================================

    /// <summary>
    /// Verifies that same content stored twice gets unique URIs.
    /// </summary>
    [Test]
    public async Task StoreAsync_SameContentTwice_GeneratesUniqueUris()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact1 = new TestArtifact { Data = "identical-data" };
        var artifact2 = new TestArtifact { Data = "identical-data" };

        // Act
        var uri1 = await store.StoreAsync(artifact1, "category", CancellationToken.None).ConfigureAwait(false);
        var uri2 = await store.StoreAsync(artifact2, "category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(uri1).IsNotEqualTo(uri2);
    }

    /// <summary>
    /// Verifies that different categories produce different URI paths.
    /// </summary>
    [Test]
    public async Task StoreAsync_DifferentCategories_ProduceDifferentPaths()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact1 = new TestArtifact { Data = "data" };
        var artifact2 = new TestArtifact { Data = "data" };

        // Act
        var uri1 = await store.StoreAsync(artifact1, "category-a", CancellationToken.None).ConfigureAwait(false);
        var uri2 = await store.StoreAsync(artifact2, "category-b", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(uri1.ToString()).Contains("category-a");
        await Assert.That(uri2.ToString()).Contains("category-b");
        await Assert.That(uri1).IsNotEqualTo(uri2);
    }

    /// <summary>
    /// Verifies that artifacts from different categories are isolated.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_FromDifferentCategories_ReturnsCorrectArtifacts()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact1 = new TestArtifact { Data = "data-from-cat1" };
        var artifact2 = new TestArtifact { Data = "data-from-cat2" };

        var uri1 = await store.StoreAsync(artifact1, "cat1", CancellationToken.None).ConfigureAwait(false);
        var uri2 = await store.StoreAsync(artifact2, "cat2", CancellationToken.None).ConfigureAwait(false);

        // Act
        var result1 = await store.RetrieveAsync<TestArtifact>(uri1, CancellationToken.None).ConfigureAwait(false);
        var result2 = await store.RetrieveAsync<TestArtifact>(uri2, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result1.Data).IsEqualTo("data-from-cat1");
        await Assert.That(result2.Data).IsEqualTo("data-from-cat2");
    }

    /// <summary>
    /// Verifies that the URI format follows expected pattern.
    /// </summary>
    [Test]
    public async Task StoreAsync_ReturnsUri_WithExpectedFormat()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test" };

        // Act
        var uri = await store.StoreAsync(artifact, "my-category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(uri.Scheme).IsEqualTo("memory");
        await Assert.That(uri.Host).IsEqualTo("artifacts");
        await Assert.That(uri.AbsolutePath).StartsWith("/my-category/");
    }

    /// <summary>
    /// Verifies that retrieve works with URI that has different host format.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WithDifferentHost_ExtractsKeyFromPath()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Create equivalent URI with different host but same path to test key extraction
        var altUri = new Uri($"memory://alternate-host{uri.AbsolutePath}");

        // Act - Use altUri which has the same path but different host
        var result = await store.RetrieveAsync<TestArtifact>(altUri, CancellationToken.None).ConfigureAwait(false);

        // Assert - Should find the artifact since the path (key) is the same
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Data).IsEqualTo("test-data");
    }

    // =========================================================================
    // G. Edge Case Tests
    // =========================================================================

    /// <summary>
    /// Verifies that delete does not affect other artifacts.
    /// </summary>
    [Test]
    public async Task DeleteAsync_DoesNotAffectOtherArtifacts()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact1 = new TestArtifact { Data = "data-1" };
        var artifact2 = new TestArtifact { Data = "data-2" };

        var uri1 = await store.StoreAsync(artifact1, "category", CancellationToken.None).ConfigureAwait(false);
        var uri2 = await store.StoreAsync(artifact2, "category", CancellationToken.None).ConfigureAwait(false);

        // Act
        await store.DeleteAsync(uri1, CancellationToken.None).ConfigureAwait(false);

        // Assert - uri2 should still be retrievable
        var result = await store.RetrieveAsync<TestArtifact>(uri2, CancellationToken.None).ConfigureAwait(false);
        await Assert.That(result.Data).IsEqualTo("data-2");
    }

    /// <summary>
    /// Verifies that store after delete uses new URI (counter not reused).
    /// </summary>
    [Test]
    public async Task StoreAsync_AfterDelete_UsesNewUri()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact1 = new TestArtifact { Data = "data-1" };
        var artifact2 = new TestArtifact { Data = "data-2" };

        var uri1 = await store.StoreAsync(artifact1, "category", CancellationToken.None).ConfigureAwait(false);
        await store.DeleteAsync(uri1, CancellationToken.None).ConfigureAwait(false);

        // Act
        var uri2 = await store.StoreAsync(artifact2, "category", CancellationToken.None).ConfigureAwait(false);

        // Assert - URI should be different (counter increments, doesn't reuse)
        await Assert.That(uri1).IsNotEqualTo(uri2);
    }

    /// <summary>
    /// Verifies that multiple stores maintain independent counters per instance.
    /// </summary>
    [Test]
    public async Task MultipleStoreInstances_HaveIndependentCounters()
    {
        // Arrange
        var store1 = new InMemoryArtifactStore();
        var store2 = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "data" };

        // Act
        var uri1 = await store1.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);
        var uri2 = await store2.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Assert - Both should have similar URIs (both start at counter 1)
        // but different store instances should be isolated
        await Assert.That(uri1.ToString()).Contains("category/1");
        await Assert.That(uri2.ToString()).Contains("category/1");
    }

    /// <summary>
    /// Verifies retrieval with empty artifact data works correctly.
    /// </summary>
    [Test]
    public async Task RoundTrip_WithEmptyData_PreservesEmptyString()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = string.Empty };

        // Act
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);
        var result = await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result.Data).IsEqualTo(string.Empty);
    }

    // =========================================================================
    // Test Fixtures
    // =========================================================================

    /// <summary>
    /// Test artifact for unit tests.
    /// </summary>
    private sealed class TestArtifact
    {
        /// <summary>
        /// Gets or initializes the test data.
        /// </summary>
        public string Data { get; init; } = string.Empty;
    }

    /// <summary>
    /// Complex artifact with nested data for round-trip tests.
    /// </summary>
    private sealed class ComplexArtifact
    {
        /// <summary>
        /// Gets or initializes the unique identifier.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Gets or initializes the name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the tags.
        /// </summary>
        public List<string> Tags { get; init; } = [];

        /// <summary>
        /// Gets or initializes the metadata dictionary.
        /// </summary>
        public Dictionary<string, string> Metadata { get; init; } = [];

        /// <summary>
        /// Gets or initializes the nested data.
        /// </summary>
        public NestedData? Nested { get; init; }
    }

    /// <summary>
    /// Nested data for complex artifact tests.
    /// </summary>
    private sealed class NestedData
    {
        /// <summary>
        /// Gets or initializes the nesting level.
        /// </summary>
        public int Level { get; init; }

        /// <summary>
        /// Gets or initializes the description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the timestamp.
        /// </summary>
        public DateTime Timestamp { get; init; }
    }
}

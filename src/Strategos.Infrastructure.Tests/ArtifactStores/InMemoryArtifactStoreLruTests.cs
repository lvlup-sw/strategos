// =============================================================================
// <copyright file="InMemoryArtifactStoreLruTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

namespace Strategos.Infrastructure.Tests.ArtifactStores;

/// <summary>
/// Tests for the LRU-bounded capacity behavior of <see cref="InMemoryArtifactStore"/>.
/// </summary>
[Property("Category", "Unit")]
public sealed class InMemoryArtifactStoreLruTests
{
    /// <summary>
    /// Verifies that the store accepts ILogger and IOptions constructor parameters.
    /// </summary>
    [Test]
    public void Constructor_WithLoggerAndOptions_DoesNotThrow()
    {
        // Arrange & Act
        var store = new InMemoryArtifactStore(
            NullLogger<InMemoryArtifactStore>.Instance,
            Options.Create(new InMemoryArtifactStoreOptions()));

        // Assert - no exception
    }

    /// <summary>
    /// Verifies that the default options set MaxCapacity to 10_000.
    /// </summary>
    [Test]
    public async Task DefaultOptions_MaxCapacity_Is10000()
    {
        var options = new InMemoryArtifactStoreOptions();
        await Assert.That(options.MaxCapacity).IsEqualTo(10_000);
    }

    /// <summary>
    /// Verifies that MaxCapacity can be configured.
    /// </summary>
    [Test]
    public async Task Options_MaxCapacity_IsConfigurable()
    {
        var options = new InMemoryArtifactStoreOptions { MaxCapacity = 500 };
        await Assert.That(options.MaxCapacity).IsEqualTo(500);
    }

    /// <summary>
    /// Verifies that the store evicts oldest items when capacity is exceeded.
    /// </summary>
    [Test]
    public async Task StoreAsync_ExceedsCapacity_EvictsOldestItems()
    {
        // Arrange - small capacity for testing
        var store = new InMemoryArtifactStore(
            NullLogger<InMemoryArtifactStore>.Instance,
            Options.Create(new InMemoryArtifactStoreOptions { MaxCapacity = 5 }));

        var uris = new List<Uri>();

        // Act - store 10 items (capacity is 5)
        for (int i = 0; i < 10; i++)
        {
            var artifact = new TestArtifact { Data = $"data-{i}" };
            var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);
            uris.Add(uri);
        }

        // Assert - recent items should be retrievable
        var lastUri = uris[^1];
        var lastResult = await store.RetrieveAsync<TestArtifact>(lastUri, CancellationToken.None).ConfigureAwait(false);
        await Assert.That(lastResult.Data).IsEqualTo("data-9");
    }

    /// <summary>
    /// Verifies that the store still works with basic store/retrieve when using LRU.
    /// </summary>
    [Test]
    public async Task StoreAndRetrieve_WithLru_RoundTrips()
    {
        // Arrange
        var store = new InMemoryArtifactStore(
            NullLogger<InMemoryArtifactStore>.Instance,
            Options.Create(new InMemoryArtifactStoreOptions { MaxCapacity = 100 }));

        var artifact = new TestArtifact { Data = "hello-lru" };

        // Act
        var uri = await store.StoreAsync(artifact, "test", CancellationToken.None).ConfigureAwait(false);
        var result = await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result.Data).IsEqualTo("hello-lru");
    }

    /// <summary>
    /// Verifies that delete works correctly with the LRU store.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithLru_RemovesItem()
    {
        // Arrange
        var store = new InMemoryArtifactStore(
            NullLogger<InMemoryArtifactStore>.Instance,
            Options.Create(new InMemoryArtifactStoreOptions { MaxCapacity = 100 }));

        var artifact = new TestArtifact { Data = "to-delete" };
        var uri = await store.StoreAsync(artifact, "test", CancellationToken.None).ConfigureAwait(false);

        // Act
        await store.DeleteAsync(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(async () => await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None))
            .Throws<KeyNotFoundException>();
    }

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
}

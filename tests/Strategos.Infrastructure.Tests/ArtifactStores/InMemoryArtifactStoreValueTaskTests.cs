// =============================================================================
// <copyright file="InMemoryArtifactStoreValueTaskTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Infrastructure.Tests.ArtifactStores;

/// <summary>
/// Tests verifying that <see cref="InMemoryArtifactStore"/> returns ValueTask for
/// allocation-free synchronous completion paths.
/// </summary>
/// <remarks>
/// <para>
/// ValueTask enables zero-allocation returns when the operation completes synchronously.
/// The in-memory implementation should always complete synchronously, allowing us to
/// verify that ValueTask is being used correctly for performance optimization.
/// </para>
/// <para>
/// Key verification: <see cref="ValueTask{TResult}.IsCompletedSuccessfully"/> is true
/// before awaiting, indicating synchronous completion without Task allocation.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class InMemoryArtifactStoreValueTaskTests
{
    // =========================================================================
    // A. StoreAsync ValueTask Tests
    // =========================================================================

    /// <summary>
    /// Verifies that StoreAsync returns a ValueTask that completes synchronously.
    /// </summary>
    /// <remarks>
    /// When IsCompletedSuccessfully is true before awaiting, the ValueTask completed
    /// synchronously without allocating a Task object on the heap.
    /// </remarks>
    [Test]
    public async Task StoreAsync_Synchronous_NoTaskAllocation()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };

        // Act
        var valueTask = store.StoreAsync(artifact, "test-category", CancellationToken.None);

        // Assert - ValueTask should complete synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        // Verify we can still get the result
        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Scheme).IsEqualTo("memory");
    }

    /// <summary>
    /// Verifies that multiple StoreAsync calls all complete synchronously.
    /// </summary>
    [Test]
    public async Task StoreAsync_MultipleCalls_AllCompleteSynchronously()
    {
        // Arrange
        var store = new InMemoryArtifactStore();

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            var artifact = new TestArtifact { Data = $"data-{i}" };
            var valueTask = store.StoreAsync(artifact, "category", CancellationToken.None);

            await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

            // Consume the result to avoid warnings
            _ = await valueTask.ConfigureAwait(false);
        }
    }

    // =========================================================================
    // B. RetrieveAsync ValueTask Tests
    // =========================================================================

    /// <summary>
    /// Verifies that RetrieveAsync returns a ValueTask that completes synchronously.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_Synchronous_NoTaskAllocation()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Act
        var valueTask = store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None);

        // Assert - ValueTask should complete synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        // Verify we can still get the result
        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.Data).IsEqualTo("test-data");
    }

    /// <summary>
    /// Verifies that multiple RetrieveAsync calls all complete synchronously.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_MultipleCalls_AllCompleteSynchronously()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            var valueTask = store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None);

            await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

            // Consume the result to avoid warnings
            _ = await valueTask.ConfigureAwait(false);
        }
    }

    // =========================================================================
    // C. DeleteAsync ValueTask Tests
    // =========================================================================

    /// <summary>
    /// Verifies that DeleteAsync returns a ValueTask that completes synchronously.
    /// </summary>
    [Test]
    public async Task DeleteAsync_Synchronous_NoTaskAllocation()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Act
        var valueTask = store.DeleteAsync(uri, CancellationToken.None);

        // Assert - ValueTask should complete synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        // Await to ensure completion
        await valueTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that DeleteAsync for non-existent artifact completes synchronously.
    /// </summary>
    [Test]
    public async Task DeleteAsync_NonExistent_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var nonExistentUri = new Uri("memory://artifacts/nonexistent/12345");

        // Act
        var valueTask = store.DeleteAsync(nonExistentUri, CancellationToken.None);

        // Assert - ValueTask should complete synchronously even for non-existent artifacts
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        // Await to ensure completion
        await valueTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that multiple DeleteAsync calls all complete synchronously.
    /// </summary>
    [Test]
    public async Task DeleteAsync_MultipleCalls_AllCompleteSynchronously()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var uris = new List<Uri>();
        for (int i = 0; i < 10; i++)
        {
            var artifact = new TestArtifact { Data = $"data-{i}" };
            var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);
            uris.Add(uri);
        }

        // Act & Assert
        foreach (var uri in uris)
        {
            var valueTask = store.DeleteAsync(uri, CancellationToken.None);

            await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

            // Await to ensure completion
            await valueTask.ConfigureAwait(false);
        }
    }

    // =========================================================================
    // D. Combined Operation ValueTask Tests
    // =========================================================================

    /// <summary>
    /// Verifies that a store-retrieve-delete cycle all complete synchronously.
    /// </summary>
    [Test]
    public async Task FullCycle_AllOperationsCompleteSynchronously()
    {
        // Arrange
        var store = new InMemoryArtifactStore();
        var artifact = new TestArtifact { Data = "full-cycle-test" };

        // Act & Assert - Store
        var storeTask = store.StoreAsync(artifact, "category", CancellationToken.None);
        await Assert.That(storeTask.IsCompletedSuccessfully).IsTrue();
        var uri = await storeTask.ConfigureAwait(false);

        // Act & Assert - Retrieve
        var retrieveTask = store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None);
        await Assert.That(retrieveTask.IsCompletedSuccessfully).IsTrue();
        var retrieved = await retrieveTask.ConfigureAwait(false);
        await Assert.That(retrieved.Data).IsEqualTo("full-cycle-test");

        // Act & Assert - Delete
        var deleteTask = store.DeleteAsync(uri, CancellationToken.None);
        await Assert.That(deleteTask.IsCompletedSuccessfully).IsTrue();
        await deleteTask.ConfigureAwait(false);
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
}

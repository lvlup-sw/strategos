// =============================================================================
// <copyright file="FileSystemArtifactStoreTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Infrastructure.Configuration;

using Microsoft.Extensions.Options;

namespace Strategos.Infrastructure.Tests.ArtifactStores;

/// <summary>
/// Tests for the <see cref="FileSystemArtifactStore"/> class.
/// </summary>
/// <remarks>
/// Tests verify the file system implementation of the artifact store contract.
/// This implementation is suitable for local deployments with durability requirements.
/// </remarks>
[Property("Category", "Unit")]
public sealed class FileSystemArtifactStoreTests : IAsyncDisposable
{
    private readonly string _testBasePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemArtifactStoreTests"/> class.
    /// </summary>
    public FileSystemArtifactStoreTests()
    {
        _testBasePath = Path.Combine(Path.GetTempPath(), $"artifact-store-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testBasePath);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_testBasePath))
        {
            Directory.Delete(_testBasePath, recursive: true);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    // =========================================================================
    // A. StoreAsync Tests
    // =========================================================================

    /// <summary>
    /// Verifies that StoreAsync returns a valid URI with file scheme.
    /// </summary>
    [Test]
    public async Task StoreAsync_WithValidArtifact_ReturnsUriWithFileScheme()
    {
        // Arrange
        var store = CreateStore();
        var artifact = new TestArtifact { Data = "test-data" };

        // Act
        var result = await store.StoreAsync(artifact, "test-category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Scheme).IsEqualTo("file");
    }

    /// <summary>
    /// Verifies that StoreAsync creates the artifact file on disk.
    /// </summary>
    [Test]
    public async Task StoreAsync_WithValidArtifact_CreatesFileOnDisk()
    {
        // Arrange
        var store = CreateStore();
        var artifact = new TestArtifact { Data = "test-data" };

        // Act
        var uri = await store.StoreAsync(artifact, "test-category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        var filePath = uri.LocalPath;
        await Assert.That(File.Exists(filePath)).IsTrue();
    }

    /// <summary>
    /// Verifies that StoreAsync throws ArgumentNullException when artifact is null.
    /// </summary>
    [Test]
    public async Task StoreAsync_WithNullArtifact_ThrowsArgumentNullException()
    {
        // Arrange
        var store = CreateStore();

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
        var store = CreateStore();
        var artifact = new TestArtifact { Data = "test-data" };

        // Act & Assert
        await Assert.That(async () => await store.StoreAsync(artifact, category!, CancellationToken.None))
            .Throws<ArgumentException>()
            .WithParameterName("category");
    }

    /// <summary>
    /// Verifies that StoreAsync creates category subdirectory.
    /// </summary>
    [Test]
    public async Task StoreAsync_WithCategory_CreatesCategorySubdirectory()
    {
        // Arrange
        var store = CreateStore();
        var artifact = new TestArtifact { Data = "test-data" };

        // Act
        var uri = await store.StoreAsync(artifact, "my-category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        var filePath = uri.LocalPath;
        await Assert.That(filePath).Contains("my-category");
        await Assert.That(Directory.Exists(Path.GetDirectoryName(filePath))).IsTrue();
    }

    /// <summary>
    /// Verifies that multiple store operations return unique URIs.
    /// </summary>
    [Test]
    public async Task StoreAsync_CalledMultipleTimes_ReturnsUniqueUris()
    {
        // Arrange
        var store = CreateStore();
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
        var store = CreateStore();
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
        var store = CreateStore();

        // Act & Assert
        await Assert.That(async () => await store.RetrieveAsync<TestArtifact>(null!, CancellationToken.None))
            .Throws<ArgumentNullException>()
            .WithParameterName("reference");
    }

    /// <summary>
    /// Verifies that RetrieveAsync throws KeyNotFoundException when file not found.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WithNonExistentReference_ThrowsKeyNotFoundException()
    {
        // Arrange
        var store = CreateStore();
        var nonExistentUri = new Uri($"file:///{_testBasePath}/nonexistent/12345.json");

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
        var store = CreateStore();
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
    /// Verifies that DeleteAsync removes the artifact file from disk.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithValidReference_RemovesFileFromDisk()
    {
        // Arrange
        var store = CreateStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);
        var filePath = uri.LocalPath;

        // Act
        await store.DeleteAsync(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(File.Exists(filePath)).IsFalse();
    }

    /// <summary>
    /// Verifies that DeleteAsync throws ArgumentNullException when reference is null.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithNullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var store = CreateStore();

        // Act & Assert
        await Assert.That(async () => await store.DeleteAsync(null!, CancellationToken.None))
            .Throws<ArgumentNullException>()
            .WithParameterName("reference");
    }

    /// <summary>
    /// Verifies that DeleteAsync is idempotent - succeeds silently for non-existent file.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithNonExistentReference_SucceedsSilently()
    {
        // Arrange
        var store = CreateStore();
        var nonExistentUri = new Uri($"file:///{_testBasePath}/nonexistent/12345.json");

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
        var store = CreateStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Act - Delete twice
        await store.DeleteAsync(uri, CancellationToken.None).ConfigureAwait(false);
        await store.DeleteAsync(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert - No exception means success (implicit assertion)
    }

    // =========================================================================
    // D. Options Tests
    // =========================================================================

    /// <summary>
    /// Verifies that constructor throws when options is null.
    /// </summary>
    [Test]
    public async Task Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new FileSystemArtifactStore(null!, NullLogger<FileSystemArtifactStore>.Instance))
            .Throws<ArgumentNullException>()
            .WithParameterName("options");
    }

    /// <summary>
    /// Verifies that custom file extension option is used for stored artifacts.
    /// </summary>
    [Test]
    public async Task StoreAsync_WithCustomFileExtension_UsesConfiguredExtension()
    {
        // Arrange
        var options = Options.Create(new FileSystemArtifactStoreOptions
        {
            BasePath = _testBasePath,
            FileExtension = ".dat",
        });
        var store = new FileSystemArtifactStore(options, NullLogger<FileSystemArtifactStore>.Instance);
        var artifact = new TestArtifact { Data = "test-data" };

        // Act
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(uri.LocalPath).EndsWith(".dat");
    }

    // =========================================================================
    // E. ValueTask Synchronous Completion Tests
    // =========================================================================

    /// <summary>
    /// Verifies that DeleteAsync returns a completed ValueTask synchronously.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WhenFileDoesNotExist_ReturnsCompletedValueTask()
    {
        // Arrange
        var store = CreateStore();
        var nonExistentUri = new Uri($"file:///{_testBasePath}/sync-test/nonexistent.json");

        // Act
        var valueTask = store.DeleteAsync(nonExistentUri, CancellationToken.None);

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();
        await valueTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that DeleteAsync returns a completed ValueTask when file exists.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WhenFileExists_DeletesAndCompletes()
    {
        // Arrange
        var store = CreateStore();
        var artifact = new TestArtifact { Data = "test-data" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None).ConfigureAwait(false);
        var filePath = uri.LocalPath;

        // Verify file exists before delete
        await Assert.That(File.Exists(filePath)).IsTrue();

        // Act
        var valueTask = store.DeleteAsync(uri, CancellationToken.None);

        // Assert - ValueTask should complete synchronously for delete
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();
        await Assert.That(File.Exists(filePath)).IsFalse();
    }

    // =========================================================================
    // F. Deserialization Error Tests
    // =========================================================================

    /// <summary>
    /// Verifies that RetrieveAsync throws InvalidOperationException when JSON deserializes to null.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WhenDeserializesToNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var store = CreateStore();
        var categoryPath = Path.Combine(_testBasePath, "null-test");
        Directory.CreateDirectory(categoryPath);
        var filePath = Path.Combine(categoryPath, "null-artifact.json");
        await File.WriteAllTextAsync(filePath, "null", CancellationToken.None).ConfigureAwait(false);
        var uri = new Uri(filePath);

        // Act & Assert
        await Assert.That(async () => await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None))
            .Throws<InvalidOperationException>();
    }

    // =========================================================================
    // G. Concurrent Access Tests
    // =========================================================================

    /// <summary>
    /// Verifies that concurrent store operations produce unique URIs.
    /// </summary>
    [Test]
    public async Task StoreAsync_ConcurrentOperations_ProducesUniqueUris()
    {
        // Arrange
        var store = CreateStore();
        const int concurrentCount = 10;
        var tasks = new List<Task<Uri>>();

        // Act - Store artifacts concurrently
        for (var i = 0; i < concurrentCount; i++)
        {
            var artifact = new TestArtifact { Data = $"data-{i}" };
            tasks.Add(store.StoreAsync(artifact, "concurrent", CancellationToken.None).AsTask());
        }

        var uris = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - All URIs should be unique
        var uniqueUris = uris.Distinct().ToList();
        await Assert.That(uniqueUris).HasCount(concurrentCount);
    }

    /// <summary>
    /// Verifies that concurrent store and retrieve operations work correctly.
    /// </summary>
    [Test]
    public async Task StoreAndRetrieve_ConcurrentOperations_WorksCorrectly()
    {
        // Arrange
        var store = CreateStore();
        const int operationCount = 5;
        var storedArtifacts = new List<(Uri Uri, string ExpectedData)>();

        // Store artifacts first
        for (var i = 0; i < operationCount; i++)
        {
            var artifact = new TestArtifact { Data = $"concurrent-data-{i}" };
            var uri = await store.StoreAsync(artifact, "concurrent-rw", CancellationToken.None).ConfigureAwait(false);
            storedArtifacts.Add((uri, $"concurrent-data-{i}"));
        }

        // Act - Retrieve all concurrently
        var retrieveTasks = storedArtifacts
            .Select(x => store.RetrieveAsync<TestArtifact>(x.Uri, CancellationToken.None).AsTask())
            .ToList();
        var retrievedArtifacts = await Task.WhenAll(retrieveTasks).ConfigureAwait(false);

        // Assert - All data should match
        for (var i = 0; i < operationCount; i++)
        {
            await Assert.That(retrievedArtifacts[i].Data).IsEqualTo(storedArtifacts[i].ExpectedData);
        }
    }

    // =========================================================================
    // H. Edge Case Tests
    // =========================================================================

    /// <summary>
    /// Verifies that storing an artifact with empty string data works correctly.
    /// </summary>
    [Test]
    public async Task StoreAndRetrieve_WithEmptyStringData_RoundTripsCorrectly()
    {
        // Arrange
        var store = CreateStore();
        var artifact = new TestArtifact { Data = string.Empty };

        // Act
        var uri = await store.StoreAsync(artifact, "edge-cases", CancellationToken.None).ConfigureAwait(false);
        var retrieved = await store.RetrieveAsync<TestArtifact>(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(retrieved.Data).IsEqualTo(string.Empty);
    }

    /// <summary>
    /// Verifies that storing an artifact with complex nested data works correctly.
    /// </summary>
    [Test]
    public async Task StoreAndRetrieve_WithComplexNestedData_RoundTripsCorrectly()
    {
        // Arrange
        var store = CreateStore();
        var artifact = new ComplexTestArtifact
        {
            Name = "root",
            Tags = ["tag1", "tag2", "tag3"],
            Nested = new NestedData { Value = 42, Description = "nested-value" },
        };

        // Act
        var uri = await store.StoreAsync(artifact, "complex", CancellationToken.None).ConfigureAwait(false);
        var retrieved = await store.RetrieveAsync<ComplexTestArtifact>(uri, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(retrieved.Name).IsEqualTo("root");
        await Assert.That(retrieved.Tags).HasCount(3);
        await Assert.That(retrieved.Tags).Contains("tag2");
        await Assert.That(retrieved.Nested).IsNotNull();
        await Assert.That(retrieved.Nested!.Value).IsEqualTo(42);
        await Assert.That(retrieved.Nested.Description).IsEqualTo("nested-value");
    }

    /// <summary>
    /// Verifies that storing artifacts in different categories creates separate directories.
    /// </summary>
    [Test]
    public async Task StoreAsync_DifferentCategories_CreatesSeparateDirectories()
    {
        // Arrange
        var store = CreateStore();
        var artifact1 = new TestArtifact { Data = "category-a" };
        var artifact2 = new TestArtifact { Data = "category-b" };

        // Act
        var uri1 = await store.StoreAsync(artifact1, "category-alpha", CancellationToken.None).ConfigureAwait(false);
        var uri2 = await store.StoreAsync(artifact2, "category-beta", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(uri1.LocalPath).Contains("category-alpha");
        await Assert.That(uri2.LocalPath).Contains("category-beta");
        await Assert.That(Directory.Exists(Path.Combine(_testBasePath, "category-alpha"))).IsTrue();
        await Assert.That(Directory.Exists(Path.Combine(_testBasePath, "category-beta"))).IsTrue();
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================
    private FileSystemArtifactStore CreateStore()
    {
        var options = Options.Create(new FileSystemArtifactStoreOptions
        {
            BasePath = _testBasePath,
        });

        return new FileSystemArtifactStore(options, NullLogger<FileSystemArtifactStore>.Instance);
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
    /// Complex test artifact with nested data for serialization tests.
    /// </summary>
    private sealed class ComplexTestArtifact
    {
        /// <summary>
        /// Gets or initializes the name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or initializes the tags collection.
        /// </summary>
        public List<string> Tags { get; init; } = [];

        /// <summary>
        /// Gets or initializes the nested data object.
        /// </summary>
        public NestedData? Nested { get; init; }
    }

    /// <summary>
    /// Nested data class for complex serialization tests.
    /// </summary>
    private sealed class NestedData
    {
        /// <summary>
        /// Gets or initializes the numeric value.
        /// </summary>
        public int Value { get; init; }

        /// <summary>
        /// Gets or initializes the description.
        /// </summary>
        public string Description { get; init; } = string.Empty;
    }
}


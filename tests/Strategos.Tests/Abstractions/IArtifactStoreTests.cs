// =============================================================================
// <copyright file="IArtifactStoreTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using NSubstitute;

namespace Strategos.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="IArtifactStore"/>.
/// </summary>
/// <remarks>
/// Tests verify the claim-check pattern contract for large artifact storage.
/// The artifact store enables workflows to handle large payloads efficiently:
/// <list type="bullet">
///   <item><description>LLM responses that exceed event size limits</description></item>
///   <item><description>Assembled context for agent steps</description></item>
///   <item><description>RAG retrieval results</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class IArtifactStoreTests
{
    // =============================================================================
    // A. StoreAsync Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StoreAsync returns a valid URI for stored artifact.
    /// </summary>
    [Test]
    public async Task StoreAsync_WithValidArtifact_ReturnsUri()
    {
        // Arrange
        var store = Substitute.For<IArtifactStore>();
        var artifact = new TestArtifact { Data = "test-data" };
        var expectedUri = new Uri("artifact://store/12345");

        store.StoreAsync(artifact, "test-category", Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Uri>(expectedUri));

        // Act
        var result = await store.StoreAsync(artifact, "test-category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(expectedUri);
    }

    /// <summary>
    /// Verifies that the StoreAsync method signature accepts generic type parameter.
    /// </summary>
    /// <remarks>
    /// Implementations must validate that the artifact parameter is not null
    /// and throw <see cref="ArgumentNullException"/>.
    /// </remarks>
    [Test]
    public async Task StoreAsync_AcceptsGenericTypeParameter()
    {
        // Arrange
        var store = Substitute.For<IArtifactStore>();
        var stringArtifact = "simple string artifact";
        var expectedUri = new Uri("artifact://store/string-123");

        store.StoreAsync(stringArtifact, "string-category", Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Uri>(expectedUri));

        // Act
        var result = await store.StoreAsync(stringArtifact, "string-category", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsEqualTo(expectedUri);
    }

    /// <summary>
    /// Verifies that the StoreAsync method supports cancellation token.
    /// </summary>
    /// <remarks>
    /// Implementations should honor the cancellation token and throw
    /// <see cref="OperationCanceledException"/> when cancelled.
    /// </remarks>
    [Test]
    public async Task StoreAsync_AcceptsCancellationToken()
    {
        // Arrange
        var store = Substitute.For<IArtifactStore>();
        var artifact = new TestArtifact { Data = "test-data" };
        var expectedUri = new Uri("artifact://store/12345");
        using var cts = new CancellationTokenSource();

        store.StoreAsync(artifact, "test-category", cts.Token)
            .Returns(new ValueTask<Uri>(expectedUri));

        // Act
        var result = await store.StoreAsync(artifact, "test-category", cts.Token).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsEqualTo(expectedUri);
        _ = store.Received(1).StoreAsync(artifact, "test-category", cts.Token);
    }

    // =============================================================================
    // B. RetrieveAsync Tests
    // =============================================================================

    /// <summary>
    /// Verifies that RetrieveAsync returns the stored artifact.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WithValidUri_ReturnsArtifact()
    {
        // Arrange
        var store = Substitute.For<IArtifactStore>();
        var expectedArtifact = new TestArtifact { Data = "retrieved-data" };
        var reference = new Uri("artifact://store/12345");

        store.RetrieveAsync<TestArtifact>(reference, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<TestArtifact>(expectedArtifact));

        // Act
        var result = await store.RetrieveAsync<TestArtifact>(reference, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Data).IsEqualTo("retrieved-data");
    }

    /// <summary>
    /// Verifies that RetrieveAsync supports generic type retrieval.
    /// </summary>
    /// <remarks>
    /// Implementations must support deserializing to different types.
    /// When the artifact does not exist, <see cref="KeyNotFoundException"/> should be thrown.
    /// </remarks>
    [Test]
    public async Task RetrieveAsync_AcceptsGenericTypeParameter()
    {
        // Arrange
        var store = Substitute.For<IArtifactStore>();
        var expectedArtifact = "simple string artifact";
        var reference = new Uri("artifact://store/string-123");

        store.RetrieveAsync<string>(reference, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(expectedArtifact));

        // Act
        var result = await store.RetrieveAsync<string>(reference, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsEqualTo(expectedArtifact);
    }

    /// <summary>
    /// Verifies that RetrieveAsync supports cancellation token.
    /// </summary>
    /// <remarks>
    /// Implementations should honor the cancellation token and throw
    /// <see cref="OperationCanceledException"/> when cancelled.
    /// When the reference is null, <see cref="ArgumentNullException"/> should be thrown.
    /// </remarks>
    [Test]
    public async Task RetrieveAsync_AcceptsCancellationToken()
    {
        // Arrange
        var store = Substitute.For<IArtifactStore>();
        var expectedArtifact = new TestArtifact { Data = "retrieved-data" };
        var reference = new Uri("artifact://store/12345");
        using var cts = new CancellationTokenSource();

        store.RetrieveAsync<TestArtifact>(reference, cts.Token)
            .Returns(new ValueTask<TestArtifact>(expectedArtifact));

        // Act
        var result = await store.RetrieveAsync<TestArtifact>(reference, cts.Token).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        _ = store.Received(1).RetrieveAsync<TestArtifact>(reference, cts.Token);
    }

    // =============================================================================
    // C. DeleteAsync Tests
    // =============================================================================

    /// <summary>
    /// Verifies that DeleteAsync completes successfully with valid URI.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithValidUri_CompletesSuccessfully()
    {
        // Arrange
        var store = Substitute.For<IArtifactStore>();
        var reference = new Uri("artifact://store/12345");

        store.DeleteAsync(reference, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act & Assert - should complete without exception
        await store.DeleteAsync(reference, CancellationToken.None).ConfigureAwait(false);
        _ = store.Received(1).DeleteAsync(reference, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that DeleteAsync is idempotent - deleting non-existent artifact succeeds.
    /// </summary>
    /// <remarks>
    /// Implementations must handle the case where the artifact does not exist.
    /// The delete operation is idempotent - deleting a non-existent artifact succeeds silently.
    /// When the reference is null, <see cref="ArgumentNullException"/> should be thrown.
    /// </remarks>
    [Test]
    public async Task DeleteAsync_WithNonExistentUri_CompletesSuccessfully()
    {
        // Arrange
        var store = Substitute.For<IArtifactStore>();
        var reference = new Uri("artifact://store/non-existent");

        store.DeleteAsync(reference, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act & Assert - should complete without exception (idempotent)
        await store.DeleteAsync(reference, CancellationToken.None).ConfigureAwait(false);
        _ = store.Received(1).DeleteAsync(reference, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that DeleteAsync supports cancellation token.
    /// </summary>
    /// <remarks>
    /// Implementations should honor the cancellation token and throw
    /// <see cref="OperationCanceledException"/> when cancelled.
    /// </remarks>
    [Test]
    public async Task DeleteAsync_AcceptsCancellationToken()
    {
        // Arrange
        var store = Substitute.For<IArtifactStore>();
        var reference = new Uri("artifact://store/12345");
        using var cts = new CancellationTokenSource();

        store.DeleteAsync(reference, cts.Token)
            .Returns(ValueTask.CompletedTask);

        // Act
        await store.DeleteAsync(reference, cts.Token).ConfigureAwait(false);

        // Assert
        _ = store.Received(1).DeleteAsync(reference, cts.Token);
    }

    // =============================================================================
    // Test Fixtures
    // =============================================================================

    /// <summary>
    /// Test artifact for unit tests.
    /// </summary>
    private sealed record TestArtifact
    {
        /// <summary>
        /// Gets or sets the test data.
        /// </summary>
        public string Data { get; init; } = string.Empty;
    }
}

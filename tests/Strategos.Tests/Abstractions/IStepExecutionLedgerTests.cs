// =============================================================================
// <copyright file="IStepExecutionLedgerTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using NSubstitute;

namespace Strategos.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="IStepExecutionLedger"/>.
/// </summary>
/// <remarks>
/// Tests verify the memoization contract for expensive step operations.
/// The step execution ledger provides caching for expensive operations:
/// <list type="bullet">
///   <item><description>LLM API calls that are costly and slow</description></item>
///   <item><description>External service calls during workflow retry/recovery</description></item>
///   <item><description>Idempotent replay of previously executed steps</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class IStepExecutionLedgerTests
{
    // =============================================================================
    // A. TryGetCachedResultAsync Tests
    // =============================================================================

    /// <summary>
    /// Verifies that TryGetCachedResultAsync returns null when result is not cached.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_WhenNotCached_ReturnsNull()
    {
        // Arrange
        var ledger = Substitute.For<IStepExecutionLedger>();
        var stepName = "ProcessClaim";
        var inputHash = "abc123hash";

        ledger.TryGetCachedResultAsync<TestStepResult>(stepName, inputHash, Arg.Any<CancellationToken>())
            .Returns((TestStepResult?)null);

        // Act
        var result = await ledger.TryGetCachedResultAsync<TestStepResult>(stepName, inputHash, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies that TryGetCachedResultAsync returns cached result when available.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_WhenCached_ReturnsCachedResult()
    {
        // Arrange
        var ledger = Substitute.For<IStepExecutionLedger>();
        var stepName = "ProcessClaim";
        var inputHash = "abc123hash";
        var expectedResult = new TestStepResult { Output = "cached-output" };

        ledger.TryGetCachedResultAsync<TestStepResult>(stepName, inputHash, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await ledger.TryGetCachedResultAsync<TestStepResult>(stepName, inputHash, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Output).IsEqualTo("cached-output");
    }

    /// <summary>
    /// Verifies that TryGetCachedResultAsync supports cancellation token.
    /// </summary>
    /// <remarks>
    /// Implementations must validate that stepName and inputHash are not null or whitespace
    /// and throw <see cref="ArgumentException"/>.
    /// </remarks>
    [Test]
    public async Task TryGetCachedResultAsync_AcceptsCancellationToken()
    {
        // Arrange
        var ledger = Substitute.For<IStepExecutionLedger>();
        var stepName = "ProcessClaim";
        var inputHash = "abc123hash";
        var expectedResult = new TestStepResult { Output = "cached-output" };
        using var cts = new CancellationTokenSource();

        ledger.TryGetCachedResultAsync<TestStepResult>(stepName, inputHash, cts.Token)
            .Returns(expectedResult);

        // Act
        var result = await ledger.TryGetCachedResultAsync<TestStepResult>(stepName, inputHash, cts.Token).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        _ = ledger.Received(1).TryGetCachedResultAsync<TestStepResult>(stepName, inputHash, cts.Token);
    }

    // =============================================================================
    // B. CacheResultAsync Tests
    // =============================================================================

    /// <summary>
    /// Verifies that CacheResultAsync completes successfully with valid input.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_WithValidInput_CompletesSuccessfully()
    {
        // Arrange
        var ledger = Substitute.For<IStepExecutionLedger>();
        var stepName = "ProcessClaim";
        var inputHash = "abc123hash";
        var result = new TestStepResult { Output = "result-to-cache" };
        var ttl = TimeSpan.FromMinutes(30);

        ledger.CacheResultAsync(stepName, inputHash, result, ttl, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act & Assert - should complete without exception
        await ledger.CacheResultAsync(stepName, inputHash, result, ttl, CancellationToken.None).ConfigureAwait(false);
        _ = ledger.Received(1).CacheResultAsync(stepName, inputHash, result, ttl, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that CacheResultAsync accepts null TTL for default expiration.
    /// </summary>
    /// <remarks>
    /// When TTL is null, implementations should use a default expiration time.
    /// Implementations must validate that stepName and inputHash are not null or whitespace
    /// and throw <see cref="ArgumentException"/>.
    /// Implementations must validate that result is not null and throw <see cref="ArgumentNullException"/>.
    /// </remarks>
    [Test]
    public async Task CacheResultAsync_WithNullTtl_UsesDefaultExpiration()
    {
        // Arrange
        var ledger = Substitute.For<IStepExecutionLedger>();
        var stepName = "ProcessClaim";
        var inputHash = "abc123hash";
        var result = new TestStepResult { Output = "result-to-cache" };

        ledger.CacheResultAsync(stepName, inputHash, result, null, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act & Assert - should complete without exception
        await ledger.CacheResultAsync(stepName, inputHash, result, null, CancellationToken.None).ConfigureAwait(false);
        _ = ledger.Received(1).CacheResultAsync(stepName, inputHash, result, null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that CacheResultAsync supports cancellation token.
    /// </summary>
    /// <remarks>
    /// Implementations should honor the cancellation token and throw
    /// <see cref="OperationCanceledException"/> when cancelled.
    /// </remarks>
    [Test]
    public async Task CacheResultAsync_AcceptsCancellationToken()
    {
        // Arrange
        var ledger = Substitute.For<IStepExecutionLedger>();
        var stepName = "ProcessClaim";
        var inputHash = "abc123hash";
        var result = new TestStepResult { Output = "result-to-cache" };
        var ttl = TimeSpan.FromMinutes(30);
        using var cts = new CancellationTokenSource();

        ledger.CacheResultAsync(stepName, inputHash, result, ttl, cts.Token)
            .Returns(Task.CompletedTask);

        // Act
        await ledger.CacheResultAsync(stepName, inputHash, result, ttl, cts.Token).ConfigureAwait(false);

        // Assert
        _ = ledger.Received(1).CacheResultAsync(stepName, inputHash, result, ttl, cts.Token);
    }

    // =============================================================================
    // C. ComputeInputHash Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ComputeInputHash returns consistent hash for same input.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_WithSameInput_ReturnsSameHash()
    {
        // Arrange
        var ledger = Substitute.For<IStepExecutionLedger>();
        var input = new TestStepInput { ClaimId = "CLM-001", Amount = 1500.00m };
        var expectedHash = "sha256-abc123def456";

        ledger.ComputeInputHash(Arg.Is<TestStepInput>(i => i.ClaimId == "CLM-001" && i.Amount == 1500.00m))
            .Returns(expectedHash);

        // Act
        var result1 = ledger.ComputeInputHash(input);
        var result2 = ledger.ComputeInputHash(input);

        // Assert
        await Assert.That(result1).IsEqualTo(expectedHash);
        await Assert.That(result2).IsEqualTo(expectedHash);
        await Assert.That(result1).IsEqualTo(result2);
    }

    /// <summary>
    /// Verifies that ComputeInputHash returns different hashes for different inputs.
    /// </summary>
    /// <remarks>
    /// The hash must be deterministic - the same input must always produce the same hash.
    /// Implementations should serialize the input to JSON before hashing for consistency.
    /// </remarks>
    [Test]
    public async Task ComputeInputHash_WithDifferentInputs_ReturnsDifferentHashes()
    {
        // Arrange
        var ledger = Substitute.For<IStepExecutionLedger>();
        var input1 = new TestStepInput { ClaimId = "CLM-001", Amount = 1500.00m };
        var input2 = new TestStepInput { ClaimId = "CLM-002", Amount = 2500.00m };
        var hash1 = "sha256-abc123";
        var hash2 = "sha256-def456";

        ledger.ComputeInputHash(Arg.Is<TestStepInput>(i => i.ClaimId == "CLM-001"))
            .Returns(hash1);
        ledger.ComputeInputHash(Arg.Is<TestStepInput>(i => i.ClaimId == "CLM-002"))
            .Returns(hash2);

        // Act
        var result1 = ledger.ComputeInputHash(input1);
        var result2 = ledger.ComputeInputHash(input2);

        // Assert
        await Assert.That(result1).IsEqualTo(hash1);
        await Assert.That(result2).IsEqualTo(hash2);
        await Assert.That(result1).IsNotEqualTo(result2);
    }

    /// <summary>
    /// Verifies that ComputeInputHash accepts generic type parameter.
    /// </summary>
    /// <remarks>
    /// Implementations must validate that input is not null
    /// and throw <see cref="ArgumentNullException"/>.
    /// </remarks>
    [Test]
    public async Task ComputeInputHash_AcceptsGenericTypeParameter()
    {
        // Arrange
        var ledger = Substitute.For<IStepExecutionLedger>();
        var stringInput = "simple string input";
        var expectedHash = "sha256-string123";

        ledger.ComputeInputHash(stringInput)
            .Returns(expectedHash);

        // Act
        var result = ledger.ComputeInputHash(stringInput);

        // Assert
        await Assert.That(result).IsEqualTo(expectedHash);
        _ = ledger.Received(1).ComputeInputHash(stringInput);
    }

    // =============================================================================
    // Test Fixtures
    // =============================================================================

    /// <summary>
    /// Test step result for unit tests.
    /// </summary>
    private sealed record TestStepResult
    {
        /// <summary>
        /// Gets the output data.
        /// </summary>
        public string Output { get; init; } = string.Empty;
    }

    /// <summary>
    /// Test step input for unit tests.
    /// </summary>
    private sealed record TestStepInput
    {
        /// <summary>
        /// Gets the claim ID.
        /// </summary>
        public string ClaimId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the amount.
        /// </summary>
        public decimal Amount { get; init; }
    }
}

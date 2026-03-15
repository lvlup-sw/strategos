// =============================================================================
// <copyright file="InMemoryStepExecutionLedgerTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Infrastructure.ExecutionLedgers;

using MemoryPack;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Strategos.Infrastructure.Tests.ExecutionLedgers;

/// <summary>
/// Tests for the <see cref="InMemoryStepExecutionLedger"/> class.
/// </summary>
/// <remarks>
/// Tests verify the in-memory implementation of the step execution ledger contract.
/// This implementation is suitable for testing and development scenarios.
/// </remarks>
[Property("Category", "Unit")]
public sealed partial class InMemoryStepExecutionLedgerTests
{
    // =========================================================================
    // A. TryGetCachedResultAsync Tests
    // =========================================================================

    /// <summary>
    /// Verifies that TryGetCachedResultAsync returns null when cache is empty.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_WhenCacheEmpty_ReturnsNull()
    {
        // Arrange
        var ledger = CreateLedger();

        // Act
        var result = await ledger.TryGetCachedResultAsync<TestResult>(
            "step-name",
            "input-hash",
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies that TryGetCachedResultAsync returns cached result when found.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_WhenCached_ReturnsCachedResult()
    {
        // Arrange
        var ledger = CreateLedger();
        var expected = new TestResult("cached-value");
        await ledger.CacheResultAsync(
            "step-name",
            "input-hash",
            expected,
            null,
            CancellationToken.None).ConfigureAwait(false);

        // Act
        var result = await ledger.TryGetCachedResultAsync<TestResult>(
            "step-name",
            "input-hash",
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo("cached-value");
    }

    /// <summary>
    /// Verifies that TryGetCachedResultAsync throws ArgumentException for null step name.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task TryGetCachedResultAsync_WithInvalidStepName_ThrowsArgumentException(string? stepName)
    {
        // Arrange
        var ledger = CreateLedger();

        // Act & Assert
        await Assert.That(async () => await ledger.TryGetCachedResultAsync<TestResult>(
            stepName!,
            "input-hash",
            CancellationToken.None))
            .Throws<ArgumentException>()
            .WithParameterName("stepName");
    }

    /// <summary>
    /// Verifies that TryGetCachedResultAsync throws ArgumentException for null input hash.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task TryGetCachedResultAsync_WithInvalidInputHash_ThrowsArgumentException(string? inputHash)
    {
        // Arrange
        var ledger = CreateLedger();

        // Act & Assert
        await Assert.That(async () => await ledger.TryGetCachedResultAsync<TestResult>(
            "step-name",
            inputHash!,
            CancellationToken.None))
            .Throws<ArgumentException>()
            .WithParameterName("inputHash");
    }

    /// <summary>
    /// Verifies that different step names have separate cache entries.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_DifferentStepNames_ReturnsSeparateResults()
    {
        // Arrange
        var ledger = CreateLedger();
        var result1 = new TestResult("result-1");
        var result2 = new TestResult("result-2");

        await ledger.CacheResultAsync("step-1", "hash", result1, null, CancellationToken.None).ConfigureAwait(false);
        await ledger.CacheResultAsync("step-2", "hash", result2, null, CancellationToken.None).ConfigureAwait(false);

        // Act
        var cached1 = await ledger.TryGetCachedResultAsync<TestResult>("step-1", "hash", CancellationToken.None).ConfigureAwait(false);
        var cached2 = await ledger.TryGetCachedResultAsync<TestResult>("step-2", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached1!.Value).IsEqualTo("result-1");
        await Assert.That(cached2!.Value).IsEqualTo("result-2");
    }

    // =========================================================================
    // B. CacheResultAsync Tests
    // =========================================================================

    /// <summary>
    /// Verifies that CacheResultAsync stores the result successfully.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_WithValidInput_StoresResult()
    {
        // Arrange
        var ledger = CreateLedger();
        var result = new TestResult("test-value");

        // Act
        await ledger.CacheResultAsync(
            "step-name",
            "input-hash",
            result,
            null,
            CancellationToken.None).ConfigureAwait(false);

        // Assert - Verify by retrieval
        var cached = await ledger.TryGetCachedResultAsync<TestResult>(
            "step-name",
            "input-hash",
            CancellationToken.None).ConfigureAwait(false);

        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("test-value");
    }

    /// <summary>
    /// Verifies that CacheResultAsync throws ArgumentException for null step name.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CacheResultAsync_WithInvalidStepName_ThrowsArgumentException(string? stepName)
    {
        // Arrange
        var ledger = CreateLedger();
        var result = new TestResult("test-value");

        // Act & Assert
        await Assert.That(() => ledger.CacheResultAsync(
            stepName!,
            "input-hash",
            result,
            null,
            CancellationToken.None))
            .Throws<ArgumentException>()
            .WithParameterName("stepName");
    }

    /// <summary>
    /// Verifies that CacheResultAsync throws ArgumentException for null input hash.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CacheResultAsync_WithInvalidInputHash_ThrowsArgumentException(string? inputHash)
    {
        // Arrange
        var ledger = CreateLedger();
        var result = new TestResult("test-value");

        // Act & Assert
        await Assert.That(() => ledger.CacheResultAsync(
            "step-name",
            inputHash!,
            result,
            null,
            CancellationToken.None))
            .Throws<ArgumentException>()
            .WithParameterName("inputHash");
    }

    /// <summary>
    /// Verifies that CacheResultAsync throws ArgumentNullException for null result.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_WithNullResult_ThrowsArgumentNullException()
    {
        // Arrange
        var ledger = CreateLedger();

        // Act & Assert
        await Assert.That(() => ledger.CacheResultAsync<TestResult>(
            "step-name",
            "input-hash",
            null!,
            null,
            CancellationToken.None))
            .Throws<ArgumentNullException>()
            .WithParameterName("result");
    }

    /// <summary>
    /// Verifies that CacheResultAsync overwrites existing cache entry.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_CalledTwice_OverwritesPreviousEntry()
    {
        // Arrange
        var ledger = CreateLedger();
        var result1 = new TestResult("first");
        var result2 = new TestResult("second");

        await ledger.CacheResultAsync("step", "hash", result1, null, CancellationToken.None).ConfigureAwait(false);

        // Act
        await ledger.CacheResultAsync("step", "hash", result2, null, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);
        await Assert.That(cached!.Value).IsEqualTo("second");
    }

    /// <summary>
    /// Verifies that CacheResultAsync respects TTL and expires entries.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_WithTtl_ExpiresAfterDuration()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var ledger = CreateLedger(timeProvider);
        var result = new TestResult("test-value");
        var ttl = TimeSpan.FromMinutes(5);

        await ledger.CacheResultAsync("step", "hash", result, ttl, CancellationToken.None).ConfigureAwait(false);

        // Act - Advance time past TTL
        timeProvider.Advance(TimeSpan.FromMinutes(6));

        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNull();
    }

    // =========================================================================
    // C. ComputeInputHash Tests
    // =========================================================================

    /// <summary>
    /// Verifies that ComputeInputHash returns consistent hash for same input.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_SameInput_ReturnsSameHash()
    {
        // Arrange
        var ledger = CreateLedger();
        var input = new TestInput(1, "test");

        // Act
        var hash1 = ledger.ComputeInputHash(input);
        var hash2 = ledger.ComputeInputHash(input);

        // Assert
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    /// <summary>
    /// Verifies that ComputeInputHash returns different hash for different input.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_DifferentInput_ReturnsDifferentHash()
    {
        // Arrange
        var ledger = CreateLedger();
        var input1 = new TestInput(1, "test");
        var input2 = new TestInput(2, "test");

        // Act
        var hash1 = ledger.ComputeInputHash(input1);
        var hash2 = ledger.ComputeInputHash(input2);

        // Assert
        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    /// <summary>
    /// Verifies that ComputeInputHash throws ArgumentNullException for null input.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var ledger = CreateLedger();

        // Act & Assert
        await Assert.That(() => ledger.ComputeInputHash<TestInput>(null!))
            .Throws<ArgumentNullException>()
            .WithParameterName("input");
    }

    /// <summary>
    /// Verifies that ComputeInputHash returns hex-encoded string.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_ReturnsHexEncodedString()
    {
        // Arrange
        var ledger = CreateLedger();
        var input = new TestInput(1, "test");

        // Act
        var hash = ledger.ComputeInputHash(input);

        // Assert - SHA256 produces 64 hex characters
        await Assert.That(hash.Length).IsEqualTo(64);
        await Assert.That(hash.All(c => char.IsLetterOrDigit(c))).IsTrue();
    }

    // =========================================================================
    // D. ValueTask Optimization Tests
    // =========================================================================

    /// <summary>
    /// Verifies that TryGetCachedResultAsync returns ValueTask to avoid Task allocations
    /// for synchronous cache operations.
    /// </summary>
    /// <remarks>
    /// This test validates the optimization where TryGetCachedResultAsync returns
    /// ValueTask instead of Task, avoiding heap allocations for the common case
    /// of synchronous dictionary lookups.
    /// </remarks>
    [Test]
    public async Task TryGetCachedResultAsync_ReturnsValueTask()
    {
        // Arrange
        var ledger = CreateLedger();
        var result = new TestResult("test-value");
        await ledger.CacheResultAsync("step", "hash", result, null, CancellationToken.None).ConfigureAwait(false);

        // Act - Get the method return type to verify it's ValueTask
        var method = typeof(InMemoryStepExecutionLedger).GetMethod(
            nameof(InMemoryStepExecutionLedger.TryGetCachedResultAsync));

        // Assert - Method should return ValueTask<TResult?>
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType.GetGenericTypeDefinition()).IsEqualTo(typeof(ValueTask<>));
    }

    /// <summary>
    /// Verifies that cache miss returns default ValueTask without allocation.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_CacheMiss_ReturnsDefaultValueTask()
    {
        // Arrange
        var ledger = CreateLedger();

        // Act
        var result = await ledger.TryGetCachedResultAsync<TestResult>(
            "nonexistent-step",
            "nonexistent-hash",
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies that cache hit returns ValueTask with result.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_CacheHit_ReturnsValueTaskWithResult()
    {
        // Arrange
        var ledger = CreateLedger();
        var expected = new TestResult("cached-value");
        await ledger.CacheResultAsync("step", "hash", expected, null, CancellationToken.None).ConfigureAwait(false);

        // Act
        var result = await ledger.TryGetCachedResultAsync<TestResult>(
            "step",
            "hash",
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo("cached-value");
    }

    // =========================================================================
    // E. Integration Tests
    // =========================================================================

    /// <summary>
    /// Verifies end-to-end flow: compute hash, cache, and retrieve.
    /// </summary>
    [Test]
    public async Task EndToEnd_ComputeCacheRetrieve_WorksCorrectly()
    {
        // Arrange
        var ledger = CreateLedger();
        var input = new TestInput(42, "workflow-step");
        var result = new TestResult("computed-result");

        // Act
        var inputHash = ledger.ComputeInputHash(input);
        await ledger.CacheResultAsync("process-step", inputHash, result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("process-step", inputHash, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("computed-result");
    }

    // =========================================================================
    // F. TTL Boundary Tests (ConcurrentDictionary Backend)
    // =========================================================================

    /// <summary>
    /// Verifies that entry is still valid at exact TTL boundary.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_AtExactTtlBoundary_EntryStillValid()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var ledger = CreateLedger(timeProvider);
        var result = new TestResult("boundary-test");
        var ttl = TimeSpan.FromMinutes(5);

        await ledger.CacheResultAsync("step", "hash", result, ttl, CancellationToken.None).ConfigureAwait(false);

        // Act - Advance time to exactly TTL (not past it)
        timeProvider.Advance(ttl);

        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert - Entry should still be valid at exact boundary
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("boundary-test");
    }

    /// <summary>
    /// Verifies that entry expires just after TTL boundary.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_JustAfterTtlBoundary_EntryExpired()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var ledger = CreateLedger(timeProvider);
        var result = new TestResult("boundary-test");
        var ttl = TimeSpan.FromMinutes(5);

        await ledger.CacheResultAsync("step", "hash", result, ttl, CancellationToken.None).ConfigureAwait(false);

        // Act - Advance time to just past TTL
        timeProvider.Advance(ttl + TimeSpan.FromTicks(1));

        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert - Entry should be expired
        await Assert.That(cached).IsNull();
    }

    /// <summary>
    /// Verifies that entries without TTL never expire.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_WithoutTtl_NeverExpires()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var ledger = CreateLedger(timeProvider);
        var result = new TestResult("persistent-value");

        await ledger.CacheResultAsync("step", "hash", result, null, CancellationToken.None).ConfigureAwait(false);

        // Act - Advance time significantly
        timeProvider.Advance(TimeSpan.FromDays(365));

        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert - Entry should still exist
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("persistent-value");
    }

    /// <summary>
    /// Verifies that expired entry is removed and returns null.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_ExpiredEntry_RemovesAndReturnsNull()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var ledger = CreateLedger(timeProvider);
        var result = new TestResult("will-expire");
        var ttl = TimeSpan.FromMinutes(1);

        await ledger.CacheResultAsync("step", "hash", result, ttl, CancellationToken.None).ConfigureAwait(false);

        // Act - Advance time past TTL and try to get twice
        timeProvider.Advance(TimeSpan.FromMinutes(2));
        var firstAttempt = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);
        var secondAttempt = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert - Both attempts should return null (entry removed on first attempt)
        await Assert.That(firstAttempt).IsNull();
        await Assert.That(secondAttempt).IsNull();
    }

    // =========================================================================
    // G. Concurrent Access Tests (ConcurrentDictionary Backend)
    // =========================================================================

    /// <summary>
    /// Verifies that concurrent writes to the cache all succeed.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_ConcurrentWrites_AllSucceed()
    {
        // Arrange
        var ledger = CreateLedger();
        const int taskCount = 100;

        // Act - Execute concurrent writes
        var tasks = Enumerable.Range(0, taskCount)
            .Select(async i =>
            {
                var result = new TestResult($"value-{i}");
                await ledger.CacheResultAsync($"step-{i}", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
            });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - All entries should be retrievable
        var retrievedCount = 0;
        for (var i = 0; i < taskCount; i++)
        {
            var cached = await ledger.TryGetCachedResultAsync<TestResult>($"step-{i}", "hash", CancellationToken.None).ConfigureAwait(false);
            if (cached != null)
            {
                retrievedCount++;
            }
        }

        await Assert.That(retrievedCount).IsEqualTo(taskCount);
    }

    /// <summary>
    /// Verifies that concurrent reads from the cache all succeed.
    /// </summary>
    [Test]
    public async Task TryGetCachedResultAsync_ConcurrentReads_AllSucceed()
    {
        // Arrange
        var ledger = CreateLedger();
        var result = new TestResult("shared-value");
        await ledger.CacheResultAsync("shared-step", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        const int taskCount = 100;

        // Act - Execute concurrent reads
        var tasks = Enumerable.Range(0, taskCount)
            .Select(async _ =>
            {
                var cached = await ledger.TryGetCachedResultAsync<TestResult>("shared-step", "hash", CancellationToken.None).ConfigureAwait(false);
                return cached?.Value;
            });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - All reads should return the same value
        await Assert.That(results.All(v => v == "shared-value")).IsTrue();
    }

    /// <summary>
    /// Verifies that concurrent reads and writes do not throw exceptions.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_ConcurrentReadsAndWrites_NoExceptions()
    {
        // Arrange
        var ledger = CreateLedger();

        // Pre-populate some entries
        for (var i = 0; i < 50; i++)
        {
            var result = new TestResult($"initial-{i}");
            await ledger.CacheResultAsync($"step-{i}", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        }

        // Act - Execute concurrent reads and writes
        var writeTasks = Enumerable.Range(50, 50)
            .Select(async i =>
            {
                var result = new TestResult($"new-{i}");
                await ledger.CacheResultAsync($"step-{i}", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
            });

        var readTasks = Enumerable.Range(0, 50)
            .Select(async i =>
            {
                await ledger.TryGetCachedResultAsync<TestResult>($"step-{i}", "hash", CancellationToken.None).ConfigureAwait(false);
            });

        // Assert - No exceptions should be thrown
        await Assert.That(async () => await Task.WhenAll(writeTasks.Concat(readTasks)).ConfigureAwait(false))
            .ThrowsNothing();
    }

    /// <summary>
    /// Verifies that concurrent overwrites to the same key work correctly.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_ConcurrentOverwrites_LastWriteWins()
    {
        // Arrange
        var ledger = CreateLedger();
        const int taskCount = 50;

        // Act - Execute concurrent overwrites to the same key
        var tasks = Enumerable.Range(0, taskCount)
            .Select(async i =>
            {
                var result = new TestResult($"value-{i}");
                await ledger.CacheResultAsync("same-step", "same-hash", result, null, CancellationToken.None).ConfigureAwait(false);
            });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - Should have some value (last write wins, but order is non-deterministic)
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("same-step", "same-hash", CancellationToken.None).ConfigureAwait(false);
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).Contains("value-");
    }

    // =========================================================================
    // H. Edge Case Tests
    // =========================================================================

    /// <summary>
    /// Verifies that very long step names are handled correctly.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_VeryLongStepName_WorksCorrectly()
    {
        // Arrange
        var ledger = CreateLedger();
        var longStepName = new string('x', 10000);
        var result = new TestResult("long-key-value");

        // Act
        await ledger.CacheResultAsync(longStepName, "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>(longStepName, "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("long-key-value");
    }

    /// <summary>
    /// Verifies that very long input hashes are handled correctly.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_VeryLongInputHash_WorksCorrectly()
    {
        // Arrange
        var ledger = CreateLedger();
        var longHash = new string('a', 10000);
        var result = new TestResult("long-hash-value");

        // Act
        await ledger.CacheResultAsync("step", longHash, result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", longHash, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("long-hash-value");
    }

    /// <summary>
    /// Verifies that large result values are handled correctly.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_LargeResultValue_WorksCorrectly()
    {
        // Arrange
        var ledger = CreateLedger();
        var largeValue = new string('z', 100000);
        var result = new TestResult(largeValue);

        // Act
        await ledger.CacheResultAsync("step", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value.Length).IsEqualTo(100000);
        await Assert.That(cached.Value).IsEqualTo(largeValue);
    }

    /// <summary>
    /// Verifies that rapid set/get cycles work correctly.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_RapidSetGetCycles_WorksCorrectly()
    {
        // Arrange
        var ledger = CreateLedger();
        const int cycleCount = 100;

        // Act & Assert - Rapid set/get cycles
        for (var i = 0; i < cycleCount; i++)
        {
            var result = new TestResult($"cycle-{i}");
            await ledger.CacheResultAsync("step", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
            var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

            await Assert.That(cached).IsNotNull();
            await Assert.That(cached!.Value).IsEqualTo($"cycle-{i}");
        }
    }

    /// <summary>
    /// Verifies that different input hashes for the same step name are isolated.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_DifferentHashes_SameStepName_AreIsolated()
    {
        // Arrange
        var ledger = CreateLedger();
        var result1 = new TestResult("hash1-value");
        var result2 = new TestResult("hash2-value");

        // Act
        await ledger.CacheResultAsync("step", "hash1", result1, null, CancellationToken.None).ConfigureAwait(false);
        await ledger.CacheResultAsync("step", "hash2", result2, null, CancellationToken.None).ConfigureAwait(false);

        var cached1 = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash1", CancellationToken.None).ConfigureAwait(false);
        var cached2 = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash2", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached1).IsNotNull();
        await Assert.That(cached1!.Value).IsEqualTo("hash1-value");
        await Assert.That(cached2).IsNotNull();
        await Assert.That(cached2!.Value).IsEqualTo("hash2-value");
    }

    /// <summary>
    /// Verifies that special characters in step names are handled correctly.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_SpecialCharactersInStepName_WorksCorrectly()
    {
        // Arrange
        var ledger = CreateLedger();
        var specialStepName = "step:with/special\\chars!@#$%^&*()";
        var result = new TestResult("special-chars-value");

        // Act
        await ledger.CacheResultAsync(specialStepName, "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>(specialStepName, "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("special-chars-value");
    }

    /// <summary>
    /// Verifies that Unicode characters in step names are handled correctly.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_UnicodeInStepName_WorksCorrectly()
    {
        // Arrange
        var ledger = CreateLedger();
        var unicodeStepName = "step-\u4e2d\u6587-\u0420\u0443\u0441\u0441\u043a\u0438\u0439-\u0639\u0631\u0628\u064a";
        var result = new TestResult("unicode-value");

        // Act
        await ledger.CacheResultAsync(unicodeStepName, "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>(unicodeStepName, "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("unicode-value");
    }

    // =========================================================================
    // I. ComputeInputHash Additional Tests
    // =========================================================================

    /// <summary>
    /// Verifies that ComputeInputHash returns lowercase hex string.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_ReturnsLowercaseHex()
    {
        // Arrange
        var ledger = CreateLedger();
        var input = new TestInput(1, "test");

        // Act
        var hash = ledger.ComputeInputHash(input);

        // Assert - SHA256 produces 64 hex characters, all lowercase
        await Assert.That(hash.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f'))).IsTrue();
    }

    /// <summary>
    /// Verifies that equivalent inputs produce identical hashes.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_EquivalentInputs_ProduceSameHash()
    {
        // Arrange
        var ledger = CreateLedger();
        var input1 = new TestInput(42, "test");
        var input2 = new TestInput(42, "test");

        // Act
        var hash1 = ledger.ComputeInputHash(input1);
        var hash2 = ledger.ComputeInputHash(input2);

        // Assert
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    /// <summary>
    /// Verifies that inputs with different IDs produce different hashes.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_DifferentIds_ProduceDifferentHashes()
    {
        // Arrange
        var ledger = CreateLedger();
        var input1 = new TestInput(1, "test");
        var input2 = new TestInput(2, "test");

        // Act
        var hash1 = ledger.ComputeInputHash(input1);
        var hash2 = ledger.ComputeInputHash(input2);

        // Assert
        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    /// <summary>
    /// Verifies that inputs with different names produce different hashes.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_DifferentNames_ProduceDifferentHashes()
    {
        // Arrange
        var ledger = CreateLedger();
        var input1 = new TestInput(1, "test1");
        var input2 = new TestInput(1, "test2");

        // Act
        var hash1 = ledger.ComputeInputHash(input1);
        var hash2 = ledger.ComputeInputHash(input2);

        // Assert
        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    // =========================================================================
    // J. TTL Edge Cases
    // =========================================================================

    /// <summary>
    /// Verifies that very short TTL works correctly.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_VeryShortTtl_ExpiresQuickly()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var ledger = CreateLedger(timeProvider);
        var result = new TestResult("short-lived");
        var ttl = TimeSpan.FromTicks(1);

        // Act
        await ledger.CacheResultAsync("step", "hash", result, ttl, CancellationToken.None).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromTicks(2));
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNull();
    }

    /// <summary>
    /// Verifies that very long TTL does not expire prematurely.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_VeryLongTtl_DoesNotExpirePrematurely()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var ledger = CreateLedger(timeProvider);
        var result = new TestResult("long-lived");
        var ttl = TimeSpan.FromDays(365);

        // Act
        await ledger.CacheResultAsync("step", "hash", result, ttl, CancellationToken.None).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromDays(364));
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("long-lived");
    }

    /// <summary>
    /// Verifies that updating TTL on existing entry works correctly.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_UpdateTtl_ExtendsExpiration()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var ledger = CreateLedger(timeProvider);
        var result = new TestResult("will-be-extended");
        var shortTtl = TimeSpan.FromMinutes(1);
        var longTtl = TimeSpan.FromMinutes(10);

        // Act - Cache with short TTL, then update with longer TTL
        await ledger.CacheResultAsync("step", "hash", result, shortTtl, CancellationToken.None).ConfigureAwait(false);
        await ledger.CacheResultAsync("step", "hash", result, longTtl, CancellationToken.None).ConfigureAwait(false);

        // Advance past original TTL but before new TTL
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert - Entry should still be valid due to extended TTL
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("will-be-extended");
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================

    private static InMemoryStepExecutionLedger CreateLedger(TimeProvider? timeProvider = null)
    {
        return new InMemoryStepExecutionLedger(timeProvider ?? TimeProvider.System, NullLogger<InMemoryStepExecutionLedger>.Instance);
    }

    // =========================================================================
    // Test Fixtures
    // =========================================================================

    /// <summary>
    /// Test result for unit tests.
    /// </summary>
    [MemoryPackable]
    private sealed partial record TestResult(string Value);

    /// <summary>
    /// Test input for hash computation tests.
    /// </summary>
    [MemoryPackable]
    private sealed partial record TestInput(int Id, string Name);
}

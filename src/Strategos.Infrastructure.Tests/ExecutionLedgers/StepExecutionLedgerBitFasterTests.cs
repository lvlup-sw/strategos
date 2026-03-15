// =============================================================================
// <copyright file="StepExecutionLedgerBitFasterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Infrastructure.ExecutionLedgers;

using MemoryPack;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Strategos.Infrastructure.Tests.ExecutionLedgers;

/// <summary>
/// Tests for BitFaster ConcurrentLru cache support in <see cref="InMemoryStepExecutionLedger"/>.
/// </summary>
/// <remarks>
/// Tests verify that the ledger can optionally use BitFaster.Caching's ConcurrentLru
/// for high-performance caching scenarios with bounded capacity.
/// </remarks>
[Property("Category", "Unit")]
public sealed partial class StepExecutionLedgerBitFasterTests
{
    // =========================================================================
    // A. BitFaster Configuration Tests
    // =========================================================================

    /// <summary>
    /// Verifies that StepExecutionLedgerOptions can enable BitFaster cache.
    /// </summary>
    [Test]
    public async Task StepExecutionLedgerOptions_BitFasterEnabled_UsesConcurrentLru()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 100
        };
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);
        var result = new TestResult { Value = "cached-value" };

        // Act
        await ledger.CacheResultAsync("step", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("cached-value");
    }

    /// <summary>
    /// Verifies that default options use ConcurrentDictionary for backwards compatibility.
    /// </summary>
    [Test]
    public async Task StepExecutionLedgerOptions_DefaultSettings_UsesConcurrentDictionary()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Assert defaults
        await Assert.That(options.UseBitFasterCache).IsFalse();
        await Assert.That(options.CacheCapacity).IsEqualTo(10000);
    }

    /// <summary>
    /// Verifies that CacheCapacity rejects zero value.
    /// </summary>
    [Test]
    public async Task StepExecutionLedgerOptions_CacheCapacityZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Act & Assert
        await Assert.That(() => options.CacheCapacity = 0).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that CacheCapacity rejects negative values.
    /// </summary>
    [Test]
    public async Task StepExecutionLedgerOptions_CacheCapacityNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Act & Assert
        await Assert.That(() => options.CacheCapacity = -1).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that CacheCapacity accepts positive values.
    /// </summary>
    [Test]
    public async Task StepExecutionLedgerOptions_CacheCapacityPositive_SetsValue()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Act
        options.CacheCapacity = 500;

        // Assert
        await Assert.That(options.CacheCapacity).IsEqualTo(500);
    }

    /// <summary>
    /// Verifies that ledger without options constructor uses default ConcurrentDictionary.
    /// </summary>
    [Test]
    public async Task InMemoryStepExecutionLedger_WithoutOptions_UsesConcurrentDictionary()
    {
        // Arrange - use existing constructor without options
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, NullLogger<InMemoryStepExecutionLedger>.Instance);
        var result = new TestResult { Value = "cached-value" };

        // Act
        await ledger.CacheResultAsync("step", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert - should work with default dictionary implementation
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("cached-value");
    }

    // =========================================================================
    // B. BitFaster Cache Behavior Tests
    // =========================================================================

    /// <summary>
    /// Verifies that BitFaster cache evicts entries when capacity is exceeded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BitFaster's ConcurrentLru uses a pseudo-LRU eviction policy with a 3-queue system
    /// (hot, warm, cold) rather than strict LRU. Key behavioral differences:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Cache hits do NOT update queue position; only writes do.</description></item>
    /// <item><description>Items cycle through queues: new items enter cold, get promoted on subsequent writes.</description></item>
    /// <item><description>Eviction occurs from the cold queue when capacity is exceeded.</description></item>
    /// </list>
    /// <para>
    /// This test verifies that with capacity=3 and 4 sequential writes, the first entry
    /// (step-1) gets evicted. This works because each entry is written only once and thus
    /// remains in the cold queue, making the oldest write the eviction candidate.
    /// </para>
    /// </remarks>
    [Test]
    public async Task BitFasterCache_WhenCapacityExceeded_EvictsOldestEntries()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 3
        };
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);

        // Act - Add 4 entries (exceeding capacity of 3)
        for (var i = 1; i <= 4; i++)
        {
            var result = new TestResult { Value = $"value-{i}" };
            await ledger.CacheResultAsync($"step-{i}", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        }

        // Assert - First entry should be evicted, last 3 should be present
        var first = await ledger.TryGetCachedResultAsync<TestResult>("step-1", "hash", CancellationToken.None).ConfigureAwait(false);
        var last = await ledger.TryGetCachedResultAsync<TestResult>("step-4", "hash", CancellationToken.None).ConfigureAwait(false);

        await Assert.That(first).IsNull();
        await Assert.That(last).IsNotNull();
        await Assert.That(last!.Value).IsEqualTo("value-4");
    }

    /// <summary>
    /// Verifies that BitFaster cache supports TTL expiration.
    /// </summary>
    [Test]
    public async Task BitFasterCache_WithTtl_ExpiresEntries()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 100
        };
        var ledger = new InMemoryStepExecutionLedger(timeProvider, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);
        var result = new TestResult { Value = "test-value" };
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        await ledger.CacheResultAsync("step", "hash", result, ttl, CancellationToken.None).ConfigureAwait(false);

        // Advance time past TTL
        timeProvider.Advance(TimeSpan.FromMinutes(6));

        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNull();
    }

    /// <summary>
    /// Verifies that BitFaster cache supports overwriting entries.
    /// </summary>
    [Test]
    public async Task BitFasterCache_OverwriteEntry_ReturnsNewValue()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 100
        };
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);
        var result1 = new TestResult { Value = "first" };
        var result2 = new TestResult { Value = "second" };

        // Act
        await ledger.CacheResultAsync("step", "hash", result1, null, CancellationToken.None).ConfigureAwait(false);
        await ledger.CacheResultAsync("step", "hash", result2, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("second");
    }

    // =========================================================================
    // C. Integration Tests
    // =========================================================================

    /// <summary>
    /// Verifies end-to-end flow with BitFaster cache: compute hash, cache, and retrieve.
    /// </summary>
    [Test]
    public async Task BitFasterCache_EndToEnd_ComputeCacheRetrieve_WorksCorrectly()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 100
        };
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);
        var input = new TestInput { Id = 42, Name = "workflow-step" };
        var result = new TestResult { Value = "computed-result" };

        // Act
        var inputHash = ledger.ComputeInputHash(input);
        await ledger.CacheResultAsync("process-step", inputHash, result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("process-step", inputHash, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("computed-result");
    }

    // =========================================================================
    // D. TTL Boundary Tests
    // =========================================================================

    /// <summary>
    /// Verifies that entry is still valid at exact TTL boundary.
    /// </summary>
    [Test]
    public async Task BitFasterCache_AtExactTtlBoundary_EntryStillValid()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 100
        };
        var ledger = new InMemoryStepExecutionLedger(timeProvider, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);
        var result = new TestResult { Value = "boundary-test" };
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        await ledger.CacheResultAsync("step", "hash", result, ttl, CancellationToken.None).ConfigureAwait(false);

        // Advance time to exactly TTL (not past it)
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
    public async Task BitFasterCache_JustAfterTtlBoundary_EntryExpired()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 100
        };
        var ledger = new InMemoryStepExecutionLedger(timeProvider, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);
        var result = new TestResult { Value = "boundary-test" };
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        await ledger.CacheResultAsync("step", "hash", result, ttl, CancellationToken.None).ConfigureAwait(false);

        // Advance time to just past TTL
        timeProvider.Advance(ttl + TimeSpan.FromTicks(1));

        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert - Entry should be expired
        await Assert.That(cached).IsNull();
    }

    /// <summary>
    /// Verifies that entries without TTL never expire.
    /// </summary>
    [Test]
    public async Task BitFasterCache_WithoutTtl_NeverExpires()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 100
        };
        var ledger = new InMemoryStepExecutionLedger(timeProvider, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);
        var result = new TestResult { Value = "persistent-value" };

        // Act
        await ledger.CacheResultAsync("step", "hash", result, null, CancellationToken.None).ConfigureAwait(false);

        // Advance time significantly
        timeProvider.Advance(TimeSpan.FromDays(365));

        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert - Entry should still exist
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("persistent-value");
    }

    // =========================================================================
    // E. Capacity Edge Cases
    // =========================================================================

    /// <summary>
    /// Verifies that BitFaster cache works with minimum capacity of 3.
    /// </summary>
    /// <remarks>
    /// BitFaster's ConcurrentLru requires a minimum capacity of 3 due to its
    /// 3-queue architecture (hot, warm, cold).
    /// </remarks>
    [Test]
    public async Task BitFasterCache_MinimumCapacity_WorksCorrectly()
    {
        // Arrange - BitFaster minimum capacity is 3
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 3
        };
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);
        var result1 = new TestResult { Value = "first" };
        var result2 = new TestResult { Value = "second" };
        var result3 = new TestResult { Value = "third" };
        var result4 = new TestResult { Value = "fourth" };

        // Act - Add 4 entries (exceeding capacity of 3)
        await ledger.CacheResultAsync("step-1", "hash", result1, null, CancellationToken.None).ConfigureAwait(false);
        await ledger.CacheResultAsync("step-2", "hash", result2, null, CancellationToken.None).ConfigureAwait(false);
        await ledger.CacheResultAsync("step-3", "hash", result3, null, CancellationToken.None).ConfigureAwait(false);
        await ledger.CacheResultAsync("step-4", "hash", result4, null, CancellationToken.None).ConfigureAwait(false);

        // Assert - First entry should be evicted, last 3 should exist
        var first = await ledger.TryGetCachedResultAsync<TestResult>("step-1", "hash", CancellationToken.None).ConfigureAwait(false);
        var fourth = await ledger.TryGetCachedResultAsync<TestResult>("step-4", "hash", CancellationToken.None).ConfigureAwait(false);

        await Assert.That(first).IsNull();
        await Assert.That(fourth).IsNotNull();
        await Assert.That(fourth!.Value).IsEqualTo("fourth");
    }

    /// <summary>
    /// Verifies that BitFaster cache with capacity less than 3 throws exception.
    /// </summary>
    /// <remarks>
    /// BitFaster's ConcurrentLru requires a minimum capacity of 3 due to its
    /// 3-queue architecture (hot, warm, cold).
    /// </remarks>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task BitFasterCache_CapacityLessThanThree_ThrowsException(int capacity)
    {
        // Arrange
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = capacity
        };

        // Act & Assert - BitFaster throws when capacity < 3
        await Assert.That(() => new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that BitFaster cache respects exact capacity limit.
    /// </summary>
    [Test]
    public async Task BitFasterCache_ExactCapacity_AllEntriesPresent()
    {
        // Arrange - Use capacity of 5 (above minimum of 3)
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 5,
        };
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);

        // Act - Add exactly 5 entries (matching capacity)
        for (var i = 1; i <= 5; i++)
        {
            var result = new TestResult { Value = $"value-{i}" };
            await ledger.CacheResultAsync($"step-{i}", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        }

        // Assert - All 5 entries should be present
        for (var i = 1; i <= 5; i++)
        {
            var cached = await ledger.TryGetCachedResultAsync<TestResult>($"step-{i}", "hash", CancellationToken.None).ConfigureAwait(false);
            await Assert.That(cached).IsNotNull();
            await Assert.That(cached!.Value).IsEqualTo($"value-{i}");
        }
    }

    // =========================================================================
    // F. Concurrent Access Tests
    // =========================================================================

    /// <summary>
    /// Verifies that BitFaster cache handles concurrent writes safely.
    /// </summary>
    [Test]
    public async Task BitFasterCache_ConcurrentWrites_AllSucceed()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 1000,
        };
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);
        const int taskCount = 100;

        // Act - Execute concurrent writes
        var tasks = Enumerable.Range(0, taskCount)
            .Select(async i =>
            {
                var result = new TestResult { Value = $"value-{i}" };
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
    /// Verifies that BitFaster cache handles concurrent reads and writes safely.
    /// </summary>
    [Test]
    public async Task BitFasterCache_ConcurrentReadsAndWrites_NoExceptions()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 100,
        };
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create(options), NullLogger<InMemoryStepExecutionLedger>.Instance);

        // Pre-populate some entries
        for (var i = 0; i < 50; i++)
        {
            var result = new TestResult { Value = $"initial-{i}" };
            await ledger.CacheResultAsync($"step-{i}", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        }

        // Act - Execute concurrent reads and writes
        var writeTasks = Enumerable.Range(50, 50)
            .Select(async i =>
            {
                var result = new TestResult { Value = $"new-{i}" };
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

    // =========================================================================
    // G. Constructor and Null Options Tests
    // =========================================================================

    /// <summary>
    /// Verifies that ledger with null options value uses dictionary cache.
    /// </summary>
    [Test]
    public async Task InMemoryStepExecutionLedger_WithNullOptionsValue_UsesDictionary()
    {
        // Arrange - Pass IOptions with null Value
        var ledger = new InMemoryStepExecutionLedger(TimeProvider.System, Options.Create<StepExecutionLedgerOptions>(null!), NullLogger<InMemoryStepExecutionLedger>.Instance);
        var result = new TestResult { Value = "test-value" };

        // Act
        await ledger.CacheResultAsync("step", "hash", result, null, CancellationToken.None).ConfigureAwait(false);
        var cached = await ledger.TryGetCachedResultAsync<TestResult>("step", "hash", CancellationToken.None).ConfigureAwait(false);

        // Assert - Should work with default dictionary implementation
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Value).IsEqualTo("test-value");
    }

    // =========================================================================
    // Test Fixtures
    // =========================================================================

    /// <summary>
    /// Test result for unit tests.
    /// </summary>
    [MemoryPackable]
    private sealed partial class TestResult
    {
        /// <summary>
        /// Gets or initializes the value.
        /// </summary>
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>
    /// Test input for hash computation tests.
    /// </summary>
    [MemoryPackable]
    private sealed partial class TestInput
    {
        /// <summary>
        /// Gets or initializes the ID.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Gets or initializes the name.
        /// </summary>
        public string Name { get; init; } = string.Empty;
    }
}

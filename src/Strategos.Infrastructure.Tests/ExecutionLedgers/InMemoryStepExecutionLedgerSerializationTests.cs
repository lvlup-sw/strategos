// =============================================================================
// <copyright file="InMemoryStepExecutionLedgerSerializationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Infrastructure.ExecutionLedgers;

using MemoryPack;

using Microsoft.Extensions.Logging.Abstractions;

namespace Strategos.Infrastructure.Tests.ExecutionLedgers;

/// <summary>
/// Tests for MemoryPack serialization in <see cref="InMemoryStepExecutionLedger"/>.
/// </summary>
/// <remarks>
/// These tests verify that the ledger correctly serializes and deserializes
/// results using MemoryPack for improved performance over JSON serialization.
/// </remarks>
[Property("Category", "Unit")]
public sealed partial class InMemoryStepExecutionLedgerSerializationTests
{
    // =========================================================================
    // A. MemoryPack Serialization Tests
    // =========================================================================

    /// <summary>
    /// Verifies that CacheResultAsync correctly serializes and deserializes using MemoryPack.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_MemoryPack_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var timeProvider = TimeProvider.System;
        var ledger = new InMemoryStepExecutionLedger(timeProvider, NullLogger<InMemoryStepExecutionLedger>.Instance);
        var stepName = "TestStep";
        var input = new MemoryPackableInput("test", 123);
        var inputHash = ledger.ComputeInputHash(input);
        var result = new MemoryPackableCacheResult("TestValue", 42);

        // Act
        await ledger.CacheResultAsync(stepName, inputHash, result, TimeSpan.FromMinutes(5), CancellationToken.None);
        var retrieved = await ledger.TryGetCachedResultAsync<MemoryPackableCacheResult>(stepName, inputHash, CancellationToken.None);

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Value).IsEqualTo("TestValue");
        await Assert.That(retrieved.Count).IsEqualTo(42);
    }

    /// <summary>
    /// Verifies that ComputeInputHash produces consistent hashes for the same input.
    /// </summary>
    [Test]
    public async Task ComputeInputHash_SameInput_ProducesConsistentHash()
    {
        // Arrange
        var timeProvider = TimeProvider.System;
        var ledger = new InMemoryStepExecutionLedger(timeProvider, NullLogger<InMemoryStepExecutionLedger>.Instance);
        var input = new MemoryPackableInput("test", 123);

        // Act
        var hash1 = ledger.ComputeInputHash(input);
        var hash2 = ledger.ComputeInputHash(input);

        // Assert
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    /// <summary>
    /// Verifies that complex nested objects serialize correctly with MemoryPack.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_ComplexObject_SerializesCorrectly()
    {
        // Arrange
        var timeProvider = TimeProvider.System;
        var ledger = new InMemoryStepExecutionLedger(timeProvider, NullLogger<InMemoryStepExecutionLedger>.Instance);
        var stepName = "ComplexStep";
        var result = new MemoryPackableComplexResult(
            "outer",
            new MemoryPackableNestedData("inner", [1, 2, 3]),
            ["a", "b", "c"]);
        var inputHash = "test-hash-123";

        // Act
        await ledger.CacheResultAsync(stepName, inputHash, result, null, CancellationToken.None);
        var retrieved = await ledger.TryGetCachedResultAsync<MemoryPackableComplexResult>(stepName, inputHash, CancellationToken.None);

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Name).IsEqualTo("outer");
        await Assert.That(retrieved.Nested).IsNotNull();
        await Assert.That(retrieved.Nested!.Label).IsEqualTo("inner");
        await Assert.That(retrieved.Nested.Values).IsEquivalentTo([1, 2, 3]);
        await Assert.That(retrieved.Tags).IsEquivalentTo(["a", "b", "c"]);
    }

    /// <summary>
    /// Verifies that null nested properties serialize correctly with MemoryPack.
    /// </summary>
    [Test]
    public async Task CacheResultAsync_NullNestedProperty_SerializesCorrectly()
    {
        // Arrange
        var timeProvider = TimeProvider.System;
        var ledger = new InMemoryStepExecutionLedger(timeProvider, NullLogger<InMemoryStepExecutionLedger>.Instance);
        var stepName = "NullNestedStep";
        var result = new MemoryPackableComplexResult("outer", null, []);
        var inputHash = "test-hash-null";

        // Act
        await ledger.CacheResultAsync(stepName, inputHash, result, null, CancellationToken.None);
        var retrieved = await ledger.TryGetCachedResultAsync<MemoryPackableComplexResult>(stepName, inputHash, CancellationToken.None);

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Name).IsEqualTo("outer");
        await Assert.That(retrieved.Nested).IsNull();
        await Assert.That(retrieved.Tags).IsEmpty();
    }

    // =========================================================================
    // Test Fixtures - MemoryPackable Types
    // =========================================================================

    /// <summary>
    /// Test input type with MemoryPack serialization support.
    /// </summary>
    [MemoryPackable]
    public partial record MemoryPackableInput(string Key, int Value);

    /// <summary>
    /// Test result type with MemoryPack serialization support.
    /// </summary>
    [MemoryPackable]
    public partial record MemoryPackableCacheResult(string Value, int Count);

    /// <summary>
    /// Nested data type for complex object tests.
    /// </summary>
    [MemoryPackable]
    public partial record MemoryPackableNestedData(string Label, int[] Values);

    /// <summary>
    /// Complex result type with nested objects for serialization tests.
    /// </summary>
    [MemoryPackable]
    public partial record MemoryPackableComplexResult(
        string Name,
        MemoryPackableNestedData? Nested,
        string[] Tags);
}

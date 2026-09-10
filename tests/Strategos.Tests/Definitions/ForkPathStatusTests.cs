// =============================================================================
// <copyright file="ForkPathStatusTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="ForkPathStatus"/> enum.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Enum contains expected values</description></item>
///   <item><description>Values are correctly defined</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class ForkPathStatusTests
{
    // =============================================================================
    // A. Enum Value Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ForkPathStatus.Pending is defined.
    /// </summary>
    [Test]
    public async Task ForkPathStatus_Pending_IsDefined()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(ForkPathStatus), ForkPathStatus.Pending)).IsTrue();
    }

    /// <summary>
    /// Verifies that ForkPathStatus.InProgress is defined.
    /// </summary>
    [Test]
    public async Task ForkPathStatus_InProgress_IsDefined()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(ForkPathStatus), ForkPathStatus.InProgress)).IsTrue();
    }

    /// <summary>
    /// Verifies that ForkPathStatus.Success is defined.
    /// </summary>
    [Test]
    public async Task ForkPathStatus_Success_IsDefined()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(ForkPathStatus), ForkPathStatus.Success)).IsTrue();
    }

    /// <summary>
    /// Verifies that ForkPathStatus.Failed is defined.
    /// </summary>
    [Test]
    public async Task ForkPathStatus_Failed_IsDefined()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(ForkPathStatus), ForkPathStatus.Failed)).IsTrue();
    }

    /// <summary>
    /// Verifies that ForkPathStatus.FailedWithRecovery is defined.
    /// </summary>
    [Test]
    public async Task ForkPathStatus_FailedWithRecovery_IsDefined()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(ForkPathStatus), ForkPathStatus.FailedWithRecovery)).IsTrue();
    }

    /// <summary>
    /// Verifies that ForkPathStatus has exactly 5 values.
    /// </summary>
    [Test]
    public async Task ForkPathStatus_HasExpectedValueCount()
    {
        // Arrange
        var values = Enum.GetValues<ForkPathStatus>();

        // Assert
        await Assert.That(values).HasCount().EqualTo(5);
    }
}

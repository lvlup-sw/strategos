// -----------------------------------------------------------------------
// <copyright file="NamingHelperTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for the <see cref="NamingHelper"/> class.
/// </summary>
[Property("Category", "Unit")]
public class NamingHelperTests
{
    // =============================================================================
    // A. GetStartCommandName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetStartCommandName returns correct format.
    /// </summary>
    [Test]
    public async Task GetStartCommandName_ReturnsCorrectFormat()
    {
        // Act
        var result = NamingHelper.GetStartCommandName("ProcessOrder");

        // Assert
        await Assert.That(result).IsEqualTo("StartProcessOrderCommand");
    }

    // =============================================================================
    // B. GetStartStepCommandName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetStartStepCommandName returns correct format.
    /// </summary>
    [Test]
    public async Task GetStartStepCommandName_ReturnsCorrectFormat()
    {
        // Act
        var result = NamingHelper.GetStartStepCommandName("ValidateOrder");

        // Assert
        await Assert.That(result).IsEqualTo("StartValidateOrderCommand");
    }

    // =============================================================================
    // C. GetExecuteCommandName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetExecuteCommandName returns correct format.
    /// </summary>
    [Test]
    public async Task GetExecuteCommandName_ReturnsCorrectFormat()
    {
        // Act
        var result = NamingHelper.GetExecuteCommandName("ValidateOrder");

        // Assert
        await Assert.That(result).IsEqualTo("ExecuteValidateOrderCommand");
    }

    // =============================================================================
    // D. GetWorkerCommandName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetWorkerCommandName returns correct format.
    /// </summary>
    [Test]
    public async Task GetWorkerCommandName_ReturnsCorrectFormat()
    {
        // Act
        var result = NamingHelper.GetWorkerCommandName("ValidateOrder");

        // Assert
        await Assert.That(result).IsEqualTo("ExecuteValidateOrderWorkerCommand");
    }

    // =============================================================================
    // E. GetCompletedEventName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetCompletedEventName returns correct format.
    /// </summary>
    [Test]
    public async Task GetCompletedEventName_ReturnsCorrectFormat()
    {
        // Act
        var result = NamingHelper.GetCompletedEventName("ValidateOrder");

        // Assert
        await Assert.That(result).IsEqualTo("ValidateOrderCompleted");
    }

    // =============================================================================
    // F. GetSagaClassName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetSagaClassName with version 1 returns unversioned name.
    /// </summary>
    [Test]
    public async Task GetSagaClassName_Version1_ReturnsUnversionedName()
    {
        // Act
        var result = NamingHelper.GetSagaClassName("ProcessOrder", 1);

        // Assert
        await Assert.That(result).IsEqualTo("ProcessOrderSaga");
    }

    /// <summary>
    /// Verifies that GetSagaClassName with version > 1 returns versioned name.
    /// </summary>
    [Test]
    public async Task GetSagaClassName_Version2_ReturnsVersionedName()
    {
        // Act
        var result = NamingHelper.GetSagaClassName("ProcessOrder", 2);

        // Assert
        await Assert.That(result).IsEqualTo("ProcessOrderSagaV2");
    }

    /// <summary>
    /// Verifies that GetSagaClassName with high version returns versioned name.
    /// </summary>
    [Test]
    public async Task GetSagaClassName_HighVersion_ReturnsVersionedName()
    {
        // Act
        var result = NamingHelper.GetSagaClassName("ProcessOrder", 10);

        // Assert
        await Assert.That(result).IsEqualTo("ProcessOrderSagaV10");
    }

    // =============================================================================
    // G. GetReducerTypeName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetReducerTypeName returns correct format.
    /// </summary>
    [Test]
    public async Task GetReducerTypeName_ReturnsCorrectFormat()
    {
        // Act
        var result = NamingHelper.GetReducerTypeName("OrderState");

        // Assert
        await Assert.That(result).IsEqualTo("OrderStateReducer");
    }

    // =============================================================================
    // G2. GetSimpleTypeName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetSimpleTypeName strips the namespace from a fully qualified
    /// (non-generic) type name.
    /// </summary>
    [Test]
    public async Task GetSimpleTypeName_FullyQualified_ReturnsSimpleName()
    {
        // Act
        var result = NamingHelper.GetSimpleTypeName("MyApp.Steps.RollbackStep");

        // Assert
        await Assert.That(result).IsEqualTo("RollbackStep");
    }

    /// <summary>
    /// Verifies that GetSimpleTypeName returns an unqualified name unchanged.
    /// </summary>
    [Test]
    public async Task GetSimpleTypeName_Unqualified_ReturnsInput()
    {
        // Act
        var result = NamingHelper.GetSimpleTypeName("RollbackStep");

        // Assert
        await Assert.That(result).IsEqualTo("RollbackStep");
    }

    /// <summary>
    /// Verifies that GetSimpleTypeName returns a VALID identifier for a fully
    /// qualified GENERIC type whose type argument is itself qualified. The old
    /// implementation split on the LAST '.', which fell inside the type argument
    /// (<c>Ns.Foo&lt;Ns.Bar&gt;</c>) and yielded <c>Bar&gt;</c> — an invalid
    /// identifier with a trailing '&gt;'.
    /// </summary>
    [Test]
    public async Task GetSimpleTypeName_QualifiedGenericWithQualifiedArg_ReturnsValidOuterName()
    {
        // Act
        var result = NamingHelper.GetSimpleTypeName("Ns.Foo<Ns.Bar>");

        // Assert
        await Assert.That(result).IsEqualTo("Foo");
        await Assert.That(result).DoesNotContain(">");
        await Assert.That(result).DoesNotContain("<");
        await Assert.That(result).DoesNotContain(".");
    }

    /// <summary>
    /// Verifies that GetSimpleTypeName strips the generic-argument suffix from a
    /// fully qualified generic with an unqualified type argument.
    /// </summary>
    [Test]
    public async Task GetSimpleTypeName_QualifiedGenericWithSimpleArg_ReturnsOuterName()
    {
        // Act
        var result = NamingHelper.GetSimpleTypeName("MyApp.Steps.Wrapper<Payload>");

        // Assert
        await Assert.That(result).IsEqualTo("Wrapper");
    }

    /// <summary>
    /// Verifies that GetSimpleTypeName strips the generic-argument suffix from an
    /// unqualified generic type name.
    /// </summary>
    [Test]
    public async Task GetSimpleTypeName_UnqualifiedGeneric_ReturnsOuterName()
    {
        // Act
        var result = NamingHelper.GetSimpleTypeName("Wrapper<Payload>");

        // Assert
        await Assert.That(result).IsEqualTo("Wrapper");
    }

    // =============================================================================
    // H. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetStartCommandName throws when input is null.
    /// </summary>
    [Test]
    public async Task GetStartCommandName_NullInput_ThrowsArgumentNullException()
    {
        await Assert.That(() => NamingHelper.GetStartCommandName(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetStartStepCommandName throws when input is null.
    /// </summary>
    [Test]
    public async Task GetStartStepCommandName_NullInput_ThrowsArgumentNullException()
    {
        await Assert.That(() => NamingHelper.GetStartStepCommandName(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetExecuteCommandName throws when input is null.
    /// </summary>
    [Test]
    public async Task GetExecuteCommandName_NullInput_ThrowsArgumentNullException()
    {
        await Assert.That(() => NamingHelper.GetExecuteCommandName(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetWorkerCommandName throws when input is null.
    /// </summary>
    [Test]
    public async Task GetWorkerCommandName_NullInput_ThrowsArgumentNullException()
    {
        await Assert.That(() => NamingHelper.GetWorkerCommandName(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetCompletedEventName throws when input is null.
    /// </summary>
    [Test]
    public async Task GetCompletedEventName_NullInput_ThrowsArgumentNullException()
    {
        await Assert.That(() => NamingHelper.GetCompletedEventName(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetSagaClassName throws when pascalName is null.
    /// </summary>
    [Test]
    public async Task GetSagaClassName_NullPascalName_ThrowsArgumentNullException()
    {
        await Assert.That(() => NamingHelper.GetSagaClassName(null!, 1))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetReducerTypeName throws when input is null.
    /// </summary>
    [Test]
    public async Task GetReducerTypeName_NullInput_ThrowsArgumentNullException()
    {
        await Assert.That(() => NamingHelper.GetReducerTypeName(null!))
            .Throws<ArgumentNullException>();
    }
}

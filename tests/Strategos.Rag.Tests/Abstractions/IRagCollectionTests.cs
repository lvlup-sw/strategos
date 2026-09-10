// =============================================================================
// <copyright file="IRagCollectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Rag.Tests.Abstractions;

/// <summary>
/// Unit tests for the <see cref="IRagCollection"/> marker interface.
/// </summary>
[Property("Category", "Unit")]
public class IRagCollectionTests
{
    [Test]
    public async Task IRagCollection_IsMarkerInterface_HasNoMembers()
    {
        // Arrange
        var interfaceType = typeof(IRagCollection);

        // Act
        var methods = interfaceType.GetMethods();
        var properties = interfaceType.GetProperties();
        var events = interfaceType.GetEvents();

        // Assert - marker interface should have no members
        await Assert.That(interfaceType.IsInterface).IsTrue();
        await Assert.That(methods.Length).IsEqualTo(0);
        await Assert.That(properties.Length).IsEqualTo(0);
        await Assert.That(events.Length).IsEqualTo(0);
    }

    [Test]
    public async Task IRagCollection_CanBeImplemented_ByClass()
    {
        // Arrange & Act
        var instance = new TestRagCollection();

        // Assert
        await Assert.That(instance).IsAssignableTo<IRagCollection>();
    }

    /// <summary>
    /// Test implementation of IRagCollection for testing purposes.
    /// </summary>
    private sealed class TestRagCollection : IRagCollection
    {
    }
}

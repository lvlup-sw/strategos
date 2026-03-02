// =============================================================================
// <copyright file="DeprecationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;

using Strategos.Agents.Configuration;
using Strategos.Rag.Adapters;

namespace Strategos.Rag.Tests;

/// <summary>
/// Reflection tests verifying that deprecated RAG types carry the <see cref="ObsoleteAttribute"/>.
/// </summary>
[Property("Category", "Unit")]
public class DeprecationTests
{
    [Test]
    public async Task IRagCollection_HasObsoleteAttribute()
    {
        // Arrange
        var type = typeof(IRagCollection);

        // Act
        var attribute = type.GetCustomAttribute<ObsoleteAttribute>();

        // Assert
        await Assert.That(attribute).IsNotNull();
        await Assert.That(attribute!.IsError).IsFalse();
    }

    [Test]
    public async Task IVectorSearchAdapterGeneric_HasObsoleteAttribute()
    {
        // Arrange
        var type = typeof(IVectorSearchAdapter<>);

        // Act
        var attribute = type.GetCustomAttribute<ObsoleteAttribute>();

        // Assert
        await Assert.That(attribute).IsNotNull();
        await Assert.That(attribute!.IsError).IsFalse();
    }

    [Test]
    public async Task InMemoryVectorSearchAdapter_HasObsoleteAttribute()
    {
        // Arrange
        var type = typeof(InMemoryVectorSearchAdapter);

        // Act
        var attribute = type.GetCustomAttribute<ObsoleteAttribute>();

        // Assert
        await Assert.That(attribute).IsNotNull();
        await Assert.That(attribute!.IsError).IsFalse();
    }

    [Test]
    public async Task RagServiceExtensions_HasObsoleteAttribute()
    {
        // Arrange
        var type = typeof(RagServiceExtensions);

        // Act
        var attribute = type.GetCustomAttribute<ObsoleteAttribute>();

        // Assert
        await Assert.That(attribute).IsNotNull();
        await Assert.That(attribute!.IsError).IsFalse();
    }

    [Test]
    public async Task RagConfiguration_HasObsoleteAttribute()
    {
        // Arrange
        var type = typeof(RagConfiguration);

        // Act
        var attribute = type.GetCustomAttribute<ObsoleteAttribute>();

        // Assert
        await Assert.That(attribute).IsNotNull();
        await Assert.That(attribute!.IsError).IsFalse();
    }
}

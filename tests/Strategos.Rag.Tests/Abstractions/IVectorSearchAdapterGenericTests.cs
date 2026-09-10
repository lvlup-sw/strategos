// =============================================================================
// <copyright file="IVectorSearchAdapterGenericTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;

namespace Strategos.Rag.Tests.Abstractions;

/// <summary>
/// Unit tests for the <see cref="IVectorSearchAdapter{TCollection}"/> generic interface.
/// </summary>
[Property("Category", "Unit")]
public class IVectorSearchAdapterGenericTests
{
    [Test]
    public async Task IVectorSearchAdapter_Generic_HasSearchAsyncMethod()
    {
        // Arrange
        var interfaceType = typeof(IVectorSearchAdapter<>);

        // Act
        var searchAsyncMethod = interfaceType.GetMethod("SearchAsync");

        // Assert
        await Assert.That(searchAsyncMethod).IsNotNull();
        await Assert.That(searchAsyncMethod!.ReturnType.GetGenericTypeDefinition())
            .IsEqualTo(typeof(Task<>));
    }

    [Test]
    public async Task IVectorSearchAdapter_SearchAsync_AcceptsFilters()
    {
        // Arrange
        var interfaceType = typeof(IVectorSearchAdapter<>);
        var searchAsyncMethod = interfaceType.GetMethod("SearchAsync");

        // Act
        var parameters = searchAsyncMethod!.GetParameters();
        var filtersParam = parameters.FirstOrDefault(p => p.Name == "filters");

        // Assert
        await Assert.That(filtersParam).IsNotNull();
        await Assert.That(filtersParam!.ParameterType.GetGenericTypeDefinition())
            .IsEqualTo(typeof(IReadOnlyDictionary<,>));
    }

    [Test]
    public async Task IVectorSearchAdapter_SearchAsync_HasDefaultParameters()
    {
        // Arrange
        var interfaceType = typeof(IVectorSearchAdapter<>);
        var searchAsyncMethod = interfaceType.GetMethod("SearchAsync");

        // Act
        var parameters = searchAsyncMethod!.GetParameters();
        var topKParam = parameters.FirstOrDefault(p => p.Name == "topK");
        var minRelevanceParam = parameters.FirstOrDefault(p => p.Name == "minRelevance");
        var filtersParam = parameters.FirstOrDefault(p => p.Name == "filters");
        var cancellationTokenParam = parameters.FirstOrDefault(p => p.Name == "cancellationToken");

        // Assert - all optional parameters should have defaults
        await Assert.That(topKParam!.HasDefaultValue).IsTrue();
        await Assert.That(topKParam.DefaultValue).IsEqualTo(5);
        await Assert.That(minRelevanceParam!.HasDefaultValue).IsTrue();
        await Assert.That(minRelevanceParam.DefaultValue).IsEqualTo(0.7);
        await Assert.That(filtersParam!.HasDefaultValue).IsTrue();
        await Assert.That(filtersParam.DefaultValue).IsNull();
        await Assert.That(cancellationTokenParam!.HasDefaultValue).IsTrue();
    }

    [Test]
    public async Task IVectorSearchAdapter_Generic_HasTypeConstraint()
    {
        // Arrange
        var interfaceType = typeof(IVectorSearchAdapter<>);

        // Act
        var typeParams = interfaceType.GetGenericArguments();
        var constraints = typeParams[0].GetGenericParameterConstraints();

        // Assert
        await Assert.That(constraints).Contains(typeof(IRagCollection));
    }

    /// <summary>
    /// Test implementation of IRagCollection for testing purposes.
    /// </summary>
    private sealed class TestRagCollection : IRagCollection
    {
    }
}

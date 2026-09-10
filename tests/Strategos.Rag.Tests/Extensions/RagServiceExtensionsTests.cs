// =============================================================================
// <copyright file="RagServiceExtensionsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Rag.Tests.Extensions;

/// <summary>
/// Unit tests for the <see cref="RagServiceExtensions"/> class.
/// </summary>
[Property("Category", "Unit")]
public class RagServiceExtensionsTests
{
    [Test]
    public async Task AddRagCollection_WithAdapter_RegistersInServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRagCollection<TestRagCollection, TestVectorSearchAdapter>();
        var provider = services.BuildServiceProvider();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IVectorSearchAdapter<TestRagCollection>));
        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.ImplementationType).IsEqualTo(typeof(TestVectorSearchAdapter));
        await Assert.That(descriptor.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    [Test]
    public async Task AddRagCollection_WithCustomLifetime_UsesSpecifiedLifetime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRagCollection<TestRagCollection, TestVectorSearchAdapter>(ServiceLifetime.Scoped);
        var provider = services.BuildServiceProvider();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IVectorSearchAdapter<TestRagCollection>));
        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
    }

    [Test]
    public async Task AddRagCollection_Generic_CanResolveAdapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRagCollection<TestRagCollection, TestVectorSearchAdapter>();
        var provider = services.BuildServiceProvider();

        // Act
        var adapter = provider.GetService<IVectorSearchAdapter<TestRagCollection>>();

        // Assert
        await Assert.That(adapter).IsNotNull();
        await Assert.That(adapter).IsTypeOf<TestVectorSearchAdapter>();
    }

    [Test]
    public async Task AddRagCollection_WithInstance_RegistersInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var adapterInstance = new TestVectorSearchAdapter();

        // Act
        services.AddRagCollection<TestRagCollection>(adapterInstance);
        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<IVectorSearchAdapter<TestRagCollection>>();

        // Assert
        await Assert.That(ReferenceEquals(resolved, adapterInstance)).IsTrue();
    }

    /// <summary>
    /// Test implementation of IRagCollection for testing purposes.
    /// </summary>
    private sealed class TestRagCollection : IRagCollection
    {
    }

    /// <summary>
    /// Test implementation of IVectorSearchAdapter for testing purposes.
    /// </summary>
    private sealed class TestVectorSearchAdapter : IVectorSearchAdapter<TestRagCollection>
    {
        public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
            string query,
            int topK = 5,
            double minRelevance = 0.7,
            IReadOnlyDictionary<string, object>? filters = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<VectorSearchResult>>([]);
        }
    }
}

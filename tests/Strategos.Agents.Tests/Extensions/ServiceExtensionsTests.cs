// =============================================================================
// <copyright file="ServiceExtensionsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Extensions;

using Microsoft.Extensions.DependencyInjection;

namespace Strategos.Agents.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="ServiceExtensions"/> covering DI registration methods.
/// </summary>
[Property("Category", "Unit")]
public class ServiceExtensionsTests
{
    /// <summary>
    /// Test implementation of IConversationThreadManager.
    /// </summary>
    private sealed class TestConversationThreadManager : IConversationThreadManager
    {
        public Task<IChatClient> CreateAgentWithThreadAsync(
            string agentType,
            string? serializedThread,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Substitute.For<IChatClient>());

        public Task<string> SerializeThreadAsync(
            string agentType,
            CancellationToken cancellationToken = default)
            => Task.FromResult("{}");
    }

    /// <summary>
    /// Test implementation of IStreamingCallback.
    /// </summary>
    private sealed class TestStreamingCallback : IStreamingCallback
    {
        public Task OnTokenReceivedAsync(
            string token,
            Guid workflowId,
            string stepName,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task OnResponseCompletedAsync(
            string fullResponse,
            Guid workflowId,
            string stepName,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that AddConversationThreadManager registers the manager as scoped.
    /// </summary>
    [Test]
    public async Task AddConversationThreadManager_RegistersAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddConversationThreadManager<TestConversationThreadManager>();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConversationThreadManager));
        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Verifies that AddConversationThreadManager returns the service collection for chaining.
    /// </summary>
    [Test]
    public async Task AddConversationThreadManager_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddConversationThreadManager<TestConversationThreadManager>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(services);
    }

    /// <summary>
    /// Verifies that AddConversationThreadManager throws on null services.
    /// </summary>
    [Test]
    public async Task AddConversationThreadManager_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        await Assert.That(() => services!.AddConversationThreadManager<TestConversationThreadManager>())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that AddStreamingCallback registers the callback as scoped.
    /// </summary>
    [Test]
    public async Task AddStreamingCallback_RegistersAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStreamingCallback<TestStreamingCallback>();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IStreamingCallback));
        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Verifies that AddStreamingCallback returns the service collection for chaining.
    /// </summary>
    [Test]
    public async Task AddStreamingCallback_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddStreamingCallback<TestStreamingCallback>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(services);
    }

    /// <summary>
    /// Verifies that AddStreamingCallback throws on null services.
    /// </summary>
    [Test]
    public async Task AddStreamingCallback_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        await Assert.That(() => services!.AddStreamingCallback<TestStreamingCallback>())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that registered IConversationThreadManager can be resolved.
    /// </summary>
    [Test]
    public async Task AddConversationThreadManager_CanResolveService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddConversationThreadManager<TestConversationThreadManager>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var manager = scope.ServiceProvider.GetService<IConversationThreadManager>();

        // Assert
        await Assert.That(manager).IsNotNull();
        await Assert.That(manager).IsTypeOf<TestConversationThreadManager>();
    }

    /// <summary>
    /// Verifies that registered IStreamingCallback can be resolved.
    /// </summary>
    [Test]
    public async Task AddStreamingCallback_CanResolveService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStreamingCallback<TestStreamingCallback>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var callback = scope.ServiceProvider.GetService<IStreamingCallback>();

        // Assert
        await Assert.That(callback).IsNotNull();
        await Assert.That(callback).IsTypeOf<TestStreamingCallback>();
    }

    /// <summary>
    /// Verifies that both extensions can be chained together.
    /// </summary>
    [Test]
    public async Task Extensions_CanBeChained()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddConversationThreadManager<TestConversationThreadManager>()
            .AddStreamingCallback<TestStreamingCallback>();

        // Assert - verify registrations exist rather than exact count (which is brittle)
        var threadManagerDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IConversationThreadManager) &&
            d.ImplementationType == typeof(TestConversationThreadManager));
        var callbackDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IStreamingCallback) &&
            d.ImplementationType == typeof(TestStreamingCallback));

        await Assert.That(threadManagerDescriptor).IsNotNull();
        await Assert.That(callbackDescriptor).IsNotNull();
    }
}


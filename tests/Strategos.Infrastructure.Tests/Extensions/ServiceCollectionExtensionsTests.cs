// =============================================================================
// <copyright file="ServiceCollectionExtensionsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Infrastructure.Configuration;
using Strategos.Infrastructure.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Strategos.Infrastructure.Tests.Extensions;

/// <summary>
/// Tests for the <see cref="ServiceCollectionExtensions"/> class.
/// </summary>
/// <remarks>
/// Tests verify that DI extension methods correctly register infrastructure services.
/// </remarks>
[Property("Category", "Unit")]
public sealed class ServiceCollectionExtensionsTests
{
    // =========================================================================
    // A. AddInMemoryArtifactStore Tests
    // =========================================================================

    /// <summary>
    /// Verifies that AddInMemoryArtifactStore registers the service.
    /// </summary>
    [Test]
    public async Task AddInMemoryArtifactStore_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        services.AddInMemoryArtifactStore();
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IArtifactStore>();
        await Assert.That(store).IsNotNull();
        await Assert.That(store).IsTypeOf<InMemoryArtifactStore>();
    }

    /// <summary>
    /// Verifies that AddInMemoryArtifactStore returns the service collection for chaining.
    /// </summary>
    [Test]
    public async Task AddInMemoryArtifactStore_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        var result = services.AddInMemoryArtifactStore();

        // Assert
        await Assert.That(result).IsSameReferenceAs(services);
    }

    /// <summary>
    /// Verifies that AddInMemoryArtifactStore registers as singleton.
    /// </summary>
    [Test]
    public async Task AddInMemoryArtifactStore_RegistersAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddInMemoryArtifactStore();
        var provider = services.BuildServiceProvider();

        // Act
        var store1 = provider.GetService<IArtifactStore>();
        var store2 = provider.GetService<IArtifactStore>();

        // Assert
        await Assert.That(store1).IsSameReferenceAs(store2);
    }

    // =========================================================================
    // B. AddFileSystemArtifactStore Tests
    // =========================================================================

    /// <summary>
    /// Verifies that AddFileSystemArtifactStore registers the service.
    /// </summary>
    [Test]
    public async Task AddFileSystemArtifactStore_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        services.AddFileSystemArtifactStore(options =>
        {
            options.BasePath = "/tmp/test";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IArtifactStore>();
        await Assert.That(store).IsNotNull();
        await Assert.That(store).IsTypeOf<FileSystemArtifactStore>();
    }

    /// <summary>
    /// Verifies that AddFileSystemArtifactStore configures options.
    /// </summary>
    [Test]
    public async Task AddFileSystemArtifactStore_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        services.AddFileSystemArtifactStore(options =>
        {
            options.BasePath = "/custom/path";
            options.FileExtension = ".data";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<IOptions<FileSystemArtifactStoreOptions>>();
        await Assert.That(options).IsNotNull();
        await Assert.That(options!.Value.BasePath).IsEqualTo("/custom/path");
        await Assert.That(options.Value.FileExtension).IsEqualTo(".data");
    }

    /// <summary>
    /// Verifies that AddFileSystemArtifactStore returns the service collection for chaining.
    /// </summary>
    [Test]
    public async Task AddFileSystemArtifactStore_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        var result = services.AddFileSystemArtifactStore(o => o.BasePath = "/tmp");

        // Assert
        await Assert.That(result).IsSameReferenceAs(services);
    }

    // =========================================================================
    // C. AddInMemoryStepExecutionLedger Tests
    // =========================================================================

    /// <summary>
    /// Verifies that AddInMemoryStepExecutionLedger registers the service.
    /// </summary>
    [Test]
    public async Task AddInMemoryStepExecutionLedger_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        services.AddInMemoryStepExecutionLedger();
        var provider = services.BuildServiceProvider();

        // Assert
        var ledger = provider.GetService<IStepExecutionLedger>();
        await Assert.That(ledger).IsNotNull();
        await Assert.That(ledger).IsTypeOf<InMemoryStepExecutionLedger>();
    }

    /// <summary>
    /// Verifies that AddInMemoryStepExecutionLedger returns the service collection for chaining.
    /// </summary>
    [Test]
    public async Task AddInMemoryStepExecutionLedger_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        var result = services.AddInMemoryStepExecutionLedger();

        // Assert
        await Assert.That(result).IsSameReferenceAs(services);
    }

    /// <summary>
    /// Verifies that AddInMemoryStepExecutionLedger registers as singleton.
    /// </summary>
    [Test]
    public async Task AddInMemoryStepExecutionLedger_RegistersAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddInMemoryStepExecutionLedger();
        var provider = services.BuildServiceProvider();

        // Act
        var ledger1 = provider.GetService<IStepExecutionLedger>();
        var ledger2 = provider.GetService<IStepExecutionLedger>();

        // Assert
        await Assert.That(ledger1).IsSameReferenceAs(ledger2);
    }

    // =========================================================================
    // D. Fluent Chaining Tests
    // =========================================================================

    /// <summary>
    /// Verifies that all extension methods can be chained fluently.
    /// </summary>
    [Test]
    public async Task AllExtensions_CanBeChainedFluently()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        services
            .AddInMemoryArtifactStore()
            .AddInMemoryStepExecutionLedger();

        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IArtifactStore>();
        var ledger = provider.GetService<IStepExecutionLedger>();

        await Assert.That(store).IsNotNull();
        await Assert.That(ledger).IsNotNull();
    }
}

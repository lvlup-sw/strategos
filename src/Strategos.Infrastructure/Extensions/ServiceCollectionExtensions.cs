// =============================================================================
// <copyright file="ServiceCollectionExtensions.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Configuration;
using Strategos.Infrastructure.ArtifactStores;
using Strategos.Infrastructure.Budget;
using Strategos.Infrastructure.Configuration;
using Strategos.Infrastructure.ExecutionLedgers;
using Strategos.Infrastructure.LoopDetection;
using Strategos.Selection;

namespace Strategos.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering workflow infrastructure services with dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide a fluent API for configuring workflow infrastructure services:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="AddInMemoryArtifactStore"/> - In-memory artifact storage for testing/development</description></item>
///   <item><description><see cref="AddFileSystemArtifactStore"/> - File system artifact storage with configuration</description></item>
///   <item><description><see cref="AddInMemoryStepExecutionLedger"/> - In-memory step execution caching</description></item>
/// </list>
/// <para>
/// All services are registered as singletons by default to ensure consistent behavior
/// across the application lifecycle.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services
///     .AddInMemoryArtifactStore()
///     .AddInMemoryStepExecutionLedger();
/// </code>
/// </example>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory artifact store implementation to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// The in-memory store is suitable for testing and development scenarios.
    /// For production use with durability requirements, use <see cref="AddFileSystemArtifactStore"/>.
    /// Registered as a singleton.
    /// </remarks>
    public static IServiceCollection AddInMemoryArtifactStore(this IServiceCollection services)
    {
        services.AddSingleton<IArtifactStore, InMemoryArtifactStore>();
        return services;
    }

    /// <summary>
    /// Adds the file system artifact store implementation to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The options configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The file system store provides durable artifact storage using the local file system.
    /// Artifacts are organized by category in subdirectories under the configured base path.
    /// </para>
    /// <para>
    /// Configuration example:
    /// <code>
    /// services.AddFileSystemArtifactStore(options =>
    /// {
    ///     options.BasePath = "/var/artifacts";
    ///     options.FileExtension = ".json";
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddFileSystemArtifactStore(
        this IServiceCollection services,
        Action<FileSystemArtifactStoreOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
        return services;
    }

    /// <summary>
    /// Adds the in-memory step execution ledger implementation to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The in-memory ledger caches step execution results for idempotent replay.
    /// It uses <see cref="TimeProvider.System"/> for TTL calculations.
    /// </para>
    /// <para>
    /// For distributed scenarios, consider using a Redis-backed implementation.
    /// Registered as a singleton.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddInMemoryStepExecutionLedger(this IServiceCollection services)
    {
        services.AddSingleton<IStepExecutionLedger>(sp =>
            new InMemoryStepExecutionLedger(TimeProvider.System));
        return services;
    }

    /// <summary>
    /// Adds the budget guard implementation to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The budget guard enforces resource constraints during workflow execution,
    /// preventing partial completion when resources are insufficient.
    /// </para>
    /// <para>
    /// Registered as a singleton.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddBudgetGuard(this IServiceCollection services)
    {
        services.AddSingleton<IBudgetGuard, BudgetGuard>();
        return services;
    }

    /// <summary>
    /// Adds the loop detector implementation to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The loop detector analyzes progress ledger entries to identify repetitive
    /// behavior patterns and recommend recovery strategies.
    /// </para>
    /// <para>
    /// Requires <see cref="ISemanticSimilarityCalculator"/> and
    /// <see cref="LoopDetectionOptions"/> to be configured.
    /// Registered as a singleton.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddLoopDetector(this IServiceCollection services)
    {
        services.AddSingleton<ILoopDetector, LoopDetector>();
        return services;
    }

    /// <summary>
    /// Adds the loop detector with configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure loop detection options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Configures and registers the loop detector with custom options.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddLoopDetector(
        this IServiceCollection services,
        Action<LoopDetectionOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<ILoopDetector, LoopDetector>();
        return services;
    }

    /// <summary>
    /// Adds all workflow orchestration services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers the complete set of workflow orchestration services:
    /// <list type="bullet">
    ///   <item><description>Budget Guard - Resource enforcement</description></item>
    ///   <item><description>Loop Detector - Repetition detection</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Requires <see cref="ISemanticSimilarityCalculator"/> and configuration
    /// options to be registered separately.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddWorkflowOrchestration(this IServiceCollection services)
    {
        services.AddBudgetGuard();
        services.AddLoopDetector();
        services.AddSingleton<ITaskCategoryClassifier, TaskCategoryClassifier>();
        return services;
    }
}

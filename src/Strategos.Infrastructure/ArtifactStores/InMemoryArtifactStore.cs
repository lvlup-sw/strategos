// =============================================================================
// <copyright file="InMemoryArtifactStore.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using BitFaster.Caching.Lru;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Strategos.Infrastructure.ArtifactStores;

/// <summary>
/// Configuration options for <see cref="InMemoryArtifactStore"/>.
/// </summary>
public sealed class InMemoryArtifactStoreOptions
{
    /// <summary>
    /// Gets or sets the maximum number of artifacts to store.
    /// When the capacity is exceeded, the least recently used artifacts are evicted.
    /// </summary>
    public int MaxCapacity { get; set; } = 10_000;
}

/// <summary>
/// In-memory implementation of <see cref="IArtifactStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores artifacts in memory using BitFaster's <see cref="ConcurrentLru{K, V}"/>
/// with a configurable bounded capacity. When the capacity is exceeded, the least recently used
/// artifacts are evicted.
/// </para>
/// <para>
/// For production use with durability requirements, use <see cref="FileSystemArtifactStore"/>
/// or a cloud-based implementation.
/// </para>
/// <list type="bullet">
///   <item><description>Thread-safe via <see cref="ConcurrentLru{K, V}"/></description></item>
///   <item><description>Bounded capacity with LRU eviction</description></item>
///   <item><description>Uses JSON serialization for artifact storage</description></item>
///   <item><description>URI scheme: memory://artifacts/{category}/{id}</description></item>
/// </list>
/// </remarks>
public sealed class InMemoryArtifactStore : IArtifactStore
{
    private readonly ConcurrentLru<string, string> _artifacts;
    private readonly ILogger<InMemoryArtifactStore> _logger;
    private long _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryArtifactStore"/> class
    /// with default settings (10,000 item capacity, no logging).
    /// </summary>
    public InMemoryArtifactStore()
        : this(
            NullLogger<InMemoryArtifactStore>.Instance,
            Options.Create(new InMemoryArtifactStoreOptions()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryArtifactStore"/> class
    /// with configurable capacity and logging.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options for cache behavior.</param>
    public InMemoryArtifactStore(
        ILogger<InMemoryArtifactStore> logger,
        IOptions<InMemoryArtifactStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        _logger = logger;
        var storeOptions = options.Value;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(storeOptions.MaxCapacity, nameof(storeOptions.MaxCapacity));
        _artifacts = new ConcurrentLru<string, string>(storeOptions.MaxCapacity);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="artifact"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="category"/> is null or whitespace.
    /// </exception>
    public ValueTask<Uri> StoreAsync<T>(T artifact, string category, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(artifact, nameof(artifact));
        ArgumentException.ThrowIfNullOrWhiteSpace(category, nameof(category));

        var id = Interlocked.Increment(ref _counter);
        var key = $"{category}/{id}";
        var json = JsonSerializer.Serialize(artifact);

        if (_artifacts.Count >= _artifacts.Capacity)
        {
            _logger.LogWarning(
                "InMemoryArtifactStore at capacity ({Capacity}). Least recently used items will be evicted.",
                _artifacts.Capacity);
        }

        _artifacts.AddOrUpdate(key, json);

        var uri = new Uri($"memory://artifacts/{key}");
        return new ValueTask<Uri>(uri);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="reference"/> is null.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no artifact exists at the specified reference.
    /// </exception>
    public ValueTask<T> RetrieveAsync<T>(Uri reference, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(reference, nameof(reference));

        var key = ExtractKeyFromUri(reference);

        if (!_artifacts.TryGet(key, out var json))
        {
            throw new KeyNotFoundException($"Artifact not found: {reference}");
        }

        var artifact = JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize artifact: {reference}");

        return new ValueTask<T>(artifact);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="reference"/> is null.
    /// </exception>
    /// <remarks>
    /// This method is idempotent - deleting a non-existent artifact succeeds silently.
    /// </remarks>
    public ValueTask DeleteAsync(Uri reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference, nameof(reference));

        var key = ExtractKeyFromUri(reference);
        _artifacts.TryRemove(key);

        return ValueTask.CompletedTask;
    }

    private static string ExtractKeyFromUri(Uri uri)
    {
        // URI format: memory://artifacts/{category}/{id}
        // Path will be: /artifacts/{category}/{id}
        var path = uri.AbsolutePath;

        if (path.StartsWith("/artifacts/", StringComparison.OrdinalIgnoreCase))
        {
            return path["/artifacts/".Length..];
        }

        return path.TrimStart('/');
    }
}

// =============================================================================
// <copyright file="FileSystemArtifactStore.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging;

using Strategos.Infrastructure.Configuration;

namespace Strategos.Infrastructure.ArtifactStores;

/// <summary>
/// File system implementation of <see cref="IArtifactStore"/> for local deployments.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores artifacts on the local file system with JSON serialization.
/// It is suitable for local deployments, single-node scenarios, or when cloud storage is not available.
/// </para>
/// <para>
/// For distributed deployments, use a cloud-based implementation (Azure Blob Storage, S3, etc.).
/// </para>
/// <list type="bullet">
///   <item><description>Artifacts organized by category in subdirectories</description></item>
///   <item><description>Uses JSON serialization for artifact storage</description></item>
///   <item><description>URI scheme: file:///{basePath}/{category}/{id}.json</description></item>
/// </list>
/// </remarks>
public sealed class FileSystemArtifactStore : IArtifactStore
{
    private readonly FileSystemArtifactStoreOptions _options;
    private readonly ILogger<FileSystemArtifactStore> _logger;
    private long _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemArtifactStore"/> class.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="logger"/> is null.
    /// </exception>
    public FileSystemArtifactStore(IOptions<FileSystemArtifactStoreOptions> options, ILogger<FileSystemArtifactStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="artifact"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="category"/> is null or whitespace.
    /// </exception>
    public async ValueTask<Uri> StoreAsync<T>(T artifact, string category, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(artifact, nameof(artifact));
        ArgumentException.ThrowIfNullOrWhiteSpace(category, nameof(category));

        var id = Interlocked.Increment(ref _counter);
        var categoryPath = Path.Combine(_options.BasePath, category);
        Directory.CreateDirectory(categoryPath);

        var fileName = $"{id}{_options.FileExtension}";
        var filePath = Path.Combine(categoryPath, fileName);

        var json = JsonSerializer.Serialize(artifact);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Stored artifact {ArtifactType} to {FilePath} in category {Category}", typeof(T).Name, filePath, category);

        return new Uri(filePath);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="reference"/> is null.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no artifact exists at the specified reference.
    /// </exception>
    public async ValueTask<T> RetrieveAsync<T>(Uri reference, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(reference, nameof(reference));

        var filePath = reference.LocalPath;

        if (!File.Exists(filePath))
        {
            throw new KeyNotFoundException($"Artifact not found: {reference}");
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        var artifact = JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize artifact: {reference}");

        _logger.LogDebug("Retrieved artifact {ArtifactType} from {FilePath}", typeof(T).Name, filePath);

        return artifact;
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

        var filePath = reference.LocalPath;

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogDebug("Deleted artifact at {FilePath}", filePath);
        }

        return ValueTask.CompletedTask;
    }
}

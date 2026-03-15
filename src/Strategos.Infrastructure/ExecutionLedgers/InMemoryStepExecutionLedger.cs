// =============================================================================
// <copyright file="InMemoryStepExecutionLedger.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using BitFaster.Caching.Lru;

using MemoryPack;

using Microsoft.Extensions.Logging;

namespace Strategos.Infrastructure.ExecutionLedgers;

/// <summary>
/// In-memory implementation of <see cref="IStepExecutionLedger"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores cached step results in memory using either a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> or BitFaster's <see cref="ConcurrentLru{K, V}"/>.
/// It is suitable for testing, development, and single-process scenarios.
/// </para>
/// <para>
/// For distributed scenarios, use a Redis or database-backed implementation.
/// </para>
/// <list type="bullet">
///   <item><description>Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/> or <see cref="ConcurrentLru{K, V}"/></description></item>
///   <item><description>Supports time-based expiration via <see cref="TimeProvider"/></description></item>
///   <item><description>Uses SHA256 for deterministic input hashing</description></item>
///   <item><description>Optional bounded capacity with LRU eviction via BitFaster</description></item>
/// </list>
/// </remarks>
public sealed class InMemoryStepExecutionLedger : IStepExecutionLedger
{
    private readonly ICacheStore _cacheStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemoryStepExecutionLedger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryStepExecutionLedger"/> class
    /// with default settings using <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider for TTL calculations.</param>
    /// <param name="logger">The logger instance.</param>
    public InMemoryStepExecutionLedger(TimeProvider timeProvider, ILogger<InMemoryStepExecutionLedger> logger)
        : this(timeProvider, null, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryStepExecutionLedger"/> class
    /// with configurable cache options.
    /// </summary>
    /// <param name="timeProvider">The time provider for TTL calculations.</param>
    /// <param name="options">Optional configuration options for cache behavior.</param>
    /// <param name="logger">The logger instance.</param>
    public InMemoryStepExecutionLedger(TimeProvider timeProvider, IOptions<StepExecutionLedgerOptions>? options, ILogger<InMemoryStepExecutionLedger> logger)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(timeProvider, nameof(timeProvider));
        _timeProvider = timeProvider;
        _logger = logger;

        var ledgerOptions = options?.Value ?? new StepExecutionLedgerOptions();
        _cacheStore = ledgerOptions.UseBitFasterCache
            ? new BitFasterCacheStore(ledgerOptions.CacheCapacity)
            : new DictionaryCacheStore();
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="stepName"/> or <paramref name="inputHash"/> is null or whitespace.
    /// </exception>
    public ValueTask<TResult?> TryGetCachedResultAsync<TResult>(
        string stepName,
        string inputHash,
        CancellationToken cancellationToken)
        where TResult : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName, nameof(stepName));
        ArgumentException.ThrowIfNullOrWhiteSpace(inputHash, nameof(inputHash));

        var key = BuildCacheKey(stepName, inputHash);

        if (!_cacheStore.TryGetValue(key, out var entry))
        {
            _logger.LogDebug("Cache miss for step {StepName} with hash {InputHash}", stepName, inputHash);
            // Return default ValueTask without allocation
            return default;
        }

        // Check TTL expiration
        if (entry.ExpiresAt.HasValue && _timeProvider.GetUtcNow() > entry.ExpiresAt.Value)
        {
            _logger.LogDebug("Cache entry expired for step {StepName} with hash {InputHash}", stepName, inputHash);
            _cacheStore.TryRemove(key);
            return default;
        }

        _logger.LogDebug("Cache hit for step {StepName} with hash {InputHash}", stepName, inputHash);
        var result = MemoryPackSerializer.Deserialize<TResult>(entry.Data);
        return new ValueTask<TResult?>(result);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="stepName"/> or <paramref name="inputHash"/> is null or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="result"/> is null.
    /// </exception>
    public Task CacheResultAsync<TResult>(
        string stepName,
        string inputHash,
        TResult result,
        TimeSpan? ttl,
        CancellationToken cancellationToken)
        where TResult : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName, nameof(stepName));
        ArgumentException.ThrowIfNullOrWhiteSpace(inputHash, nameof(inputHash));
        ArgumentNullException.ThrowIfNull(result, nameof(result));

        var key = BuildCacheKey(stepName, inputHash);
        var data = MemoryPackSerializer.Serialize(result);

        DateTimeOffset? expiresAt = ttl.HasValue
            ? _timeProvider.GetUtcNow().Add(ttl.Value)
            : null;

        var entry = new CacheEntry(data, expiresAt);
        _cacheStore.AddOrUpdate(key, entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="input"/> is null.
    /// </exception>
    /// <remarks>
    /// The hash is computed by serializing the input using MemoryPack and then
    /// computing SHA256 hash of the serialized bytes.
    /// </remarks>
    public string ComputeInputHash<TInput>(TInput input)
        where TInput : class
    {
        ArgumentNullException.ThrowIfNull(input, nameof(input));

        var bytes = MemoryPackSerializer.Serialize(input);
        var hashBytes = SHA256.HashData(bytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string BuildCacheKey(string stepName, string inputHash)
        => $"{stepName}:{inputHash}";

    /// <summary>
    /// Cache entry containing serialized data and optional expiration time.
    /// </summary>
    /// <param name="Data">The serialized byte array value.</param>
    /// <param name="ExpiresAt">The optional expiration time.</param>
    internal sealed record CacheEntry(byte[] Data, DateTimeOffset? ExpiresAt);

    /// <summary>
    /// Interface for cache storage abstraction.
    /// </summary>
    private interface ICacheStore
    {
        /// <summary>
        /// Tries to get a value from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="entry">The cache entry if found.</param>
        /// <returns><c>true</c> if found; otherwise, <c>false</c>.</returns>
        bool TryGetValue(string key, out CacheEntry entry);

        /// <summary>
        /// Adds or updates a value in the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="entry">The cache entry to store.</param>
        void AddOrUpdate(string key, CacheEntry entry);

        /// <summary>
        /// Tries to remove a value from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <returns><c>true</c> if removed; otherwise, <c>false</c>.</returns>
        bool TryRemove(string key);
    }

    /// <summary>
    /// Cache store implementation using <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </summary>
    private sealed class DictionaryCacheStore : ICacheStore
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

        /// <inheritdoc/>
        public bool TryGetValue(string key, out CacheEntry entry)
        {
            return _cache.TryGetValue(key, out entry!);
        }

        /// <inheritdoc/>
        public void AddOrUpdate(string key, CacheEntry entry)
        {
            _cache[key] = entry;
        }

        /// <inheritdoc/>
        public bool TryRemove(string key)
        {
            return _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Cache store implementation using BitFaster's <see cref="ConcurrentLru{K, V}"/>.
    /// </summary>
    /// <remarks>
    /// Provides bounded capacity with LRU eviction for high-throughput scenarios.
    /// </remarks>
    private sealed class BitFasterCacheStore : ICacheStore
    {
        private readonly ConcurrentLru<string, CacheEntry> _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitFasterCacheStore"/> class.
        /// </summary>
        /// <param name="capacity">The maximum capacity of the cache.</param>
        public BitFasterCacheStore(int capacity)
        {
            _cache = new ConcurrentLru<string, CacheEntry>(capacity);
        }

        /// <inheritdoc/>
        public bool TryGetValue(string key, out CacheEntry entry)
        {
            return _cache.TryGet(key, out entry!);
        }

        /// <inheritdoc/>
        public void AddOrUpdate(string key, CacheEntry entry)
        {
            _cache.AddOrUpdate(key, entry);
        }

        /// <inheritdoc/>
        public bool TryRemove(string key)
        {
            return _cache.TryRemove(key);
        }
    }
}

// =============================================================================
// <copyright file="InMemoryArtifactStoreOptions.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

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

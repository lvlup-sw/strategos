// =============================================================================
// <copyright file="InvariantCatalog.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Architecture.Tests;

/// <summary>The parsed frontmatter of <c>.exarchos/invariants.md</c>.</summary>
/// <param name="SchemaVersion">The <c>schema-version</c> scalar.</param>
/// <param name="Invariants">The entries, in file order.</param>
internal sealed record InvariantCatalog(int SchemaVersion, IReadOnlyList<InvariantEntry> Invariants);

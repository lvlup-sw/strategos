// =============================================================================
// <copyright file="InvariantEntry.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Architecture.Tests;

/// <summary>One <c>invariants:</c> entry of <c>.exarchos/invariants.md</c>.</summary>
/// <param name="Id">The <c>U-N</c> id.</param>
/// <param name="Line">One-based line of the <c>- id:</c> item, for messages.</param>
/// <param name="Dimension">The <c>dimension</c> scalar.</param>
/// <param name="AppliesTo">The <c>applies-to</c> globs, in file order.</param>
/// <param name="References">The <c>references</c> paths, in file order.</param>
/// <param name="Severity">The <c>severity.default</c> value, if present.</param>
/// <param name="Mode">The <c>enforcement.mode</c> value, if present.</param>
internal sealed record InvariantEntry(
    string Id,
    int Line,
    string? Dimension,
    IReadOnlyList<string> AppliesTo,
    IReadOnlyList<string> References,
    string? Severity,
    string? Mode);

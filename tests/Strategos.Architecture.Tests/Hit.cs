// =============================================================================
// <copyright file="Hit.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Architecture.Tests;

/// <summary>
/// One matching line from a repository scan, in <c>path:line: text</c> form so a
/// failing check names the offending file the way the bash in
/// <c>deterministic-checks.md</c> does.
/// </summary>
/// <param name="Path">Repo-relative path with forward slashes.</param>
/// <param name="Line">One-based line number.</param>
/// <param name="Text">The trimmed original line.</param>
internal sealed record Hit(string Path, int Line, string Text)
{
    /// <inheritdoc />
    public override string ToString() => $"{Path}:{Line}: {Text}";
}

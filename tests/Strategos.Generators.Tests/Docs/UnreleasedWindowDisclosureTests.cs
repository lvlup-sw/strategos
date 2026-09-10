// -----------------------------------------------------------------------
// <copyright file="UnreleasedWindowDisclosureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;

namespace Strategos.Generators.Tests.Docs;

/// <summary>
/// Release gate for behaviour changes accumulated inside the unreleased window.
/// </summary>
/// <remarks>
/// <para>
/// The last published package set is v2.10.0. Several releases therefore reach a consumer in
/// one restore, and a reviewer whose baseline is <c>main</c> is structurally blind to the
/// difference: each change looked small against its own parent commit. Three such changes had
/// no entry — the <c>AllowDiagnosticFork</c> composition restriction, the AONT216 graph-freeze
/// narrowing, and two <c>ActionCalculus</c> members deleted from the unreleased API — and one
/// stated caveat understated the truth about legacy compensation in a bound workflow.
/// </para>
/// <para>
/// This gate reads only the <c>[3.0.0-rc.1]</c> section, so an entry in an older section does not
/// satisfy it. Placeholder comments left for other authors are not content and are stripped
/// before matching, so an unfilled placeholder cannot make the gate pass.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class UnreleasedWindowDisclosureTests
{
    /// <summary>The identifiers that must appear in the 3.0.0-rc.1 section.</summary>
    /// <returns>Tokens to search for.</returns>
    public static IEnumerable<string> RequiredIdentifiers()
    {
        yield return "AllowDiagnosticFork";
        yield return "AONT216";
        yield return "AuthoredRollbackAgrees";
    }

    /// <summary>Each undisclosed change of this window is named in the release's own section.</summary>
    /// <param name="identifier">The identifier the section must name.</param>
    /// <returns>A task representing the assertion.</returns>
    [Test]
    [MethodDataSource(nameof(RequiredIdentifiers))]
    public async Task ReleaseSection_NamesTheUndisclosedWindowChange(string identifier)
    {
        var section = await ReadReleaseSectionAsync();

        await Assert.That(section.Contains(identifier, StringComparison.Ordinal)).IsTrue()
            .Because(
                $"'{identifier}' names a behaviour change made inside the unreleased window; a "
                + "consumer upgrading from the last published release only sees it if the "
                + "3.0.0-rc.1 section says so");
    }

    /// <summary>
    /// The legacy-compensation caveat states the case in which it is an unsuppressible error.
    /// </summary>
    /// <returns>A task representing the assertion.</returns>
    [Test]
    public async Task ReleaseSection_QualifiesTheLegacyCompensationCaveat()
    {
        var section = await ReadReleaseSectionAsync();

        await Assert.That(section.Contains("remains runtime-only", StringComparison.Ordinal))
            .IsTrue()
            .Because("the compatibility sentence for legacy .Compensate<T>() is the subject here");
        await Assert.That(section.Contains("AGWF044", StringComparison.Ordinal)).IsTrue()
            .Because(
                "in a workflow named by a BoundToWorkflow action the legacy overload is AGWF044, "
                + "a NotConfigurable error, so 'runtime-only' without that scope reads as a "
                + "no-op migration when the real remedy is one ontology inverse action per "
                + "compensated occurrence");
    }

    private static async Task<string> ReadReleaseSectionAsync()
    {
        var path = Path.Combine(RepoRoot(), "CHANGELOG.md");
        var content = await File.ReadAllTextAsync(path);

        // Carve between "## [3.0.0-rc.1]" and the next "## [" heading.
        var match = Regex.Match(
            content,
            @"^##\s+\[3\.0\.0-rc\.1\](?<body>.*?)(?=^##\s+\[)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        if (!match.Success)
        {
            throw new InvalidOperationException(
                "CHANGELOG.md has no '## [3.0.0-rc.1]' section followed by a later release heading.");
        }

        // An unfilled placeholder is a note to another author, not disclosure.
        return Regex.Replace(match.Groups["body"].Value, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"Could not locate repository root (global.json) from {AppContext.BaseDirectory}.");
        }

        return directory.FullName;
    }
}

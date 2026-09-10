// =============================================================================
// <copyright file="ApiDriftRemediationMessageTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Tests.FixtureExport;

namespace Strategos.Tests.PublicApi;

/// <summary>
/// #51 builder API-stability gate (PR-B), task T11 (and the T12 doc-presence
/// check). When a builder signature drifts from the baseline, the CI gate must
/// fail closed AND print the cross-product remediation protocol VERBATIM. The
/// exarchos strategos-api-mirror.test.ts consumer and the human reviewer both
/// rely on that exact string, so it is pinned here as a contract.
/// </summary>
public sealed class ApiDriftRemediationMessageTests
{
    /// <summary>
    /// The verbatim remediation protocol. If this string changes, the gate
    /// script, CONTRIBUTING.md, the CHANGELOG section header, and the
    /// IWorkflowBuilder doc-comment must all change together — this test is the
    /// tripwire that forces that.
    /// </summary>
    private const string Remediation =
        "Update PublicAPI.Unshipped.txt and add a CHANGELOG entry under Cross-product breaking changes.";

    private static string GateScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "check-builder-api-stability.sh");

    private static string ContributingPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "CONTRIBUTING.md");

    private static string ChangelogPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "CHANGELOG.md");

    [Test]
    public async Task GateScript_EmitsVerbatimRemediationMessage()
    {
        var script = await File.ReadAllTextAsync(GateScriptPath);

        await Assert.That(script)
            .Contains(Remediation);
    }

    [Test]
    public async Task Contributing_DocumentsTheCrossProductProtocol()
    {
        var contributing = await File.ReadAllTextAsync(ContributingPath);

        await Assert.That(contributing)
            .Contains("Cross-product breaking changes");
        await Assert.That(contributing)
            .Contains("PublicAPI.Unshipped.txt");
    }

    [Test]
    public async Task Changelog_HasCrossProductBreakingChangesSection()
    {
        var changelog = await File.ReadAllTextAsync(ChangelogPath);

        // Present even when empty — forces author intent on every release that
        // touches the builder surface (design §6.2).
        await Assert.That(changelog)
            .Contains("Cross-product breaking changes");
    }
}

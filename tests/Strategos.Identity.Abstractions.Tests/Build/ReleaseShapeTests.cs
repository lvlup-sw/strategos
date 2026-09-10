// -----------------------------------------------------------------------
// <copyright file="ReleaseShapeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;

namespace Strategos.Identity.Abstractions.Tests.Build;

/// <summary>
/// T14: release-shape verification anchored against CHANGELOG content.
/// </summary>
/// <remarks>
/// <para>
/// The release artifact (nupkg files) is produced by <c>dotnet pack</c> at
/// CI time. This test guards the in-repo state: CHANGELOG carries the
/// preview.1 section, the abstractions package csproj declares the expected
/// PackageId, and its <c>&lt;Description&gt;</c> documents the identity seam
/// so NuGet consumers can discover it. The full pack verification (three
/// nupkgs at exactly <c>2.7.0-preview.1</c>) is run as a CI gate via:
/// </para>
/// <code>
/// dotnet pack src/Strategos.Identity.Abstractions/Strategos.Identity.Abstractions.csproj /p:MinVerVersionOverride=2.7.0-preview.1
/// dotnet pack src/Strategos.Generators/Strategos.Generators.csproj             /p:MinVerVersionOverride=2.7.0-preview.1
/// dotnet pack src/Strategos/Strategos.csproj                                   /p:MinVerVersionOverride=2.7.0-preview.1
/// </code>
/// <para>
/// MinVer is git-tag-driven for releases (v-prefixed tags); the
/// <c>MinVerVersionOverride</c> property is the supported off-tag-build override.
/// </para>
/// </remarks>
[Property("Category", "Build")]
public class ReleaseShapeTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CHANGELOG.md")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from test assembly base directory.");
        }
    }

    /// <summary>
    /// CHANGELOG.md must carry the 2.7.0-preview.1 release section per the plan.
    /// </summary>
    [Test]
    public async Task Release_Changelog_ContainsPreview1Section()
    {
        var changelogPath = Path.Combine(RepoRoot, "CHANGELOG.md");
        var content = await File.ReadAllTextAsync(changelogPath);

        await Assert.That(content).Contains("## [2.7.0-preview.1] - 2026-05-17");
        await Assert.That(content).Contains("Strategos.Identity.Abstractions");
        await Assert.That(content).Contains("PropagateIncomingHeaderToOutgoing");
    }

    /// <summary>
    /// The abstractions csproj <c>&lt;Description&gt;</c> — the consumer-facing NuGet
    /// metadata — must document the identity seam and its header propagation so
    /// consumers can discover it. This replaces an earlier assertion against a root
    /// README.md subsection that was intentionally retired in the v2.9.1 README
    /// refactor (commit d0657d8); the release-shape guard now anchors to stable
    /// package metadata rather than mutable overview prose.
    /// </summary>
    [Test]
    public async Task Release_AbstractionsCsproj_DocumentsIdentitySeam()
    {
        var csprojPath = Path.Combine(RepoRoot, "src", "Strategos.Identity.Abstractions", "Strategos.Identity.Abstractions.csproj");
        var content = await File.ReadAllTextAsync(csprojPath);

        await Assert.That(content).Contains("Identity seam");
        await Assert.That(content).Contains("IAgentIdentityProvider");
        await Assert.That(content).Contains("propagation");
    }

    /// <summary>
    /// The new abstractions csproj must declare the expected PackageId so the
    /// produced nupkg files match the release plan.
    /// </summary>
    [Test]
    public async Task Release_AbstractionsCsproj_DeclaresExpectedPackageId()
    {
        var csprojPath = Path.Combine(RepoRoot, "src", "Strategos.Identity.Abstractions", "Strategos.Identity.Abstractions.csproj");
        var content = await File.ReadAllTextAsync(csprojPath);

        await Assert.That(content).Contains("<PackageId>LevelUp.Strategos.Identity.Abstractions</PackageId>");
    }
}

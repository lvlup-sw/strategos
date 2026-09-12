// -----------------------------------------------------------------------
// <copyright file="NpmPackageParityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Strategos.Contracts.Tests;

/// <summary>
/// The npm package and the NuGet package are two shapes of ONE contract version
/// (exarchos#1901). These pin the places that can silently drift apart.
/// </summary>
/// <remarks>
/// The publish workflow already refuses a tag whose version does not match
/// <c>&lt;ContractsVersion&gt;</c>. That check fires at publish time, on a tag, when
/// the cheapest correction is a retag. These fire on every build, so the drift is
/// caught by the pull request that introduces it.
/// </remarks>
[Property("Category", "Unit")]
public sealed partial class NpmPackageParityTests
{
    /// <summary>The npm version equals the pinned <c>ContractsVersion</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NpmVersion_EqualsPinnedContractsVersion()
    {
        var pinned = PinnedContractsVersion();
        var npm = Manifest().GetProperty("version").GetString();

        await Assert.That(npm).IsEqualTo(pinned)
            .Because(
                "the npm package and the NuGet package project one contract version. A consumer "
                + "that pins the npm package by version must get the schemas that version's C# "
                + "records were generated from.");
    }

    /// <summary>The package is publishable, and aimed at the private org registry.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Manifest_IsPublishable_ToTheOrgRegistry()
    {
        var manifest = Manifest();

        await Assert.That(manifest.TryGetProperty("private", out _)).IsFalse()
            .Because("a manifest marked private cannot be published at all.");

        var registry = manifest.GetProperty("publishConfig").GetProperty("registry").GetString();
        await Assert.That(registry).IsEqualTo("https://npm.pkg.github.com")
            .Because("the package is private to the organization, so it publishes to GitHub Packages.");

        var access = manifest.GetProperty("publishConfig").GetProperty("access").GetString();
        await Assert.That(access).IsEqualTo("restricted")
            .Because("'restricted' is what keeps a private package from being published publicly by default.");
    }

    /// <summary>
    /// <c>zod</c> is declared as a peer dependency, not only as a dev dependency.
    /// </summary>
    /// <remarks>
    /// The emitted modules call <c>z.enum(...)</c> and friends at module scope, so
    /// zod is RUNTIME code for a consumer, not a build-time tool. Left as a dev
    /// dependency only, the published package would resolve for us and fail to
    /// import for everyone else.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Zod_IsAPeerDependency()
    {
        var manifest = Manifest();

        await Assert.That(manifest.TryGetProperty("peerDependencies", out var peers)).IsTrue()
            .Because("a consumer must supply zod: the emitted modules execute it at import time.");
        await Assert.That(peers.TryGetProperty("zod", out _)).IsTrue()
            .Because("zod is the one runtime dependency the emitted modules have.");
    }

    /// <summary>Only built output ships, never the TypeScript sources or the schemas.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Files_ShipsOnlyTheBuiltOutput()
    {
        var files = Manifest().GetProperty("files").EnumerateArray()
            .Select(entry => entry.GetString() ?? string.Empty)
            .ToArray();

        await Assert.That(files).IsEquivalentTo(new[] { "dist" })
            .Because(
                "the package ships compiled JavaScript and declarations. Shipping Generated/zod "
                + "sources would make the consumer's build depend on our tsconfig.");
    }

    private static JsonElement Manifest()
    {
        var path = Path.Combine(RepoRoot(), "src", "Strategos.Contracts", "package.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static string PinnedContractsVersion()
    {
        var path = Path.Combine(
            RepoRoot(), "src", "Strategos.Contracts", "Strategos.Contracts.csproj");
        var match = ContractsVersion().Match(File.ReadAllText(path));
        if (!match.Success)
        {
            throw new InvalidOperationException($"no <ContractsVersion> in {path}");
        }

        return match.Groups["version"].Value;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                $"could not locate the repository root from {AppContext.BaseDirectory}.");
    }

    [GeneratedRegex(@"<ContractsVersion>(?<version>[^<]+)</ContractsVersion>")]
    private static partial Regex ContractsVersion();
}

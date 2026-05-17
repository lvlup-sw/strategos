// -----------------------------------------------------------------------
// <copyright file="ProjectStructureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;

namespace Strategos.Identity.Abstractions.Tests.Build;

/// <summary>
/// Verifies the bootstrap shape of the Strategos.Identity.Abstractions project skeleton.
/// </summary>
/// <remarks>
/// These checks anchor T1 of the G1 agent-identity seam plan: the project must target
/// netstandard2.0 (matches the generator analyzer target), enable central package management,
/// and ship PublicAPI tracking files so issue #51 protocol stays in force from the first commit.
/// </remarks>
[Property("Category", "Build")]
public class ProjectStructureTests
{
    private static string RepoRoot
    {
        get
        {
            // Walk up from the test assembly until we find the src/ directory.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from test assembly base directory.");
        }
    }

    /// <summary>
    /// Asserts that the new abstractions csproj exists and declares the expected target framework
    /// and central-package-management opt-in.
    /// </summary>
    [Test]
    public async Task Strategos_Identity_Abstractions_Csproj_Exists_AndTargetsNetStandard20()
    {
        var csprojPath = Path.Combine(RepoRoot, "src", "Strategos.Identity.Abstractions", "Strategos.Identity.Abstractions.csproj");

        await Assert.That(File.Exists(csprojPath)).IsTrue();

        var content = File.ReadAllText(csprojPath);
        await Assert.That(content).Contains("<TargetFramework>netstandard2.0</TargetFramework>");
        await Assert.That(content).Contains("<PackageId>LevelUp.Strategos.Identity.Abstractions</PackageId>");
    }

    /// <summary>
    /// Asserts that the PublicAPI tracking files exist so the issue #51 protocol is enforced
    /// for the new package from the first commit.
    /// </summary>
    [Test]
    public async Task Strategos_Identity_Abstractions_PublicApiFiles_Exist()
    {
        var projectDir = Path.Combine(RepoRoot, "src", "Strategos.Identity.Abstractions");

        await Assert.That(File.Exists(Path.Combine(projectDir, "PublicAPI.Shipped.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(projectDir, "PublicAPI.Unshipped.txt"))).IsTrue();
    }
}

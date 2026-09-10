// -----------------------------------------------------------------------
// <copyright file="PathRoutingKeyTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="PathRoutingKey"/>.
/// </summary>
[Property("Category", "Unit")]
public sealed class PathRoutingKeyTests
{
    /// <summary>
    /// Fork keys encode construct, fork id, <c>Path{n}</c>, and phase name.
    /// </summary>
    [Test]
    public async Task ForFork_ValidArgs_StoresConstructPathAndPhaseName()
    {
        var key = PathRoutingKey.ForFork("fulfilment", 1, "TechnicalAnalysis");

        await Assert.That(key.Construct).IsEqualTo(PathConstructKind.Fork);
        await Assert.That(key.ConstructId).IsEqualTo("fulfilment");
        await Assert.That(key.PathId).IsEqualTo("Path1");
        await Assert.That(key.PhaseName).IsEqualTo("TechnicalAnalysis");
    }

    /// <summary>
    /// Branch keys encode construct, branch id, path prefix, and phase name.
    /// </summary>
    [Test]
    public async Task ForBranch_ValidArgs_StoresConstructPathAndPhaseName()
    {
        var key = PathRoutingKey.ForBranch("claim-type", "Express", "ExpediteShipment");

        await Assert.That(key.Construct).IsEqualTo(PathConstructKind.Branch);
        await Assert.That(key.ConstructId).IsEqualTo("claim-type");
        await Assert.That(key.PathId).IsEqualTo("Express");
        await Assert.That(key.PhaseName).IsEqualTo("ExpediteShipment");
    }

    /// <summary>
    /// Two fork paths that share a phase name stay distinct by path id.
    /// </summary>
    [Test]
    public async Task ForFork_SharedPhaseName_KeysDifferByPathId()
    {
        var path0 = PathRoutingKey.ForFork("fulfilment", 0, "AnalyzeStep");
        var path1 = PathRoutingKey.ForFork("fulfilment", 1, "AnalyzeStep");

        await Assert.That(path0).IsNotEqualTo(path1);
        await Assert.That(path0.PhaseName).IsEqualTo(path1.PhaseName);
    }

    /// <summary>
    /// ForFork rejects a negative path index.
    /// </summary>
    [Test]
    public async Task ForFork_NegativePathIndex_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => PathRoutingKey.ForFork("fulfilment", -1, "AnalyzeStep"))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// ForFork rejects a missing phase name.
    /// </summary>
    [Test]
    public async Task ForFork_BlankPhaseName_ThrowsArgumentException()
    {
        await Assert.That(() => PathRoutingKey.ForFork("fulfilment", 0, " "))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// ForBranch rejects a missing path prefix.
    /// </summary>
    [Test]
    public async Task ForBranch_BlankPathPrefix_ThrowsArgumentException()
    {
        await Assert.That(() => PathRoutingKey.ForBranch("claim-type", " ", "ExpediteShipment"))
            .Throws<ArgumentException>();
    }
}

// -----------------------------------------------------------------------
// <copyright file="ForkPathCompletedNamingTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Unit tests for <see cref="ForkPathCompletedNaming"/>.
/// </summary>
[Property("Category", "Unit")]
public class ForkPathCompletedNamingTests
{
    /// <summary>
    /// Instance-named fork path-ends that share a type use the phase name as the
    /// completed-event stem, matching <c>Start{PhaseName}Command</c>.
    /// </summary>
    [Test]
    public async Task For_SharedTypeWithInstanceNames_StemsArePhaseNames()
    {
        var model = ForkPathMessageFixtures.SharedTypeInstanceNamed();
        var naming = ForkPathCompletedNaming.For(model);

        var path0 = PathRoutingKey.ForFork("analysis", 0, "Technical");
        var path1 = PathRoutingKey.ForFork("analysis", 1, "Fundamental");

        await Assert.That(naming.StemFor(path0, "AnalyzeStep")).IsEqualTo("Technical");
        await Assert.That(naming.StemFor(path1, "AnalyzeStep")).IsEqualTo("Fundamental");
        await Assert.That(naming.IsQualifiedPhase("Technical")).IsTrue();
        await Assert.That(naming.HasUnqualifiedUse("AnalyzeStep", model)).IsFalse();
        await Assert.That(naming.HasUnqualifiedUse("PrepareStep", model)).IsTrue();
    }

    /// <summary>
    /// When two path-ends share a phase name, the stem is path-qualified so the
    /// completed CLR types stay distinct.
    /// </summary>
    [Test]
    public async Task For_SharedPhaseName_StemsIncludePathId()
    {
        var model = ForkPathMessageFixtures.SharedPhaseName();
        var naming = ForkPathCompletedNaming.For(model);

        var path0 = PathRoutingKey.ForFork("analysis", 0, "AnalyzeStep");
        var path1 = PathRoutingKey.ForFork("analysis", 1, "AnalyzeStep");

        await Assert.That(naming.StemFor(path0, "AnalyzeStep")).IsEqualTo("Path0_AnalyzeStep");
        await Assert.That(naming.StemFor(path1, "AnalyzeStep")).IsEqualTo("Path1_AnalyzeStep");
        await Assert.That(naming.QualifiedInstances[0].IsPathQualified).IsTrue();
        await Assert.That(naming.QualifiedInstances[1].IsPathQualified).IsTrue();
    }

    /// <summary>
    /// Unique-type fork paths keep the step type as the stem.
    /// </summary>
    [Test]
    public async Task For_UniqueTypes_StemIsStepType()
    {
        var model = ForkPathMessageFixtures.UniqueTypes();
        var naming = ForkPathCompletedNaming.For(model);

        var path0 = PathRoutingKey.ForFork("analysis", 0, "TechnicalAnalyzeStep");
        var path1 = PathRoutingKey.ForFork("analysis", 1, "FundamentalAnalyzeStep");

        await Assert.That(naming.StemFor(path0, "TechnicalAnalyzeStep")).IsEqualTo("TechnicalAnalyzeStep");
        await Assert.That(naming.StemFor(path1, "FundamentalAnalyzeStep")).IsEqualTo("FundamentalAnalyzeStep");
        await Assert.That(naming.IsQualifiedPhase("TechnicalAnalyzeStep")).IsFalse();
        await Assert.That(naming.QualifiedInstances).IsEmpty();
    }

    /// <summary>
    /// Shared-type interiors (not just path-ends) are qualified by phase name.
    /// </summary>
    [Test]
    public async Task For_SharedTypeInteriors_StemsArePhaseNames()
    {
        var model = ForkPathMessageFixtures.SharedTypeInteriors();
        var naming = ForkPathCompletedNaming.For(model);

        var intake0 = PathRoutingKey.ForFork("analysis", 0, "TechnicalIntake");
        var intake1 = PathRoutingKey.ForFork("analysis", 1, "FundamentalIntake");

        await Assert.That(naming.StemFor(intake0, "AnalyzeStep")).IsEqualTo("TechnicalIntake");
        await Assert.That(naming.StemFor(intake1, "AnalyzeStep")).IsEqualTo("FundamentalIntake");
        await Assert.That(naming.IsQualifiedPhase("TechnicalIntake")).IsTrue();
        await Assert.That(naming.IsQualifiedPhase("TechReport")).IsTrue();
    }

    /// <summary>
    /// Unique-type paths keep <c>Start{PhaseName}Command</c>; colliding unnamed
    /// same-type paths use the path-qualified stem.
    /// </summary>
    [Test]
    public async Task StartCommandStem_SharedPhaseName_UsesPathId()
    {
        var colliding = ForkPathCompletedNaming.For(ForkPathMessageFixtures.SharedPhaseName());
        var unique = ForkPathCompletedNaming.For(ForkPathMessageFixtures.UniqueTypes());

        var path0 = PathRoutingKey.ForFork("analysis", 0, "AnalyzeStep");
        var unique0 = PathRoutingKey.ForFork("analysis", 0, "TechnicalAnalyzeStep");

        await Assert.That(colliding.StartCommandStem(path0, "AnalyzeStep")).IsEqualTo("Path0_AnalyzeStep");
        await Assert.That(unique.StartCommandStem(unique0, "TechnicalAnalyzeStep")).IsEqualTo("TechnicalAnalyzeStep");
    }
}

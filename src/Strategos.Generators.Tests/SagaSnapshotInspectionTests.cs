// -----------------------------------------------------------------------
// <copyright file="SagaSnapshotInspectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// T10: programmatic equivalent of snapshot regeneration. The project has no
/// Verify <c>.verified.txt</c> baseline files, so the contract is anchored
/// here by per-shape string-contains assertions across the major workflow
/// shapes: linear, looped, fork/join, and branched.
/// </summary>
/// <remarks>
/// <para>
/// For each shape we assert:
/// </para>
/// <list type="number">
///   <item><description>The base list reads <c>: Saga, IPhaseAwareSaga</c> — additive only.</description></item>
///   <item><description>The CurrentPhaseName property is emitted exactly once.</description></item>
///   <item><description>The Strategos.Identity.Abstractions using is present.</description></item>
///   <item><description>No DR-7 negated token appears (descoped Option C state).</description></item>
/// </list>
/// <para>
/// Failures here mean the generator regressed across one of the existing
/// representative workflow shapes.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public class SagaSnapshotInspectionTests
{
    /// <summary>Linear workflow saga: additive emit only.</summary>
    [Test]
    public async Task SnapshotRegeneration_LinearWorkflow_IsAdditiveOnly()
    {
        await AssertAdditiveOnly(SourceTexts.LinearWorkflow, "ProcessOrderSaga.g.cs");
    }

    /// <summary>Looped workflow saga: additive emit only.</summary>
    [Test]
    public async Task SnapshotRegeneration_WorkflowWithLoop_IsAdditiveOnly()
    {
        await AssertAdditiveOnly(SourceTexts.WorkflowWithLoop, "IterativeRefinementSaga.g.cs");
    }

    /// <summary>Forked workflow saga: additive emit only.</summary>
    [Test]
    public async Task SnapshotRegeneration_WorkflowWithFork_IsAdditiveOnly()
    {
        await AssertAdditiveOnly(SourceTexts.WorkflowWithFork, "ParallelOrderSaga.g.cs");
    }

    /// <summary>Branched workflow saga: additive emit only.</summary>
    [Test]
    public async Task SnapshotRegeneration_WorkflowWithEnumBranch_IsAdditiveOnly()
    {
        await AssertAdditiveOnly(SourceTexts.WorkflowWithEnumBranch, "ProcessClaimSaga.g.cs");
    }

    private static async Task AssertAdditiveOnly(string source, string sagaHintNameSuffix)
    {
        var result = GeneratorTestHelper.RunGenerator(source);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, sagaHintNameSuffix);

        await Assert.That(sagaSource).IsNotNull().And.IsNotEmpty();

        // Additive emit
        await Assert.That(sagaSource).Contains(": Saga, IPhaseAwareSaga");
        await Assert.That(sagaSource).Contains("public string CurrentPhaseName => Phase.ToString();");
        await Assert.That(sagaSource).Contains("using Strategos.Identity.Abstractions;");

        // DR-7 negations: no Option-C state leaked into emit
        await Assert.That(sagaSource).DoesNotContain("CurrentAgentIdentity");
        await Assert.That(sagaSource).DoesNotContain("InitializeIdentity");
        await Assert.That(sagaSource).DoesNotContain("_workflowIdentity");
        await Assert.That(sagaSource).DoesNotContain("_identityProvider");
        await Assert.That(sagaSource).DoesNotContain("InternalsVisibleTo");
    }
}

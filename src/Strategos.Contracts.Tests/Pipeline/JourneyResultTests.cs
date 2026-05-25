// =============================================================================
// <copyright file="JourneyResultTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// S1 (issue #63) — the <c>JourneyResult</c> response envelope. Like
/// <c>MergeGateDecision</c> it composes the shared S2 base blocks
/// (<c>_meta</c>/<c>_perf</c>); it types its journey outcomes as
/// <c>WorkflowRef</c>.
/// </summary>
[Property("Category", "Pipeline")]
public class JourneyResultTests
{
    /// <summary>
    /// Asserts the result envelope carries the <c>_meta</c>/<c>_perf</c> base
    /// blocks and a read-only <c>JourneyOutcomes</c> list typed as
    /// <c>WorkflowRef</c>.
    /// </summary>
    [Test]
    public async Task JourneyResult_ExtendsBaseMeta_AndTypesJourneyOutcomes()
    {
        var result = IntentEnvelopeTests.ResolveGenerated("JourneyResult");
        await Assert.That(result).IsNotNull()
            .Because("the JourneyResult envelope must be generated.");
        await Assert.That(result!.IsSealed).IsTrue();

        await MergeGateDecisionTests.AssertHasBlock(result, "ResponseMetaV1", "_meta");
        await MergeGateDecisionTests.AssertHasBlock(result, "PerfMetaV1", "_perf");

        var outcomes = result.GetProperty("JourneyOutcomes");
        await Assert.That(outcomes).IsNotNull()
            .Because("the result must type its journey outcomes.");
        await Assert.That(outcomes!.PropertyType.IsGenericType
                && outcomes.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            .IsTrue()
            .Because("JourneyOutcomes must be IReadOnlyList<T> (INV-7).");
        await Assert.That(outcomes.PropertyType.GetGenericArguments()[0].Name).IsEqualTo("WorkflowRef")
            .Because("journey outcomes are typed as WorkflowRef, not strings (INV-8).");
    }
}

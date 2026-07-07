// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkParityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using Strategos.Definitions;

namespace Strategos.Generators.Tests.Parity;

/// <summary>
/// Forcing-function parity guard over the diagnostic-fork declarable surface (DR-7,
/// #151), modeled on <see cref="StepConfigParityTests"/>. It reflects the public IR
/// fields of <see cref="DiagnosticForkDefinition"/> — the components a saga lowering
/// must honor (anchors, permitted triggers, compensation seed, <c>maxForks</c> bound) —
/// and asserts that EVERY one is explicitly classified in exactly one of two
/// author-maintained sets:
/// <list type="bullet">
///   <item><description>
///     <see cref="Lowered"/> — the member lowers into the generated Wolverine+Marten
///     saga, proven by a NAMED behavioral (compile-run-saga on a real host) test.
///   </description></item>
///   <item><description>
///     <see cref="Deferred"/> — the member is intentionally not yet lowered, carrying a
///     tracking issue AND a declared-but-inert diagnosability guarantee (AGWF022), so a
///     declared-but-unlowered fork cannot masquerade as working.
///   </description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// The declarative half (builder IR + wire projection), the generator IR model, and the
/// saga lowering all landed under #151, so EVERY fork IR member is now
/// <see cref="Lowered"/>, each pointing at a real-host behavioral proof. The guard makes
/// silence impossible: a NEW (or renamed) fork IR member fails this test until the author
/// classifies it as <see cref="Lowered"/> (with a behavioral proof) or <see cref="Deferred"/>
/// (with a tracking issue + declared-but-inert diagnosability guarantee).
/// </para>
/// <para>
/// The "declared-but-inert diagnosability guarantee" is the promise that a declared but
/// unlowered fork is surfaced by the AGWF022 declared-but-inert diagnostic (the same
/// mechanism <c>DeclaredButInertTests</c> proves for step config), rather than being
/// silently dropped by the generator. Registering a deferred member as a structural field —
/// not merely prose — keeps any future deferral honest: emitting the fork parse without the
/// diagnostic, or dropping the diagnostic, is a forcing-function violation.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class DiagnosticForkParityTests
{
    /// <summary>
    /// The behavioral test file (in the behavioral suite) that carries the real-host fork
    /// lowering proofs — every <see cref="Lowered"/> member points at a test method here.
    /// </summary>
    private const string LoweringProofFile =
        "Strategos.Generators.Behavioral.Tests/DiagnosticForkLoweringTests.cs";

    /// <summary>
    /// Fork IR members that LOWER into the generated saga (DR-9, #151), each mapped to the
    /// behavioral (compile-run-saga, real-host) test that proves the lowering. The saga
    /// lowering landed under #151, so EVERY fork IR member is now classified here.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, LoweredProof> Lowered =
        new Dictionary<string, LoweredProof>(StringComparer.Ordinal)
        {
            // Anchor-step lowering: the fork decision site's anchor guard admits a fork only
            // at a declared anchor moniker; a fork at an undeclared anchor is refused.
            [nameof(DiagnosticForkDefinition.AnchorStepIds)] = new(
                "Behavioral_ForkAtUndeclaredAnchor_IsRefused",
                LoweringProofFile),

            // Permitted-trigger / evidence-schema lowering: the DR-8 occurrence chokepoint
            // refuses a fork whose permitted trigger arrives without its required evidence.
            [nameof(DiagnosticForkDefinition.PermittedTriggers)] = new(
                "Behavioral_ForkWithoutEvidence_IsRefused",
                LoweringProofFile),

            // Compensation-seed lowering: a valid fork seeds compensation by routing its
            // declared seed into the merged Compensate/OnFailure trigger site (#140).
            [nameof(DiagnosticForkDefinition.CompensationSeed)] = new(
                "Behavioral_ValidFork_SeedsCompensationThroughMergedTriggerSite",
                LoweringProofFile),

            // maxForks-bound lowering: the forced-exit guard routes an overflowing fork to the
            // blocked / human-escalation terminal phase (the loop MaxIterations precedent).
            [nameof(DiagnosticForkDefinition.MaxForks)] = new(
                "Behavioral_ForkExceedingMaxForks_RoutesToBlockedTerminal",
                LoweringProofFile),
        };

    /// <summary>
    /// Fork IR members intentionally NOT yet lowered. Empty now that the #151 saga-lowering
    /// follow-on has landed and moved every member to <see cref="Lowered"/> with a
    /// behavioral proof; retained so a FUTURE fork IR member can be parked here (with a
    /// tracking issue + declared-but-inert diagnosability guarantee) rather than silently
    /// dropped.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, DeferredEntry> Deferred =
        new Dictionary<string, DeferredEntry>(StringComparer.Ordinal);

    /// <summary>
    /// Asserts every fork IR member is classified in exactly one of <see cref="Lowered"/>
    /// or <see cref="Deferred"/>, that each lowered entry names a behavioral proof, and
    /// that each deferred entry carries BOTH a tracking issue AND a declared-but-inert
    /// diagnosability guarantee.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkParity_EveryMember_IsLoweredOrDeferred()
    {
        var members = EnumerateForkSurface().ToList();

        var (unclassified, doubleClassified) = Classify(members);

        await Assert.That(unclassified)
            .IsEmpty()
            .Because(
                "every diagnostic-fork IR member must be classified as Lowered (with a behavioral " +
                "proof) or Deferred (with a tracking issue + declared-but-inert guarantee); " +
                "unclassified: " + string.Join(", ", unclassified));

        await Assert.That(doubleClassified)
            .IsEmpty()
            .Because(
                "a member must be in EXACTLY one set; double-classified: " +
                string.Join(", ", doubleClassified));

        foreach (var (member, proof) in Lowered)
        {
            await Assert.That(proof.BehavioralTest)
                .IsNotEmpty()
                .Because($"Lowered member '{member}' must reference a behavioral lowering proof");
            await Assert.That(proof.BehavioralTestFile)
                .Contains("Behavioral.Tests")
                .Because($"Lowered member '{member}' proof must live in the behavioral suite, not a shape test");
        }

        foreach (var (member, entry) in Deferred)
        {
            await Assert.That(entry.TrackingIssue)
                .IsGreaterThan(0)
                .Because($"Deferred member '{member}' must carry a tracking issue number");
            await Assert.That(entry.DiagnosabilityGuarantee)
                .IsNotEmpty()
                .Because(
                    $"Deferred member '{member}' must name a declared-but-inert diagnosability " +
                    "guarantee so it cannot masquerade as silently lowered");
        }
    }

    /// <summary>
    /// Negative guard for the forcing function: a synthetic surface with an UNCLASSIFIED
    /// member is flagged, and a member placed in BOTH sets is flagged as
    /// double-classified. Proves the guard fails when a new (or mis-classified) fork
    /// member appears, without mutating the production surface.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkParity_UnclassifiedOrDoubleClassifiedMember_IsFlagged()
    {
        var syntheticSurface = new[]
        {
            nameof(DiagnosticForkDefinition.AnchorStepIds), // classified (Lowered)
            "SomeBrandNewForkKnob",                         // unclassified -> must be flagged
        };

        var (unclassified, _) = Classify(syntheticSurface);

        await Assert.That(unclassified)
            .Contains("SomeBrandNewForkKnob")
            .Because("a member in neither Lowered nor Deferred must be reported as unclassified");

        await Assert.That(unclassified)
            .DoesNotContain(nameof(DiagnosticForkDefinition.AnchorStepIds))
            .Because("a classified member must NOT be reported as unclassified");

        var doubleSurface = new[] { nameof(DiagnosticForkDefinition.AnchorStepIds) };
        var doubleSets = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(DiagnosticForkDefinition.AnchorStepIds),
        };
        var (_, doubleClassified) = Classify(
            doubleSurface,
            loweredKeys: doubleSets,
            deferredKeys: doubleSets);

        await Assert.That(doubleClassified)
            .Contains(nameof(DiagnosticForkDefinition.AnchorStepIds))
            .Because("a member present in BOTH sets must be reported as double-classified");
    }

    private static (List<string> Unclassified, List<string> DoubleClassified) Classify(
        IEnumerable<string> members,
        ISet<string>? loweredKeys = null,
        ISet<string>? deferredKeys = null)
    {
        var lowered = loweredKeys ?? new HashSet<string>(Lowered.Keys, StringComparer.Ordinal);
        var deferred = deferredKeys ?? new HashSet<string>(Deferred.Keys, StringComparer.Ordinal);

        var unclassified = new List<string>();
        var doubleClassified = new List<string>();

        foreach (var member in members)
        {
            var inLowered = lowered.Contains(member);
            var inDeferred = deferred.Contains(member);

            if (inLowered && inDeferred)
            {
                doubleClassified.Add(member);
            }
            else if (!inLowered && !inDeferred)
            {
                unclassified.Add(member);
            }
        }

        return (unclassified, doubleClassified);
    }

    /// <summary>
    /// Enumerates the declarable diagnostic-fork surface: the public instance properties
    /// of <see cref="DiagnosticForkDefinition"/> that represent lowerable state.
    /// </summary>
    /// <returns>The distinct set of classifiable member names.</returns>
    private static IEnumerable<string> EnumerateForkSurface() =>
        typeof(DiagnosticForkDefinition)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// A lowered-member proof: the behavioral test name and the file it lives in.
    /// </summary>
    /// <param name="BehavioralTest">The behavioral test method that proves the lowering.</param>
    /// <param name="BehavioralTestFile">The behavioral test file (must be in the behavioral suite).</param>
    private sealed record LoweredProof(string BehavioralTest, string BehavioralTestFile);

    /// <summary>
    /// A deferred-member entry: the tracking issue, the declared-but-inert diagnosability
    /// guarantee, and a short reason.
    /// </summary>
    /// <param name="TrackingIssue">The GitHub issue number tracking the deferral.</param>
    /// <param name="DiagnosabilityGuarantee">
    /// The declared-but-inert diagnostic id guaranteeing the deferred member is surfaced
    /// rather than silently dropped.
    /// </param>
    /// <param name="Reason">A short human-readable reason for the deferral.</param>
    private sealed record DeferredEntry(int TrackingIssue, string DiagnosabilityGuarantee, string Reason);
}

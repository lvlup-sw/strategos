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
/// This task (the builder surface) delivers the declarative half end to end: the fork
/// is captured in the builder IR and projected to the wire contract. The generator IR
/// model and the saga lowering are the follow-on tasks under #151, so EVERY fork IR
/// member is <see cref="Deferred"/> today. When the lowering lands, the author must move
/// the member to <see cref="Lowered"/> (pointing at a behavioral proof) — the guard
/// makes silence impossible.
/// </para>
/// <para>
/// The "declared-but-inert diagnosability guarantee" is the promise that a declared but
/// unlowered fork is surfaced by the AGWF022 declared-but-inert diagnostic (the same
/// mechanism <c>DeclaredButInertTests</c> proves for step config), rather than being
/// silently dropped by the generator. Registering it here as a structural field — not
/// merely prose — keeps the deferral honest: emitting the fork parse without the
/// diagnostic, or dropping the diagnostic, is a forcing-function violation.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class DiagnosticForkParityTests
{
    /// <summary>
    /// The declared-but-inert diagnostic that guarantees a deferred (declared but not yet
    /// lowered) fork member is surfaced at compile time rather than silently dropped.
    /// </summary>
    private const string DeclaredButInertDiagnostic = "AGWF022";

    /// <summary>
    /// Fork IR members that LOWER into the generated saga, each mapped to the behavioral
    /// (compile-run-saga, real-host) test that proves the lowering. Empty until the
    /// #151 lowering follow-on lands.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, LoweredProof> Lowered =
        new Dictionary<string, LoweredProof>(StringComparer.Ordinal);

    /// <summary>
    /// Fork IR members intentionally NOT yet lowered, each carrying its tracking issue
    /// and its declared-but-inert diagnosability guarantee. #151 (the saga-lowering
    /// follow-on) will move these to <see cref="Lowered"/> with behavioral proofs.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, DeferredEntry> Deferred =
        new Dictionary<string, DeferredEntry>(StringComparer.Ordinal)
        {
            [nameof(DiagnosticForkDefinition.AnchorStepIds)] = new(
                151,
                DeclaredButInertDiagnostic,
                "Anchor-step lowering (the fork-guard site) is deferred to the #151 saga-lowering "
                + "follow-on; until then a declared fork is surfaced by the declared-but-inert "
                + "diagnostic rather than silently dropped."),
            [nameof(DiagnosticForkDefinition.PermittedTriggers)] = new(
                151,
                DeclaredButInertDiagnostic,
                "Permitted-trigger / evidence-schema lowering (the per-trigger evidence guard) is "
                + "deferred to the #151 saga-lowering follow-on; declared-but-inert diagnosable."),
            [nameof(DiagnosticForkDefinition.CompensationSeed)] = new(
                151,
                DeclaredButInertDiagnostic,
                "Compensation-seed lowering (routing the fork's rollback into the merged "
                + "Compensate/OnFailure trigger site) is deferred to the #151 saga-lowering "
                + "follow-on; declared-but-inert diagnosable."),
            [nameof(DiagnosticForkDefinition.MaxForks)] = new(
                151,
                DeclaredButInertDiagnostic,
                "maxForks-bound lowering (the forced-exit guard routing an overflowing fork to a "
                + "blocked / human-escalation terminal) is deferred to the #151 saga-lowering "
                + "follow-on; declared-but-inert diagnosable."),
        };

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
            nameof(DiagnosticForkDefinition.AnchorStepIds), // classified (Deferred)
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

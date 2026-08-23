// -----------------------------------------------------------------------
// <copyright file="StepConfigParityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using Strategos.Builders;
using Strategos.Definitions;

namespace Strategos.Generators.Tests.Parity;

/// <summary>
/// Forcing-function parity guard (#143, G-6 6.1) over the declared step-configuration
/// surface. Reflects the public surface of <see cref="IStepConfiguration{TState}"/> and
/// the public fields of <see cref="StepConfigurationDefinition"/> and asserts that EVERY
/// configurable member is explicitly classified in exactly one of two author-maintained
/// sets:
/// <list type="bullet">
///   <item><description>
///     <see cref="Lowered"/> — the member lowers into the generated Wolverine+Marten saga,
///     proven by a NAMED <em>behavioral</em> (compile-run-saga on a real host) test, not a
///     shape/golden test.
///   </description></item>
///   <item><description>
///     <see cref="Deferred"/> — the member is intentionally not yet lowered, carrying a
///     tracking issue number.
///   </description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// This is a forcing function: when a new configuration member is added to either surface
/// and is NOT classified here, the guard fails. The author must then either point it at a
/// behavioral lowering proof (move it to <see cref="Lowered"/>) or file a deferral
/// (move it to <see cref="Deferred"/> with an issue). A config member is "done" only with
/// a behavioral proof or a tracked deferral — never with a shape/golden test alone.
/// </para>
/// <para>
/// The keys in both sets are the reflected member <em>names</em>. Overloads (e.g. the two
/// <c>WithRetry</c> overloads) collapse to a single name, which is the unit of
/// classification.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class StepConfigParityTests
{
    /// <summary>
    /// The set of declared step-configuration members that LOWER into the generated saga,
    /// each mapped to the behavioral (compile-run-saga, real-host) test that proves the
    /// lowering. Shape/golden tests do NOT qualify — the proof must run the generated saga.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, LoweredProof> Lowered =
        new Dictionary<string, LoweredProof>(StringComparer.Ordinal)
        {
            // --- IStepConfiguration<TState> builder methods ---
            ["RequireConfidence"] = new(
                "ConfidenceBehaviorTests.Saga_HighConfidence_ProceedsOnPrimaryPath",
                "Strategos.Generators.Behavioral.Tests/ConfidenceBehaviorTests.cs"),
            ["OnLowConfidence"] = new(
                "ConfidenceBehaviorTests.Saga_LowConfidence_RoutesToOnLowConfidenceBranch",
                "Strategos.Generators.Behavioral.Tests/ConfidenceBehaviorTests.cs"),

            // Fork-path confidence lowering (DR-4 / #145 gap A): a fork path's LAST step
            // (the "fork handler") now lowers its confidence gate into the generated fork
            // path-completed handler, proven behaviorally on the real host. Intermediate
            // (non-last) fork-path confidence stays deferred (see Deferred below).
            ["RequireConfidence(fork-path)"] = new(
                "ForkPathConfidenceTests.Saga_ForkPathLowConfidence_RoutesToOnLowConfidenceHandler",
                "Strategos.Generators.Behavioral.Tests/ForkPathConfidenceTests.cs"),
            ["OnLowConfidence(fork-path)"] = new(
                "ForkPathConfidenceTests.Saga_ForkPathLowConfidence_AppendsLowConfidenceRoutedEvent",
                "Strategos.Generators.Behavioral.Tests/ForkPathConfidenceTests.cs"),

            // Loop-body / nested-RepeatUntil confidence lowering (DR-5 / #145 gap B): a loop
            // body's LAST step now lowers its confidence gate into the generated loop completed
            // handler, proven behaviorally on the real host. Confidence on an INTERMEDIATE
            // (non-last) loop-body step stays deferred (see Deferred below). Before this, ALL
            // nested-RepeatUntil confidence was dropped from the IR and inert.
            ["RequireConfidence(nested-RepeatUntil)"] = new(
                "NestedRepeatUntilConfidenceTests.Saga_LoopBodyHighConfidence_ContinuesLoopEvaluation",
                "Strategos.Generators.Behavioral.Tests/NestedRepeatUntilConfidenceTests.cs"),
            ["OnLowConfidence(nested-RepeatUntil)"] = new(
                "NestedRepeatUntilConfidenceTests.Saga_LoopBodyLowConfidence_RoutesToOnLowConfidenceHandler",
                "Strategos.Generators.Behavioral.Tests/NestedRepeatUntilConfidenceTests.cs"),
            ["Compensate"] = new(
                "CompensationBehaviorTests.Saga_RetryExhaustedWithCompensate_RunsCompensationOnceAndTransitionsToFailed",
                "Strategos.Generators.Behavioral.Tests/CompensationBehaviorTests.cs"),
            ["WithRetry"] = new(
                "RetryBehaviorTests.Saga_StepWithWithRetry2_InvokesStepExactlyTwiceThenSucceeds",
                "Strategos.Generators.Behavioral.Tests/RetryBehaviorTests.cs"),
            ["WithTimeout"] = new(
                "TimeoutBehaviorTests.Saga_StepExceedsTimeout_RoutesToTimeoutPath",
                "Strategos.Generators.Behavioral.Tests/TimeoutBehaviorTests.cs"),
            ["ValidateState"] = new(
                "ValidationBehaviorTests.Saga_StepWithValidateState_GuardFails_RoutesToValidationFailedWithoutDispatchingStep",
                "Strategos.Generators.Behavioral.Tests/ValidationBehaviorTests.cs"),
            ["WithContext"] = new(
                "ContextBehaviorTests.Saga_StepWithContext_AssemblesContextAndInvokesExecuteSimilarity",
                "Strategos.Generators.Behavioral.Tests/ContextBehaviorTests.cs"),

            // --- StepConfigurationDefinition IR fields (same lowering proof as the
            //     builder method that populates each) ---
            ["ConfidenceThreshold"] = new(
                "ConfidenceBehaviorTests.Saga_HighConfidence_ProceedsOnPrimaryPath",
                "Strategos.Generators.Behavioral.Tests/ConfidenceBehaviorTests.cs"),
            ["Compensation"] = new(
                "CompensationBehaviorTests.Saga_RetryExhaustedWithCompensate_RunsCompensationOnceAndTransitionsToFailed",
                "Strategos.Generators.Behavioral.Tests/CompensationBehaviorTests.cs"),
            ["Retry"] = new(
                "RetryBehaviorTests.Saga_StepWithWithRetry2_InvokesStepExactlyTwiceThenSucceeds",
                "Strategos.Generators.Behavioral.Tests/RetryBehaviorTests.cs"),
            ["Timeout"] = new(
                "TimeoutBehaviorTests.Saga_StepExceedsTimeout_RoutesToTimeoutPath",
                "Strategos.Generators.Behavioral.Tests/TimeoutBehaviorTests.cs"),
            ["Validation"] = new(
                "ValidationBehaviorTests.Saga_StepWithValidateState_GuardFails_RoutesToValidationFailedWithoutDispatchingStep",
                "Strategos.Generators.Behavioral.Tests/ValidationBehaviorTests.cs"),
            ["Context"] = new(
                "ContextBehaviorTests.Saga_StepWithContext_AssemblesContextAndInvokesExecuteSimilarity",
                "Strategos.Generators.Behavioral.Tests/ContextBehaviorTests.cs"),
        };

    /// <summary>
    /// The set of declared step-configuration member sub-paths intentionally NOT yet
    /// lowered, each carrying its tracking issue. These keys are sub-paths of members that
    /// ARE lowered for the linear/top-level case but whose fork-path / nested-loop variants
    /// are deferred — they intentionally do NOT collide with the surface member names, so
    /// the surface-coverage assertion is unaffected; they document the known deferrals and
    /// are themselves validated to carry a tracking issue.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, DeferredEntry> Deferred =
        new Dictionary<string, DeferredEntry>(StringComparer.Ordinal)
        {
            // Confidence config on an INTERMEDIATE (non-last) fork-path step IS threaded into
            // the IR (so AGWF018 still fires on a bad threshold) but is not lowered into saga
            // routing: only a fork path's LAST step lowers its gate into the path-completed
            // handler (the moved (fork-path) entries above). An intermediate fork-path step
            // runs through the generic completed handler with no gate — structurally
            // diagnosable and still guarded by AGWF022 (DeclaredButInert / DeclaredButInertTests).
            ["RequireConfidence(fork-path-intermediate)"] = new(
                145,
                "Confidence gating on an intermediate (non-last) fork-path step is deferred to "
                + "v2.10.0 / DR-17; the config reaches the IR and is AGWF022-guarded "
                + "(DeclaredButInertTests), so it is structurally diagnosable."),
            ["OnLowConfidence(fork-path-intermediate)"] = new(
                145,
                "OnLowConfidence routing on an intermediate (non-last) fork-path step is deferred "
                + "to v2.10.0 / DR-17; the config reaches the IR and is AGWF022-guarded "
                + "(DeclaredButInertTests), so it is structurally diagnosable."),
            // Loop-body / nested-RepeatUntil confidence on an INTERMEDIATE (non-last) loop-body
            // step is threaded into the IR (task 009 promoted the loop body to configured
            // StepModel records on LoopModel.BodySteps) but is not lowered into saga routing:
            // only a loop body's LAST step lowers its gate into the loop completed handler (the
            // moved (nested-RepeatUntil) entries above). An intermediate loop-body step is
            // structurally diagnosable and guarded by the declared-but-inert diagnostic
            // (DeclaredButInertTests) — no longer the silently-inert, undiagnosable case it was
            // (#145 gap B).
            ["RequireConfidence(nested-RepeatUntil-intermediate)"] = new(
                145,
                "Confidence gating on an intermediate (non-last) loop-body step is deferred to "
                + "v2.10.0 / DR-17; the config reaches the IR (LoopModel.BodySteps) and is "
                + "declared-but-inert-guarded (DeclaredButInertTests), so it is structurally "
                + "diagnosable."),
            ["OnLowConfidence(nested-RepeatUntil-intermediate)"] = new(
                145,
                "OnLowConfidence routing on an intermediate (non-last) loop-body step is deferred "
                + "to v2.10.0 / DR-17; the config reaches the IR (LoopModel.BodySteps) and is "
                + "declared-but-inert-guarded (DeclaredButInertTests), so it is structurally "
                + "diagnosable."),
        };

    /// <summary>
    /// Asserts every member of the declared step-configuration surface (builder methods and
    /// IR fields) is classified in exactly one of <see cref="Lowered"/> or
    /// <see cref="Deferred"/>, that each lowered entry names a behavioral proof, and that
    /// each deferred entry carries a tracking issue.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StepConfigParity_EveryMember_IsLoweredOrDeferred()
    {
        var members = EnumerateConfigSurface().ToList();

        var (unclassified, doubleClassified) = Classify(members);

        await Assert.That(unclassified)
            .IsEmpty()
            .Because(
                "every declared step-config member must be classified as Lowered (with a " +
                "behavioral proof) or Deferred (with a tracking issue); unclassified: " +
                string.Join(", ", unclassified));

        await Assert.That(doubleClassified)
            .IsEmpty()
            .Because(
                "a member must be in EXACTLY one set; double-classified: " +
                string.Join(", ", doubleClassified));

        // Every Lowered entry must name a behavioral proof (not a shape/golden test) that
        // actually RUNS. A substring search over the proof file is not a forcing function:
        // a suppressed test, a commented-out test and a name that only appears inside a
        // <see cref> all contain the method name and all three prove nothing. The inspector
        // parses the file and requires a real test-method declaration a default run
        // executes. (Parse-level check; we deliberately do NOT add a project reference to
        // the behavioral suite, which would pull Testcontainers/Marten.)
        var solutionRoot = FindSolutionRoot();
        foreach (var (member, proof) in Lowered)
        {
            await Assert.That(proof.BehavioralTest)
                .IsNotEmpty()
                .Because($"Lowered member '{member}' must reference a behavioral lowering proof");
            await Assert.That(proof.BehavioralTestFile)
                .Contains("Behavioral.Tests")
                .Because($"Lowered member '{member}' proof must live in the behavioral suite, not a shape test");

            // The reference is "ClassName.MethodName"; the method name is the last segment.
            var methodName = proof.BehavioralTest.Split('.').Last();
            var proofPath = Path.Combine(solutionRoot, proof.BehavioralTestFile);
            var inspection = BehavioralProofInspector.InspectFile(proofPath, methodName);

            await Assert.That(inspection.Status)
                .IsEqualTo(BehavioralProofStatus.Running)
                .Because(
                    $"Lowered member '{member}' names behavioral proof '{proof.BehavioralTest}' in "
                    + $"'{proof.BehavioralTestFile}', which must be a test that actually runs — "
                    + inspection.Detail);
        }

        // Every Deferred entry must carry a tracking issue.
        foreach (var (member, entry) in Deferred)
        {
            await Assert.That(entry.TrackingIssue)
                .IsGreaterThan(0)
                .Because($"Deferred member '{member}' must carry a tracking issue number");
        }
    }

    /// <summary>
    /// Negative guard for the forcing function: a synthetic config surface that includes an
    /// UNCLASSIFIED member must be flagged by the same classification logic, and a member
    /// placed in BOTH sets must be flagged as double-classified. This proves the guard fails
    /// when a new (or mis-classified) member appears, without having to actually add a real
    /// member to the production surface.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StepConfigParity_UnclassifiedOrDoubleClassifiedMember_IsFlagged()
    {
        // A synthetic surface: two real classified members plus one fabricated member that
        // appears in NEITHER set, and one that appears in BOTH.
        var syntheticSurface = new[]
        {
            "WithRetry",                 // classified (Lowered)
            "RequireConfidence",         // classified (Lowered)
            "WithBrandNewUnloweredKnob", // unclassified -> must be flagged
        };

        var (unclassified, _) = Classify(syntheticSurface);

        await Assert.That(unclassified)
            .Contains("WithBrandNewUnloweredKnob")
            .Because("a member in neither Lowered nor Deferred must be reported as unclassified");

        await Assert.That(unclassified)
            .DoesNotContain("WithRetry")
            .Because("a classified member must NOT be reported as unclassified");

        // A member present in both sets must be reported as double-classified.
        var doubleSurface = new[] { "WithRetry" };
        var doubleSets = new HashSet<string>(StringComparer.Ordinal) { "WithRetry" };
        var (_, doubleClassified) = Classify(
            doubleSurface,
            loweredKeys: doubleSets,
            deferredKeys: doubleSets);

        await Assert.That(doubleClassified)
            .Contains("WithRetry")
            .Because("a member present in BOTH sets must be reported as double-classified");
    }

    /// <summary>
    /// Negative guard for the PROOF check: the guard must reject a named proof that does not
    /// run. A suppressed test, a commented-out test, a name that only appears inside a
    /// <c>&lt;see cref&gt;</c> and a plain helper that happens to share the name all satisfy
    /// a substring search, and all four prove nothing — which is how provably-false parity
    /// entries stood. Each case is exercised against a SYNTHETIC fixture file whose skip
    /// state this repository owns, never against a real test in the behavioral suite: a live
    /// test's skip is removed the moment the defect it is blocked on is fixed, which would
    /// silently invert this assertion into a tautology.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StepConfigParity_NamedProofThatDoesNotRun_IsRejected()
    {
        var fixturePath = SyntheticProofFixturePath();

        // A real, running proof is the one and only accepted shape.
        var running = BehavioralProofInspector.InspectFile(
            fixturePath, "Saga_ShipOrder_CompletesOnRealHost");
        await Assert.That(running.Status)
            .IsEqualTo(BehavioralProofStatus.Running)
            .Because("a test method a default run executes is a real proof and must be accepted");
        await Assert.That(running.IsRunningProof)
            .IsTrue()
            .Because("the accepted status must be the one the parity guard keys on");

        // A skipped test: present in the file text, but it can never fail.
        var skipped = BehavioralProofInspector.InspectFile(
            fixturePath, "Saga_RefundOrder_ReleasesPayment");
        await Assert.That(skipped.Status)
            .IsEqualTo(BehavioralProofStatus.Suppressed)
            .Because("a skipped test never runs, so it cannot prove a lowering");

        // An opt-in-only test is excluded from a default run for the same reason.
        var explicitOnly = BehavioralProofInspector.InspectFile(
            fixturePath, "Saga_CancelOrder_RestocksInventory");
        await Assert.That(explicitOnly.Status)
            .IsEqualTo(BehavioralProofStatus.Suppressed)
            .Because("an opt-in-only test is not executed by a default run, so it cannot prove a lowering");

        // A class-level suppression suppresses every test the class declares.
        var suppressedByClass = BehavioralProofInspector.InspectFile(
            fixturePath, "Saga_SettleOrder_PostsLedgerEntry");
        await Assert.That(suppressedByClass.Status)
            .IsEqualTo(BehavioralProofStatus.Suppressed)
            .Because("a suppressed declaring class suppresses every test inside it");

        // A name that exists only inside a doc-comment reference.
        var docCommentOnly = BehavioralProofInspector.InspectFile(
            fixturePath, "Saga_ChargePayment_EmitsReceipt");
        await Assert.That(docCommentOnly.Status)
            .IsEqualTo(BehavioralProofStatus.ReferencedButNotDeclared)
            .Because("a name that appears only in a <see cref> is a reference, not a proof");

        // A commented-out test is likewise text without a declaration.
        var commentedOut = BehavioralProofInspector.InspectFile(
            fixturePath, "Saga_ValidateOrder_RejectsUnknownSku");
        await Assert.That(commentedOut.Status)
            .IsEqualTo(BehavioralProofStatus.ReferencedButNotDeclared)
            .Because("a commented-out test is text, not a declaration, and never runs");

        // A declared method with no test attribute is not a test at all.
        var notATest = BehavioralProofInspector.InspectFile(
            fixturePath, "Saga_ArchiveOrder_PurgesState");
        await Assert.That(notATest.Status)
            .IsEqualTo(BehavioralProofStatus.NotAnExecutableTest)
            .Because("a declared method with no test attribute is never executed by a runner");

        // A name that is not in the file at all — the stale/typo'd reference case.
        var absent = BehavioralProofInspector.InspectFile(
            fixturePath, "Saga_DispatchOrder_NotifiesCarrier");
        await Assert.That(absent.Status)
            .IsEqualTo(BehavioralProofStatus.Absent)
            .Because("a proof name absent from the file it names is a stale reference");

        // None of the rejected shapes may be reported as a running proof.
        foreach (var rejected in new[]
                 {
                     skipped, explicitOnly, suppressedByClass, docCommentOnly,
                     commentedOut, notATest, absent,
                 })
        {
            await Assert.That(rejected.IsRunningProof)
                .IsFalse()
                .Because($"a non-running proof must not be accepted: {rejected.Detail}");
        }
    }

    /// <summary>
    /// A referenced proof file that is not on disk is reported as missing rather than being
    /// silently treated as containing the proof.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StepConfigParity_ProofFileNotOnDisk_IsRejected()
    {
        var missingPath = Path.Combine(
            FindSolutionRoot(),
            "Strategos.Generators.Behavioral.Tests",
            "NoSuchProofFileTests.cs");

        var inspection = BehavioralProofInspector.InspectFile(missingPath, "Saga_ShipOrder_CompletesOnRealHost");

        await Assert.That(inspection.Status)
            .IsEqualTo(BehavioralProofStatus.FileMissing)
            .Because("a proof file that does not exist cannot contain a proof");
        await Assert.That(inspection.IsRunningProof)
            .IsFalse()
            .Because("a missing proof file must not be accepted as a running proof");
    }

    /// <summary>
    /// Resolves the synthetic proof fixture used by the guard's negative tests. It is stored
    /// with a non-compiling extension so its deliberately skipped and commented-out methods
    /// are never built into this project.
    /// </summary>
    /// <returns>The absolute path to the synthetic proof fixture file.</returns>
    private static string SyntheticProofFixturePath() => Path.Combine(
        FindSolutionRoot(),
        "Strategos.Generators.Tests",
        "Fixtures",
        "ParityGuard",
        "BehavioralProofFixture.cs.txt");

    /// <summary>
    /// Splits the supplied member names into the unclassified (in neither set) and
    /// double-classified (in both sets) buckets, using the supplied Lowered/Deferred key
    /// sets (defaulting to the production allowlists).
    /// </summary>
    /// <param name="members">The member names to classify.</param>
    /// <param name="loweredKeys">Override for the Lowered key set (defaults to <see cref="Lowered"/>).</param>
    /// <param name="deferredKeys">Override for the Deferred key set (defaults to <see cref="Deferred"/>).</param>
    /// <returns>The unclassified and double-classified member names.</returns>
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
    /// Walks up from the running test assembly's directory to the solution root — the directory
    /// containing <c>strategos.sln</c> (the <c>src</c> dir) — so the relative
    /// <see cref="LoweredProof.BehavioralTestFile"/> paths can be resolved at test runtime
    /// regardless of the build output layout.
    /// </summary>
    /// <returns>The absolute path to the solution root directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no ancestor contains <c>strategos.sln</c>.</exception>
    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "strategos.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the solution root (no ancestor of "
            + $"'{AppContext.BaseDirectory}' contains strategos.sln).");
    }

    /// <summary>
    /// Enumerates the declared step-configuration surface: the public instance methods of
    /// <see cref="IStepConfiguration{TState}"/> and the public instance properties of
    /// <see cref="StepConfigurationDefinition"/> that represent configurable state. Static
    /// members and the inherited <see cref="object"/> members are excluded. Names are
    /// returned distinct so overloads collapse to a single classifiable unit.
    /// </summary>
    /// <returns>The distinct set of classifiable member names.</returns>
    private static IEnumerable<string> EnumerateConfigSurface()
    {
        var builderMethods = typeof(IStepConfiguration<>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name);

        var definitionFields = typeof(StepConfigurationDefinition)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name);

        return builderMethods.Concat(definitionFields).Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    /// A lowered-member proof: the behavioral test name and the file it lives in.
    /// </summary>
    /// <param name="BehavioralTest">The behavioral test method that proves the lowering.</param>
    /// <param name="BehavioralTestFile">The behavioral test file (must be in the behavioral suite).</param>
    private sealed record LoweredProof(string BehavioralTest, string BehavioralTestFile);

    /// <summary>
    /// A deferred-member entry: the tracking issue and a short reason.
    /// </summary>
    /// <param name="TrackingIssue">The GitHub issue number tracking the deferral.</param>
    /// <param name="Reason">A short human-readable reason for the deferral.</param>
    private sealed record DeferredEntry(int TrackingIssue, string Reason);
}

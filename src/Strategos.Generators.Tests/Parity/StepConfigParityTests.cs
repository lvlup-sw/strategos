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
            // Fork-path confidence config IS threaded into the IR (so AGWF018 still fires on a
            // bad threshold) but is not lowered into saga routing — it is structurally
            // diagnosable and is guarded by AGWF022 (DeclaredButInert / DeclaredButInertTests).
            ["RequireConfidence(fork-path)"] = new(
                134,
                "Confidence gating on a fork path is deferred to v2.10.0 / DR-17; the config "
                + "reaches the IR and is AGWF022-guarded (DeclaredButInertTests), so it is "
                + "structurally diagnosable."),
            ["OnLowConfidence(fork-path)"] = new(
                134,
                "OnLowConfidence routing on a fork path is deferred to v2.10.0 / DR-17; the "
                + "config reaches the IR and is AGWF022-guarded (DeclaredButInertTests), so it "
                + "is structurally diagnosable."),
            // Distinct from the fork-path case above: loop-body / nested-RepeatUntil confidence
            // config is DROPPED from the IR entirely by step extraction, so an IR-based
            // diagnostic structurally CANNOT see it — it is NOT AGWF022-guarded. Silently inert,
            // tracked under #134 for v2.10.0.
            ["OnLowConfidence(nested-RepeatUntil)"] = new(
                134,
                "OnLowConfidence inside a nested RepeatUntil loop is dropped from the IR by step "
                + "extraction (structurally undiagnosable — no AGWF022), deferred to "
                + "v2.10.0 / DR-17."),
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
        // actually EXISTS: the referenced file must be on disk AND the named test method must
        // appear in it. A non-empty string alone is not a forcing function — a stale/typo'd
        // reference would silently pass. (Grep-level check; we deliberately do NOT add a
        // project reference to the behavioral suite, which would pull Testcontainers/Marten.)
        var solutionRoot = FindSolutionRoot();
        foreach (var (member, proof) in Lowered)
        {
            await Assert.That(proof.BehavioralTest)
                .IsNotEmpty()
                .Because($"Lowered member '{member}' must reference a behavioral lowering proof");
            await Assert.That(proof.BehavioralTestFile)
                .Contains("Behavioral.Tests")
                .Because($"Lowered member '{member}' proof must live in the behavioral suite, not a shape test");

            var proofPath = Path.Combine(solutionRoot, proof.BehavioralTestFile);
            await Assert.That(File.Exists(proofPath))
                .IsTrue()
                .Because($"Lowered member '{member}' references behavioral proof file '{proof.BehavioralTestFile}', which must exist on disk at '{proofPath}'");

            // The reference is "ClassName.MethodName"; the method name is the last segment.
            var methodName = proof.BehavioralTest.Split('.').Last();
            var fileText = File.ReadAllText(proofPath);
            await Assert.That(fileText.Contains(methodName, StringComparison.Ordinal))
                .IsTrue()
                .Because($"Lowered member '{member}' references behavioral test method '{methodName}', which must appear in '{proof.BehavioralTestFile}'");
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

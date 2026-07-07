// =============================================================================
// <copyright file="LossinessMarkerTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Strategos.Contracts;
using Strategos.Contracts.SchemaDiff;

namespace Strategos.Tests.Contracts;

/// <summary>
/// The DR-14 (marking half) lossiness guard: the honest generalization of the
/// LB-1 lossy class beyond delegate steps. Where a builder-IR concept cannot be
/// carried on the declarative wire, the projection must make the loss
/// <b>visible</b> — either by a wire <b>marker</b> (like the delegate step's
/// <c>lambda: true</c>, and now the approval's <c>hasContext: true</c>) or by a
/// <b>presence rule</b> (the carrier's shape survives on the wire, so the drop is
/// recoverable without a marker: branch points, loops, and validation
/// predicates).
/// </summary>
/// <remarks>
/// <para>
/// Modeled on <see cref="ProjectionExhaustivenessTests"/> /
/// <see cref="ProjectionStepKindMappingTests"/>: the keystone is a
/// <b>parity-style exhaustiveness guard</b> (T-LM4) that reflects over the
/// approval-configuration surface — the surface the existing exhaustiveness test
/// does <em>not</em> descend into — and forces <b>every</b> member to be either
/// (a) demonstrably marker-covered (proven behaviorally: setting it drives the
/// <c>hasContext</c> marker into the emitted JSON) or (b) on the documented
/// exclusion allow-list. A new approval-config field that is silently dropped
/// (neither marked nor allow-listed) fails the guard.
/// </para>
/// <para>
/// The marker's scope is a load-bearing decision (DR-14, spec lines 148/175/185):
/// the wire drops the approval's <b>context body</b> entirely and carries no
/// static-context data field, so <c>hasContext</c> marks the presence of
/// <em>any</em> configured context — a static message
/// (<see cref="ApprovalConfiguration.StaticContext"/>) or a runtime context
/// factory (<see cref="ApprovalConfiguration.ContextFactoryExpression"/>). This
/// is what lets the import front-end (DR-14 rejection half) distinguish a
/// context-free approval point (importable) from a context-bearing one
/// (rejected), and is why the #53 corpus's static-context
/// <c>awaitApproval</c> fixtures are a non-vacuous rejection bucket.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public class LossinessMarkerTests
{
    /// <summary>The indented-JSON needle proving the marker was emitted at true.</summary>
    private const string HasContextMarkerNeedle = "\"hasContext\": true";

    /// <summary>A marker approver role — the CLR type is irrelevant to the marker.</summary>
    private sealed class TestApprover;

    // -------------------------------------------------------------------------
    // EXPLICIT, DOCUMENTED EXCLUSION ALLOW-LIST
    //
    // An ApprovalConfiguration member appears here iff its loss is INTENTIONALLY
    // not surfaced by the hasContext marker. Every entry carries a one-line
    // justification. A future approval-config field that is neither marker-covered
    // (proven below) nor listed here fails the exhaustiveness guard — forcing a
    // deliberate mark-or-exclude decision (the DR-14 discipline).
    // -------------------------------------------------------------------------
    private static readonly IReadOnlyDictionary<string, string> ConfigExclusions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Declarative approval category (ApprovalType); not carried on the wire
            // in 0.2.0 — a deferred data field, recoverable from the approval shape.
            [nameof(ApprovalConfiguration.Type)] =
                "Declarative approval category (ApprovalType); not carried on the wire in 0.2.0 (deferred data field).",

            // Runtime approval timeout — a runtime-execution concern, not part of
            // the declarative wire structure in 0.2.0.
            [nameof(ApprovalConfiguration.Timeout)] =
                "Runtime approval timeout; a runtime-execution concern, not the declarative wire structure in 0.2.0.",

            // Approver-UI option list — a runtime/UI concern, not carried on the
            // declarative wire in 0.2.0.
            [nameof(ApprovalConfiguration.Options)] =
                "Approver-UI option list; a runtime/UI concern, not carried on the declarative wire in 0.2.0.",

            // Approval UI metadata — a runtime/UI concern, not carried on the
            // declarative wire in 0.2.0.
            [nameof(ApprovalConfiguration.StaticMetadata)] =
                "Approval UI metadata; a runtime/UI concern, not carried on the declarative wire in 0.2.0.",

            // Runtime metadata factory expressions (CLR lambdas captured as strings)
            // — a separate LB-1-class lossy carrier deferred with no wire marker in
            // 0.2.0; recorded here rather than silently dropped.
            [nameof(ApprovalConfiguration.DynamicMetadataExpressions)] =
                "Runtime metadata factory expressions (CLR lambdas); a separate LB-1-class carrier deferred with no marker in 0.2.0.",
        };

    /// <summary>
    /// ApprovalConfiguration members whose presence must drive the single
    /// <c>hasContext</c> marker. Each maps a member name to a probe config that
    /// sets <b>only</b> that member to a non-default value, so the guard proves the
    /// member individually surfaces the marker.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ApprovalConfiguration> MarkerProbes =
        new Dictionary<string, ApprovalConfiguration>(StringComparer.Ordinal)
        {
            [nameof(ApprovalConfiguration.StaticContext)] =
                new() { StaticContext = "probe-static-context" },

            [nameof(ApprovalConfiguration.ContextFactoryExpression)] =
                new() { ContextFactoryExpression = "state => \"probe-dynamic-context\"" },
        };

    // -------------------------------------------------------------------------
    // Part A — marker contract (behavioral, exercised through the public DSL).
    // -------------------------------------------------------------------------

    /// <summary>
    /// T-LM1 — a static-context approval
    /// (<see cref="Strategos.Abstractions.IApprovalBuilder{TState, TApprover}.WithContext"/>)
    /// projects with the <c>hasContext: true</c> marker.
    /// </summary>
    [Test]
    public async Task StaticContextApproval_EmitsHasContextTrueMarker()
    {
        var workflow = Workflow<TestWorkflowState>.Create("approval-static-context")
            .StartWith<ValidateStep>()
            .AwaitApproval<TestApprover>(a => a.WithContext("please approve this request"))
            .Finally<CompleteStep>();

        var json = ContractsJson.Serialize(workflow.ToContract());

        await Assert.That(json.Contains(HasContextMarkerNeedle, StringComparison.Ordinal)).IsTrue()
            .Because("a configured static approval context is lossy on the wire; its presence must be marked by hasContext:true (DR-14).");
    }

    /// <summary>
    /// T-LM2 — a dynamic context-factory approval
    /// (<see cref="Strategos.Abstractions.IApprovalBuilder{TState, TApprover}.WithContextFrom"/>)
    /// projects with the <c>hasContext: true</c> marker; the factory body itself is
    /// dropped (LB-1) exactly like the delegate step's lambda body.
    /// </summary>
    [Test]
    public async Task DynamicContextFactoryApproval_EmitsHasContextTrueMarker()
    {
        var workflow = Workflow<TestWorkflowState>.Create("approval-dynamic-context")
            .StartWith<ValidateStep>()
            .AwaitApproval<TestApprover>(a => a.WithContextFrom(s => $"Order {s.OrderId} needs sign-off"))
            .Finally<CompleteStep>();

        var json = ContractsJson.Serialize(workflow.ToContract());

        await Assert.That(json.Contains(HasContextMarkerNeedle, StringComparison.Ordinal)).IsTrue()
            .Because("a runtime context factory is the CLR-lambda LB-1 loss; its presence must be marked by hasContext:true (DR-14).");
    }

    /// <summary>
    /// T-LM3 — a context-free approval point carries <b>no</b> marker: the additive
    /// field is omitted so the addition stays non-breaking and a context-free
    /// approval remains in the importable subset (DR-14 rejection half keys off the
    /// marker's absence).
    /// </summary>
    [Test]
    public async Task ContextFreeApproval_OmitsHasContextMarker()
    {
        var workflow = Workflow<TestWorkflowState>.Create("approval-context-free")
            .StartWith<ValidateStep>()
            .AwaitApproval<TestApprover>(a => a.WithTimeout(TimeSpan.FromHours(2)))
            .Finally<CompleteStep>();

        var json = ContractsJson.Serialize(workflow.ToContract());

        await Assert.That(json.Contains("hasContext", StringComparison.Ordinal)).IsFalse()
            .Because("a context-free approval point has nothing lossy to mark; hasContext must be omitted, not emitted false.");
    }

    // -------------------------------------------------------------------------
    // Part B — the keystone: parity-style exhaustiveness over the approval-config
    // surface (mirrors ProjectionExhaustivenessTests, extended to the surface it
    // does not descend into).
    // -------------------------------------------------------------------------

    /// <summary>
    /// T-LM4 — every public settable <see cref="ApprovalConfiguration"/> member is
    /// <b>either</b> demonstrably marker-covered (proven: setting it drives
    /// <c>hasContext: true</c> into the emitted JSON) <b>or</b> on the documented
    /// exclusion allow-list. A new approval-config field that is silently dropped
    /// (neither marked nor allow-listed) fails here.
    /// </summary>
    [Test]
    public async Task EveryApprovalConfigMember_IsMarkerCoveredOrExplicitlyExcluded()
    {
        var markerCovered = MarkerCoveredMemberNames();

        foreach (var prop in SettableProperties(typeof(ApprovalConfiguration)))
        {
            var isMarked = markerCovered.Contains(prop.Name);
            var isExcluded = ConfigExclusions.ContainsKey(prop.Name);

            await Assert.That(isMarked || isExcluded).IsTrue()
                .Because(
                    $"ApprovalConfiguration.{prop.Name} is neither surfaced by the hasContext marker " +
                    "nor on the documented exclusion allow-list. Either drive it into " +
                    "WorkflowDefinitionProjection.HasApprovalContext (and prove it emits hasContext:true) " +
                    "or add it to ConfigExclusions with a one-line justification (DR-14).");

            // A member must not be BOTH marked and excluded — a stale allow-list
            // entry that lies about a field the marker actually covers.
            await Assert.That(isMarked && isExcluded).IsFalse()
                .Because(
                    $"ApprovalConfiguration.{prop.Name} is on the exclusion allow-list yet its value " +
                    "surfaces the hasContext marker — the allow-list entry is stale and must be removed.");
        }
    }

    /// <summary>
    /// T-LM5 — both context members individually drive the marker. Pins the marker
    /// scope (static <em>and</em> dynamic context) so a narrowing that drops one of
    /// them fails loudly rather than silently.
    /// </summary>
    [Test]
    public async Task BothContextMembers_IndividuallyDriveTheHasContextMarker()
    {
        var markerCovered = MarkerCoveredMemberNames();

        await Assert.That(markerCovered).Contains(nameof(ApprovalConfiguration.StaticContext))
            .Because("a static context message is lossy on the wire and must drive the hasContext marker.");
        await Assert.That(markerCovered).Contains(nameof(ApprovalConfiguration.ContextFactoryExpression))
            .Because("a runtime context factory is lossy on the wire and must drive the hasContext marker.");
    }

    /// <summary>
    /// T-LM6 — the allow-list and probe map may only name members that exist on
    /// <see cref="ApprovalConfiguration"/>. Guards against an entry rotting after a
    /// builder rename.
    /// </summary>
    [Test]
    public async Task ExclusionAndProbeMaps_OnlyNameExistingMembers()
    {
        var members = SettableProperties(typeof(ApprovalConfiguration))
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var name in ConfigExclusions.Keys)
        {
            await Assert.That(members.Contains(name)).IsTrue()
                .Because($"ConfigExclusions names '{name}', not a settable member of ApprovalConfiguration.");
        }

        foreach (var name in MarkerProbes.Keys)
        {
            await Assert.That(members.Contains(name)).IsTrue()
                .Because($"MarkerProbes names '{name}', not a settable member of ApprovalConfiguration.");
        }
    }

    /// <summary>
    /// T-LM7 — every exclusion carries a non-empty justification, so the allow-list
    /// stays self-documenting.
    /// </summary>
    [Test]
    public async Task EveryExclusion_HasNonEmptyJustification()
    {
        foreach (var (name, reason) in ConfigExclusions)
        {
            await Assert.That(string.IsNullOrWhiteSpace(reason)).IsFalse()
                .Because($"ConfigExclusions['{name}'] must carry a justification.");
        }
    }

    // -------------------------------------------------------------------------
    // Part C — presence rules: the CLR-predicate carriers whose shape survives on
    // the wire, so their drop is recoverable WITHOUT a marker (DR-14). Each pairs a
    // projection drop-site with its presence rule.
    // -------------------------------------------------------------------------

    /// <summary>
    /// T-LM8 — a branch point's CLR discriminator predicate is dropped, but the
    /// branch <b>shape</b> (a <c>branchPoints</c> entry with its paths) is present
    /// on the wire — detectable by presence, no marker needed.
    /// </summary>
    [Test]
    public async Task BranchPoint_IsDetectableByShapePresence()
    {
        var workflow = Workflow<TestWorkflowState>.Create("presence-branch")
            .StartWith<ValidateStep>()
            .Branch(
                s => s.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto, p => p.Then<AutoProcessStep>()),
                BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(
                    p => p.Then<ManualProcessStep>()))
            .Finally<CompleteStep>();

        var v1 = workflow.ToContract();

        await Assert.That(v1.BranchPoints.Count).IsGreaterThan(0)
            .Because("a branch point is detectable by the branchPoints shape on the wire — no marker needed (DR-14).");
    }

    /// <summary>
    /// T-LM9 — a loop's CLR exit condition is dropped, but the loop <b>shape</b>
    /// (a <c>loops</c> entry with its body) is present on the wire — detectable by
    /// presence, no marker needed.
    /// </summary>
    [Test]
    public async Task Loop_IsDetectableByShapePresence()
    {
        var workflow = Workflow<TestWorkflowState>.Create("presence-loop")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                s => s.QualityScore >= 0.9m,
                "Refine",
                loop => loop.Then<CritiqueStep>(),
                maxIterations: 3)
            .Finally<CompleteStep>();

        var v1 = workflow.ToContract();

        await Assert.That(v1.Loops.Count).IsGreaterThan(0)
            .Because("a loop is detectable by the loops shape on the wire — no marker needed (DR-14).");
    }

    /// <summary>
    /// T-LM10 — a validation predicate surfaces on the wire as a descriptive
    /// <c>predicateExpression</c> string — detectable by presence, no marker needed.
    /// </summary>
    [Test]
    public async Task ValidationPredicate_IsCarriedAsDescriptiveString()
    {
        var workflow = Workflow<TestWorkflowState>.Create("presence-validation")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(step => step.ValidateState(s => s.IterationCount > 0, "must iterate at least once"))
            .Finally<CompleteStep>();

        var json = ContractsJson.Serialize(workflow.ToContract());

        await Assert.That(json.Contains("predicateExpression", StringComparison.Ordinal)).IsTrue()
            .Because("a validation predicate is carried as a descriptive predicateExpression string — detectable by presence, no marker needed (DR-14).");
    }

    // -------------------------------------------------------------------------
    // Part D — the marker is additive (JsonSchemaDiff NON-BREAKING).
    // -------------------------------------------------------------------------

    /// <summary>
    /// T-LM11 — adding the optional <c>hasContext</c> marker to the approval schema
    /// is a NON-BREAKING change: it lands in <c>properties</c> but never in
    /// <c>required</c>, so existing producers/consumers are unaffected.
    /// </summary>
    [Test]
    public async Task HasContextMarker_IsAdditive_NonBreaking()
    {
        const string previous =
            """
            {
              "$id": "ApprovalDefinition.json",
              "type": "object",
              "properties": {
                "approvalPointId": { "type": "string" },
                "approverType": { "type": "string" },
                "precedingStepId": { "type": "string" }
              },
              "required": ["approvalPointId", "approverType", "precedingStepId"]
            }
            """;

        const string next =
            """
            {
              "$id": "ApprovalDefinition.json",
              "type": "object",
              "properties": {
                "approvalPointId": { "type": "string" },
                "approverType": { "type": "string" },
                "precedingStepId": { "type": "string" },
                "hasContext": { "type": "boolean", "const": true }
              },
              "required": ["approvalPointId", "approverType", "precedingStepId"]
            }
            """;

        var result = JsonSchemaDiff.Compare(previous, next);

        await Assert.That(result.HasBreakingChanges).IsFalse()
            .Because("hasContext is optional (never in required) — an additive marker must be non-breaking (DR-14).");
        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking);
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.NonBreaking
                && c.Description.Contains("hasContext", StringComparison.Ordinal));
    }

    // -------------------------------------------------------------------------
    // Helpers.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs each <see cref="MarkerProbes"/> config through the projection and
    /// returns the member names whose probe demonstrably surfaced the
    /// <c>hasContext: true</c> marker in the emitted JSON.
    /// </summary>
    private static IReadOnlySet<string> MarkerCoveredMemberNames()
    {
        var marked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, config) in MarkerProbes)
        {
            if (ProjectSingleApprovalJson(config).Contains(HasContextMarkerNeedle, StringComparison.Ordinal))
            {
                marked.Add(name);
            }
        }

        return marked;
    }

    /// <summary>
    /// Projects a single approval carrying <paramref name="config"/> through the
    /// public <see cref="WorkflowDefinitionProjection.ToContract{TState}"/> seam and
    /// returns the canonical JSON. Isolating one approval keeps the marker signal
    /// unambiguous.
    /// </summary>
    private static string ProjectSingleApprovalJson(ApprovalConfiguration config)
    {
        var approval = ApprovalDefinition.Create(typeof(TestApprover), config, "preceding-step");
        var workflow = WorkflowDefinition<TestWorkflowState>.Create("marker-probe")
            .WithApprovalPoints([approval]);

        return ContractsJson.Serialize(workflow.ToContract());
    }

    /// <summary>
    /// The public, instance data properties of a type — the consumer-populatable
    /// surface the projection must account for (mirrors
    /// <see cref="ProjectionExhaustivenessTests"/>). Static members (e.g.
    /// <see cref="ApprovalConfiguration.Default"/>) and indexers are excluded.
    /// </summary>
    private static IEnumerable<PropertyInfo> SettableProperties(Type type)
    {
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            yield return prop;
        }
    }
}

// =============================================================================
// <copyright file="ForkEdgeFixtureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.Json;

using NJsonSchema;

using Strategos.Contracts;
using Strategos.Contracts.SchemaDiff;

using BuilderForkDef = Strategos.Definitions.DiagnosticForkDefinition;
using BuilderPermittedTrigger = Strategos.Definitions.PermittedForkTriggerDefinition;
using ForkTrigger = Strategos.Contracts.Generated.ForkTrigger;

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// DR-10 (projection half, #151 → #100) — the EXPORT companion to the wire-contract
/// shape (<c>DiagnosticForkSchemaTests</c>) and the builder surface
/// (<c>DiagnosticForkBuilderTests</c>). A new fork-edge fixture family that extends the
/// #53 corpus: real <c>AllowDiagnosticFork</c> builder invocations, each projected
/// through <c>ToContract()</c> and serialized by the contracts canonical serializer,
/// then (a) round-tripped field-by-field, (b) exported and validated against the bundled
/// <c>workflow-definition-v1.schema.json</c> equivalence-gate schema, and (c) proven to
/// leave the fork slot a NON-BREAKING addition on that same fixture-gate schema.
/// </summary>
/// <remarks>
/// <para>
/// The eight-combinator #53 corpus (<see cref="WorkflowCorpus"/>) predates the DR-7
/// fork edge and is deliberately left untouched — a fork edge is not one of its eight
/// combinator families and adding a ninth tag would perturb the export gate's
/// <c>Tags.Count == 8</c> contract. This family therefore lives in its own tree
/// (<see cref="ForkFixturesDir"/>), so it extends the corpus additively without
/// destabilizing the existing export.
/// </para>
/// <para>
/// The projection itself (<c>WorkflowDefinitionProjection.ProjectDiagnosticFork</c> /
/// <c>ProjectPermittedForkTrigger</c>) was wired by task 011; this family is the
/// export-side proof that it round-trips every fork-edge member cleanly and stays
/// schema-valid and non-breaking.
/// </para>
/// </remarks>
[Property("Category", "FixtureExport")]
[NotInParallel("fixture-export-fork")]
public sealed class ForkEdgeFixtureTests
{
    /// <summary>The corpus tag for the fork-edge fixture family.</summary>
    private const string ForkTag = "diagnosticFork";

    /// <summary>
    /// The dedicated fork-edge corpus tree — a sibling of the #53
    /// <c>builder-fixtures</c> directory so the fork family never move-aside-clobbers
    /// the eight-combinator export (and vice versa).
    /// </summary>
    private static string ForkFixturesDir =>
        Path.Combine(FixturePaths.RepoRoot, "artifacts", "builder-fixtures-fork");

    // -------------------------------------------------------------------------
    // The fork-edge fixture family — every case is a real AllowDiagnosticFork
    // builder invocation, spanning the shapes the edge can take: each closed
    // trigger, single/multiple anchors, single/multiple permitted triggers,
    // two edges on one workflow, and a fork composed with a `then` chain.
    // -------------------------------------------------------------------------

    /// <summary>Builds the fork-edge fixture family.</summary>
    private static IReadOnlyList<WorkflowCorpus.Case> ForkEdgeCases()
    {
        var cases = new List<WorkflowCorpus.Case>();

        // Single anchor / single trigger — one case per closed trigger value, so
        // every member of the ForkTrigger vocabulary round-trips by wire value.
        var triggers = new[]
        {
            ForkTrigger.RatificationFailure,
            ForkTrigger.GateContradiction,
            ForkTrigger.OperatorExplicit,
        };
        for (var i = 0; i < triggers.Length; i++)
        {
            var trigger = triggers[i];
            var n = i;
            cases.Add(new WorkflowCorpus.Case(ForkTag, $"fork-single-{trigger}",
                Workflow<TestWorkflowState>.Create($"fork-single-{n}")
                    .StartWith<ValidateStep>()
                    .AllowDiagnosticFork(fork => fork
                        .Anchor($"Anchor{n}")
                        .PermitTrigger(trigger, $"evidenceField{n}")
                        .WithCompensationSeed($"CompensationSeed{n}")
                        .MaxForks(n + 1))
                    .Finally<CompleteStep>()));
        }

        // Multiple anchors — first via the required parameter, the rest via params.
        cases.Add(new WorkflowCorpus.Case(ForkTag, "fork-multi-anchor",
            Workflow<TestWorkflowState>.Create("fork-multi-anchor")
                .StartWith<ValidateStep>()
                .Then<ProcessStep>()
                .AllowDiagnosticFork(fork => fork
                    .Anchor("RatifyDeployment", "ApproveRollout", "SealVerdict")
                    .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId")
                    .WithCompensationSeed("RollbackProvisionalStamp")
                    .MaxForks(3))
                .Finally<CompleteStep>()));

        // Multiple permitted triggers, each with its own evidence-ref schema.
        cases.Add(new WorkflowCorpus.Case(ForkTag, "fork-multi-trigger",
            Workflow<TestWorkflowState>.Create("fork-multi-trigger")
                .StartWith<ValidateStep>()
                .AllowDiagnosticFork(fork => fork
                    .Anchor("Ratify")
                    .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId")
                    .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
                    .WithCompensationSeed("RollbackRatification")
                    .MaxForks(5))
                .Finally<CompleteStep>()));

        // Two fork edges on a single workflow — both must survive independently.
        cases.Add(new WorkflowCorpus.Case(ForkTag, "fork-two-edges",
            Workflow<TestWorkflowState>.Create("fork-two-edges")
                .StartWith<ValidateStep>()
                .AllowDiagnosticFork(fork => fork
                    .Anchor("StepA")
                    .PermitTrigger(ForkTrigger.RatificationFailure, "eventId")
                    .WithCompensationSeed("SeedA")
                    .MaxForks(2))
                .AllowDiagnosticFork(fork => fork
                    .Anchor("StepB")
                    .PermitTrigger(ForkTrigger.OperatorExplicit, "operatorId")
                    .WithCompensationSeed("SeedB")
                    .MaxForks(4))
                .Finally<CompleteStep>()));

        // A fork edge composed after a linear `then` chain — the edge is orthogonal
        // to the surrounding combinators and rides the same wire document.
        cases.Add(new WorkflowCorpus.Case(ForkTag, "fork-with-chain",
            Workflow<TestWorkflowState>.Create("fork-with-chain")
                .StartWith<ValidateStep>()
                .Then<ProcessStep>()
                .Then<NotifyStep>()
                .AllowDiagnosticFork(fork => fork
                    .Anchor("Notify")
                    .PermitTrigger(ForkTrigger.GateContradiction, "gateId")
                    .WithCompensationSeed("RollbackNotify")
                    .MaxForks(1))
                .Finally<CompleteStep>()));

        return cases;
    }

    // -------------------------------------------------------------------------
    // T-FE1 — the family exports to disk and every fixture validates against the
    // bundled equivalence-gate schema (the #53 T23 gate, extended to fork edges).
    // Proves the fork family "regenerates cleanly": ToContract() → canonical JSON
    // → schema-valid, exactly as the #53 corpus export requires.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Exports the fork-edge family and validates every emitted fixture against
    /// <c>workflow-definition-v1.schema.json</c> — the fork slot and its nested
    /// shape must satisfy the same wire schema the cross-product consumers read.
    /// </summary>
    [Test]
    public async Task ForkEdgeFixtures_Export_AndValidateAgainst_WorkflowDefinitionV1Schema()
    {
        var cases = ForkEdgeCases();
        await Assert.That(cases.Count).IsGreaterThan(0)
            .Because("the fork-edge family must be populated for this gate to be meaningful.");

        var manifest = FixtureExporter.Export(cases, ForkFixturesDir);

        await Assert.That(manifest.Count).IsEqualTo(cases.Count)
            .Because("every fork-edge case must export to exactly one fixture.");

        // Every manifest entry is a real fork-tagged file on disk.
        foreach (var entry in manifest.Fixtures)
        {
            await Assert.That(entry.Tag).IsEqualTo(ForkTag);
            var path = Path.Combine(ForkFixturesDir, entry.Path);
            await Assert.That(File.Exists(path)).IsTrue()
                .Because($"fork fixture {entry.Name} must be written to {path}.");
        }

        var schemaJson = await File.ReadAllTextAsync(FixturePaths.WorkflowSchemaPath);
        var schema = await JsonSchema.FromJsonAsync(schemaJson);

        var failures = new List<string>();
        foreach (var entry in manifest.Fixtures)
        {
            var json = await File.ReadAllTextAsync(Path.Combine(ForkFixturesDir, entry.Path));
            var errors = schema.Validate(json);
            if (errors.Count > 0)
            {
                failures.Add($"{entry.Name}: {string.Join("; ", errors.Select(e => e.ToString()))}");
            }
        }

        await Assert.That(failures.Count).IsEqualTo(0)
            .Because("every projected fork-edge fixture must validate against the wire schema:\n"
                + string.Join("\n", failures));
    }

    // -------------------------------------------------------------------------
    // T-FE2 — a fully-specified fork edge round-trips EVERY member through
    // ToContract(): the typed projection reconstructs each field, and the
    // canonical JSON carries the diagnosticForks slot (INV-8 monikers + the
    // snake_case ForkTrigger wire values).
    // -------------------------------------------------------------------------

    /// <summary>
    /// A fork edge with multiple anchors and multiple permitted triggers projects
    /// with every declared component preserved — anchors, per-trigger evidence-ref
    /// schema, the <c>maxForks</c> bound, and the compensation seed — both on the
    /// typed <c>WorkflowDefinitionV1.DiagnosticForks</c> slot and in the emitted JSON.
    /// </summary>
    [Test]
    public async Task ForkEdge_FullySpecified_RoundTripsEveryFieldThroughToContract()
    {
        var workflow = Workflow<TestWorkflowState>.Create("fork-roundtrip")
            .StartWith<ValidateStep>()
            .AllowDiagnosticFork(fork => fork
                .Anchor("RatifyDeployment", "SealVerdict")
                .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId")
                .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
                .WithCompensationSeed("RollbackProvisionalStamp")
                .MaxForks(4))
            .Finally<CompleteStep>();

        var v1 = workflow.ToContract();

        await Assert.That(v1.DiagnosticForks).IsNotNull()
            .Because("a workflow with a fork edge must carry the diagnosticForks slot.");
        await Assert.That(v1.DiagnosticForks!).HasCount(1);

        var edge = v1.DiagnosticForks![0];
        await Assert.That(edge.AnchorStepIds).IsEquivalentTo(new[] { "RatifyDeployment", "SealVerdict" })
            .Because("every anchor moniker must survive projection in order.");
        await Assert.That(edge.CompensationSeed).IsEqualTo("RollbackProvisionalStamp");
        await Assert.That(edge.MaxForks).IsEqualTo(4);

        await Assert.That(edge.PermittedTriggers).HasCount(2);
        await Assert.That(edge.PermittedTriggers[0].Trigger).IsEqualTo(ForkTrigger.RatificationFailure);
        await Assert.That(edge.PermittedTriggers[0].RequiredEvidenceFields)
            .IsEquivalentTo(new[] { "provisionalStampEventId" });
        await Assert.That(edge.PermittedTriggers[1].Trigger).IsEqualTo(ForkTrigger.GateContradiction);
        await Assert.That(edge.PermittedTriggers[1].RequiredEvidenceFields)
            .IsEquivalentTo(new[] { "leftGateId", "rightGateId" });

        // And the canonical JSON carries the slot with its monikers and the closed
        // ForkTrigger *wire* values (snake_case), not the CLR enum member names.
        var json = ContractsJson.Serialize(v1);
        await Assert.That(json.Contains("\"diagnosticForks\"", StringComparison.Ordinal)).IsTrue()
            .Because("the wire document must carry the diagnosticForks slot key.");
        await Assert.That(json.Contains("RollbackProvisionalStamp", StringComparison.Ordinal)).IsTrue();
        await Assert.That(json.Contains("\"ratification_failure\"", StringComparison.Ordinal)).IsTrue()
            .Because("the trigger serializes to its snake_case wire value, not the CLR name.");
        await Assert.That(json.Contains("\"gate_contradiction\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(json.Contains("leftGateId", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>
    /// T-FE3 — every case in the family projects a populated, well-formed
    /// diagnosticForks slot: at least one edge, and each edge carries at least one
    /// anchor, at least one permitted trigger (with a non-empty evidence-ref set), a
    /// non-empty compensation seed, and a <c>maxForks</c> bound of at least 1.
    /// </summary>
    [Test]
    public async Task ForkEdgeFamily_EveryCase_ProjectsPopulatedDiagnosticForksSlot()
    {
        foreach (var c in ForkEdgeCases())
        {
            var v1 = c.Workflow.ToContract();

            await Assert.That(v1.DiagnosticForks).IsNotNull()
                .Because($"{c.Name}: a fork-edge case must project the diagnosticForks slot.");
            await Assert.That(v1.DiagnosticForks!.Count).IsGreaterThan(0)
                .Because($"{c.Name}: the fork slot must carry at least one edge.");

            foreach (var edge in v1.DiagnosticForks!)
            {
                await Assert.That(edge.AnchorStepIds.Count).IsGreaterThan(0)
                    .Because($"{c.Name}: a fork edge must anchor somewhere (INV-8 monikers).");
                await Assert.That(string.IsNullOrWhiteSpace(edge.CompensationSeed)).IsFalse()
                    .Because($"{c.Name}: the compensation seed is required and non-empty.");
                await Assert.That(edge.MaxForks).IsGreaterThanOrEqualTo(1)
                    .Because($"{c.Name}: the maxForks bound must be at least 1.");
                await Assert.That(edge.PermittedTriggers.Count).IsGreaterThan(0)
                    .Because($"{c.Name}: a fork edge must permit at least one trigger.");

                foreach (var trigger in edge.PermittedTriggers)
                {
                    await Assert.That(trigger.RequiredEvidenceFields.Count).IsGreaterThan(0)
                        .Because($"{c.Name}: each permitted trigger declares its evidence-ref schema.");
                }
            }

            // The serialized wire document carries the slot for every case.
            var json = ContractsJson.Serialize(v1);
            await Assert.That(json.Contains("\"diagnosticForks\"", StringComparison.Ordinal)).IsTrue()
                .Because($"{c.Name}: the diagnosticForks slot must appear in the wire JSON.");
        }
    }

    // -------------------------------------------------------------------------
    // T-FE4 — projection exhaustiveness DESCENT into the fork-edge sub-shape.
    // The #53 root-level exhaustiveness guard proves the DiagnosticForks slot
    // surfaces; this descends one level and forces EVERY member of the builder-IR
    // DiagnosticForkDefinition / PermittedForkTriggerDefinition to be either
    // demonstrably projected (a sentinel value reaches the wire JSON) or on a
    // documented drop allow-list. A future fork-edge field that ProjectDiagnosticFork
    // silently forgets fails here.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Members of the builder-IR fork-edge shape intentionally dropped from the
    /// wire. Currently empty: every fork-edge member is projected. A future dropped
    /// field must be listed here with a one-line justification, never silently
    /// omitted.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ForkMemberExclusions =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// T-FE4 — every public settable member of <c>DiagnosticForkDefinition</c> and
    /// <c>PermittedForkTriggerDefinition</c> is demonstrably projected (its sentinel
    /// value surfaces in the emitted JSON) or on the documented drop allow-list.
    /// </summary>
    [Test]
    public async Task EveryForkEdgeMember_IsProjectedOrExplicitlyExcluded()
    {
        var present = ProjectedForkMemberNames();

        var forkMembers = SettableProperties(typeof(BuilderForkDef))
            .Concat(SettableProperties(typeof(BuilderPermittedTrigger)));

        foreach (var prop in forkMembers)
        {
            var isProjected = present.Contains(prop.Name);
            var isExcluded = ForkMemberExclusions.ContainsKey(prop.Name);

            await Assert.That(isProjected || isExcluded).IsTrue()
                .Because(
                    $"fork-edge member {prop.DeclaringType!.Name}.{prop.Name} is neither projected " +
                    "into the wire output nor on the documented drop allow-list. Either wire it into " +
                    "WorkflowDefinitionProjection.ProjectDiagnosticFork/ProjectPermittedForkTrigger " +
                    "(and prove it surfaces) or add it to ForkMemberExclusions with a justification.");

            await Assert.That(isProjected && isExcluded).IsFalse()
                .Because(
                    $"{prop.DeclaringType!.Name}.{prop.Name} is on the drop allow-list yet its value " +
                    "surfaces on the wire — the allow-list entry is stale.");
        }
    }

    /// <summary>
    /// T-FE5 — the drop allow-list may only name members that exist on the fork-edge
    /// builder-IR shape, guarding against an entry rotting after a rename.
    /// </summary>
    [Test]
    public async Task ForkMemberExclusions_OnlyNameExistingMembers()
    {
        var members = SettableProperties(typeof(BuilderForkDef))
            .Concat(SettableProperties(typeof(BuilderPermittedTrigger)))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in ForkMemberExclusions.Keys)
        {
            await Assert.That(members.Contains(name)).IsTrue()
                .Because($"ForkMemberExclusions names '{name}', not a fork-edge builder-IR member.");
        }
    }

    // -------------------------------------------------------------------------
    // T-FE6 — the DR-10 versioning posture on the EQUIVALENCE-GATE schema. The
    // wire-shape family (DiagnosticForkSchemaTests) pins non-breaking on the
    // per-type WorkflowDefinitionV1.json; this pins it on the *bundled*
    // workflow-definition-v1.schema.json — the very artifact the fork fixtures
    // validate against — so the fixture-gate schema and the versioning claim stay
    // coupled to one file.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adding the OPTIONAL <c>diagnosticForks</c> slot to the bundled fixture-gate
    /// schema is a purely additive, NON-BREAKING change. Diffs the shipped schema
    /// against a copy of itself with the root slot removed, so the only delta is the
    /// added optional property.
    /// </summary>
    [Test]
    public async Task AddingDiagnosticForksSlot_ToBundledFixtureGateSchema_IsNonBreaking()
    {
        var emitted = await File.ReadAllTextAsync(FixturePaths.WorkflowSchemaPath);

        using var doc = JsonDocument.Parse(emitted);
        await Assert.That(doc.RootElement.GetProperty("properties").TryGetProperty("diagnosticForks", out _))
            .IsTrue()
            .Because("the bundled schema must actually carry the root diagnosticForks slot to remove.");

        // Synthesize the pre-DR-10 root: the shipped schema with the diagnosticForks
        // root property removed (JsonSchemaDiff compares top-level properties/required).
        var before = RemoveRootProperty(emitted, "diagnosticForks");

        var result = JsonSchemaDiff.Compare(before, emitted);

        await Assert.That(result.HasBreakingChanges).IsFalse()
            .Because("adding an OPTIONAL diagnosticForks slot is additive — never breaking (DR-10).");
        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking)
            .Because("the DR-10 wire addition is an additive minor, not a major bump.");
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.NonBreaking
                && c.Description.Contains("diagnosticForks", StringComparison.Ordinal))
            .Because("the differ must report the added optional diagnosticForks property.");
    }

    // -------------------------------------------------------------------------
    // Helpers.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a single fork edge with every builder-IR member set to a distinctive
    /// sentinel, projects it through <c>ToContract()</c>, serializes, and returns the
    /// set of fork-edge member names whose sentinel demonstrably surfaced in the JSON.
    /// </summary>
    private static IReadOnlySet<string> ProjectedForkMemberNames()
    {
        var present = new HashSet<string>(StringComparer.Ordinal);

        var workflow = Workflow<TestWorkflowState>.Create("fork-member-probe")
            .StartWith<ValidateStep>()
            .AllowDiagnosticFork(fork => fork
                .Anchor("anchor-sentinel-alpha")
                .PermitTrigger(ForkTrigger.GateContradiction, "evidence-sentinel-field")
                .WithCompensationSeed("seed-sentinel-omega")
                .MaxForks(4242))
            .Finally<CompleteStep>();

        var json = ContractsJson.Serialize(workflow.ToContract());

        // DiagnosticForkDefinition members.
        Record(present, nameof(BuilderForkDef.AnchorStepIds), json, "anchor-sentinel-alpha");
        Record(present, nameof(BuilderForkDef.CompensationSeed), json, "seed-sentinel-omega");
        Record(present, nameof(BuilderForkDef.MaxForks), json, "4242");
        // The PermittedTriggers collection surfaces via its projected content.
        Record(present, nameof(BuilderForkDef.PermittedTriggers), json, "gate_contradiction");

        // PermittedForkTriggerDefinition members.
        Record(present, nameof(BuilderPermittedTrigger.Trigger), json, "gate_contradiction");
        Record(present, nameof(BuilderPermittedTrigger.RequiredEvidenceFields), json, "evidence-sentinel-field");

        return present;
    }

    private static void Record(ISet<string> present, string memberName, string json, string needle)
    {
        if (json.Contains(needle, StringComparison.Ordinal))
        {
            present.Add(memberName);
        }
    }

    /// <summary>
    /// The public, instance data properties of a builder-IR type — the surface the
    /// projection must account for (mirrors <c>ProjectionExhaustivenessTests</c>).
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

    /// <summary>
    /// Returns <paramref name="schemaJson"/> with a single top-level
    /// <c>properties.&lt;name&gt;</c> entry removed — the "before" baseline for the
    /// additive-slot diff.
    /// </summary>
    private static string RemoveRootProperty(string schemaJson, string name)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(schemaJson)!;
        node["properties"]!.AsObject().Remove(name);
        return node.ToJsonString();
    }
}

// =============================================================================
// <copyright file="CrossProductRoundTripTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using NJsonSchema;

using Strategos.Contracts;
using Strategos.Contracts.Generated;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// T31 — the cross-product round-trip harness (design §Resilience item 2,
/// exarchos#1247). Two directions, both run OFFLINE against OUR OWN emitted
/// artifacts:
/// <list type="number">
///   <item>
///     <b>fixture → Zod:</b> our emitted workflow-IR fixtures must parse against
///     a Zod schema generated from our own bundled JSON Schema (the zod-smoke
///     pipeline proves the JSON Schema → Zod step). This stands in, offline, for
///     "a fixture must parse against Exarchos's generated Zod".
///   </item>
///   <item>
///     <b>IR → our C# schema:</b> a representative IR JSON validates against our
///     C#-side (NJsonSchema) workflow schema — standing in, offline, for "an
///     Exarchos-emitted IR must validate against our schema".
///   </item>
/// </list>
/// <para>
/// EXTERNAL-COORDINATION SEAM (exarchos#1247): the PRODUCTION cross-product gate
/// must pin Exarchos's *published* Zod snapshot and run our fixtures against
/// THAT (not against Zod we re-derive from our own schema). Pinning that
/// snapshot is coordinated in exarchos#1247 and is OUT OF SCOPE here — this
/// harness derives Zod from our own schema so it stays runnable offline and in a
/// single repo. The seam is the <c>--zod-source</c> input of the harness
/// (currently "self"); production swaps it for the pinned Exarchos barrel. See
/// also <c>src/Strategos.Contracts/scripts/cross-product-roundtrip.mjs</c> and
/// the README "Cross-product round-trip" note.
/// </para>
/// </summary>
[Property("Category", "Pipeline")]
[NotInParallel("tsp-compile")]
public class CrossProductRoundTripTests
{
    /// <summary>
    /// Drives the offline harness: generate Zod from our bundled workflow schema,
    /// parse every emitted fixture against it, AND validate a representative IR
    /// against our C#-side NJsonSchema. Both directions must pass.
    /// </summary>
    [Test]
    public async Task CrossProduct_FixtureValidatesAgainstZod_AndZodIrValidatesHere()
    {
        // Direction 1 — fixture → Zod (offline, our own schema-derived Zod).
        var harness = Path.Combine(
            RepoLayout.ContractsProjectDir, "scripts", "cross-product-roundtrip.mjs");
        await Assert.That(File.Exists(harness)).IsTrue()
            .Because($"expected the cross-product round-trip harness at {harness}");

        var fixturesDir = RepoLayout.BuilderFixturesDir;
        await Assert.That(Directory.Exists(fixturesDir)).IsTrue()
            .Because($"expected exported fixtures at {fixturesDir} (run the #53 fixture export first)");

        var run = await Cli.RunAsync(
            "node",
            $"\"{harness}\" --fixtures \"{fixturesDir}\" --zod-source self",
            RepoLayout.ContractsProjectDir);

        await Assert.That(run.ExitCode).IsEqualTo(0)
            .Because($"every emitted fixture must parse against schema-derived Zod (offline):\n{run.Output}");
        await Assert.That(run.Output).Contains("fixtures parsed")
            .Because(run.Output);

        // Direction 2 — a representative IR validates against our C#-side schema.
        var schemaJson = await File.ReadAllTextAsync(RepoLayout.WorkflowSchemaPath);
        var schema = await JsonSchema.FromJsonAsync(schemaJson);

        var representative = await File.ReadAllTextAsync(FirstFixture(fixturesDir));
        var errors = schema.Validate(representative);

        await Assert.That(errors.Count).IsEqualTo(0)
            .Because("a representative IR must validate against our C# (NJsonSchema) schema:\n"
                + string.Join("\n", errors.Select(e => e.ToString())));
    }

    /// <summary>
    /// S1–S4 (issues #63/#64/#65/#66) — the SMQ cross-product round-trip. For each
    /// of the four new top-level types (MergeGateDecision, JourneyResult,
    /// WorkflowCatalog, WorkflowRef), a C# binding serializes to JSON that
    /// validates against the emitted JSON Schema (C# direction, NJsonSchema), and
    /// the same fixtures parse against schema-derived Zod (TS direction, via the
    /// .mjs harness). Both directions must pass.
    /// </summary>
    [Test]
    public async Task CrossProductRoundTrip_SmqEnvelopes_ValidateBothDirections()
    {
        var fixturesDir = Path.Combine(
            Directory.CreateTempSubdirectory("smq-fixtures-").FullName, "smq");
        Directory.CreateDirectory(fixturesDir);

        try
        {
            // Build consumer-faithful bindings for each new top-level type.
            var bindings = SmqBindings();

            // Direction 1 — C# binding → JSON validates against its emitted JSON Schema.
            var schemaDir = EventSchemas.SchemaDir;
            foreach (var (typeName, value) in bindings)
            {
                var json = ContractsJson.Serialize(value);
                await File.WriteAllTextAsync(Path.Combine(fixturesDir, typeName + ".json"), json);

                var schemaPath = Path.Combine(schemaDir, typeName + ".json");
                var schema = await JsonSchema.FromFileAsync(schemaPath);
                var errors = schema.Validate(json);
                await Assert.That(errors.Count).IsEqualTo(0)
                    .Because($"{typeName} binding must validate against its emitted JSON Schema:\n"
                        + string.Join("\n", errors.Select(e => e.ToString())) + "\n" + json);

                // Deserialize the fixture back into the binding (round-trips).
                var back = System.Text.Json.JsonSerializer.Deserialize(
                    json, value!.GetType(), ContractsJson.Options);
                await Assert.That(back).IsNotNull()
                    .Because($"{typeName} fixture must deserialize back into its binding.");
            }

            // Direction 2 — the same fixtures parse against schema-derived Zod.
            var harness = Path.Combine(
                RepoLayout.ContractsProjectDir, "scripts", "cross-product-roundtrip.mjs");
            var run = await Cli.RunAsync(
                "node",
                $"\"{harness}\" --smq-fixtures \"{fixturesDir}\"",
                RepoLayout.ContractsProjectDir);

            await Assert.That(run.ExitCode).IsEqualTo(0)
                .Because($"every SMQ fixture must parse against schema-derived Zod (offline):\n{run.Output}");
            await Assert.That(run.Output).Contains("SMQ fixtures parsed")
                .Because(run.Output);
        }
        finally
        {
            Directory.Delete(Directory.GetParent(fixturesDir)!.FullName, recursive: true);
        }
    }

    /// <summary>
    /// Consumer-faithful bindings for the four new top-level SMQ types, keyed by
    /// the emitted schema/type name they validate against.
    /// </summary>
    private static IReadOnlyList<(string TypeName, object Value)> SmqBindings()
    {
        var catalogRef = new CatalogWorkflowRef { WorkflowId = "merge-gate", CatalogVersion = "1.4.0" };

        var meta = new MergeQueueMetaV1
        {
            Degraded = false,
            HeadSha = "abc123",
            BaseSha = "def456",
            MergeGroupId = "mg-7",
            EvaluatorTier = "tier-1",
        };
        var perf = new PerfMetaV1
        {
            Ms = 42,
            InputTokens = 1200,
            OutputTokens = 340,
            CacheReadTokens = 80,
        };

        var decision = new MergeGateDecision
        {
            SchemaVersion = "merge-gate.v1",
            Decision = MergeDecision.RunE2e,
            Confidence = 0.87,
            Rationale = "schema change touches the wire contract; run e2e",
            DiffClassification = DiffClassification.Schema,
            RiskSignals = ["touches-contract", "cross-repo"],
            SuggestedJourneys = [catalogRef],
            PromptId = "merge-gate-prompt.v3",
            ModelId = "claude-opus-4-7",
            Meta = meta,
            Perf = perf,
            NextActions =
            [
                new RunBuildkitePipelineAction { Params = new BuildkitePipelineParams { Journeys = [catalogRef] } },
                new EscalateHumanAction { Reason = "diff too large" },
            ],
        };

        var result = new JourneyResult
        {
            Outcome = JourneyOutcomeStatus.Partial,
            JourneyOutcomes =
            [
                new JourneyOutcome
                {
                    WorkflowId = "merge-gate",
                    CatalogVersion = "1.4.0",
                    Outcome = JourneyOutcomeStatus.AllFailed,
                    EvidenceRef = "s3://evidence/run-7",
                },
            ],
            BudgetConsumed = new BudgetConsumedV1
            {
                InputTokens = 9000,
                OutputTokens = 2200,
                CacheReadTokens = 500,
                CostUsd = 0.42,
            },
            ProvenanceRef = "provenance://g3/run-7",
            Meta = new MergeQueueMetaV1
            {
                Degraded = true,
                DegradedReason = DegradedReason.JudgeUnavailable,
                HeadSha = "abc123",
                BaseSha = "def456",
                MergeGroupId = "mg-7",
                EvaluatorTier = "tier-2",
            },
            Perf = perf,
            NextActions = [new EscalateHumanAction { Reason = "journey failed" }],
        };

        var catalog = new WorkflowCatalog
        {
            CatalogVersion = "1.4.0",
            Entries =
            [
                new WorkflowCatalogEntry
                {
                    WorkflowId = "merge-gate",
                    CatalogVersion = "1.4.0",
                    Definition = new WorkflowDefinitionV1
                    {
                        SchemaVersion = "1.0",
                        Name = "merge-gate",
                        Steps = [],
                        Transitions = [],
                        BranchPoints = [],
                        Loops = [],
                        ForkPoints = [],
                        FailureHandlers = [],
                        ApprovalPoints = [],
                    },
                },
            ],
        };

        return
        [
            ("MergeGateDecision", decision),
            ("JourneyResult", result),
            ("WorkflowCatalog", catalog),
            ("WorkflowRef", catalogRef),
        ];
    }

    private static string FirstFixture(string fixturesDir)
    {
        return Directory.EnumerateFiles(fixturesDir, "*.json", SearchOption.AllDirectories)
            .First(p => !p.EndsWith("index.json", StringComparison.Ordinal));
    }
}

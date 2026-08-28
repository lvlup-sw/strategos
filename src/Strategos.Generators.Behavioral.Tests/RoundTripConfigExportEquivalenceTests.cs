// -----------------------------------------------------------------------
// <copyright file="RoundTripConfigExportEquivalenceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;

using Strategos.Contracts;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// Task 019 (#100), DR-15 (L5) — ties the config family's behavioral round-trip to the twin's ACTUAL
/// projection. The retry twin-equivalence run
/// (<see cref="RoundTripBehavioralTests.ConfigRetryJsonImport_RunsIdentically_ToCSharpTwin"/>) drives a
/// hand-authored JSON import (<c>roundtrip-config.workflow.json</c>) against an independently
/// hand-authored C# twin — so on its own it never proves the JSON IS the twin's export. This test
/// closes that gap: it exports the twin via its real <c>ToContract()</c> projection, renames the
/// deliberately-distinct step monikers / workflow name (they MUST differ so the two authoring forms do
/// not share a step CLR type — CS0101), and proves the result is field-for-field equivalent to the
/// on-disk import fixture the build actually imports and runs. The import fixture is therefore the
/// twin's export transcribed, not an independent hand-authoring — so the behavioral run of the import
/// saga is a genuine round-trip of the twin's <c>ToContract()</c> output.
/// </summary>
/// <remarks>
/// Pure projection/comparison — no host, so it runs without Docker. StepIds/transitionIds are random
/// GUIDs (<c>Guid.NewGuid().ToString("N")</c>), so equivalence is asserted on the behaviorally-meaningful
/// content: workflow name, the ordered step monikers + terminal flags, and the retry policy on the
/// middle step.
/// </remarks>
[Property("Category", "FixtureExport")]
public sealed class RoundTripConfigExportEquivalenceTests
{
    /// <summary>
    /// The config twin's <c>ToContract()</c> export, with its distinct monikers/name renamed to the
    /// import's, is field-for-field equivalent (name, ordered step monikers + terminal flags, middle-step
    /// retry policy) to the on-disk <c>roundtrip-config.workflow.json</c> the build imports and runs.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConfigImportFixture_IsTheConfigTwinsActualToContractExport()
    {
        // The twin's real projection → wire JSON, with the forced-distinct monikers/name renamed to the
        // import's (RtConfigTwin* → RtConfigImport*, roundtrip-config-twin → roundtrip-config-import).
        var twinExportJson = ContractsJson.Serialize(RoundtripConfigTwinWorkflowDefinition.Definition.ToContract())
            .Replace("RtConfigTwin", "RtConfigImport", StringComparison.Ordinal)
            .Replace("roundtrip-config-twin", "roundtrip-config-import", StringComparison.Ordinal);

        var fixtureJson = await File.ReadAllTextAsync(LocateConfigImportFixture());

        var twin = Summarize(twinExportJson);
        var fixture = Summarize(fixtureJson);

        await Assert.That(twin.Name).IsEqualTo(fixture.Name)
            .Because("the import fixture's workflow name must be the twin's exported name (renamed).");
        await Assert.That(string.Join("|", twin.StepMonikers)).IsEqualTo(string.Join("|", fixture.StepMonikers))
            .Because("the import fixture's ordered step monikers must be the twin's exported monikers (renamed).");
        await Assert.That(string.Join("|", twin.TerminalFlags)).IsEqualTo(string.Join("|", fixture.TerminalFlags))
            .Because("the import fixture's ordered terminal flags must match the twin's export.");
        await Assert.That(twin.WorkRetryMaxAttempts).IsEqualTo(fixture.WorkRetryMaxAttempts)
            .Because("the import fixture's retry policy on the middle step must be the twin's exported .WithRetry(3) (maxAttempts=3).");
        await Assert.That(twin.WorkRetryMaxAttempts).IsNotNull()
            .Because("the comparison is non-vacuous only if the twin actually exported a retry policy on its middle step.");
    }

    /// <summary>
    /// Extracts the behaviorally-meaningful content of a wire workflow JSON: the name, the ordered step
    /// monikers, the ordered terminal flags, and the retry <c>maxAttempts</c> on the middle
    /// (<c>RtConfigImportWork</c>) step.
    /// </summary>
    private static (string? Name, List<string> StepMonikers, List<bool> TerminalFlags, int? WorkRetryMaxAttempts) Summarize(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var name = (string?)root["name"];

        var monikers = new List<string>();
        var terminals = new List<bool>();
        int? workRetry = null;

        foreach (var step in root["steps"]!.AsArray())
        {
            var stepObj = step!.AsObject();
            var moniker = (string?)stepObj["stepType"] ?? string.Empty;
            monikers.Add(moniker);
            terminals.Add((bool?)stepObj["isTerminal"] ?? false);

            if (moniker == "RtConfigImportWork")
            {
                workRetry = (int?)stepObj["configuration"]?["retry"]?["maxAttempts"];
            }
        }

        return (name, monikers, terminals, workRetry);
    }

    /// <summary>
    /// Resolves the on-disk <c>roundtrip-config.workflow.json</c> import fixture by walking up from the
    /// test assembly to the repo root (the directory holding <c>src/strategos.slnx</c>).
    /// </summary>
    private static string LocateConfigImportFixture()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "src", "strategos.slnx")))
            {
                return Path.Combine(
                    dir, "src", "Strategos.Generators.Behavioral.Tests", "Workflows", "roundtrip-config.workflow.json");
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate repo root (no src/strategos.slnx) from " + AppContext.BaseDirectory);
    }
}

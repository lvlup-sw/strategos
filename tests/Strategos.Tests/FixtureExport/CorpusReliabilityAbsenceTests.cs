// =============================================================================
// <copyright file="CorpusReliabilityAbsenceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Contracts;

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// DR-2 (issue #150) — the AUTHORING-CHANNEL guard. Gate reliability is
/// telemetry-measured, never hand-authored: no builder/DSL combinator may emit a
/// reliability block. This runs the entire #53 builder corpus through the SAME
/// projection + canonical serialization path the fixture export uses
/// (<c>ToContract()</c> → <see cref="ContractsJson"/>) and asserts that ZERO
/// authored workflows carry a reliability annotation on the wire. If a future
/// change ever adds a builder surface that authors reliability, this gate goes red.
/// </summary>
[Property("Category", "FixtureExport")]
public sealed class CorpusReliabilityAbsenceTests
{
    /// <summary>
    /// Serializes every corpus case through the authoring → wire path and asserts
    /// none contains the token <c>reliability</c> (case-insensitive) — no authoring
    /// channel smuggles a measured-reliability block onto the wire.
    /// </summary>
    [Test]
    public async Task AuthoredCorpus_CarriesNoReliabilityBlock_OnTheWire()
    {
        var cases = WorkflowCorpus.All();

        // Sanity: the corpus must be populated, else the gate passes vacuously.
        await Assert.That(cases.Count).IsGreaterThanOrEqualTo(100)
            .Because("the #53 corpus must be populated for this absence gate to be meaningful.");

        var offenders = new List<string>();
        foreach (var c in cases)
        {
            var json = ContractsJson.Serialize(c.Workflow.ToContract());
            if (json.Contains("reliability", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(c.Name);
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("reliability is telemetry-measured, never hand-authored — no builder "
                + "combinator may author it (DR-2). Offending fixtures:\n"
                + string.Join("\n", offenders.Take(10)));
    }
}

// =============================================================================
// <copyright file="CatalogRoundTripTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T29 — the invariant catalog round-trips a representative v2 entry AND a v3
/// fixture without loss. Routes through the canonical <c>ContractsJson</c> path
/// (the same serializer the #53 fixture export uses): JSON → generated
/// <c>InvariantEntry</c> record → JSON, asserting structural equality. The v3
/// fixture exercises the recursive <c>CheckNode</c> tree and the
/// <c>Enforcement</c> union; the v2 fixture proves back-compat (no v3 field
/// leaks in on round-trip).
/// </summary>
[Property("Category", "Diagnostics")]
public class CatalogRoundTripTests
{
    // A representative v2 catalog entry — v2 fields only, no v3 fields. Mirrors
    // the shape of Exarchos's existing invariants.md frontmatter.
    private const string V2Entry = """
        {
          "id": "INV-3",
          "dimension": "DIM-3",
          "axis": "divergent-instances",
          "cost-of-load": "medium",
          "applies-to": ["strategos", "basileus"],
          "summary": "A vocabulary that crosses a product boundary is single-sourced.",
          "axiom_overlap": ["A-2", "A-7"],
          "citations": ["adr-0042"],
          "references": ["#50", "#98"]
        }
        """;

    // A representative v3 entry — v2 fields plus every v3 field, exercising the
    // recursive CheckNode tree (all-of over a grep leaf and a nested not/scope)
    // and the Enforcement union (check mode).
    private const string V3Entry = """
        {
          "id": "INV-4",
          "dimension": "DIM-8",
          "axis": "sandbox",
          "cost-of-load": "low",
          "applies-to": ["exarchos"],
          "summary": "The contract is declarative-only; it never serializes executable code.",
          "axiom_overlap": ["A-1"],
          "citations": ["lb-1"],
          "references": ["#98"],
          "phase-affinity": ["design", "implement"],
          "workflow-affinity": ["exarchos-implement"],
          "state-affinity": ["validated", "enriched"],
          "severity": "blocking",
          "integrity-class": "structural",
          "enforcement": {
            "mode": "check",
            "check": {
              "kind": "all-of",
              "children": [
                { "kind": "grep", "pattern": "exec\\\\(", "file-glob": "**/*.ts" },
                {
                  "kind": "scope",
                  "file-glob": "src/**",
                  "child": {
                    "kind": "not",
                    "child": { "kind": "heuristic", "threshold": 0.8 }
                  }
                }
              ]
            }
          }
        }
        """;

    /// <summary>
    /// Deserializes each fixture into the generated <c>InvariantEntry</c> via the
    /// canonical serializer, re-serializes, and asserts the re-emitted JSON is
    /// structurally identical to the input — no field dropped, no v3 field
    /// synthesised on the v2 entry, and the recursive CheckNode / Enforcement
    /// union preserved verbatim.
    /// </summary>
    [Test]
    [Arguments(nameof(V2Entry))]
    [Arguments(nameof(V3Entry))]
    public async Task InvariantCatalog_RoundTrips_V2AndV3_WithoutLoss(string fixtureName)
    {
        var fixture = fixtureName == nameof(V2Entry) ? V2Entry : V3Entry;

        var asm = typeof(ContractsMarker).Assembly;
        var entryType = asm.GetTypes().First(t => t.Name == "InvariantEntry");

        // JSON -> record -> JSON, via the canonical ContractsJson path.
        var options = Strategos.Contracts.ContractsJson.Options;
        var entry = JsonSerializer.Deserialize(fixture, entryType, options);
        await Assert.That(entry).IsNotNull()
            .Because($"the {fixtureName} fixture must deserialize into InvariantEntry.");

        var serializeGeneric = typeof(JsonSerializer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "Serialize"
                && m.GetParameters().Length == 3
                && m.GetParameters()[1].ParameterType == typeof(Type));
        var roundTripped = (string)serializeGeneric.Invoke(null, [entry, entryType, options])!;

        var input = JsonNode.Parse(fixture)!;
        var output = JsonNode.Parse(roundTripped)!;

        await Assert.That(JsonEquals(input, output)).IsTrue()
            .Because($"the {fixtureName} entry must round-trip without loss.\nexpected:\n{fixture}\nactual:\n{roundTripped}");
    }

    /// <summary>Structural (order-insensitive) JSON equality.</summary>
    private static bool JsonEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        switch (a)
        {
            case JsonObject ao when b is JsonObject bo:
                if (ao.Count != bo.Count)
                {
                    return false;
                }

                foreach (var (key, value) in ao)
                {
                    if (!bo.TryGetPropertyValue(key, out var other) || !JsonEquals(value, other))
                    {
                        return false;
                    }
                }

                return true;

            case JsonArray aa when b is JsonArray ba:
                if (aa.Count != ba.Count)
                {
                    return false;
                }

                for (var i = 0; i < aa.Count; i++)
                {
                    if (!JsonEquals(aa[i], ba[i]))
                    {
                        return false;
                    }
                }

                return true;

            case JsonValue:
                return b is JsonValue
                    && a.ToJsonString() == b.ToJsonString();

            default:
                return false;
        }
    }
}

// =============================================================================
// <copyright file="InvariantEntryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T28 — <c>InvariantEntry</c> v3 (#98). The catalog entry carries the v2 fields
/// plus the additive, OPTIONAL v3 fields (back-compat: every v3 field is
/// optional). Wire names are preserved verbatim (kebab-case / snake_case) via
/// quoted TypeSpec identifiers so the generated decoder validates Exarchos's YAML
/// frontmatter without re-casing.
/// </summary>
[Property("Category", "Diagnostics")]
[NotInParallel("tsp-compile")]
public class InvariantEntryTests
{
    /// <summary>The v2 fields, by their exact wire name.</summary>
    private static readonly string[] V2Fields =
    [
        "id", "dimension", "axis", "cost-of-load", "applies-to",
        "summary", "axiom_overlap", "citations", "references",
    ];

    /// <summary>The additive v3 fields, by their exact wire name. All optional.</summary>
    private static readonly string[] V3Fields =
    [
        "phase-affinity", "workflow-affinity", "state-affinity",
        "enforcement", "severity", "integrity-class",
    ];

    /// <summary>
    /// Asserts <c>InvariantEntry</c> declares every v2 + v3 field by its exact
    /// wire name, that the v3 fields are all optional (back-compat), and that the
    /// kebab/snake wire names are preserved verbatim.
    /// </summary>
    [Test]
    public async Task InvariantEntry_V3_AddsOptionalFields_PreservesKebabWireNames()
    {
        var compile = await TspToolchain.CompileAsync();
        await Assert.That(compile.ExitCode).IsEqualTo(0).Because(compile.Output);

        var root = await EventSchemas.LoadAsync("InvariantEntry");
        var props = root.GetProperty("properties");

        var required = root.TryGetProperty("required", out var reqEl)
            ? reqEl.EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string?>();

        // Every v2 + v3 field is declared by its exact wire name.
        foreach (var field in V2Fields.Concat(V3Fields))
        {
            await Assert.That(props.TryGetProperty(field, out _)).IsTrue()
                .Because($"InvariantEntry must declare the '{field}' field by its exact wire name.");
        }

        // Every v3 field must be OPTIONAL (additive back-compat).
        foreach (var v3 in V3Fields)
        {
            await Assert.That(required.Contains(v3)).IsFalse()
                .Because($"v3 field '{v3}' must be optional for back-compat with v2 catalogs.");
        }

        // The kebab/snake wire names are preserved verbatim (no camelCase leak).
        await Assert.That(props.TryGetProperty("cost-of-load", out _)).IsTrue()
            .Because("the kebab-case wire name 'cost-of-load' must be verbatim, not 'costOfLoad'.");
        await Assert.That(props.TryGetProperty("axiom_overlap", out _)).IsTrue()
            .Because("the snake_case wire name 'axiom_overlap' must be preserved verbatim.");

        // enforcement references the Enforcement union (T27).
        var enforcementRef = Path.GetFileNameWithoutExtension(
            props.GetProperty("enforcement").GetProperty("$ref").GetString());
        await Assert.That(enforcementRef).IsEqualTo("Enforcement")
            .Because("the enforcement field must reference the Enforcement union.");
    }
}

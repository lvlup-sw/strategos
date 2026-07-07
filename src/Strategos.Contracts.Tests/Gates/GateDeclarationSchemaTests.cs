// =============================================================================
// <copyright file="GateDeclarationSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Runtime.CompilerServices;

using NJsonSchema;
using NJsonSchema.Validation;

using Strategos.Contracts.SchemaDiff;

namespace Strategos.Contracts.Tests.Gates;

/// <summary>
/// DR-2 (issue #150) — <c>GateDeclaration</c> and its optional, provenance-required
/// <c>GateReliability</c> annotation. Asserts (a) the emitted schema makes
/// <c>reliability.source</c> MANDATORY (a reliability block without provenance
/// fails schema validation — reliability is telemetry-measured, never
/// hand-authored); (b) <c>class</c>/<c>id</c> are required on the declaration while
/// <c>reliability</c> is optional; (c) both models emit as immutable
/// <c>sealed record</c>s with <c>{ get; init; }</c> (INV-6/7); and (d) the family
/// is a purely additive, NON-BREAKING extension of the wire contract.
/// </summary>
[Property("Category", "Gates")]
public sealed class GateDeclarationSchemaTests
{
    private const string GateDeclarationSchema = "GateDeclaration";
    private const string GateReliabilitySchema = "GateReliability";

    /// <summary>
    /// The PROVENANCE gate: <c>GateReliability.source</c> is schema-required, so a
    /// reliability block that omits it fails validation. A complete block validates.
    /// This is the mechanical enforcement of "reliability is telemetry-measured,
    /// never hand-authored" — there is no anonymous, unattributable reliability.
    /// </summary>
    [Test]
    public async Task GateReliability_WithoutSource_FailsSchemaValidation()
    {
        await Assert.That(EventSchemas.Exists(GateReliabilitySchema)).IsTrue()
            .Because("`tsp compile` must emit GateReliability.json (run scripts/contracts-codegen.sh).");

        var schema = await JsonSchema.FromFileAsync(
            Path.Combine(EventSchemas.SchemaDir, GateReliabilitySchema + ".json"));

        // A reliability block WITHOUT provenance — must be rejected.
        const string missingSource =
            """{ "fpr": 0.02, "sampleSize": 500, "asOf": "2026-07-07T00:00:00Z" }""";
        var missingErrors = schema.Validate(missingSource);
        await Assert.That(missingErrors.Count).IsGreaterThan(0)
            .Because("a reliability block without `source` (provenance) must fail schema validation.");
        await Assert.That(missingErrors.Any(e =>
                e.Kind == ValidationErrorKind.PropertyRequired
                && e.Property == "source"))
            .IsTrue()
            .Because("the validation failure must be the MISSING REQUIRED `source` property.");

        // A fully-measured block WITH provenance — must validate.
        const string withSource =
            """
            { "fpr": 0.02, "sampleSize": 500, "asOf": "2026-07-07T00:00:00Z",
              "source": "telemetry://gate-eval/run-42" }
            """;
        var okErrors = schema.Validate(withSource);
        await Assert.That(okErrors.Count).IsEqualTo(0)
            .Because("a complete, provenance-bearing reliability block must validate:\n"
                + string.Join("\n", okErrors.Select(e => e.ToString())));
    }

    /// <summary>
    /// The declaration shape: <c>class</c> (the DR-1 typed gate identity) and
    /// <c>id</c> are REQUIRED; <c>reliability</c> is OPTIONAL (a freshly-declared
    /// gate carries none). <c>class</c> is a <c>$ref</c> to the shared
    /// <c>GateClass</c> enum, not a loose string.
    /// </summary>
    [Test]
    public async Task GateDeclaration_RequiresClassAndId_ReliabilityOptional()
    {
        await Assert.That(EventSchemas.Exists(GateDeclarationSchema)).IsTrue()
            .Because("`tsp compile` must emit GateDeclaration.json (run scripts/contracts-codegen.sh).");

        var root = await EventSchemas.LoadAsync(GateDeclarationSchema);

        var required = RequiredNames(root);
        await Assert.That(required).Contains("class")
            .Because("the typed gate class is the mandatory identity of a declaration (DR-1).");
        await Assert.That(required).Contains("id")
            .Because("a declaration must carry a stable id.");
        await Assert.That(required).DoesNotContain("reliability")
            .Because("reliability is measured after the fact — OPTIONAL on the declaration (DR-2).");

        // `class` binds the shared GateClass enum by $ref (INV-8 single vocabulary),
        // never an inline/loose string.
        await Assert.That(root.TryGetProperty("properties", out var props)).IsTrue();
        await Assert.That(props.TryGetProperty("class", out var classProp)).IsTrue();
        await Assert.That(classProp.TryGetProperty("$ref", out var classRef)).IsTrue()
            .Because("`class` must reference the shared GateClass enum, not a loose string.");
        await Assert.That(Path.GetFileNameWithoutExtension(classRef.GetString())).IsEqualTo("GateClass");

        // `reliability`, when present, binds the GateReliability model by $ref.
        await Assert.That(props.TryGetProperty("reliability", out var relProp)).IsTrue();
        await Assert.That(relProp.TryGetProperty("$ref", out var relRef)).IsTrue();
        await Assert.That(Path.GetFileNameWithoutExtension(relRef.GetString())).IsEqualTo("GateReliability");
    }

    /// <summary>
    /// INV-6/INV-7 — both emitted records are <c>sealed</c> and every settable
    /// property is init-only (immutable). <c>GateDeclaration.Reliability</c> is
    /// nullable (the optional annotation) while <c>Class</c> is the typed
    /// <c>GateClass</c> enum. This is a focused mirror of the surface-wide
    /// <c>EmitterShapeTests</c> guarantee, pinned to the DR-2 family.
    /// </summary>
    [Test]
    public async Task GateDeclarationAndReliability_AreSealedImmutableRecords()
    {
        var asm = typeof(ContractsMarker).Assembly;
        var declType = asm.GetType("Strategos.Contracts.Generated.GateDeclaration");
        var relType = asm.GetType("Strategos.Contracts.Generated.GateReliability");

        await Assert.That(declType).IsNotNull()
            .Because("the codegen must emit Strategos.Contracts.Generated.GateDeclaration.");
        await Assert.That(relType).IsNotNull()
            .Because("the codegen must emit Strategos.Contracts.Generated.GateReliability.");

        foreach (var type in new[] { declType!, relType! })
        {
            await Assert.That(type.IsSealed).IsTrue()
                .Because($"{type.Name} must be a sealed record (INV-6).");

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.Name == "EqualityContract")
                {
                    continue;
                }

                var setter = prop.SetMethod;
                await Assert.That(setter).IsNotNull()
                    .Because($"{type.Name}.{prop.Name} must be settable via init.");
                var isInitOnly = setter!.ReturnParameter
                    .GetRequiredCustomModifiers()
                    .Any(m => m == typeof(IsExternalInit));
                await Assert.That(isInitOnly).IsTrue()
                    .Because($"{type.Name}.{prop.Name} must be init-only, not a mutable set (INV-7).");
            }
        }

        // `Class` is the typed GateClass enum (the shared identity), not a string.
        var classProp = declType!.GetProperty("Class");
        await Assert.That(classProp).IsNotNull();
        await Assert.That(classProp!.PropertyType).IsEqualTo(
            asm.GetType("Strategos.Contracts.Generated.GateClass"));

        // `Reliability` is the OPTIONAL nested annotation (nullable reference).
        var relProp = declType.GetProperty("Reliability");
        await Assert.That(relProp).IsNotNull();
        await Assert.That(relProp!.PropertyType).IsEqualTo(relType);

        var nullability = new NullabilityInfoContext().Create(relProp);
        await Assert.That(nullability.ReadState).IsEqualTo(NullabilityState.Nullable)
            .Because("GateDeclaration.Reliability must be optional (nullable) — DR-2.");
    }

    /// <summary>
    /// The DR-2 versioning posture: adding the optional <c>reliability</c>
    /// annotation to a gate declaration is a purely additive, NON-BREAKING wire
    /// change (design §Resilience item 3 — additive-only minors). The differ
    /// (enum-aware since task 025) sees exactly one change — an added OPTIONAL
    /// property — and classifies it non-breaking. Runs against the ACTUAL emitted
    /// <c>GateDeclaration.json</c> so it is coupled to the shipped artifact.
    /// </summary>
    [Test]
    public async Task GateDeclaration_AddingOptionalReliability_IsNonBreaking()
    {
        await Assert.That(EventSchemas.Exists(GateDeclarationSchema)).IsTrue()
            .Because("`tsp compile` must emit GateDeclaration.json.");

        // The pre-DR-2 core: the gate declaration BEFORE the reliability annotation
        // existed (class + id only). The emitted schema adds `reliability` on top.
        const string preReliability =
            """
            {
              "$id": "GateDeclaration.json",
              "type": "object",
              "properties": {
                "class": { "$ref": "GateClass.json" },
                "id": { "type": "string" }
              },
              "required": ["class", "id"]
            }
            """;

        var emitted = await File.ReadAllTextAsync(
            Path.Combine(EventSchemas.SchemaDir, GateDeclarationSchema + ".json"));

        var result = JsonSchemaDiff.Compare(preReliability, emitted);

        await Assert.That(result.HasBreakingChanges).IsFalse()
            .Because("adding an OPTIONAL reliability annotation is additive — never breaking (DR-2).");
        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking)
            .Because("the DR-2 family is an additive minor, not a major bump.");
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.NonBreaking
                && c.Description.Contains("reliability", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> RequiredNames(System.Text.Json.JsonElement root)
    {
        if (root.TryGetProperty("required", out var required)
            && required.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return required.EnumerateArray()
                .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        }

        return Array.Empty<string>();
    }
}

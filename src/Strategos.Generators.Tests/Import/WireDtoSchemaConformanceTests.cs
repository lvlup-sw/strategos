// -----------------------------------------------------------------------
// <copyright file="WireDtoSchemaConformanceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;

using Strategos.Generators.Import;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// DR-12 (#100) conformance guard: pins the hand-authored wire-DTO twins in
/// <c>Strategos.Generators.Import</c> to the Contracts-emitted JSON Schema
/// (<c>src/Strategos.Contracts/schemas/json-schema/*.json</c>).
/// </summary>
/// <remarks>
/// <para>
/// The generator is an isolated netstandard2.0 analyzer that cannot reference
/// <c>Strategos.Contracts</c> or System.Text.Json, so the import reader binds JSON
/// onto hand-authored twins instead of the real contract types. This test — in the
/// net-current test project, which CAN load the schemas — is the mechanical parity
/// gate that keeps those twins honest: it fails when a twin drifts from the schema
/// in EITHER direction — a missing field, an extra field, or a wrong JSON type.
/// </para>
/// <para>
/// Twins are discovered by the <see cref="IWireContractDto"/> marker and matched to
/// a schema file by type name (twin <c>GateStep</c> ↔ <c>GateStep.json</c>). Object
/// schemas are checked property-by-property; the <c>StepDefinition</c> <c>anyOf</c>
/// union is checked arm-set against the twin's subclasses. Wire enums are carried as
/// their string values on the twins (INV-8 polyglot identity), so enum-member drift
/// is out of scope here (covered by the Contracts schema tests).
/// </para>
/// </remarks>
[Property("Category", "WorkflowIr")]
public sealed class WireDtoSchemaConformanceTests
{
    private static readonly string SchemaDir = LocateSchemaDir();

    private static Assembly GeneratorsAssembly => typeof(WorkflowIncrementalGenerator).Assembly;

    /// <summary>
    /// Every discovered twin must have a matching emitted schema document — an
    /// orphan twin (no schema) is a drift in the twin→schema direction.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EveryTwin_HasMatchingSchemaFile()
    {
        var orphans = WireDtoTypes()
            .Where(t => !File.Exists(Path.Combine(SchemaDir, t.Name + ".json")))
            .Select(t => t.Name)
            .ToList();

        await Assert.That(orphans).IsEmpty()
            .Because("every wire-DTO twin must map to an emitted schema document (twin name == schema file name).");
    }

    /// <summary>
    /// Each object-schema twin must declare EXACTLY the schema's property set — no
    /// missing field, no extra field — and each shared field's JSON type category
    /// must match. This is the two-directional drift gate.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ObjectTwins_MatchSchemaPropertiesAndTypes_InEitherDirection()
    {
        var discrepancies = new List<string>();

        foreach (var twin in WireDtoTypes())
        {
            var schema = LoadSchemaRoot(twin.Name);

            // Union / enum schemas carry no `properties` object — the union is
            // covered by StepUnion_ArmsMatchTwinSubclasses; skip here.
            if (!schema.TryGetProperty("properties", out var props)
                || props.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var schemaFields = props.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);

            var twinFields = twin.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => ToWireName(p.Name), p => p, StringComparer.Ordinal);

            foreach (var missing in schemaFields.Keys.Where(k => !twinFields.ContainsKey(k)))
            {
                discrepancies.Add($"{twin.Name}: schema field '{missing}' is missing from the twin");
            }

            foreach (var extra in twinFields.Keys.Where(k => !schemaFields.ContainsKey(k)))
            {
                discrepancies.Add($"{twin.Name}: twin field '{extra}' is not in the schema");
            }

            foreach (var shared in schemaFields.Keys.Where(twinFields.ContainsKey))
            {
                var schemaCat = SchemaCategory(schemaFields[shared]);
                var twinCat = ClrCategory(twinFields[shared].PropertyType);
                if (!string.Equals(schemaCat, twinCat, StringComparison.Ordinal))
                {
                    discrepancies.Add(
                        $"{twin.Name}.{shared}: schema type '{schemaCat}' but twin type '{twinCat}'");
                }
            }
        }

        await Assert.That(discrepancies).IsEmpty()
            .Because("wire-DTO twins must not drift from the schema in either direction:\n"
                + string.Join("\n", discrepancies));
    }

    /// <summary>
    /// The <c>StepDefinition</c> <c>anyOf</c> union arms must correspond exactly to
    /// the twin's subclasses — a new step kind added to the schema (or removed) is a
    /// drift the twin subclass set must track.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StepUnion_ArmsMatchTwinSubclasses()
    {
        var schema = LoadSchemaRoot(nameof(StepDefinition));
        await Assert.That(schema.TryGetProperty("anyOf", out var anyOf)).IsTrue()
            .Because("StepDefinition is the discriminated step union (anyOf).");

        var schemaArms = anyOf.EnumerateArray()
            .Select(a => a.TryGetProperty("$ref", out var r) ? RefName(r.GetString()) : null)
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var twinArms = GeneratorsAssembly.GetTypes()
            .Where(t => t.BaseType == typeof(StepDefinition))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        await Assert.That(twinArms).IsEquivalentTo(schemaArms)
            .Because("the step-union twin subclasses must match the schema's anyOf arms exactly.");
    }

    /// <summary>
    /// Coverage floor: the critical import-subset twins must all be present, so a
    /// silent under-population of the twin graph cannot pass the conformance gate.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportSubset_CoversTheCriticalTwins()
    {
        var present = WireDtoTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        string[] required =
        [
            nameof(WorkflowDefinitionV1),
            nameof(SkillStep),
            nameof(HandlerStep),
            nameof(GateStep),
            nameof(DelegateStep),
            nameof(ApprovalStep),
            nameof(StepConfigurationDefinition),
            nameof(GateDeclaration),
            nameof(DiagnosticForkDefinition),
            nameof(PermittedForkTrigger),
            nameof(ApprovalDefinition),
        ];

        var absent = required.Where(r => !present.Contains(r)).ToList();

        await Assert.That(absent).IsEmpty()
            .Because("the DR-12 import subset must model every critical wire twin.");
    }

    /// <summary>
    /// Task-024 pin: the <c>ApprovalDefinition</c> twin must carry the
    /// <c>hasContext</c> lossiness marker from birth (DR-14), or the conformance
    /// gate would open a known-red window against the schema.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ApprovalTwin_CarriesHasContextMarker()
    {
        var hasContext = typeof(ApprovalDefinition)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => string.Equals(ToWireName(p.Name), "hasContext", StringComparison.Ordinal)
                && ClrCategory(p.PropertyType) == "boolean");

        await Assert.That(hasContext).IsTrue()
            .Because("ApprovalDefinition must carry the boolean hasContext marker (task 024 / DR-14).");
    }

    /// <summary>
    /// Packaging invariant: the generator assembly must reference no JSON-serializer
    /// package (System.Text.Json / Newtonsoft) — the import reader is vendored and
    /// dependency-free, keeping the isolated netstandard2.0 analyzer at zero package
    /// dependencies. Mirrors the AssemblyDependency guard pattern.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GeneratorAssembly_HasNoJsonSerializerDependency()
    {
        var leaks = GeneratorsAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n.Contains("System.Text.Json", StringComparison.OrdinalIgnoreCase)
                || n.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await Assert.That(leaks).IsEmpty()
            .Because("the import reader must be vendored and dependency-free (zero analyzer package deps).");
    }

    // ---- helpers ------------------------------------------------------------

    private static IEnumerable<Type> WireDtoTypes() =>
        GeneratorsAssembly.GetTypes()
            .Where(t => !t.IsInterface
                && typeof(IWireContractDto).IsAssignableFrom(t)
                && t != typeof(IWireContractDto))
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    private static string ToWireName(string pascal) =>
        string.IsNullOrEmpty(pascal)
            ? pascal
            : char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);

    private static string RefName(string? refValue) =>
        Path.GetFileNameWithoutExtension(refValue) ?? string.Empty;

    private static string ClrCategory(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string))
        {
            return "string";
        }

        if (t == typeof(bool))
        {
            return "boolean";
        }

        if (t == typeof(int) || t == typeof(long) || t == typeof(short))
        {
            return "integer";
        }

        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal))
        {
            return "number";
        }

        if (t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t))
        {
            return "array";
        }

        return "object";
    }

    private static string SchemaCategory(JsonElement propSchema)
    {
        if (propSchema.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
        {
            return typeEl.GetString()!;
        }

        if (propSchema.TryGetProperty("$ref", out var refEl) && refEl.ValueKind == JsonValueKind.String)
        {
            var refRoot = LoadSchemaRoot(RefName(refEl.GetString()));
            if (refRoot.TryGetProperty("type", out var refType) && refType.ValueKind == JsonValueKind.String)
            {
                return refType.GetString()!;
            }

            // A ref to a union (anyOf) resolves to an object slot.
            return "object";
        }

        // Inline anyOf / oneOf / unspecified — an object slot.
        return "object";
    }

    private static JsonElement LoadSchemaRoot(string name)
    {
        var path = Path.Combine(SchemaDir, name + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"expected emitted schema at {path}", path);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    private static string LocateSchemaDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "src", "strategos.slnx")))
            {
                return Path.Combine(dir, "src", "Strategos.Contracts", "schemas", "json-schema");
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "could not locate the repo root (no src/strategos.slnx walking up from "
            + AppContext.BaseDirectory + ").");
    }
}

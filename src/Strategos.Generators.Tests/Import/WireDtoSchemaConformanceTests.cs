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
            nameof(ActionReferenceV1),
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
    /// The action-reference twin is proof-bearing input, so its three identity fields
    /// remain required non-blank strings in the canonical schema and remain string
    /// fields on the isolated-analyzer twin.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ActionReferenceTwin_PinsRequiredNonBlankIdentityContract()
    {
        var schema = LoadSchemaRoot(nameof(ActionReferenceV1));
        var schemaProperties = schema.GetProperty("properties")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        var required = schema.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var expected = new[] { "domainName", "objectTypeName", "actionName" };

        await Assert.That(schemaProperties.Keys).IsEquivalentTo(expected)
            .Because("ActionReferenceV1 must not gain or lose an identity component silently.");
        await Assert.That(required).IsEquivalentTo(expected)
            .Because("all three action identity components are required on the wire.");

        var twinProperties = typeof(ActionReferenceV1)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => ToWireName(property.Name), property => property, StringComparer.Ordinal);

        foreach (var name in expected)
        {
            var property = schemaProperties[name];
            await Assert.That(property.GetProperty("type").GetString()).IsEqualTo("string");
            await Assert.That(property.GetProperty("minLength").GetInt32()).IsEqualTo(1);
            await Assert.That(property.GetProperty("pattern").GetString()).IsEqualTo(".*\\S.*");
            await Assert.That(twinProperties[name].PropertyType).IsEqualTo(typeof(string));
        }
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

    /// <summary>
    /// The wire contract has TWO schema representations: the per-model documents in
    /// <c>schemas/json-schema/</c> and the self-contained
    /// <c>workflow-definition-v1.schema.json</c> that <c>bundle-workflow-schema.mjs</c>
    /// inlines them into. Consumers pick one — Exarchos derives Zod from the per-model
    /// tree, the equivalence gate validates against the bundle — so a constraint
    /// present in one and absent from the other is a silent split in the contract.
    /// This compares every validation constraint that carries meaning for a producer:
    /// <c>required</c>, <c>minLength</c>, <c>pattern</c>, and <c>default</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerModelAndBundledSchemas_AgreeOnEveryConstraint()
    {
        var bundle = ContractsSchemaPaths.LoadBundle();
        var definitions = bundle.GetProperty("definitions");
        var discrepancies = new List<string>();
        var comparedModels = 0;
        var comparedProperties = 0;

        foreach (var definition in definitions.EnumerateObject())
        {
            var perModelPath = Path.Combine(SchemaDir, definition.Name + ".json");
            if (!File.Exists(perModelPath))
            {
                discrepancies.Add(
                    $"{definition.Name}: bundled definition has no per-model schema document");
                continue;
            }

            comparedModels++;
            var perModel = LoadSchemaRoot(definition.Name);
            var bundled = definition.Value;

            var perModelRequired = RequiredNames(perModel);
            var bundledRequired = RequiredNames(bundled);
            if (!perModelRequired.SetEquals(bundledRequired))
            {
                discrepancies.Add(
                    $"{definition.Name}: required differs — per-model [{string.Join(", ", perModelRequired.Order(StringComparer.Ordinal))}] "
                    + $"vs bundled [{string.Join(", ", bundledRequired.Order(StringComparer.Ordinal))}]");
            }

            var perModelProperties = PropertyMap(perModel);
            var bundledProperties = PropertyMap(bundled);
            if (!perModelProperties.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(bundledProperties.Keys))
            {
                discrepancies.Add($"{definition.Name}: property sets differ between the two representations");
                continue;
            }

            foreach (var (name, perModelProperty) in perModelProperties)
            {
                comparedProperties++;
                var bundledProperty = bundledProperties[name];
                foreach (var keyword in new[] { "minLength", "pattern", "default" })
                {
                    var inPerModel = perModelProperty.TryGetProperty(keyword, out var left);
                    var inBundled = bundledProperty.TryGetProperty(keyword, out var right);
                    if (inPerModel != inBundled)
                    {
                        discrepancies.Add(
                            $"{definition.Name}.{name}: '{keyword}' present in "
                            + (inPerModel ? "the per-model schema only" : "the bundled schema only"));
                    }
                    else if (inPerModel && !JsonElement.DeepEquals(left, right))
                    {
                        discrepancies.Add(
                            $"{definition.Name}.{name}: '{keyword}' differs — "
                            + $"per-model {left.GetRawText()} vs bundled {right.GetRawText()}");
                    }
                }
            }
        }

        await Assert.That(discrepancies).IsEmpty()
            .Because("the two schema representations of the wire contract must not drift:\n"
                + string.Join("\n", discrepancies));

        // Coverage floor: an empty or near-empty comparison would pass vacuously.
        await Assert.That(comparedModels).IsGreaterThan(20)
            .Because("the bundle must actually carry the workflow IR definitions.");
        await Assert.That(comparedProperties).IsGreaterThan(50)
            .Because("the comparison must actually reach the definitions' properties.");
    }

    /// <summary>
    /// The bundle must carry the constraints that make the comparison above
    /// meaningful. Without this, deleting <c>minLength</c> / <c>pattern</c> /
    /// <c>default</c> from BOTH representations would leave the parity test green.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BothSchemaRepresentations_CarryTheCompensationIdentityConstraints()
    {
        var perModel = LoadSchemaRoot("CompensationConfiguration");
        var bundled = ContractsSchemaPaths.LoadBundle()
            .GetProperty("definitions")
            .GetProperty("CompensationConfiguration");

        foreach (var schema in new[] { perModel, bundled })
        {
            var moniker = schema.GetProperty("properties").GetProperty("compensationStepType");
            await Assert.That(moniker.GetProperty("minLength").GetInt32()).IsEqualTo(1);
            await Assert.That(moniker.GetProperty("pattern").GetString()).IsEqualTo(@".*\S.*");
            await Assert.That(RequiredNames(schema)).Contains("compensationStepType");
            await Assert.That(schema.GetProperty("properties")
                .GetProperty("requiredOnFailure")
                .GetProperty("default")
                .ValueKind)
                .IsEqualTo(JsonValueKind.True);
        }
    }

    /// <summary>
    /// Where the generated contract record exposes the wire's required set as
    /// <c>[JsonRequired]</c>, the two must agree exactly. The attribute is what the
    /// runtime deserializer enforces, so a property that is required on the wire but
    /// not attributed binds a null into a non-nullable slot, and one attributed but
    /// not required on the wire rejects documents the contract promises to accept.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GeneratedRecords_JsonRequiredMatchesSchemaRequired()
    {
        var generatedAssembly = typeof(Strategos.Contracts.Generated.WorkflowDefinitionV1).Assembly;
        var discrepancies = new List<string>();
        var compared = 0;

        foreach (var schemaFile in Directory.GetFiles(SchemaDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(schemaFile);
            var generated = generatedAssembly.GetType("Strategos.Contracts.Generated." + name);
            if (generated is null || !generated.IsClass)
            {
                continue;
            }

            var schema = LoadSchemaRoot(name);
            if (!schema.TryGetProperty("properties", out var properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            compared++;
            var schemaRequired = RequiredNames(schema);
            var attributed = generated
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttributes()
                    .Any(attribute => attribute.GetType().Name == "JsonRequiredAttribute"))
                .Select(property => WireNameOf(property))
                .ToHashSet(StringComparer.Ordinal);

            // A union arm's discriminator (`kind` / `mode` / `verb` / ...) is not a CLR
            // property at all: System.Text.Json reads and writes it through the base
            // type's [JsonPolymorphic] converter, which rejects a document that omits
            // it. That is a STRONGER guarantee than [JsonRequired], so the discriminator
            // is required on the wire and enforced — just not by the attribute.
            var discriminator = PolymorphicDiscriminatorOf(generated);
            foreach (var missing in schemaRequired.Except(attributed, StringComparer.Ordinal))
            {
                if (string.Equals(missing, discriminator, StringComparison.Ordinal))
                {
                    continue;
                }

                discrepancies.Add($"{name}.{missing}: required on the wire but not [JsonRequired]");
            }

            foreach (var extra in attributed.Except(schemaRequired, StringComparer.Ordinal))
            {
                discrepancies.Add($"{name}.{extra}: [JsonRequired] but not required on the wire");
            }
        }

        await Assert.That(discrepancies).IsEmpty()
            .Because("[JsonRequired] on a generated record must mirror the schema's required set:\n"
                + string.Join("\n", discrepancies));
        await Assert.That(compared).IsGreaterThan(20)
            .Because("the comparison must actually reach the generated record surface.");

        // The discriminator exemption above must not be a blanket skip: a union arm
        // really does inherit a [JsonPolymorphic] base naming the schema's const-pinned
        // discriminator, so omitting it is still rejected on read.
        await Assert.That(PolymorphicDiscriminatorOf(typeof(Strategos.Contracts.Generated.SkillStep)))
            .IsEqualTo("kind")
            .Because("a union arm's discriminator is enforced by the polymorphic converter, "
                + "which is why it is exempt from the [JsonRequired] comparison.");
    }

    // ---- helpers ------------------------------------------------------------

    private static HashSet<string> RequiredNames(JsonElement schema)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("required", out var required)
            && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in required.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    names.Add(element.GetString()!);
                }
            }
        }

        return names;
    }

    private static Dictionary<string, JsonElement> PropertyMap(JsonElement schema)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (schema.TryGetProperty("properties", out var properties)
            && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                map[property.Name] = property.Value;
            }
        }

        return map;
    }

    /// <summary>
    /// Returns the discriminator property name a generated union arm inherits from its
    /// <c>[JsonPolymorphic]</c> base, or <see langword="null"/> when the type is not a
    /// union arm.
    /// </summary>
    private static string? PolymorphicDiscriminatorOf(Type generated)
    {
        for (var type = generated.BaseType; type is not null; type = type.BaseType)
        {
            var attribute = type.GetCustomAttributes(inherit: false)
                .FirstOrDefault(a => a.GetType().Name == "JsonPolymorphicAttribute");
            if (attribute is not null)
            {
                return attribute.GetType()
                    .GetProperty("TypeDiscriminatorPropertyName")?
                    .GetValue(attribute) as string;
            }
        }

        return null;
    }

    /// <summary>Resolves a generated property's wire name from its
    /// <c>[JsonPropertyName]</c>, falling back to the camel-cased CLR name.</summary>
    private static string WireNameOf(PropertyInfo property)
    {
        var attribute = property.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name == "JsonPropertyNameAttribute");
        var name = attribute?.GetType().GetProperty("Name")?.GetValue(attribute) as string;
        return name ?? ToWireName(property.Name);
    }

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
            if (File.Exists(Path.Combine(dir, "strategos.slnx")))
            {
                return Path.Combine(dir, "src", "Strategos.Contracts", "schemas", "json-schema");
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "could not locate the repo root (no strategos.slnx walking up from "
            + AppContext.BaseDirectory + ").");
    }
}

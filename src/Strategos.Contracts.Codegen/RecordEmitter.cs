// =============================================================================
// <copyright file="RecordEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Strategos.Contracts.Codegen;

/// <summary>
/// Emits C# contracts from TypeSpec-emitted JSON Schema documents:
/// <list type="bullet">
///   <item>object schemas → a <c>sealed record</c> with <c>{ get; init; }</c>
///   members and <see cref="IReadOnlyList{T}"/> collections (INV-6 / INV-7);</item>
///   <item>string-enum schemas → a C# <c>enum</c> whose members carry
///   <c>[JsonStringEnumMemberName]</c> so the wire value round-trips verbatim
///   (handles <c>@encodedName</c> / kebab-case / snake_case wire names);</item>
/// </list>
/// The emitter reads the raw JSON directly (rather than relying on a third-party
/// resolver) so cross-file <c>$ref</c>s resolve to the correct generated type by
/// document name. Open objects (e.g. <c>Record&lt;unknown&gt;</c>) carry no
/// members and are intentionally not emitted as records — they surface as
/// <c>object</c>-typed payload properties on their referrers.
/// </summary>
public static class RecordEmitter
{
    private const string Namespace = "Strategos.Contracts.Generated";

    /// <summary>Generates records/enums for every <c>*.json</c> schema in <paramref name="schemasDir"/>.</summary>
    /// <param name="schemasDir">Directory containing emitted JSON Schema documents.</param>
    /// <param name="outputDir">Directory the <c>*.g.cs</c> files are written to.</param>
    /// <returns>Process exit code (0 on success).</returns>
    public static async Task<int> RunAsync(string schemasDir, string outputDir)
    {
        if (!Directory.Exists(schemasDir))
        {
            await Console.Error.WriteLineAsync($"schemas dir not found: {schemasDir}").ConfigureAwait(false);
            return 1;
        }

        Directory.CreateDirectory(outputDir);

        // Clean stale generated files so deletions in TypeSpec propagate.
        foreach (var stale in Directory.GetFiles(outputDir, "*.g.cs"))
        {
            File.Delete(stale);
        }

        // The AGWF catalog schemas (the AgwfCode enum + the AgwfEntry* data
        // models) are owned by AgwfCatalogEmitter, not the general record/enum
        // pass: it emits the enum with *symbolic* member names (the AgwfEntry*
        // files are pure data carriers, not consumer-facing C# types). Exclude
        // them here so they never surface as ordinary records/enums.
        var schemaFiles = Directory.GetFiles(schemasDir, "*.json")
            .Where(f => !IsAgwfCatalogSchema(Path.GetFileName(f)))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        // First pass — classify every document so $ref targets resolve to the
        // right generated kind (enum vs record vs open-object).
        var docs = new Dictionary<string, SchemaDoc>(StringComparer.Ordinal);
        foreach (var schemaFile in schemaFiles)
        {
            var fileName = Path.GetFileName(schemaFile);
            using var parsed = JsonDocument.Parse(await File.ReadAllTextAsync(schemaFile).ConfigureAwait(false));
            docs[fileName] = SchemaDoc.Classify(fileName, parsed.RootElement);
        }

        // Second pass — index every discriminated-union arm to the union it
        // belongs to (and the discriminator value its `kind` const carries), so
        // the arm record can be emitted as a derived type of the union base and
        // System.Text.Json polymorphism round-trips on `kind`.
        var armToUnion = BuildArmIndex(docs);

        foreach (var doc in docs.Values)
        {
            string? source = doc.Kind switch
            {
                SchemaKind.Enum => EmitEnum(doc),
                SchemaKind.DiscriminatedUnion => EmitUnionBase(doc, docs),
                SchemaKind.Record => EmitRecord(doc, docs, armToUnion),
                _ => null, // open object / scalar alias: not a standalone type.
            };

            if (source is not null)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(outputDir, doc.TypeName + ".g.cs"), source).ConfigureAwait(false);
            }
        }

        // AGWF single-source catalog (#52): emit the AgwfCode enum (symbolic
        // member names), the canonical agwf-catalog.json data artifact, and the
        // docs/diagnostics/agwf.md reference from the AgwfEntry* schema consts.
        return await AgwfCatalogEmitter.RunAsync(schemasDir, outputDir).ConfigureAwait(false);
    }

    /// <summary>
    /// True for the AGWF catalog schemas owned by <see cref="AgwfCatalogEmitter"/>
    /// (the <c>AgwfCode</c> enum and every <c>AgwfEntry*</c> data model).
    /// </summary>
    private static bool IsAgwfCatalogSchema(string fileName) =>
        string.Equals(fileName, AgwfCatalogEmitter.EnumSchemaFileName, StringComparison.Ordinal)
        || fileName.StartsWith(AgwfCatalogEmitter.EntryPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Maps each union-arm document name to the union it belongs to: the union's
    /// generated base type name, the discriminator property's wire name, and the
    /// discriminator value the arm pins. Arms not part of any union are absent
    /// from the map.
    /// </summary>
    private static IReadOnlyDictionary<string, ArmBinding> BuildArmIndex(
        IReadOnlyDictionary<string, SchemaDoc> docs)
    {
        var index = new Dictionary<string, ArmBinding>(StringComparer.Ordinal);
        foreach (var doc in docs.Values)
        {
            if (doc.Kind != SchemaKind.DiscriminatedUnion)
            {
                continue;
            }

            var discriminatorName = ResolveDiscriminatorName(doc, docs);

            foreach (var armFile in doc.UnionArmRefs)
            {
                if (!docs.TryGetValue(armFile, out var arm))
                {
                    continue;
                }

                var discriminator = arm.Properties
                    .FirstOrDefault(p => string.Equals(p.WireName, discriminatorName, StringComparison.Ordinal))
                    ?.ConstValue;

                if (discriminator is not null)
                {
                    index[armFile] = new ArmBinding(doc.TypeName, discriminatorName, discriminator);
                }
            }
        }

        return index;
    }

    /// <summary>
    /// Resolves the discriminator property's wire name for a union: the property
    /// every arm pins with a <c>const</c> literal. Prefers <c>kind</c> when
    /// present (the workflow-IR convention); otherwise picks the single
    /// const-pinned property common to all arms (e.g. <c>mode</c> for
    /// <c>Enforcement</c>). This keeps the emitter union-agnostic rather than
    /// hard-coding one discriminator name.
    /// </summary>
    private static string ResolveDiscriminatorName(
        SchemaDoc union, IReadOnlyDictionary<string, SchemaDoc> docs)
    {
        var arms = union.UnionArmRefs
            .Select(f => docs.TryGetValue(f, out var a) ? a : null)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        if (arms.Count == 0)
        {
            return "kind";
        }

        // Candidate = a property pinned to a const on EVERY arm.
        bool PinnedOnAllArms(string wireName) => arms.All(a =>
            a.Properties.Any(p =>
                string.Equals(p.WireName, wireName, StringComparison.Ordinal) && p.ConstValue is not null));

        if (PinnedOnAllArms("kind"))
        {
            return "kind";
        }

        var candidates = arms[0].Properties
            .Where(p => p.ConstValue is not null)
            .Select(p => p.WireName)
            .Where(PinnedOnAllArms)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return candidates.Count == 1 ? candidates[0] : "kind";
    }

    /// <summary>
    /// Emits the union base as an abstract record carrying the
    /// <c>[JsonPolymorphic]</c> discriminator and one <c>[JsonDerivedType]</c>
    /// per arm, so a serialized arm round-trips through the base-typed property
    /// (e.g. <c>WorkflowDefinitionV1.Steps</c>).
    /// </summary>
    private static string EmitUnionBase(SchemaDoc doc, IReadOnlyDictionary<string, SchemaDoc> docs)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, usings: ["System.Text.Json.Serialization"]);

        if (!string.IsNullOrEmpty(doc.Description))
        {
            AppendXmlDoc(sb, doc.Description!, indent: string.Empty);
        }

        var discriminatorName = ResolveDiscriminatorName(doc, docs);
        sb.Append("[JsonPolymorphic(TypeDiscriminatorPropertyName = \"")
          .Append(discriminatorName).AppendLine("\")]");
        foreach (var armFile in doc.UnionArmRefs)
        {
            if (!docs.TryGetValue(armFile, out var arm))
            {
                continue;
            }

            var discriminator = arm.Properties
                .FirstOrDefault(p => string.Equals(p.WireName, discriminatorName, StringComparison.Ordinal))
                ?.ConstValue;
            if (discriminator is null)
            {
                continue;
            }

            sb.Append("[JsonDerivedType(typeof(").Append(arm.TypeName)
              .Append("), \"").Append(discriminator).AppendLine("\")]");
        }

        sb.Append("public abstract record ").Append(doc.TypeName).AppendLine(";");
        return sb.ToString();
    }

    private static string EmitEnum(SchemaDoc doc)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, usings: ["System.Text.Json.Serialization"]);

        if (!string.IsNullOrEmpty(doc.Description))
        {
            AppendXmlDoc(sb, doc.Description!, indent: string.Empty);
        }

        // Serialize as the string wire value (honouring [JsonStringEnumMemberName]),
        // not the numeric ordinal — cross-product consumers (Basileus, Zod) read
        // these as strings.
        sb.Append("[JsonConverter(typeof(JsonStringEnumConverter<")
          .Append(doc.TypeName).AppendLine(">))]");
        sb.Append("public enum ").Append(doc.TypeName).AppendLine();
        sb.AppendLine("{");

        for (var i = 0; i < doc.EnumValues.Count; i++)
        {
            var wire = doc.EnumValues[i];
            var member = ToPascalCase(wire);

            // Preserve the exact wire value when the C# member name diverges
            // from it (kebab-case, snake_case, camelCase, reserved words …),
            // so System.Text.Json round-trips the wire form verbatim.
            if (!string.Equals(member, wire, StringComparison.Ordinal))
            {
                sb.Append("    [JsonStringEnumMemberName(\"").Append(wire).AppendLine("\")]");
            }

            sb.Append("    ").Append(member).Append(',');
            sb.AppendLine();
            if (i < doc.EnumValues.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EmitRecord(
        SchemaDoc doc,
        IReadOnlyDictionary<string, SchemaDoc> docs,
        IReadOnlyDictionary<string, ArmBinding> armToUnion)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, usings: ["System.Collections.Generic", "System.Text.Json.Serialization"]);

        if (!string.IsNullOrEmpty(doc.Description))
        {
            AppendXmlDoc(sb, doc.Description!, indent: string.Empty);
        }

        // A discriminated-union arm derives from the union base and lets
        // System.Text.Json own the `kind` discriminator (so the arm must not
        // re-declare it as an ordinary property).
        var isUnionArm = armToUnion.TryGetValue(doc.FileName, out var binding);
        var emitProps = isUnionArm
            ? doc.Properties.Where(p => !string.Equals(p.WireName, binding!.DiscriminatorName, StringComparison.Ordinal)).ToList()
            : doc.Properties;

        sb.Append("public sealed record ").Append(doc.TypeName);
        if (isUnionArm)
        {
            sb.Append(" : ").Append(binding!.BaseTypeName);
        }

        sb.AppendLine();
        sb.AppendLine("{");

        for (var i = 0; i < emitProps.Count; i++)
        {
            var prop = emitProps[i];
            var propName = ToPascalCase(prop.WireName);
            var clrType = MapType(prop, docs, out var isValueEnum, out var isReference);

            if (!string.IsNullOrEmpty(prop.Description))
            {
                AppendXmlDoc(sb, prop.Description!, indent: "    ");
            }

            sb.Append("    [JsonPropertyName(\"").Append(prop.WireName).AppendLine("\")]");
            sb.Append("    public ").Append(clrType).Append(' ').Append(propName)
              .Append(" { get; init; }");

            // Required reference types need a null-forgiving default so the
            // nullable analyzer does not flag the uninitialised non-null member.
            // Value types (incl. enums) and optional members are left unassigned.
            if (prop.Required && isReference)
            {
                sb.Append(" = default!;");
            }

            sb.AppendLine();
            if (i < emitProps.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string MapType(
        PropertyInfo prop,
        IReadOnlyDictionary<string, SchemaDoc> docs,
        out bool isEnum,
        out bool isReference)
    {
        isEnum = false;
        string clr;

        if (prop.IsArray)
        {
            var itemType = prop.ItemRef is not null
                ? ResolveRef(prop.ItemRef, docs, out _)
                : MapScalar(prop.ItemScalarType);
            clr = $"IReadOnlyList<{itemType}>";
            isReference = true;
        }
        else if (prop.Ref is not null)
        {
            clr = ResolveRef(prop.Ref, docs, out isEnum);
            // A $ref to a record/open-object is a reference type; an enum ref
            // is a value type.
            isReference = !isEnum;
        }
        else
        {
            clr = MapScalar(prop.ScalarType);
            isReference = clr is "string" or "object";
        }

        if (!prop.Required)
        {
            clr += "?";
        }

        return clr;
    }

    private static string ResolveRef(
        string refName, IReadOnlyDictionary<string, SchemaDoc> docs, out bool isEnum)
    {
        isEnum = false;
        if (docs.TryGetValue(refName, out var target))
        {
            switch (target.Kind)
            {
                case SchemaKind.Enum:
                    isEnum = true;
                    return target.TypeName;
                case SchemaKind.Record:
                case SchemaKind.DiscriminatedUnion:
                    // A union ref resolves to the [JsonPolymorphic] base record.
                    return target.TypeName;
            }
        }

        // Open object / scalar alias / unknown ref: an opaque payload.
        return "object";
    }

    private static string MapScalar(string? jsonType) => jsonType switch
    {
        "string" => "string",
        "integer" => "int",
        "number" => "double",
        "boolean" => "bool",
        _ => "object",
    };

    private static void AppendHeader(StringBuilder sb, IEnumerable<string> usings)
    {
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//     Generated by Strategos.Contracts.Codegen from TypeSpec-emitted JSON Schema.");
        sb.AppendLine("//     DO NOT EDIT — hand-edits are rejected by the codegen-guard CI workflow.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        foreach (var u in usings)
        {
            sb.Append("using ").Append(u).AppendLine(";");
        }

        sb.AppendLine();
        sb.Append("namespace ").Append(Namespace).AppendLine(";");
        sb.AppendLine();
    }

    private static void AppendXmlDoc(StringBuilder sb, string text, string indent)
    {
        sb.Append(indent).AppendLine("/// <summary>");
        foreach (var line in text.Split('\n'))
        {
            sb.Append(indent).Append("/// ").AppendLine(System.Security.SecurityElement.Escape(line.TrimEnd('\r')));
        }

        sb.Append(indent).AppendLine("/// </summary>");
    }

    private static string ToPascalCase(string name)
    {
        var parts = name.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(char.ToUpper(part[0], CultureInfo.InvariantCulture));
            if (part.Length > 1)
            {
                sb.Append(part[1..]);
            }
        }

        return sb.ToString();
    }

    private enum SchemaKind
    {
        OpenObjectOrScalar,
        Enum,
        Record,
        DiscriminatedUnion,
    }

    /// <summary>
    /// Binds a discriminated-union arm to its union: the generated base type the
    /// arm derives from, the discriminator property's wire name (e.g. <c>kind</c>
    /// or <c>mode</c>), and the discriminator value (the arm's pinned const)
    /// System.Text.Json writes/reads.
    /// </summary>
    private sealed record ArmBinding(string BaseTypeName, string DiscriminatorName, string Discriminator);

    /// <summary>A property of an object schema, flattened from the raw JSON.</summary>
    private sealed record PropertyInfo
    {
        public required string WireName { get; init; }

        public string? Description { get; init; }

        public bool Required { get; init; }

        /// <summary>The <c>const</c> literal value of the property, if any (e.g. a union arm's <c>kind</c>).</summary>
        public string? ConstValue { get; init; }

        /// <summary>Document name a non-array property <c>$ref</c>s, if any.</summary>
        public string? Ref { get; init; }

        /// <summary>JSON scalar type for a non-ref, non-array property.</summary>
        public string? ScalarType { get; init; }

        public bool IsArray { get; init; }

        /// <summary>Document name the array items <c>$ref</c>, if any.</summary>
        public string? ItemRef { get; init; }

        /// <summary>JSON scalar type of the array items, if not a ref.</summary>
        public string? ItemScalarType { get; init; }
    }

    /// <summary>A classified JSON Schema document.</summary>
    private sealed record SchemaDoc
    {
        public required string FileName { get; init; }

        public required string TypeName { get; init; }

        public required SchemaKind Kind { get; init; }

        public string? Description { get; init; }

        public IReadOnlyList<string> EnumValues { get; init; } = [];

        public IReadOnlyList<PropertyInfo> Properties { get; init; } = [];

        /// <summary>Arm document names (file names) for a discriminated-union document.</summary>
        public IReadOnlyList<string> UnionArmRefs { get; init; } = [];

        public static SchemaDoc Classify(string fileName, JsonElement root)
        {
            var typeName = ToPascalCase(Path.GetFileNameWithoutExtension(fileName));
            var description = root.TryGetProperty("description", out var d) ? d.GetString() : null;

            // String enum → C# enum.
            if (root.TryGetProperty("enum", out var enumEl) && enumEl.ValueKind == JsonValueKind.Array)
            {
                var values = enumEl.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList();
                return new SchemaDoc
                {
                    FileName = fileName,
                    TypeName = typeName,
                    Kind = SchemaKind.Enum,
                    Description = description,
                    EnumValues = values,
                };
            }

            // Top-level anyOf of $refs → discriminated union (the TypeSpec
            // `union` form). Each arm carries its own `kind` const; the union
            // base is emitted as a [JsonPolymorphic] abstract record.
            if (root.TryGetProperty("anyOf", out var anyOfEl)
                && anyOfEl.ValueKind == JsonValueKind.Array)
            {
                var armRefs = anyOfEl.EnumerateArray()
                    .Where(a => a.TryGetProperty("$ref", out _))
                    .Select(a => Path.GetFileName(a.GetProperty("$ref").GetString())!)
                    .ToList();
                if (armRefs.Count > 0)
                {
                    return new SchemaDoc
                    {
                        FileName = fileName,
                        TypeName = typeName,
                        Kind = SchemaKind.DiscriminatedUnion,
                        Description = description,
                        UnionArmRefs = armRefs,
                    };
                }
            }

            // Object with named properties → record. Open objects (no
            // properties) are payload placeholders, not standalone types.
            var isObject = root.TryGetProperty("type", out var t)
                && t.ValueKind == JsonValueKind.String
                && t.GetString() == "object";
            if (isObject
                && root.TryGetProperty("properties", out var propsEl)
                && propsEl.ValueKind == JsonValueKind.Object
                && propsEl.EnumerateObject().Any())
            {
                var required = root.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.Array
                    ? reqEl.EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string?>();

                var props = new List<PropertyInfo>();
                foreach (var p in propsEl.EnumerateObject())
                {
                    props.Add(ReadProperty(p.Name, p.Value, required.Contains(p.Name)));
                }

                return new SchemaDoc
                {
                    FileName = fileName,
                    TypeName = typeName,
                    Kind = SchemaKind.Record,
                    Description = description,
                    Properties = props,
                };
            }

            return new SchemaDoc
            {
                FileName = fileName,
                TypeName = typeName,
                Kind = SchemaKind.OpenObjectOrScalar,
                Description = description,
            };
        }

        private static PropertyInfo ReadProperty(string wireName, JsonElement prop, bool required)
        {
            var description = prop.TryGetProperty("description", out var d) ? d.GetString() : null;

            // Array property.
            if (prop.TryGetProperty("type", out var pt)
                && pt.ValueKind == JsonValueKind.String
                && pt.GetString() == "array")
            {
                string? itemRef = null;
                string? itemScalar = null;
                if (prop.TryGetProperty("items", out var items))
                {
                    if (items.TryGetProperty("$ref", out var iref))
                    {
                        itemRef = Path.GetFileName(iref.GetString());
                    }
                    else if (items.TryGetProperty("type", out var itype))
                    {
                        itemScalar = itype.GetString();
                    }
                }

                return new PropertyInfo
                {
                    WireName = wireName,
                    Description = description,
                    Required = required,
                    IsArray = true,
                    ItemRef = itemRef,
                    ItemScalarType = itemScalar,
                };
            }

            // $ref property (enum or nested record).
            if (prop.TryGetProperty("$ref", out var refEl))
            {
                return new PropertyInfo
                {
                    WireName = wireName,
                    Description = description,
                    Required = required,
                    Ref = Path.GetFileName(refEl.GetString()),
                };
            }

            // Scalar (or inline union → falls through to object).
            string? scalar = prop.TryGetProperty("type", out var st) && st.ValueKind == JsonValueKind.String
                ? st.GetString()
                : null;

            // String const (e.g. a union arm's `kind` discriminator literal, or
            // the IR root's `schemaVersion: "1.0"`).
            string? constValue = prop.TryGetProperty("const", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;

            return new PropertyInfo
            {
                WireName = wireName,
                Description = description,
                Required = required,
                ScalarType = scalar,
                ConstValue = constValue,
            };
        }
    }
}

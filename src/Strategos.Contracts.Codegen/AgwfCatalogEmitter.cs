// =============================================================================
// <copyright file="AgwfCatalogEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strategos.Contracts.Codegen;

/// <summary>
/// Emits the AGWF single-source diagnostic catalog (#52) from the TypeSpec-emitted
/// JSON Schema. The AGWF codes and their metadata are authored once in
/// <c>Diagnostics/AgwfCatalog.tsp</c> as literal-typed entry models (R-lit; DR-1):
/// <c>tsp compile</c> emits one <c>AgwfEntry*.json</c> schema per code whose
/// fields carry <c>const</c> literals. This emitter reads those consts and
/// produces three artifacts:
/// <list type="bullet">
///   <item><c>Generated/AgwfCode.g.cs</c> — the C# enum with <em>symbolic</em>
///   member names (recovered from each entry model's name) and
///   <c>[JsonStringEnumMemberName]</c> mapping each to its <c>AGWF0xx</c> wire
///   value (so the round-trip is by member name, not ordinal — INV-5);</item>
///   <item><c>Generated/agwf-catalog.json</c> — the canonical data artifact
///   (manifest + ordered entries) Exarchos consumes to derive its TS enum;</item>
///   <item><c>docs/diagnostics/agwf.md</c> — the human reference table.</item>
/// </list>
/// The <see cref="RecordEmitter"/> skips the <c>AgwfCode</c> and
/// <c>AgwfEntry*</c> schemas so this emitter is their sole owner.
/// </summary>
public static class AgwfCatalogEmitter
{
    private const string Namespace = "Strategos.Contracts.Generated";
    private const string CatalogVersion = "0.2.0";

    /// <summary>The prefix of every AGWF entry schema file (e.g. <c>AgwfEntryEmptyWorkflowName.json</c>).</summary>
    public const string EntryPrefix = "AgwfEntry";

    /// <summary>The AGWF enum schema file name (owned by this emitter, not <see cref="RecordEmitter"/>).</summary>
    public const string EnumSchemaFileName = "AgwfCode.json";

    /// <summary>
    /// Reads the <c>AgwfEntry*.json</c> schemas under <paramref name="schemasDir"/>
    /// and emits the enum + catalog JSON into <paramref name="outputDir"/> and the
    /// Markdown reference into <c>docs/diagnostics/agwf.md</c> at the repo root.
    /// </summary>
    /// <param name="schemasDir">Directory containing the emitted JSON Schema documents.</param>
    /// <param name="outputDir">The <c>Generated/</c> directory the C# enum + catalog JSON are written to.</param>
    /// <returns>Process exit code (0 on success).</returns>
    public static async Task<int> RunAsync(string schemasDir, string outputDir)
    {
        var entryFiles = Directory.GetFiles(schemasDir, EntryPrefix + "*.json");
        if (entryFiles.Length == 0)
        {
            await Console.Error.WriteLineAsync(
                $"no AGWF entry schemas ({EntryPrefix}*.json) found in {schemasDir}").ConfigureAwait(false);
            return 1;
        }

        var entries = new List<AgwfEntry>();
        foreach (var file in entryFiles)
        {
            // The entry model name carries the symbolic enum member name:
            //   AgwfEntryEmptyWorkflowName.json -> EmptyWorkflowName
            var memberName = Path.GetFileNameWithoutExtension(file)[EntryPrefix.Length..];

            using var parsed = JsonDocument.Parse(await File.ReadAllTextAsync(file).ConfigureAwait(false));
            var props = parsed.RootElement.GetProperty("properties");

            entries.Add(new AgwfEntry(
                Name: memberName,
                Id: ConstOf(props, "id"),
                Severity: ConstOf(props, "severity"),
                Summary: ConstOf(props, "summary"),
                Remediation: ConstOf(props, "remediation"),
                Since: ConstOf(props, "since")));
        }

        // Stable, deterministic ordering by wire ID (ascending) — the gaps stay
        // gaps; the order is what the catalog JSON, the enum, and the doc table
        // all share so regeneration is idempotent.
        entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        Directory.CreateDirectory(outputDir);
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "AgwfCode.g.cs"), EmitEnum(entries)).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "AgwfCodes.g.cs"), EmitCodeConstants(entries)).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "agwf-catalog.json"), EmitCatalogJson(entries)).ConfigureAwait(false);

        // The reference page lives at the repo root (docs/diagnostics/agwf.md),
        // not under the output dir. When the emitter runs against a throwaway
        // output dir outside the repo (the codegen-guard's regenerate-into-temp
        // diff path), the repo root is unreachable — skip the doc in that case;
        // the JSON + C# under the temp dir are all the guard diffs.
        var docPath = TryResolveDocPath(outputDir);
        if (docPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(docPath)!);
            await File.WriteAllTextAsync(docPath, EmitMarkdown(entries)).ConfigureAwait(false);
        }

        return 0;
    }

    private static string ConstOf(JsonElement props, string field) =>
        props.GetProperty(field).GetProperty("const").GetString()!;

    /// <summary>
    /// Resolves <c>docs/diagnostics/agwf.md</c> at the repo root by walking up
    /// from the output directory to the directory containing <c>src/strategos.sln</c>.
    /// Returns <see langword="null"/> when no repo root is found (e.g. the emitter
    /// is running against a throwaway output dir outside the repository).
    /// </summary>
    private static string? TryResolveDocPath(string outputDir)
    {
        var dir = Path.GetFullPath(outputDir);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "src", "strategos.sln")))
            {
                return Path.Combine(dir, "docs", "diagnostics", "agwf.md");
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static string EmitEnum(IReadOnlyList<AgwfEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//     Generated by Strategos.Contracts.Codegen from TypeSpec-emitted JSON Schema.");
        sb.AppendLine("//     DO NOT EDIT — hand-edits are rejected by the codegen-guard CI workflow.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.Append("namespace ").Append(Namespace).AppendLine(";");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// AGWF workflow source-generator diagnostic codes (#52). Single canonical");
        sb.AppendLine("/// source: authored in <c>Diagnostics/AgwfCatalog.tsp</c>. Member names are");
        sb.AppendLine("/// the stable contract Exarchos&apos;s TypeScript enum round-trips against by");
        sb.AppendLine("/// name (not ordinal — INV-5); each maps to its <c>AGWF0xx</c> wire value.");
        sb.AppendLine("/// </summary>");
        sb.Append("[JsonConverter(typeof(JsonStringEnumConverter<AgwfCode>))]").AppendLine();
        sb.AppendLine("public enum AgwfCode");
        sb.AppendLine("{");

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            sb.Append("    /// <summary>").Append(SecurityEscape(e.Id)).Append(" — ")
              .Append(SecurityEscape(e.Summary)).AppendLine(".</summary>");
            sb.Append("    [JsonStringEnumMemberName(\"").Append(e.Id).AppendLine("\")]");
            sb.Append("    ").Append(e.Name).Append(',').AppendLine();
            if (i < entries.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Emits a netstandard2.0-safe constants class (<c>AgwfCodes</c>) mapping each
    /// symbolic member name to its <c>AGWF0xx</c> wire string as a
    /// <c>public const string</c>. The <c>Strategos.Generators</c> analyzer
    /// (netstandard2.0, no <c>System.Text.Json</c>) links this file as source so
    /// <c>WorkflowDiagnostics</c> sources its diagnostic IDs from the single
    /// catalog source without hand-authoring any <c>AGWF0xx</c> literal (#52).
    /// </summary>
    private static string EmitCodeConstants(IReadOnlyList<AgwfEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//     Generated by Strategos.Contracts.Codegen from TypeSpec-emitted JSON Schema.");
        sb.AppendLine("//     DO NOT EDIT — hand-edits are rejected by the codegen-guard CI workflow.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine();
        sb.Append("namespace ").Append(Namespace).AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// AGWF diagnostic code identities (#52), single-sourced from");
        sb.AppendLine("    /// <c>AgwfCatalog.tsp</c>. Plain <c>const</c> strings (netstandard2.0-safe)");
        sb.AppendLine("    /// so the source generator can reference the codes without hand-authoring");
        sb.AppendLine("    /// any <c>AGWF0xx</c> literal.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static class AgwfCodes");
        sb.AppendLine("    {");
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            sb.Append("        /// <summary>").Append(SecurityEscape(e.Id)).Append(" — ")
              .Append(SecurityEscape(e.Summary)).AppendLine(".</summary>");
            sb.Append("        public const string ").Append(e.Name)
              .Append(" = \"").Append(e.Id).AppendLine("\";");
            if (i < entries.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EmitCatalogJson(IReadOnlyList<AgwfEntry> entries)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,

            // The catalog is a human-readable published data artifact; emit
            // verbatim characters (no '/< escaping) so the JSON and
            // the Markdown table carry identical, readable text.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        var manifest = new CatalogManifest(
            CatalogVersion: CatalogVersion,
            Count: entries.Count,
            Entries: [.. entries]);

        // Trailing newline so the file is POSIX-clean and the codegen-guard diff
        // is stable across editors.
        return JsonSerializer.Serialize(manifest, options) + "\n";
    }

    private static string EmitMarkdown(IReadOnlyList<AgwfEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- =============================================================== -->");
        sb.AppendLine("<!-- GENERATED by Strategos.Contracts.Codegen from AgwfCatalog.tsp.  -->");
        sb.AppendLine("<!-- DO NOT EDIT — hand-edits are rejected by the codegen-guard CI.  -->");
        sb.AppendLine("<!-- =============================================================== -->");
        sb.AppendLine();
        sb.AppendLine("# AGWF — Workflow generator diagnostics");
        sb.AppendLine();
        sb.AppendLine(
            "The `AGWF` codes are the workflow source-generator diagnostics. They are");
        sb.AppendLine(
            "single-sourced from `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp`");
        sb.AppendLine(
            "(#52). The Roslyn analyzer (`WorkflowDiagnostics`) sources its code identity");
        sb.AppendLine(
            "from the generated `AgwfCode` enum; this table is generated from the same source.");
        sb.AppendLine();
        sb.AppendLine("| id | severity | summary | remediation | since |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var e in entries)
        {
            sb.Append("| ").Append(e.Id)
              .Append(" | ").Append(e.Severity)
              .Append(" | ").Append(MdCell(e.Summary))
              .Append(" | ").Append(MdCell(e.Remediation))
              .Append(" | ").Append(e.Since)
              .AppendLine(" |");
        }

        return sb.ToString();
    }

    /// <summary>Escapes a value for safe inclusion in a Markdown table cell.</summary>
    private static string MdCell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", string.Empty, StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);

    private static string SecurityEscape(string value) =>
        System.Security.SecurityElement.Escape(value) ?? value;

    /// <summary>A single AGWF catalog entry (symbolic name + wire id + metadata).</summary>
    private sealed record AgwfEntry(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("severity")] string Severity,
        [property: JsonPropertyName("summary")] string Summary,
        [property: JsonPropertyName("remediation")] string Remediation,
        [property: JsonPropertyName("since")] string Since);

    /// <summary>The catalog manifest serialized to <c>agwf-catalog.json</c>.</summary>
    private sealed record CatalogManifest(
        [property: JsonPropertyName("catalog_version")] string CatalogVersion,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("entries")] IReadOnlyList<AgwfEntry> Entries);
}

// =============================================================================
// <copyright file="InvariantCatalogParser.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Strategos.Architecture.Tests;

/// <summary>
/// A deliberately small, line-based reader for the YAML frontmatter of
/// <c>.exarchos/invariants.md</c>. It understands exactly the shape the catalog uses
/// (schema-version 3: a top-level <c>invariants:</c> list whose items carry scalar
/// keys, the <c>applies-to</c> / <c>references</c> lists, the <c>severity</c> and
/// <c>enforcement</c> mappings and folded <c>summary</c> / <c>audit-prompt</c>
/// scalars) and throws with the file, line and offending text on anything else.
/// No YAML package is pulled in for a file this regular; a shape the parser does
/// not know is a change to review, not something to skip over.
/// </summary>
internal static class InvariantCatalogParser
{
    /// <summary>Repo-relative path of the catalog.</summary>
    public const string CatalogPath = ".exarchos/invariants.md";

    private const string Fence = "---";
    private const string IdItem = "- id:";

    /// <summary>Parses the catalog at <see cref="CatalogPath"/>.</summary>
    public static InvariantCatalog Load() => Parse(RepoTree.RequireFile(CatalogPath));

    /// <summary>Parses the catalog at an absolute path.</summary>
    public static InvariantCatalog Parse(string absolutePath)
    {
        var name = RepoTree.Relative(absolutePath);
        var lines = File.ReadAllLines(absolutePath);
        if (lines.Length == 0 || lines[0] != Fence)
        {
            Fail(name, 1, "expected the file to open with a '---' frontmatter fence", lines.Length == 0 ? string.Empty : lines[0]);
        }

        var end = Array.IndexOf(lines, Fence, 1);
        if (end < 0)
        {
            Fail(name, lines.Length, "the '---' frontmatter fence is never closed", string.Empty);
        }

        int? schemaVersion = null;
        var inInvariants = false;
        var entries = new List<InvariantEntry>();
        EntryBuilder? current = null;
        List<string>? currentList = null;
        string? nested = null;
        int? folded = null;

        for (var i = 1; i < end; i++)
        {
            var raw = lines[i];
            var lineNo = i + 1;
            var text = raw.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            var indent = raw.Length - raw.TrimStart().Length;
            if (folded is int foldedIndent)
            {
                if (indent > foldedIndent)
                {
                    continue;
                }

                folded = null;
            }

            if (indent == 0)
            {
                currentList = null;
                nested = null;
                if (TrySplit(text, out var key, out var value) && key == "schema-version")
                {
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                    {
                        Fail(name, lineNo, "schema-version must be an integer", raw);
                    }

                    schemaVersion = version;
                }
                else if (text == "invariants:")
                {
                    inInvariants = true;
                }
                else
                {
                    Fail(name, lineNo, "unexpected top-level line (only 'schema-version:' and 'invariants:' are known)", raw);
                }

                continue;
            }

            if (!inInvariants)
            {
                Fail(name, lineNo, "indented line before 'invariants:'", raw);
            }

            if (indent == 2)
            {
                if (!text.StartsWith(IdItem, StringComparison.Ordinal))
                {
                    Fail(name, lineNo, "expected a '- id: U-N' list item", raw);
                }

                Flush(current, entries);
                current = new EntryBuilder(text[IdItem.Length..].Trim(), lineNo);
                currentList = null;
                nested = null;
                continue;
            }

            if (current is null)
            {
                Fail(name, lineNo, "entry content before the first '- id:' item", raw);
            }

            if (indent == 4)
            {
                currentList = null;
                nested = null;
                if (!TrySplit(text, out var key, out var value))
                {
                    Fail(name, lineNo, "expected 'key: value' at entry level", raw);
                }

                switch (key)
                {
                    case "applies-to" when value.Length == 0:
                        currentList = current.AppliesTo;
                        break;
                    case "references" when value.Length == 0:
                        currentList = current.References;
                        break;
                    case "summary" when value is ">" or "|":
                        folded = 4;
                        break;
                    case "severity" when value.Length == 0:
                        nested = "severity";
                        break;
                    case "enforcement" when value.Length == 0:
                        nested = "enforcement";
                        break;
                    case "dimension" when value.Length > 0:
                        current.Dimension = value;
                        break;
                    case "axis" or "cost-of-load" or "integrity-class" when value.Length > 0:
                        break;
                    default:
                        Fail(name, lineNo, $"unexpected entry key '{key}' (or a scalar where a block was expected)", raw);
                        break;
                }

                continue;
            }

            if (indent == 6)
            {
                if (currentList is not null && text.StartsWith("- ", StringComparison.Ordinal))
                {
                    currentList.Add(text[2..].Trim());
                    continue;
                }

                if (nested is not null && TrySplit(text, out var key, out var value))
                {
                    switch (nested, key)
                    {
                        case ("severity", "default") when value.Length > 0:
                            current.Severity = value;
                            continue;
                        case ("enforcement", "mode") when value.Length > 0:
                            current.Mode = value;
                            continue;
                        case ("enforcement", "audit-prompt") when value is ">" or "|":
                            folded = 6;
                            continue;
                    }
                }

                Fail(name, lineNo, "unexpected nested line (known: list items under applies-to/references, severity.default, enforcement.mode, enforcement.audit-prompt)", raw);
            }

            Fail(name, lineNo, $"unexpected indentation of {indent} spaces", raw);
        }

        Flush(current, entries);

        if (schemaVersion is null)
        {
            Fail(name, end + 1, "missing 'schema-version'", string.Empty);
        }

        if (!inInvariants)
        {
            Fail(name, end + 1, "missing 'invariants:'", string.Empty);
        }

        return new InvariantCatalog(schemaVersion.Value, entries);
    }

    private static bool TrySplit(string text, out string key, out string value)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = text[..colon].Trim();
        value = text[(colon + 1)..].Trim();
        return true;
    }

    private static void Flush(EntryBuilder? current, List<InvariantEntry> entries)
    {
        if (current is not null)
        {
            entries.Add(current.Build());
        }
    }

    [DoesNotReturn]
    private static void Fail(string file, int line, string message, string raw) =>
        throw new InvalidDataException($"{file}:{line}: {message}: '{raw}'");

    private sealed class EntryBuilder(string id, int line)
    {
        public List<string> AppliesTo { get; } = [];

        public List<string> References { get; } = [];

        public string? Dimension { get; set; }

        public string? Severity { get; set; }

        public string? Mode { get; set; }

        public InvariantEntry Build()
        {
            if (id.Length == 0)
            {
                Fail(CatalogPath, line, "an entry has an empty id", IdItem);
            }

            return new InvariantEntry(id, line, Dimension, AppliesTo, References, Severity, Mode);
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="AgwfSingleSourceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Strategos.Generators.Tests.Diagnostics;

/// <summary>
/// T6 — the AGWF single-source grep gate (DIM-5 / INV-5). The diagnostic codes
/// are authored once in TypeSpec (<c>AgwfCatalog.tsp</c>) and surfaced to the
/// generator via the generated <c>AgwfCodes</c> constants. No production C#
/// source may hand-author an <c>AGWF0xx</c> literal — every reporting path must
/// route through the generated constants. This test fails while
/// <c>WorkflowDiagnostics.cs</c> still embeds <c>id: "AGWF0xx"</c> literals.
///
/// Scope mirrors the issue's grep AC: <c>src/Strategos*</c> C# sources excluding
/// emitter-owned <c>Generated/</c> output. Test projects are excluded — they are
/// <em>consumers</em> asserting against the public diagnostic IDs, not the
/// authoring surface the single-source gate guards.
/// </summary>
[Property("Category", "Unit")]
public class AgwfSingleSourceTests
{
    // Mirrors the issue's grep AC verbatim: any AGWF0xx token (code literal OR
    // doc-comment) in production source is a parallel-authoring surface.
    private static readonly Regex AgwfLiteral = new(
        "AGWF0[0-9]{2}", RegexOptions.Compiled);

    /// <summary>
    /// Walks every production C# source under <c>src/Strategos*</c> (excluding
    /// <c>Generated/</c>, build output, and <c>*.Tests</c> projects) and asserts
    /// none embeds a quoted <c>AGWF0xx</c> literal.
    /// </summary>
    [Test]
    public async Task WorkflowDiagnostics_NoHandAuthoredAgwfLiterals_GrepGate()
    {
        var srcRoot = FindSrcRoot();

        var offenders = new List<string>();
        foreach (var dir in Directory.GetDirectories(srcRoot, "Strategos*"))
        {
            var name = Path.GetFileName(dir);
            if (name.EndsWith(".Tests", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (IsExcludedPath(file))
                {
                    continue;
                }

                var text = await File.ReadAllTextAsync(file);
                if (AgwfLiteral.IsMatch(text))
                {
                    offenders.Add(Path.GetRelativePath(srcRoot, file));
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("AGWF codes are single-sourced (#52/INV-5); no production C# may hand-author an "
                + "\"AGWF0xx\" literal. Offending files: " + string.Join(", ", offenders));
    }

    private static bool IsExcludedPath(string file)
    {
        var normalized = file.Replace('\\', '/');
        return normalized.Contains("/Generated/", StringComparison.Ordinal)
            || normalized.Contains("/bin/", StringComparison.Ordinal)
            || normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string FindSrcRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var src = Path.Combine(dir, "src");
            if (File.Exists(Path.Combine(src, "strategos.sln")))
            {
                return src;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate src/ (no src/strategos.sln found walking up from "
            + AppContext.BaseDirectory + ").");
    }
}

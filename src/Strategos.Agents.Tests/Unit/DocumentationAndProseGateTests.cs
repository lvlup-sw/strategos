// =============================================================================
// <copyright file="DocumentationAndProseGateTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Strategos.Agents.Tests.Unit;

/// <summary>
/// T-023 / DR-7 / DR-10 / DR-11 documentation + grep-gate enforcement.
/// Five tests, one per gate:
///   1. README trivial example fits in ≤15 lines including usings.
///   2. CHANGELOG has the 2.7.0 breaking entry for the agent-step contract.
///   3. scripts/check-agag-hygiene.sh exits 0.
///   4. scripts/check-catch-discipline.sh exits 0.
///   5. scripts/check-prose.sh exits 0.
/// Bash-based gates are skipped on non-Unix runners with a clear marker.
/// </summary>
[Property("Category", "Unit")]
public sealed class DocumentationAndProseGateTests
{
    [Test]
    public async Task Readme_TrivialExample_FitsInFifteenLinesIncludingUsings()
    {
        var readmePath = Path.Combine(RepoRoot(), "src", "Strategos.Agents", "README.md");
        var content = await File.ReadAllTextAsync(readmePath);

        // The "Agent Steps" section's first fenced csharp block is the canonical
        // trivial example. Find any fenced ```csharp block under that heading
        // and assert its content (between the fences, exclusive) is ≤15 lines.
        var match = Regex.Match(
            content,
            "###\\s+Agent Steps\\s*\\n.*?```csharp\\s*\\n(?<body>.*?)\\n```",
            RegexOptions.Singleline);

        await Assert.That(match.Success).IsTrue();

        var body = match.Groups["body"].Value;
        var lineCount = body.Split('\n').Length;
        await Assert.That(lineCount).IsLessThanOrEqualTo(15);
    }

    [Test]
    public async Task Changelog_270Section_HasBreakingAgentStepContractEntry()
    {
        var changelogPath = Path.Combine(RepoRoot(), "CHANGELOG.md");
        var content = await File.ReadAllTextAsync(changelogPath);

        // Carve out the 2.7.0 section: from "## [2.7.0]" (NOT "## [2.7.0-preview.")
        // up to (but not including) the next "## [" heading.
        var sectionMatch = Regex.Match(
            content,
            "^##\\s+\\[2\\.7\\.0\\](?![\\.\\-])(?<body>.*?)(?=^##\\s+\\[)",
            RegexOptions.Multiline | RegexOptions.Singleline);

        await Assert.That(sectionMatch.Success).IsTrue();

        var section = sectionMatch.Groups["body"].Value;
        await Assert.That(section.Contains("Changed (BREAKING)")).IsTrue();
        await Assert.That(section.Contains("Agent step contract")).IsTrue();
    }

    [Test]
    public async Task Readme_UsesWithToolSource_NotWithMcpToolSource()
    {
        var readmePath = Path.Combine(RepoRoot(), "src", "Strategos.Agents", "README.md");
        var content = await File.ReadAllTextAsync(readmePath);

        // The README must demonstrate the generalized .WithToolSource(...) API and
        // register BOTH an in-process AgentToolSource and an McpToolSource (T-017).
        await Assert.That(content.Contains("WithToolSource", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("AgentToolSource", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("McpToolSource", StringComparison.Ordinal)).IsTrue();

        // The pre-rename surface must be gone from the prose.
        await Assert.That(content.Contains("WithMcpToolSource", StringComparison.Ordinal)).IsFalse();
        await Assert.That(content.Contains("IMcpToolSource", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Changelog_DocumentsToolSourceIn270()
    {
        var changelogPath = Path.Combine(RepoRoot(), "CHANGELOG.md");
        var content = await File.ReadAllTextAsync(changelogPath);

        var sectionMatch = Regex.Match(
            content,
            "^##\\s+\\[2\\.7\\.0\\](?![\\.\\-])(?<body>.*?)(?=^##\\s+\\[)",
            RegexOptions.Multiline | RegexOptions.Singleline);

        await Assert.That(sectionMatch.Success).IsTrue();

        var section = sectionMatch.Groups["body"].Value;
        // Intra-development framing: IToolSource / AgentToolSource / WithToolSource
        // documented as part of 2.7.0's agent surface (not a migration recipe).
        await Assert.That(section.Contains("IToolSource", StringComparison.Ordinal)).IsTrue();
        await Assert.That(section.Contains("AgentToolSource", StringComparison.Ordinal)).IsTrue();
        await Assert.That(section.Contains("WithToolSource", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ProductionSource_HasNoLegacyMcpToolSourceNames()
    {
        // T-017 grep gate: zero IMcpToolSource / WithMcpToolSource literals in
        // PRODUCTION source. Scope: src/Strategos.Agents/ + src/Strategos.Agents.Mcp/,
        // excluding *.Tests projects (whose negative-assertion tests legitimately
        // name the deleted symbols).
        var root = RepoRoot();
        var productionDirs = new[]
        {
            Path.Combine(root, "src", "Strategos.Agents"),
            Path.Combine(root, "src", "Strategos.Agents.Mcp"),
        };

        var offenders = new System.Collections.Generic.List<string>();
        foreach (var dir in productionDirs)
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (ext is not (".cs" or ".md" or ".csproj"))
                {
                    continue;
                }

                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var text = await File.ReadAllTextAsync(file);
                if (text.Contains("IMcpToolSource", StringComparison.Ordinal)
                    || text.Contains("WithMcpToolSource", StringComparison.Ordinal))
                {
                    offenders.Add(file);
                }
            }
        }

        await Assert.That(offenders)
            .IsEmpty()
            .Because("production source must use IToolSource / WithToolSource, not the deleted MCP-only names.");
    }

    [Test]
    public async Task Readme_HasStreamingExample()
    {
        var readmePath = Path.Combine(RepoRoot(), "src", "Strategos.Agents", "README.md");
        var content = await File.ReadAllTextAsync(readmePath);

        // T-018: the streaming section must demonstrate WithStreaming + IStreamingHandler
        // and the example body must be ≤15 lines.
        var match = Regex.Match(
            content,
            "###\\s+Streaming Responses\\s*\\n.*?```csharp\\s*\\n(?<body>.*?)\\n```",
            RegexOptions.Singleline);

        await Assert.That(match.Success).IsTrue();
        await Assert.That(content.Contains("WithStreaming", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("IStreamingHandler", StringComparison.Ordinal)).IsTrue();

        var lineCount = match.Groups["body"].Value.Split('\n').Length;
        await Assert.That(lineCount).IsLessThanOrEqualTo(15);
    }

    [Test]
    public async Task Readme_Diagnostics_EnumeratesAGAG007And009()
    {
        var readmePath = Path.Combine(RepoRoot(), "src", "Strategos.Agents", "README.md");
        var content = await File.ReadAllTextAsync(readmePath);

        // T-019: AGAG007 + AGAG009 enumerated with their exception types; AGAG008 noted reserved.
        await Assert.That(content.Contains("AGAG007", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("AgentToolSourceException", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("AGAG009", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("AgentStreamingException", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("AGAG008", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Contains("reserved", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task AgagLiterals_OnlyInDiagnosticsAndExceptionSites()
    {
        // T-019 grep gate: quoted "AGAG0NN" literals appear ONLY in AgentDiagnostics.cs
        // and exception-construction sites under src/Strategos.Agents/. Comments that
        // mention AGAG codes without quotes are fine. Mirrors check-agag-hygiene.sh but
        // expressed in-process so it runs on every platform.
        var targetDir = Path.Combine(RepoRoot(), "src", "Strategos.Agents");
        var literal = new Regex("\"AGAG[0-9]+\"");

        var offenders = new System.Collections.Generic.List<string>();
        foreach (var file in Directory.EnumerateFiles(targetDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var inDiagnostics = file.Contains($"{Path.DirectorySeparatorChar}Diagnostics{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
            var inExceptions = file.Contains($"{Path.DirectorySeparatorChar}Exceptions{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
            if (inDiagnostics || inExceptions)
            {
                continue;
            }

            var text = await File.ReadAllTextAsync(file);
            if (literal.IsMatch(text))
            {
                offenders.Add(file);
            }
        }

        await Assert.That(offenders)
            .IsEmpty()
            .Because("AGAG literals must live only in Diagnostics/ or Exceptions/ — use AgentDiagnostics.AGAG### constants elsewhere.");
    }

    [Test]
    public async Task Source_AgentDiagnosticsLiterals_OnlyAppearInDiagnosticsAndExceptionFiles()
    {
        await RunShellGateAsync("check-agag-hygiene.sh");
    }

    [Test]
    public async Task Source_NoParameterlessCatch_ExistsInStrategosAgents()
    {
        await RunShellGateAsync("check-catch-discipline.sh");
    }

    [Test]
    public async Task Prose_NoAIVocabularyClusters_InStrategosAgentsSourceAndDocs()
    {
        await RunShellGateAsync("check-prose.sh");
    }

    // -------------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------------

    private static string RepoRoot()
    {
        // Walk up from the test binary's location until we find global.json.
        // Tests run from bin/Debug/netX/, so this terminates within a few hops.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                $"Could not locate repository root (global.json) from {AppContext.BaseDirectory}.");
        }

        return dir.FullName;
    }

    private static async Task RunShellGateAsync(string scriptName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // The grep gates require bash; CI is Linux and macOS devs have bash.
            // Windows-only contributors see a SKIP marker rather than a false GREEN.
            Console.WriteLine($"[SKIP] {scriptName} requires bash; not available on Windows.");
            return;
        }

        var scriptPath = Path.Combine(RepoRoot(), "scripts", scriptName);
        await Assert.That(File.Exists(scriptPath))
            .IsTrue()
            .Because($"expected gate script at {scriptPath}");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = scriptPath,
            WorkingDirectory = RepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to launch bash for {scriptName}.");

        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync();

        await Assert.That(proc.ExitCode)
            .IsEqualTo(0)
            .Because(
                $"{scriptName} should exit 0 against the current tree.\n" +
                $"stdout:\n{stdout}\nstderr:\n{stderr}");
    }
}

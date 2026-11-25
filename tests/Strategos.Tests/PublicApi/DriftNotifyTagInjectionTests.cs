// =============================================================================
// <copyright file="DriftNotifyTagInjectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;
using System.Text;

using Strategos.Tests.FixtureExport;

namespace Strategos.Tests.PublicApi;

/// <summary>
/// The public-API drift notify job copies the nearest <c>v*</c> tag into a step
/// output and then places that output in the notify step's <c>run</c> script.
/// A tag is attacker-controlled (repository write includes tag pushes; the
/// default-branch ruleset does not cover tags). These tests plant such a tag
/// and run the workflow steps the way the Actions runner does. They fail while
/// the tag is still able to execute in the notify step.
/// </summary>
public sealed class DriftNotifyTagInjectionTests
{
    private const string Placeholder = "placeholder-exarchos-issues-pat";

    private static string WorkflowPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, ".github", "workflows", "public-api-drift.yml");

    private static string PayloadScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "build-drift-issue.sh");

    [Test]
    public async Task QuotedTag_DoesNotExecute_WhenNotifyCommandSucceeds()
    {
        // The quote closes the run-script argument and the rest of the tag is a
        // following command. The runner invokes bash -e, so that command runs
        // when the notify script exits 0 (the issue was filed).
        await RunAttackAsync(ghExitCode: 0, PayloadShape.QuoteThenCommand);
    }

    [Test]
    public async Task QuotedTag_DoesNotExecute_WhenNotifyCommandFails()
    {
        // Same quote break, but the payload is a command substitution attached
        // to the first command. bash -e does not skip that expansion when the
        // notify script exits non-zero.
        await RunAttackAsync(ghExitCode: 1, PayloadShape.QuoteThenSubstitution);
    }

    private static async Task RunAttackAsync(int ghExitCode, PayloadShape shape)
    {
        var root = Path.Combine(Path.GetTempPath(), "strategos-drift-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var marker = Path.Combine(root, "canary.txt");
        var tag = BuildTag(shape, marker);
        try
        {
            var head = await CreateRepositoryAsync(root, tag);
            var described = (await RunAsync("git", root, "describe", "--tags", "--abbrev=0", "--match", "v*", "HEAD^")).Stdout.Trim();
            await Assert.That(described).IsEqualTo(tag);

            var workflow = await File.ReadAllTextAsync(WorkflowPath);
            var steps = ParseNotifySteps(workflow);
            var diffStep = steps.Single(step => step.Id == "diff");
            var notifyStep = steps.Single(step => step.Run is not null && step.Run.Contains("build-drift-issue.sh", StringComparison.Ordinal));

            var outputPath = Path.Combine(root, "github-output.txt");
            await File.WriteAllTextAsync(outputPath, string.Empty);
            var diffScript = SubstituteExpressions(
                diffStep.Run ?? throw new InvalidOperationException("diff step has no run script."),
                expr => Eval(expr, head, outputs: null, stepEnv: null));
            await RunBashAsync(root, diffScript, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["GITHUB_OUTPUT"] = outputPath,
            });

            if (File.Exists(marker))
            {
                throw new InvalidOperationException("The diff step executed the tag before the notify step.");
            }

            var outputs = ParseGitHubOutput(await File.ReadAllTextAsync(outputPath));
            if (!ConditionHolds(notifyStep.If, head, outputs))
            {
                await Assert.That(File.Exists(marker)).IsFalse();
                return;
            }

            var stepEnv = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in notifyStep.Env)
            {
                stepEnv[key] = SubstituteExpressions(value, expr => Eval(expr, head, outputs, stepEnv));
            }

            Directory.CreateDirectory(Path.Combine(root, "scripts"));
            File.Copy(PayloadScriptPath, Path.Combine(root, "scripts", "build-drift-issue.sh"), overwrite: true);

            var stubDir = Path.Combine(root, "stub-bin");
            Directory.CreateDirectory(stubDir);
            var ghLog = Path.Combine(root, "gh.log");
            var ghStub = Path.Combine(stubDir, "gh");
            await File.WriteAllTextAsync(
                ghStub,
                "#!/bin/sh\nprintf '%s\\n' \"$*\" >> \"$GH_LOG\"\nexit \"$GH_EXIT\"\n");
            var chmod = await RunAsync("chmod", root, "u+x", ghStub);
            if (chmod.ExitCode != 0)
            {
                throw new InvalidOperationException(chmod.Stderr);
            }

            var notifyScript = SubstituteExpressions(
                notifyStep.Run!,
                expr => Eval(expr, head, outputs, stepEnv));
            var notifyEnv = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["GH_LOG"] = ghLog,
                ["GH_EXIT"] = ghExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["PATH"] = stubDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            };
            foreach (var (key, value) in stepEnv)
            {
                notifyEnv[key] = value;
            }

            await RunBashAsync(root, notifyScript, notifyEnv);

            if (File.Exists(marker))
            {
                var wrotePlaceholder = await File.ReadAllTextAsync(marker) == Placeholder;
                throw new InvalidOperationException(
                    wrotePlaceholder
                        ? "The notify step executed the tag and wrote EXARCHOS_ISSUES_PAT to the marker file."
                        : "The notify step executed the tag and created the marker file.");
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private enum PayloadShape
    {
        QuoteThenCommand,
        QuoteThenSubstitution,
    }

    private static string BuildTag(PayloadShape shape, string marker)
    {
        // No space and no colon: both are rejected by git check-ref-format.
        // ${IFS} expands to a space when the notify shell parses the tag.
        return shape switch
        {
            PayloadShape.QuoteThenCommand =>
                "v9\";printf${IFS}%s${IFS}$EXARCHOS_ISSUES_PAT${IFS}>${IFS}" + marker + ";#",
            PayloadShape.QuoteThenSubstitution =>
                "v9\"$(printf${IFS}%s${IFS}$EXARCHOS_ISSUES_PAT${IFS}>${IFS}" + marker + ");#",
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown payload shape."),
        };
    }

    private static async Task<string> CreateRepositoryAsync(string root, string tag)
    {
        await RunAsync("git", root, "init", "-q", "-b", "main");
        await RunAsync("git", root, "config", "user.email", "drift-test@example.com");
        await RunAsync("git", root, "config", "user.name", "drift-test");
        var baseline = Path.Combine(root, "src", "Strategos", "PublicAPI");
        var ontology = Path.Combine(root, "src", "Strategos.Ontology");
        Directory.CreateDirectory(baseline);
        Directory.CreateDirectory(ontology);
        await File.WriteAllTextAsync(Path.Combine(baseline, "PublicAPI.Shipped.txt"), "base\n");
        await File.WriteAllTextAsync(Path.Combine(ontology, "PublicAPI.Shipped.txt"), "ont\n");
        await RunAsync("git", root, "add", ".");
        await RunAsync("git", root, "commit", "-q", "-m", "base");
        await RunAsync("git", root, "tag", "v1.0.0");
        await File.WriteAllTextAsync(Path.Combine(baseline, "PublicAPI.Shipped.txt"), "unchanged\n");
        await RunAsync("git", root, "add", ".");
        await RunAsync("git", root, "commit", "-q", "-m", "parent");
        await RunAsync("git", root, "tag", tag);
        await File.WriteAllTextAsync(Path.Combine(baseline, "PublicAPI.Shipped.txt"), "changed\n");
        await RunAsync("git", root, "add", ".");
        await RunAsync("git", root, "commit", "-q", "-m", "push");
        var head = (await RunAsync("git", root, "rev-parse", "HEAD")).Stdout.Trim();
        return head;
    }

    /// <summary>
    /// Insert <c>${{ }}</c> values once, in order, without shell escaping.
    /// This is the runner's <c>format()</c> concatenation of <c>ConvertToString()</c>
    /// results (actions/runner v2.338.2 TemplateReader + Format.ToString). Inserted
    /// text is not scanned for further expressions.
    /// </summary>
    private static string SubstituteExpressions(string text, Func<string, string> eval)
    {
        var builder = new StringBuilder();
        var index = 0;
        while (index < text.Length)
        {
            var start = text.IndexOf("${{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                builder.Append(text.AsSpan(index));
                break;
            }

            builder.Append(text.AsSpan(index, start - index));
            var end = text.IndexOf("}}", start + 3, StringComparison.Ordinal);
            if (end < 0)
            {
                throw new InvalidOperationException("Unclosed ${{ in workflow text.");
            }

            builder.Append(eval(text.Substring(start + 3, end - (start + 3))));
            index = end + 2;
        }

        return builder.ToString();
    }

    private static string Eval(
        string expression,
        string head,
        IReadOnlyDictionary<string, string>? outputs,
        IReadOnlyDictionary<string, string>? stepEnv)
    {
        var expr = expression.Trim();
        if (expr == "github.server_url")
        {
            return "https://github.com";
        }

        if (expr == "github.repository")
        {
            return "lvlup-sw/strategos";
        }

        if (expr == "github.sha")
        {
            return head;
        }

        const string outputPrefix = "steps.diff.outputs.";
        if (expr.StartsWith(outputPrefix, StringComparison.Ordinal))
        {
            var name = expr[outputPrefix.Length..];
            if (outputs is null || !outputs.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException($"Step output '{name}' was not written.");
            }

            return value;
        }

        const string envPrefix = "env.";
        if (expr.StartsWith(envPrefix, StringComparison.Ordinal))
        {
            var name = expr[envPrefix.Length..];
            if (stepEnv is null || !stepEnv.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException($"Step env '{name}' was not set.");
            }

            return value;
        }

        if (expr == "secrets.EXARCHOS_ISSUES_PAT")
        {
            return Placeholder;
        }

        throw new InvalidOperationException($"Workflow expression '{expr}' is not part of this attack path.");
    }

    private static bool ConditionHolds(
        string? condition,
        string head,
        IReadOnlyDictionary<string, string> outputs)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        var parts = condition.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var sides = part.Split("==", 2, StringSplitOptions.TrimEntries);
            if (sides.Length != 2)
            {
                throw new InvalidOperationException($"Unsupported step if: {condition}");
            }

            var left = sides[0].StartsWith("${{", StringComparison.Ordinal)
                ? SubstituteExpressions(sides[0], expr => Eval(expr, head, outputs, stepEnv: null))
                : Eval(sides[0], head, outputs, stepEnv: null);
            var right = sides[1].Trim().Trim('\'', '"');
            if (!string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parse a <c>GITHUB_OUTPUT</c> file the way actions/runner v2.338.2
    /// <c>EnvFileKeyValuePairs</c> does, including the heredoc form.
    /// </summary>
    private static Dictionary<string, string> ParseGitHubOutput(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        var line = ReadLine(text, ref index);
        while (line is not null)
        {
            if (line.Length > 0)
            {
                var equalsIndex = line.IndexOf('=', StringComparison.Ordinal);
                var heredocIndex = line.IndexOf("<<", StringComparison.Ordinal);
                string key;
                string output;
                if (equalsIndex >= 0 && (heredocIndex < 0 || equalsIndex < heredocIndex))
                {
                    var split = line.Split('=', 2);
                    key = split[0];
                    output = split.Length > 1 ? split[1] : string.Empty;
                }
                else if (heredocIndex >= 0 && (equalsIndex < 0 || heredocIndex < equalsIndex))
                {
                    var split = line.Split("<<", 2, StringSplitOptions.None);
                    if (split.Length != 2 || split[0].Length == 0 || split[1].Length == 0)
                    {
                        throw new InvalidOperationException($"Invalid GITHUB_OUTPUT line '{line}'.");
                    }

                    key = split[0];
                    var delimiter = split[1];
                    var startIndex = index;
                    var endIndex = index;
                    var tempLine = ReadLine(text, ref index, out var newline);
                    while (!string.Equals(tempLine, delimiter, StringComparison.Ordinal))
                    {
                        if (tempLine is null || newline is null)
                        {
                            throw new InvalidOperationException($"GITHUB_OUTPUT delimiter '{delimiter}' was not closed.");
                        }

                        endIndex = index - newline.Length;
                        tempLine = ReadLine(text, ref index, out newline);
                    }

                    output = endIndex > startIndex ? text.Substring(startIndex, endIndex - startIndex) : string.Empty;
                }
                else
                {
                    throw new InvalidOperationException($"Invalid GITHUB_OUTPUT line '{line}'.");
                }

                result[key] = output;
            }

            line = ReadLine(text, ref index);
        }

        return result;
    }

    private static string? ReadLine(string text, ref int index)
    {
        return ReadLine(text, ref index, out _);
    }

    private static string? ReadLine(string text, ref int index, out string? newline)
    {
        if (index >= text.Length)
        {
            newline = null;
            return null;
        }

        var original = index;
        var lf = text.IndexOf('\n', index);
        if (lf < 0)
        {
            index = text.Length;
            newline = null;
            return text[original..];
        }

        index = lf + 1;
        newline = "\n";
        return text.Substring(original, lf - original);
    }

    private static List<WorkflowStep> ParseNotifySteps(string yaml)
    {
        var lines = yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line == "  notify-on-drift:");
        if (start < 0)
        {
            throw new InvalidOperationException("notify-on-drift job was not found.");
        }

        var end = lines.Length;
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0 && !lines[i].StartsWith(' ') && !lines[i].StartsWith('#'))
            {
                end = i;
                break;
            }

            if (lines[i].StartsWith("  ", StringComparison.Ordinal) && !lines[i].StartsWith("   ", StringComparison.Ordinal) && lines[i].TrimEnd().EndsWith(':'))
            {
                end = i;
                break;
            }
        }

        var stepsLine = -1;
        for (var i = start; i < end; i++)
        {
            if (lines[i].Trim() == "steps:")
            {
                stepsLine = i;
                break;
            }
        }

        if (stepsLine < 0)
        {
            throw new InvalidOperationException("notify-on-drift has no steps.");
        }

        var stepsIndent = LeadingSpaces(lines[stepsLine]);
        var steps = new List<WorkflowStep>();
        WorkflowStep? current = null;
        for (var i = stepsLine + 1; i < end; i++)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                if (current?.RunBlock is not null)
                {
                    current.RunBlock.Append('\n');
                }

                continue;
            }

            var indent = LeadingSpaces(line);
            if (indent <= stepsIndent)
            {
                break;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith("- name:", StringComparison.Ordinal))
            {
                if (current?.RunBlock is not null)
                {
                    current.Run = current.RunBlock.ToString().TrimEnd('\n');
                    current.RunBlock = null;
                }

                current = new WorkflowStep { Name = trimmed["- name:".Length..].Trim() };
                steps.Add(current);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (current.RunBlock is not null)
            {
                if (current.RunIndent < 0)
                {
                    current.RunIndent = indent;
                }

                if (indent >= current.RunIndent)
                {
                    current.RunBlock.Append(line[current.RunIndent..]);
                    current.RunBlock.Append('\n');
                    continue;
                }

                current.Run = current.RunBlock.ToString().TrimEnd('\n');
                current.RunBlock = null;
            }

            if (trimmed.StartsWith("id:", StringComparison.Ordinal))
            {
                current.Id = trimmed["id:".Length..].Trim();
            }
            else if (trimmed.StartsWith("if:", StringComparison.Ordinal))
            {
                current.If = trimmed["if:".Length..].Trim();
            }
            else if (trimmed.StartsWith("run:", StringComparison.Ordinal))
            {
                var rest = trimmed["run:".Length..].Trim();
                if (rest is "|" or "|-")
                {
                    current.RunBlock = new StringBuilder();
                    current.RunIndent = -1;
                }
                else if (rest.StartsWith('>'))
                {
                    throw new InvalidOperationException("Folded run scalars are not used by this workflow.");
                }
                else
                {
                    current.Run = rest.Trim('"', '\'');
                }
            }
            else if (trimmed == "env:")
            {
                current.InEnv = true;
                current.EnvIndent = indent;
            }
            else if (current.InEnv && indent > current.EnvIndent && trimmed.Contains(':', StringComparison.Ordinal))
            {
                var colon = trimmed.IndexOf(':');
                var key = trimmed[..colon].Trim();
                var value = trimmed[(colon + 1)..].Trim().Trim('"', '\'');
                current.Env[key] = value;
            }
            else
            {
                current.InEnv = false;
            }
        }

        if (current?.RunBlock is not null)
        {
            current.Run = current.RunBlock.ToString().TrimEnd('\n');
        }

        if (steps.Count == 0)
        {
            throw new InvalidOperationException("No steps were parsed from notify-on-drift.");
        }

        return steps;
    }

    private static int LeadingSpaces(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// actions/runner v2.338.2 ScriptHandler runs a <c>run</c> step with bash
    /// when it exists, and with the <c>sh</c> argument format <c>-e {script}</c>.
    /// The process is started with <c>UseShellExecute = false</c>.
    /// </summary>
    private static async Task RunBashAsync(string workingDirectory, string script, IReadOnlyDictionary<string, string> env)
    {
        var scriptPath = Path.Combine(workingDirectory, "step-" + Guid.NewGuid().ToString("N") + ".sh");
        await File.WriteAllTextAsync(scriptPath, script.Replace("\r\n", "\n", StringComparison.Ordinal));
        await RunAsync("bash", workingDirectory, env, "-e", scriptPath);
    }

    private static Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string fileName,
        string workingDirectory,
        params string[] args)
    {
        return RunAsync(fileName, workingDirectory, env: null, args);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string fileName,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? env,
        params string[] args)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        psi.Environment.Remove("EXARCHOS_ISSUES_PAT");
        if (env is not null)
        {
            foreach (var (key, value) in env)
            {
                psi.Environment[key] = value;
            }
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (fileName == "git" && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr}");
        }

        return (process.ExitCode, stdout, stderr);
    }

    private sealed class WorkflowStep
    {
        public string Name { get; set; } = string.Empty;

        public string? Id { get; set; }

        public string? If { get; set; }

        public string? Run { get; set; }

        public Dictionary<string, string> Env { get; } = new(StringComparer.Ordinal);

        public bool InEnv { get; set; }

        public int EnvIndent { get; set; }

        public StringBuilder? RunBlock { get; set; }

        public int RunIndent { get; set; }
    }
}

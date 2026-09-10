// =============================================================================
// <copyright file="RepoTree.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Strategos.Architecture.Tests;

/// <summary>
/// Resolves the repository root (the directory holding <c>strategos.slnx</c>) by
/// walking up from the test assembly, and exposes the file-scanning primitives the
/// architecture tests share. Every scan is anchored here and every scanned path is
/// required to exist, so a check cannot go blind because a directory was renamed:
/// it fails naming the missing path instead of returning zero hits.
/// </summary>
internal static class RepoTree
{
    private static readonly string[] ExcludedDirectories = ["bin", "obj", "node_modules"];

    private static readonly Lazy<IReadOnlyList<string>> LazyTrackedFiles = new(ListTrackedFiles);

    /// <summary>Gets the repository root (directory containing <c>strategos.slnx</c>).</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>Gets every path <c>git ls-files</c> reports, repo-relative with forward slashes.</summary>
    public static IReadOnlyList<string> TrackedFiles => LazyTrackedFiles.Value;

    /// <summary>Turns a repo-relative, forward-slash path into an absolute one.</summary>
    public static string Absolute(string relativePath) =>
        Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Turns an absolute path into a repo-relative, forward-slash one.</summary>
    public static string Relative(string absolutePath) =>
        Path.GetRelativePath(Root, absolutePath).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>Returns the absolute path of a directory that must exist.</summary>
    public static string RequireDirectory(string relativePath)
    {
        var absolute = Absolute(relativePath);
        if (!Directory.Exists(absolute))
        {
            throw new DirectoryNotFoundException(
                $"'{relativePath}' does not exist under {Root}; a check scanning it would return zero hits.");
        }

        return absolute;
    }

    /// <summary>Returns the absolute path of a file that must exist.</summary>
    public static string RequireFile(string relativePath)
    {
        var absolute = Absolute(relativePath);
        if (!File.Exists(absolute))
        {
            throw new FileNotFoundException(
                $"'{relativePath}' does not exist under {Root}; a check reading it would return zero hits.",
                absolute);
        }

        return absolute;
    }

    /// <summary>
    /// Lists the direct children of <paramref name="relativeParent"/> whose name starts
    /// with <paramref name="prefix"/> (the bash idiom <c>src/Strategos.Ontology*/</c>).
    /// </summary>
    public static IReadOnlyList<string> DirectoriesStartingWith(string relativeParent, string prefix)
    {
        var parent = RequireDirectory(relativeParent);
        return Directory.EnumerateDirectories(parent)
            .Where(d => Path.GetFileName(d).StartsWith(prefix, StringComparison.Ordinal))
            .Select(Relative)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Enumerates files under one required directory, skipping build output.</summary>
    public static IReadOnlyList<string> Files(string relativeDirectory, params string[] searchPatterns) =>
        Files([relativeDirectory], searchPatterns);

    /// <summary>Enumerates files under several required directories, skipping build output.</summary>
    public static IReadOnlyList<string> Files(IEnumerable<string> relativeDirectories, params string[] searchPatterns)
    {
        var result = new List<string>();
        foreach (var relativeDirectory in relativeDirectories)
        {
            var directory = RequireDirectory(relativeDirectory);
            foreach (var pattern in searchPatterns)
            {
                result.AddRange(
                    Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
                        .Where(file => !IsExcluded(file)));
            }
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>Keeps the files whose full text matches <paramref name="pattern"/>.</summary>
    public static IReadOnlyList<string> WhereContent(IEnumerable<string> files, Regex pattern) =>
        files.Where(file => pattern.IsMatch(File.ReadAllText(file))).ToList();

    /// <summary>
    /// Reports every line matching <paramref name="pattern"/>. When <paramref name="normalize"/>
    /// is given it is applied to the whole file first (it must preserve line breaks) so a
    /// scan can ignore comments or string literals while still reporting the original line.
    /// </summary>
    public static IReadOnlyList<Hit> Grep(
        IEnumerable<string> files,
        Regex pattern,
        Func<string, string>? normalize = null,
        Func<string, bool>? lineFilter = null)
    {
        var hits = new List<Hit>();
        foreach (var file in files)
        {
            var original = File.ReadAllText(file);
            var originalLines = SplitLines(original);
            var scannedLines = normalize is null ? originalLines : SplitLines(normalize(original));
            if (scannedLines.Length != originalLines.Length)
            {
                throw new InvalidOperationException(
                    $"normalizer changed the line count of {Relative(file)} ({originalLines.Length} -> {scannedLines.Length}).");
            }

            for (var i = 0; i < scannedLines.Length; i++)
            {
                if (lineFilter is not null && !lineFilter(scannedLines[i]))
                {
                    continue;
                }

                if (pattern.IsMatch(scannedLines[i]))
                {
                    hits.Add(new Hit(Relative(file), i + 1, originalLines[i].Trim()));
                }
            }
        }

        return hits;
    }

    /// <summary>Formats hits one per line for an assertion message.</summary>
    public static string Format(IEnumerable<Hit> hits) => string.Join("\n", hits.Select(h => h.ToString()));

    private static string[] SplitLines(string text) => text.Split('\n');

    private static bool IsExcluded(string absolutePath)
    {
        var segments = Relative(absolutePath).Split('/');
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (ExcludedDirectories.Contains(segments[i], StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "strategos.slnx")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate repo root (no strategos.slnx) from " + AppContext.BaseDirectory);
    }

    private static IReadOnlyList<string> ListTrackedFiles()
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("-z");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to launch 'git ls-files' in {Root}.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'git ls-files' failed in {Root} (exit {process.ExitCode}): {stderr.Trim()}");
        }

        var files = stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (files.Length == 0)
        {
            throw new InvalidOperationException($"'git ls-files' listed nothing in {Root}; the tracked-file inventory is unusable.");
        }

        return files;
    }
}

// =============================================================================
// <copyright file="InvariantCatalogTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.RegularExpressions;

namespace Strategos.Architecture.Tests;

/// <summary>
/// Keeps the two representations of the invariants catalog paired: the
/// machine-readable <c>.exarchos/invariants.md</c> that exarchos loads and the prose
/// <c>docs/architecture/invariants/U-N-*.md</c> that people cite. Every id has one
/// prose file and vice versa, every referenced path exists, every <c>applies-to</c>
/// glob still matches tracked files, the README table lists every id and
/// <c>.exarchos.yml</c> registers the catalog. This is the pairing the invariants
/// README promises.
/// </summary>
public class InvariantCatalogTests
{
    private const string ProseDirectory = "docs/architecture/invariants";

    private static readonly string[] ExpectedIds = ["U-1", "U-2", "U-3", "U-4", "U-5", "U-6", "U-7", "U-8"];

    private static readonly Regex ProseFileName = new(@"^(U-\d+)-.+\.md$", RegexOptions.CultureInvariant);

    private static readonly Regex ReadmeRow = new(@"^\| \[(U-\d+)\]\(([^)]+)\) \|", RegexOptions.CultureInvariant);

    private static readonly Regex CatalogRegistration = new(@"^-\s*path:\s*\.exarchos/invariants\.md$", RegexOptions.CultureInvariant);

    /// <summary>(a) The catalog carries exactly U-1..U-8, each once.</summary>
    [Test]
    public async Task Catalog_Ids_AreExactlyU1ThroughU8_WithNoDuplicates()
    {
        var catalog = InvariantCatalogParser.Load();
        var ids = catalog.Invariants.Select(e => e.Id).ToList();

        var duplicates = ids.GroupBy(id => id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} (x{g.Count()})")
            .ToList();
        var missing = ExpectedIds.Except(ids, StringComparer.Ordinal).ToList();
        var unexpected = ids.Except(ExpectedIds, StringComparer.Ordinal).ToList();

        await Assert.That(duplicates).IsEmpty()
            .Because($"an id must appear once in {InvariantCatalogParser.CatalogPath}; duplicated: {string.Join(", ", duplicates)}");
        await Assert.That(missing).IsEmpty()
            .Because($"{InvariantCatalogParser.CatalogPath} lost an invariant: {string.Join(", ", missing)}");
        await Assert.That(unexpected).IsEmpty()
            .Because($"{InvariantCatalogParser.CatalogPath} carries ids outside U-1..U-8: {string.Join(", ", unexpected)}. Adding an invariant means a new U-N-*.md, a README row and an update to this test's expected set.");
    }

    /// <summary>(b) Every id has exactly one U-N-*.md and every U-N-*.md has a catalog id.</summary>
    [Test]
    public async Task Catalog_EveryId_HasExactlyOneProseFile_AndEveryProseFile_HasACatalogId()
    {
        var catalog = InvariantCatalogParser.Load();
        var directory = RepoTree.RequireDirectory(ProseDirectory);
        var proseFiles = Directory.EnumerateFiles(directory, "U-*.md")
            .Select(Path.GetFileName)
            .Select(f => f!)
            .Order(StringComparer.Ordinal)
            .ToList();

        var malformed = proseFiles.Where(f => !ProseFileName.IsMatch(f)).ToList();
        await Assert.That(malformed).IsEmpty()
            .Because($"files under {ProseDirectory} starting with 'U-' must be named U-N-<slug>.md: {string.Join(", ", malformed)}");

        var filesById = proseFiles
            .GroupBy(f => ProseFileName.Match(f).Groups[1].Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var problems = new List<string>();
        foreach (var entry in catalog.Invariants)
        {
            if (!filesById.TryGetValue(entry.Id, out var files))
            {
                problems.Add($"{entry.Id} (catalog line {entry.Line}) has no {ProseDirectory}/{entry.Id}-*.md");
            }
            else if (files.Count > 1)
            {
                problems.Add($"{entry.Id} has {files.Count} prose files: {string.Join(", ", files)}");
            }
        }

        var catalogIds = catalog.Invariants.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        problems.AddRange(filesById.Keys
            .Where(id => !catalogIds.Contains(id))
            .Select(id => $"{ProseDirectory}/{string.Join(", ", filesById[id])} has no '{id}' entry in {InvariantCatalogParser.CatalogPath}"));

        await Assert.That(problems).IsEmpty()
            .Because("the catalog and the prose directory must stay paired one-to-one:\n" + string.Join("\n", problems));
    }

    /// <summary>(c) Every path under <c>references:</c> exists relative to the repo root.</summary>
    [Test]
    public async Task Catalog_EveryReferencedPath_Exists()
    {
        var catalog = InvariantCatalogParser.Load();
        var missing = new List<string>();
        foreach (var entry in catalog.Invariants)
        {
            await Assert.That(entry.References).IsNotEmpty()
                .Because($"{entry.Id} (catalog line {entry.Line}) lists no references");

            missing.AddRange(entry.References
                .Where(reference => !File.Exists(RepoTree.Absolute(reference)) && !Directory.Exists(RepoTree.Absolute(reference)))
                .Select(reference => $"{entry.Id}: {reference}"));
        }

        await Assert.That(missing).IsEmpty()
            .Because("every 'references' path in the catalog must exist (a moved file must be re-pointed):\n" + string.Join("\n", missing));
    }

    /// <summary>(d) Every <c>applies-to:</c> glob matches at least one tracked file.</summary>
    [Test]
    public async Task Catalog_EveryAppliesToGlob_MatchesAtLeastOneTrackedFile()
    {
        var catalog = InvariantCatalogParser.Load();
        var tracked = RepoTree.TrackedFiles;
        var dead = new List<string>();
        foreach (var entry in catalog.Invariants)
        {
            await Assert.That(entry.AppliesTo).IsNotEmpty()
                .Because($"{entry.Id} (catalog line {entry.Line}) lists no applies-to globs");

            foreach (var glob in entry.AppliesTo)
            {
                var regex = Glob.ToRegex(glob);
                if (!tracked.Any(regex.IsMatch))
                {
                    dead.Add($"{entry.Id}: {glob}");
                }
            }
        }

        await Assert.That(dead).IsEmpty()
            .Because($"every applies-to glob must match a tracked file ({tracked.Count} tracked), otherwise exarchos never loads the invariant for a diff:\n" + string.Join("\n", dead));
    }

    /// <summary>(e) The README table lists every catalog id, linking to its prose file.</summary>
    [Test]
    public async Task Readme_Table_ListsEveryCatalogId()
    {
        var catalog = InvariantCatalogParser.Load();
        var readme = RepoTree.RequireFile($"{ProseDirectory}/README.md");
        var rows = File.ReadAllLines(readme)
            .Select(line => ReadmeRow.Match(line))
            .Where(m => m.Success)
            .Select(m => (Id: m.Groups[1].Value, Link: m.Groups[2].Value))
            .ToList();

        var catalogIds = catalog.Invariants.Select(e => e.Id).ToList();
        var rowIds = rows.Select(r => r.Id).ToList();
        var missingRows = catalogIds.Except(rowIds, StringComparer.Ordinal).ToList();
        var staleRows = rowIds.Except(catalogIds, StringComparer.Ordinal).ToList();
        var brokenLinks = rows
            .Where(r => !File.Exists(RepoTree.Absolute($"{ProseDirectory}/{r.Link}")))
            .Select(r => $"{r.Id} -> {r.Link}")
            .ToList();

        await Assert.That(missingRows).IsEmpty()
            .Because($"{ProseDirectory}/README.md's table lacks a row for: {string.Join(", ", missingRows)}");
        await Assert.That(staleRows).IsEmpty()
            .Because($"{ProseDirectory}/README.md's table lists ids the catalog does not have: {string.Join(", ", staleRows)}");
        await Assert.That(brokenLinks).IsEmpty()
            .Because($"{ProseDirectory}/README.md's table links to files that do not exist: {string.Join(", ", brokenLinks)}");
    }

    /// <summary>(f) <c>.exarchos.yml</c> registers the catalog so exarchos actually loads it.</summary>
    [Test]
    public async Task ExarchosConfig_RegistersTheCatalog()
    {
        var config = RepoTree.RequireFile(".exarchos.yml");
        var registrations = File.ReadAllLines(config)
            .Select(line => line.Trim())
            .Where(line => CatalogRegistration.IsMatch(line))
            .ToList();

        await Assert.That(registrations).HasCount().EqualTo(1)
            .Because($".exarchos.yml must register '{InvariantCatalogParser.CatalogPath}' exactly once under invariants.catalogs");
    }

    /// <summary>
    /// Every entry is <c>mode: audit</c>: the catalog header states that the mechanical
    /// gates run here as tests rather than as <c>mode: check</c> trees, so a check
    /// entry would be a gate exarchos evaluates and this suite does not.
    /// </summary>
    [Test]
    public async Task Catalog_EveryEntry_IsAuditMode()
    {
        var catalog = InvariantCatalogParser.Load();
        var notAudit = catalog.Invariants
            .Where(e => e.Mode != "audit")
            .Select(e => $"{e.Id} (catalog line {e.Line}): mode '{e.Mode ?? "<missing>"}'")
            .ToList();

        await Assert.That(notAudit).IsEmpty()
            .Because("every catalog entry is enforcement.mode: audit; mechanical checks belong in DeterministicChecksTests:\n" + string.Join("\n", notAudit));
    }
}

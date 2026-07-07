// -----------------------------------------------------------------------
// <copyright file="ImportFixtureLoader.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// Loads the hand-authored import-fixture family (task 019, DR-15) from the copied
/// <c>Import/ImportFixtures</c> output directory. The fixtures are hand-written wire JSON — DISTINCT
/// from the builder-produced #53 corpus (whose charter forbids hand-written JSON) — used to pin the
/// import channel's rejection / tolerance gates from real files on disk.
/// </summary>
internal static class ImportFixtureLoader
{
    private static readonly string FixturesDirectory =
        Path.Combine(AppContext.BaseDirectory, "Import", "ImportFixtures");

    /// <summary>
    /// Loads a hand-authored import fixture by file name, returning its <c>*.workflow.json</c> path
    /// (the name the generator's diagnostics report) and its content.
    /// </summary>
    /// <param name="fileName">The fixture file name (e.g. <c>delegate-step.workflow.json</c>).</param>
    /// <returns>The fixture file name (as the AdditionalFile path) and its JSON content.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the fixture is not found in the output directory.</exception>
    public static (string Path, string Content) Load(string fileName)
    {
        var fullPath = Path.Combine(FixturesDirectory, fileName);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Import fixture '{fileName}' was not found under '{FixturesDirectory}'. " +
                "Ensure Import/ImportFixtures/*.workflow.json is copied to the output directory.",
                fullPath);
        }

        return (fileName, File.ReadAllText(fullPath));
    }
}

// -----------------------------------------------------------------------
// <copyright file="ContractsSchemaPaths.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using System.Text.Json;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// Locates the Contracts-emitted JSON Schema tree from the test output directory.
/// The schemas are emitter-owned build output of <c>Strategos.Contracts</c>, not
/// test content, so they are read from the repository rather than copied.
/// </summary>
internal static class ContractsSchemaPaths
{
    /// <summary>Gets the repository root (the directory holding <c>strategos.slnx</c>).</summary>
    internal static string RepoRoot { get; } = LocateRepoRoot();

    /// <summary>Gets the per-model schema directory (<c>schemas/json-schema</c>).</summary>
    internal static string SchemaDir { get; } =
        Path.Combine(RepoRoot, "src", "Strategos.Contracts", "schemas", "json-schema");

    /// <summary>Gets the bundled, self-contained workflow IR schema.</summary>
    internal static string BundledWorkflowSchema { get; } =
        Path.Combine(
            RepoRoot,
            "src",
            "Strategos.Contracts",
            "schemas",
            "workflow-definition-v1.schema.json");

    /// <summary>Loads a per-model schema document by model name.</summary>
    /// <param name="name">The model name (the schema file's base name).</param>
    /// <returns>The parsed root element.</returns>
    internal static JsonElement LoadModel(string name)
    {
        var path = Path.Combine(SchemaDir, name + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"expected emitted schema at {path}", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    /// <summary>Loads the bundled workflow IR schema.</summary>
    /// <returns>The parsed root element.</returns>
    internal static JsonElement LoadBundle()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(BundledWorkflowSchema));
        return document.RootElement.Clone();
    }

    private static string LocateRepoRoot()
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
            "could not locate the repo root (no strategos.slnx walking up from "
            + AppContext.BaseDirectory + ").");
    }
}

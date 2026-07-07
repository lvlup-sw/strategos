namespace Strategos.Ontology.MCP.Hosting.Tests;

/// <summary>
/// Resolves the directory the Contracts TypeSpec emitter writes JSON Schema into
/// (<c>src/Strategos.Contracts/schemas/json-schema/</c>) by walking up from the test
/// assembly's output directory to the repo root (the directory holding
/// <c>src/strategos.sln</c>). The abstained-event schema is loaded as a FILE so the
/// hosting test validates against the emitted contract without the ontology
/// core/hosting taking a Strategos.Contracts dependency (DR-16 independence).
/// </summary>
internal static class SchemaFiles
{
    /// <summary>Absolute path to the emitted JSON Schema directory.</summary>
    public static string Dir { get; } = Resolve();

    private static string Resolve()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "src", "strategos.sln")))
            {
                return Path.Combine(dir, "src", "Strategos.Contracts", "schemas", "json-schema");
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate repo root (no src/strategos.sln) walking up from " + AppContext.BaseDirectory + ".");
    }
}

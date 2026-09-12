using System.Text.Json;

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>Both emitters must refuse a oneOf they cannot safely lower.</summary>
public class StructuralUnionEmitterTests
{
    [Test]
    public async Task Overlapping_OneOf_Stops_Both_Emitters()
    {
        var root = Directory.CreateTempSubdirectory("invariant-union-probe-").FullName;
        try
        {
            var schemas = Path.Combine(root, "schemas", "json-schema");
            Directory.CreateDirectory(schemas);
            foreach (var name in new[] { "First", "Second" })
            {
                await File.WriteAllTextAsync(Path.Combine(schemas, name + ".json"), """
                    {"type":"object","properties":{"value":{"type":"string"}},"required":["value"],"additionalProperties":false}
                    """);
            }

            await File.WriteAllTextAsync(Path.Combine(schemas, "Overlap.json"), """
                {"oneOf":[{"$ref":"First.json"},{"$ref":"Second.json"}]}
                """);
            var scripts = Path.Combine(root, "scripts");
            Directory.CreateDirectory(scripts);
            File.Copy(Path.Combine(RepoLayout.ContractsProjectDir, "scripts", "emit-zod.mjs"), Path.Combine(scripts, "emit-zod.mjs"));
            var zod = await Cli.RunAsync("node", "scripts/emit-zod.mjs", root);
            await Assert.That(zod.ExitCode).IsNotEqualTo(0).Because(zod.Output);
            await Assert.That(zod.Output).Contains("overlapping oneOf");
            var project = Path.Combine(RepoLayout.RepoRoot, "src", "Strategos.Contracts.Codegen", "Strategos.Contracts.Codegen.csproj");
            var csharp = await Cli.RunAsync("dotnet", $"run --project \"{project}\" -- \"{schemas}\" \"{Path.Combine(root, "generated")}\"");
            await Assert.That(csharp.ExitCode).IsNotEqualTo(0).Because(csharp.Output);
            await Assert.That(csharp.Output).Contains("overlapping oneOf");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

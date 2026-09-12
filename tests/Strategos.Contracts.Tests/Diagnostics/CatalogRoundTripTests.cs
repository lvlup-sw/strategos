using System.Text.Json;
using System.Text.Json.Nodes;
using Strategos.Contracts;
using Strategos.Contracts.Generated;

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>Real consumer catalogs and adversarial fixtures must agree across all three projections.</summary>
[Property("Category", "Diagnostics")]
[NotInParallel("invariant-conformance")]
public class CatalogRoundTripTests
{
    [Test]
    public async Task Catalogs_And_Checks_Validate_And_RoundTrip_WithoutLoss()
    {
        await TspToolchain.EnsureRestoredAsync();
        var normalize = await Cli.RunAsync("node", "scripts/invariant-corpus.mjs", RepoLayout.ContractsProjectDir);
        await Assert.That(normalize.ExitCode).IsEqualTo(0).Because(normalize.Output);
        using var corpus = JsonDocument.Parse(normalize.Output);
        await Assert.That(corpus.RootElement.GetArrayLength()).IsGreaterThan(37);
        foreach (var test in corpus.RootElement.EnumerateArray())
        {
            var name = test.GetProperty("name").GetString();
            var model = test.GetProperty("model").GetString();
            var type = typeof(InvariantEntry).Assembly.GetType($"Strategos.Contracts.Generated.{model}", throwOnError: true)!;
            var json = test.GetProperty("document").GetRawText();
            var valid = test.GetProperty("valid").GetBoolean();
            object? value = null;
            JsonException? rejection = null;
            try
            {
                value = JsonSerializer.Deserialize(json, type, ContractsJson.Options);
            }
            catch (JsonException ex)
            {
                rejection = ex;
            }

            await Assert.That(rejection is null).IsEqualTo(valid).Because($"{name}: {rejection}");
            if (valid)
            {
                await Assert.That(value).IsNotNull().Because(name!);
                var output = JsonSerializer.Serialize(value, type, ContractsJson.Options);
                await Assert.That(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(output))).IsTrue()
                    .Because($"{name}: expected {json}; got {output}");
            }
        }

        var build = await Cli.RunAsync("npm", "run build:package", RepoLayout.ContractsProjectDir);
        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        var schemas = await Cli.RunAsync("node", "scripts/verify-invariant-conformance.mjs", RepoLayout.ContractsProjectDir);
        await Assert.That(schemas.ExitCode).IsEqualTo(0).Because(schemas.Output);
        await Assert.That(schemas.Output).Contains("JSON Schema and emitted Zod agree");
    }

    [Test]
    public async Task Installed_Npm_Package_Accepts_The_Shared_Corpus()
    {
        await TspToolchain.EnsureRestoredAsync();
        var result = await Cli.RunAsync("node", "scripts/verify-invariant-package.mjs", RepoLayout.ContractsProjectDir);
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        await Assert.That(result.Output).Contains("installed-package cases passed");
    }

    [Test]
    public async Task Severity_Maps_Are_Typed_And_Removed_Field_Is_Absent()
    {
        await Assert.That(typeof(InvariantSeverity).GetProperty(nameof(InvariantSeverity.ByWorkflow))!.PropertyType)
            .IsEqualTo(typeof(IReadOnlyDictionary<string, InvariantSeverityLevel>));
        await Assert.That(typeof(InvariantEntry).GetProperty("AxiomOverlap")).IsNull();
        var leaf = new GrepLeaf { Pattern = "x", Kind = "exec" };
        await Assert.That(() => ContractsJson.Serialize<CheckNode>(leaf)).Throws<JsonException>();
    }
}

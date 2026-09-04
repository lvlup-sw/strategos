using System.Text.Json;

namespace Strategos.Contracts.Tests.Ontology;

/// <summary>
/// #170: TypeSpec operation decorators are executable authoring primitives,
/// not comments. Their intent survives JSON Schema and C# emission.
/// </summary>
public sealed class ActionDecoratorTests
{
    [Test]
    public async Task DecoratorLibrary_UsesExternDeclarationsBackedByJavaScript()
    {
        var ontologyDirectory = Path.Combine(RepoLayout.ContractsProjectDir, "Ontology");
        var declarations = await File.ReadAllTextAsync(
            Path.Combine(ontologyDirectory, "decorators.tsp"));
        var implementation = await File.ReadAllTextAsync(
            Path.Combine(ontologyDirectory, "decorators.mjs"));

        await Assert.That(declarations).Contains("extern dec objectKind");
        await Assert.That(declarations).Contains("extern dec authority");
        await Assert.That(declarations).Contains("extern dec relation");
        await Assert.That(declarations).Contains("extern dec clients");
        await Assert.That(declarations).Contains("extern dec confirm");
        await Assert.That(declarations).Contains("extern dec readOnly");
        await Assert.That(implementation).Contains("setExtension");
        await Assert.That(implementation).Contains("createTypeSpecLibrary");
        await Assert.That(implementation).Contains("contract-action-requires-model");
        await Assert.That(implementation).Contains("contract-action-shared-model");
        await Assert.That(implementation).Contains("return undefined");
    }

    [Test]
    public async Task JsonSchema_PreservesAllActionMetadataWithoutRuntimeField()
    {
        var schemaPath = Path.Combine(
            RepoLayout.ContractsProjectDir,
            "schemas",
            "json-schema",
            "InspectPositionRequest.json");
        using var parsed = JsonDocument.Parse(await File.ReadAllTextAsync(schemaPath));
        var schema = parsed.RootElement;

        await Assert.That(schema.GetProperty("x-strategos-action-name").GetString())
            .IsEqualTo("inspectPosition");
        await Assert.That(schema.GetProperty("x-strategos-domain").GetString())
            .IsEqualTo("Trading");
        await Assert.That(schema.GetProperty("x-strategos-object").GetString())
            .IsEqualTo("Position");
        await Assert.That(schema.GetProperty("x-strategos-authority").GetString())
            .IsEqualTo("position.reader");
        await Assert.That(schema.GetProperty("x-strategos-relation").GetString())
            .IsEqualTo("owner");
        await Assert.That(schema.GetProperty("x-strategos-link-path").EnumerateArray()
                .Select(value => value.GetString()!))
            .IsEquivalentTo(["Portfolio"]);
        await Assert.That(schema.GetProperty("x-strategos-clients").EnumerateArray()
                .Select(value => value.GetString()!))
            .IsEquivalentTo(["mcp", "web"]);
        await Assert.That(schema.GetProperty("x-strategos-confirm").GetBoolean()).IsFalse();
        await Assert.That(schema.GetProperty("x-strategos-read-only").GetBoolean()).IsTrue();

        var raw = schema.GetRawText();
        await Assert.That(raw.Contains("\"runtime\"", StringComparison.OrdinalIgnoreCase)).IsFalse();
    }

    [Test]
    public async Task CSharpExtension_EmitsContractAuthoredDescriptorCatalog()
    {
        var generatedPath = Path.Combine(
            RepoLayout.RepoRoot,
            "src",
            "Strategos.Ontology",
            "Contracts",
            "Generated",
            "ContractOntology.g.cs");
        var generated = await File.ReadAllTextAsync(generatedPath);

        await Assert.That(generated).Contains("new ActionDescriptor(\"inspectPosition\"");
        await Assert.That(generated).Contains("DescriptorSource.HandAuthoredContract");
        await Assert.That(generated).Contains("RequiredAuthority = \"position.reader\"");
        await Assert.That(generated).Contains("PreconditionKind.RelationHolds");
        await Assert.That(generated).Contains("AllowedClients = ImmutableArray.Create(\"mcp\", \"web\")");
        await Assert.That(generated).Contains("RequiresConfirmation = false");
    }

    [Test]
    public async Task RecordEmitter_ExposesAcyclicExtensionSeam()
    {
        var codegenDirectory = Path.Combine(
            RepoLayout.RepoRoot,
            "src",
            "Strategos.Contracts.Codegen");
        var seam = await File.ReadAllTextAsync(
            Path.Combine(codegenDirectory, "ISchemaEmissionExtension.cs"));
        var ontologyExtension = await File.ReadAllTextAsync(
            Path.Combine(codegenDirectory, "OntologyContractEmitter.cs"));
        var codegenProject = await File.ReadAllTextAsync(
            Path.Combine(codegenDirectory, "Strategos.Contracts.Codegen.csproj"));

        await Assert.That(seam).Contains("interface ISchemaEmissionExtension");
        await Assert.That(ontologyExtension).Contains(": ISchemaEmissionExtension");
        await Assert.That(codegenProject.Contains(
            "ProjectReference",
            StringComparison.Ordinal)).IsFalse();
    }
}

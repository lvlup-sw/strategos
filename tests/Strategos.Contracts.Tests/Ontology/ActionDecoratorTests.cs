using System.Text.Json;

namespace Strategos.Contracts.Tests.Ontology;

/// <summary>
/// #170: TypeSpec operation decorators are executable authoring primitives,
/// not comments. Their intent survives JSON Schema and C# emission.
/// </summary>
[NotInParallel("tsp-compile")]
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
        await Assert.That(declarations).Contains("extern dec requires");
        await Assert.That(declarations).Contains("extern dec ensures");
        await Assert.That(declarations).Contains("extern dec clients");
        await Assert.That(declarations).Contains("extern dec confirm");
        await Assert.That(declarations).Contains("extern dec readOnly");
        await Assert.That(implementation).Contains("setExtension");
        await Assert.That(implementation).Contains("createTypeSpecLibrary");
        await Assert.That(implementation).Contains("contract-action-requires-model");
        await Assert.That(implementation).Contains("contract-action-shared-model");
        await Assert.That(implementation).Contains("contract-action-relation-path-segment");
        await Assert.That(implementation).Contains("contract-action-semantic-name");
        await Assert.That(implementation).Contains("contract-action-subject");
        await Assert.That(implementation).Contains("contract-action-duplicate-identity");
        await Assert.That(implementation).Contains("contract-action-subject-kind");
        await Assert.That(implementation).Contains("x-strategos-requires-v1");
        await Assert.That(implementation).Contains("x-strategos-ensures-v1");
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
        var requirements = schema.GetProperty("x-strategos-requires-v1").EnumerateArray().ToArray();
        await Assert.That(requirements.Length).IsEqualTo(2);
        await Assert.That(requirements[0].GetProperty("strength").GetString()).IsEqualTo("soft");
        await Assert.That(requirements[0].GetProperty("predicate").GetProperty("kind").GetString())
            .IsEqualTo("property-comparison");
        await Assert.That(requirements[0].GetProperty("predicate").GetProperty("property")
            .GetProperty("scalarKind").GetString()).IsEqualTo("string");
        await Assert.That(requirements[0].GetProperty("expression").GetString())
            .IsEqualTo("status != \"closed\"");
        await Assert.That(requirements[1].GetProperty("strength").GetString()).IsEqualTo("hard");
        var relation = requirements[1].GetProperty("predicate");
        await Assert.That(relation.GetProperty("kind").GetString()).IsEqualTo("relation-holds");
        await Assert.That(relation.GetProperty("relationName").GetString()).IsEqualTo("owner");
        await Assert.That(relation.GetProperty("linkPath").EnumerateArray()
                .Select(value => value.GetString()!))
            .IsEquivalentTo(["Portfolio"]);
        var guarantee = schema.GetProperty("x-strategos-ensures-v1")[0]
            .GetProperty("predicate");
        await Assert.That(guarantee.GetProperty("kind").GetString()).IsEqualTo("relation-holds");
        await Assert.That(schema.GetProperty("x-strategos-ensures-v1")[0]
            .GetProperty("expression").GetString()).IsEqualTo("principal -[owner]-> Portfolio");
        await Assert.That(schema.TryGetProperty("x-strategos-relation", out _)).IsFalse();
        await Assert.That(schema.TryGetProperty("x-strategos-link-path", out _)).IsFalse();
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

        await Assert.That(generated).Contains(
            "new ActionDescriptor(new ActionSubject(\"Trading\", \"Position\"), \"inspectPosition\"");
        await Assert.That(generated).Contains("DescriptorSource.HandAuthoredContract");
        await Assert.That(generated).Contains("RequiredAuthority = \"position.reader\"");
        await Assert.That(generated).Contains("ActionPredicate.RelationHolds(\"owner\", \"Portfolio\")");
        await Assert.That(generated).Contains("ConstraintStrength.Hard");
        await Assert.That(generated).Contains("ActionPredicate.Property(new PredicatePropertyReference(");
        await Assert.That(generated).Contains("Ensures =");
        await Assert.That(generated).Contains("new ActionGuarantee(");
        await Assert.That(generated).Contains("SymbolKey = \"typespec://Trading/Position\"");
        await Assert.That(generated).Contains("AllowedClients = ImmutableArray.Create(\"mcp\", \"web\")");
        await Assert.That(generated).Contains("RequiresConfirmation = false");
    }

    [Test]
    public async Task RelationDecorators_RejectWhitespaceOnlyLinkPathSegment()
    {
        var sourcePath = Path.Combine(
            RepoLayout.ContractsProjectDir,
            $".invalid-relation-{Guid.NewGuid():N}.tsp");
        var output = Directory.CreateTempSubdirectory("contracts-invalid-relation-").FullName;

        try
        {
            await File.WriteAllTextAsync(sourcePath, """
                import "./main.tsp";

                using LevelUp.Strategos.Ontology;

                model InvalidBlankRelationSugarResult {
                  value: string;
                }

                @relation("owner", "   ")
                op invalidBlankRelationSugar(): InvalidBlankRelationSugarResult;

                model InvalidBlankTypedRelationResult {
                  value: string;
                }

                @requires(#{
                  kind: "not",
                  predicate: #{
                    kind: "relation-holds",
                    relationName: "owner",
                    linkPath: #["   "]
                  }
                })
                op invalidBlankTypedRelation(): InvalidBlankTypedRelationResult;
                """);

            var result = await Cli.RunAsync(
                "npx",
                $"tsp compile \"{sourcePath}\" --output-dir \"{output}\"",
                RepoLayout.ContractsProjectDir);

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
            await Assert.That(result.Output).Contains("contract-action-relation-path-segment");
            await Assert.That(result.Output).Contains("segment 0 must be non-blank");
            await Assert.That(result.Output).Contains("invalidBlankRelationSugar");
            await Assert.That(result.Output).Contains("invalidBlankTypedRelation");
        }
        finally
        {
            File.Delete(sourcePath);
            Directory.Delete(output, recursive: true);
        }
    }

    [Test]
    public async Task ActionDecorators_RejectWhitespaceOnlySemanticNames()
    {
        var sourcePath = Path.Combine(
            RepoLayout.ContractsProjectDir,
            $".invalid-semantic-name-{Guid.NewGuid():N}.tsp");
        var output = Directory.CreateTempSubdirectory("contracts-invalid-semantic-name-").FullName;

        try
        {
            await File.WriteAllTextAsync(sourcePath, """
                import "./main.tsp";

                using LevelUp.Strategos.Ontology;
                using LevelUp.Strategos.Contracts;

                model BlankDomainResult { value: string; }
                @objectKind("   ", "Fixture", "entity")
                op invalidBlankDomain(): BlankDomainResult;

                model BlankAuthorityResult { value: string; }
                @authority("   ")
                op invalidBlankAuthority(): BlankAuthorityResult;

                model BlankRelationResult { value: string; }
                @relation("   ")
                op invalidBlankRelation(): BlankRelationResult;

                model BlankLinkResult { value: string; }
                @requires(#{ kind: "link-exists", linkName: "   " })
                op invalidBlankLink(): BlankLinkResult;

                model BlankPropertyResult { value: string; }
                @ensures(#{
                  kind: "property-comparison",
                  property: #{ name: "   ", scalarKind: ActionPredicateScalarKindV1.String, isNullable: false },
                  operator: ActionComparisonOperatorV1.Equal,
                  value: #{ kind: "string", value: "ready" }
                })
                op invalidBlankProperty(): BlankPropertyResult;

                model BlankCustomResult { value: string; }
                @requires(#{
                  kind: "custom",
                  evaluatorKey: "   ",
                  arguments: #[],
                  readSet: #[#{ kind: "property", name: "   " }]
                })
                op invalidBlankCustom(): BlankCustomResult;
                """);

            var result = await Cli.RunAsync(
                "npx",
                $"tsp compile \"{sourcePath}\" --output-dir \"{output}\"",
                RepoLayout.ContractsProjectDir);

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
            await Assert.That(result.Output).Contains("contract-action-semantic-name");
            await Assert.That(result.Output).Contains("invalidBlankDomain");
            await Assert.That(result.Output).Contains("invalidBlankAuthority");
            await Assert.That(result.Output).Contains("invalidBlankRelation");
            await Assert.That(result.Output).Contains("invalidBlankLink");
            await Assert.That(result.Output).Contains("invalidBlankProperty");
            await Assert.That(result.Output).Contains("invalidBlankCustom");
        }
        finally
        {
            File.Delete(sourcePath);
            Directory.Delete(output, recursive: true);
        }
    }

    [Test]
    public async Task NamespacedSameNamedOperations_CannotShareMetadataModel()
    {
        var sourcePath = Path.Combine(
            RepoLayout.ContractsProjectDir,
            $".shared-namespaced-model-{Guid.NewGuid():N}.tsp");
        var output = Directory.CreateTempSubdirectory("contracts-shared-namespaced-model-").FullName;

        try
        {
            await File.WriteAllTextAsync(sourcePath, """
                import "./main.tsp";

                using LevelUp.Strategos.Ontology;

                model SharedRequest { value: string; }

                namespace Alpha {
                  @objectKind("Testing", "AlphaFixture", "entity")
                  @requires(#{ kind: "true" })
                  op run(input: SharedRequest): void;
                }

                namespace Beta {
                  @objectKind("Testing", "BetaFixture", "entity")
                  @ensures(#{ kind: "true" })
                  op run(input: SharedRequest): void;
                }
                """);

            var result = await Cli.RunAsync(
                "npx",
                $"tsp compile \"{sourcePath}\" --output-dir \"{output}\"",
                RepoLayout.ContractsProjectDir);

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
            await Assert.That(result.Output).Contains("contract-action-shared-model");
            await Assert.That(result.Output).Contains("Alpha.run");
            await Assert.That(result.Output).Contains("Beta.run");
        }
        finally
        {
            File.Delete(sourcePath);
            Directory.Delete(output, recursive: true);
        }
    }

    [Test]
    public async Task NamespacedSameNamedOperations_CannotDeclareDuplicateActionIdentity()
    {
        var sourcePath = Path.Combine(
            RepoLayout.ContractsProjectDir,
            $".duplicate-action-identity-{Guid.NewGuid():N}.tsp");
        var output = Directory.CreateTempSubdirectory("contracts-duplicate-action-identity-").FullName;

        try
        {
            await File.WriteAllTextAsync(sourcePath, """
                import "./main.tsp";

                using LevelUp.Strategos.Ontology;

                namespace Alpha {
                  model RunRequest { value: string; }

                  @objectKind("Testing", "Fixture", "entity")
                  @requires(#{ kind: "true" })
                  op run(input: RunRequest): void;
                }

                namespace Beta {
                  model RunRequest { value: string; }

                  @objectKind("Testing", "Fixture", "entity")
                  @ensures(#{ kind: "true" })
                  op run(input: RunRequest): void;
                }
                """);

            var result = await Cli.RunAsync(
                "npx",
                $"tsp compile \"{sourcePath}\" --output-dir \"{output}\"",
                RepoLayout.ContractsProjectDir);

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
            await Assert.That(result.Output).Contains("contract-action-duplicate-identity");
            await Assert.That(result.Output).Contains("Alpha.run");
            await Assert.That(result.Output).Contains("Beta.run");
            await Assert.That(result.Output).Contains("Testing/Fixture.run");
        }
        finally
        {
            File.Delete(sourcePath);
            Directory.Delete(output, recursive: true);
        }
    }

    [Test]
    public async Task OperationsOnSameSubject_CannotDeclareConflictingObjectKinds()
    {
        var sourcePath = Path.Combine(
            RepoLayout.ContractsProjectDir,
            $".conflicting-subject-kind-{Guid.NewGuid():N}.tsp");
        var output = Directory.CreateTempSubdirectory("contracts-conflicting-subject-kind-").FullName;

        try
        {
            await File.WriteAllTextAsync(sourcePath, """
                import "./main.tsp";

                using LevelUp.Strategos.Ontology;

                namespace Alpha {
                  model StartRequest { value: string; }

                  @objectKind("Testing", "Fixture", "entity")
                  op start(input: StartRequest): void;
                }

                namespace Beta {
                  model CompleteRequest { value: string; }

                  @objectKind("Testing", "Fixture", "process")
                  op complete(input: CompleteRequest): void;
                }
                """);

            var result = await Cli.RunAsync(
                "npx",
                $"tsp compile \"{sourcePath}\" --output-dir \"{output}\"",
                RepoLayout.ContractsProjectDir);

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
            await Assert.That(result.Output).Contains("contract-action-subject-kind");
            await Assert.That(result.Output).Contains("Alpha.start");
            await Assert.That(result.Output).Contains("Beta.complete");
            await Assert.That(result.Output).Contains("Testing/Fixture");
            await Assert.That(result.Output).Contains("entity");
            await Assert.That(result.Output).Contains("process");
        }
        finally
        {
            File.Delete(sourcePath);
            Directory.Delete(output, recursive: true);
        }
    }

    [Test]
    public async Task ActionMetadata_RequiresObjectKindSubject()
    {
        var sourcePath = Path.Combine(
            RepoLayout.ContractsProjectDir,
            $".orphan-action-metadata-{Guid.NewGuid():N}.tsp");
        var output = Directory.CreateTempSubdirectory("contracts-orphan-action-metadata-").FullName;

        try
        {
            await File.WriteAllTextAsync(sourcePath, """
                import "./main.tsp";

                using LevelUp.Strategos.Ontology;

                model OrphanActionRequest { value: string; }

                @requires(#{ kind: "true" })
                op orphan(input: OrphanActionRequest): void;
                """);

            var result = await Cli.RunAsync(
                "npx",
                $"tsp compile \"{sourcePath}\" --output-dir \"{output}\"",
                RepoLayout.ContractsProjectDir);

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
            await Assert.That(result.Output).Contains("contract-action-subject");
            await Assert.That(result.Output).Contains("orphan");
            await Assert.That(result.Output).Contains("@objectKind");
        }
        finally
        {
            File.Delete(sourcePath);
            Directory.Delete(output, recursive: true);
        }
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

    [Test]
    public async Task CSharpExtension_RejectsUnknownPredicateDiscriminator()
    {
        var schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "UnknownPredicate.json",
              "type": "object",
              "x-strategos-action-name": "invalidAction",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "entity",
              "x-strategos-requires-v1": [
                {
                  "strength": "hard",
                  "expression": "future()",
                  "predicate": { "kind": "future-predicate" }
                }
              ]
            }
            """;

        await AssertCodegenRejects(schema, "unknown predicate kind 'future-predicate'");
    }

    [Test]
    public async Task CSharpExtension_RejectsRequirementWhoseDisplayDoesNotMatchNormalizedPredicate()
    {
        var schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "MismatchedRequirementDisplay.json",
              "type": "object",
              "x-strategos-action-name": "invalidAction",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "entity",
              "x-strategos-requires-v1": [
                {
                  "strength": "hard",
                  "expression": "(true && link(\"Owner\") exists)",
                  "predicate": {
                    "kind": "all",
                    "predicates": [
                      { "kind": "true" },
                      { "kind": "link-exists", "linkName": "Owner" }
                    ]
                  }
                }
              ]
            }
            """;

        await AssertCodegenRejects(
            schema,
            "property 'expression' must equal the predicate's canonical display 'link(\"Owner\") exists'");
    }

    [Test]
    public async Task CSharpExtension_RejectsGuaranteeWhoseDisplayDoesNotMatchPredicate()
    {
        var schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "MismatchedGuaranteeDisplay.json",
              "type": "object",
              "x-strategos-action-name": "invalidAction",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "entity",
              "x-strategos-ensures-v1": [
                {
                  "expression": "false",
                  "predicate": { "kind": "true" }
                }
              ]
            }
            """;

        await AssertCodegenRejects(
            schema,
            "property 'expression' must equal the predicate's canonical display 'true'");
    }

    [Test]
    public async Task CSharpExtension_RejectsLegacyRelationMetadata()
    {
        var schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "LegacyRelation.json",
              "type": "object",
              "x-strategos-action-name": "legacyAction",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "entity",
              "x-strategos-relation": "owner",
              "x-strategos-link-path": ["Portfolio"]
            }
            """;

        await AssertCodegenRejects(schema, "legacy relation metadata is not accepted");
    }

    [Test]
    [Arguments("x-strategos-action-name")]
    [Arguments("x-strategos-domain")]
    [Arguments("x-strategos-object")]
    [Arguments("x-strategos-object-kind")]
    [Arguments("x-strategos-authority")]
    public async Task CSharpExtension_RejectsBlankSemanticMetadata(string propertyName)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = "BlankMetadata.json",
            ["type"] = "object",
            ["x-strategos-action-name"] = "inspect",
            ["x-strategos-domain"] = "Testing",
            ["x-strategos-object"] = "Fixture",
            ["x-strategos-object-kind"] = "entity",
            ["x-strategos-authority"] = "fixture.reader",
        };
        metadata[propertyName] = "   ";

        await AssertCodegenRejects(
            JsonSerializer.Serialize(metadata),
            $"metadata '{propertyName}' must be a non-empty string");
    }

    [Test]
    public async Task CSharpExtension_RejectsActionMetadataWithoutSubjectIdentity()
    {
        var schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "OrphanActionMetadata.json",
              "type": "object",
              "x-strategos-requires-v1": [
                {
                  "strength": "hard",
                  "expression": "true",
                  "predicate": { "kind": "true" }
                }
              ]
            }
            """;

        await AssertCodegenRejects(
            schema,
            "metadata 'x-strategos-requires-v1' requires complete @objectKind action identity");
    }

    [Test]
    public async Task CSharpExtension_RejectsDuplicateActionIdentityAcrossSchemas()
    {
        const string first = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "FirstRun.json",
              "type": "object",
              "x-strategos-action-name": "run",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "entity"
            }
            """;
        const string second = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "SecondRun.json",
              "type": "object",
              "x-strategos-action-name": "run",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "entity"
            }
            """;

        await AssertCodegenRejects(
            new Dictionary<string, string>
            {
                ["FirstRun.json"] = first,
                ["SecondRun.json"] = second,
            },
            "action identity 'Testing/Fixture.run': is declared 2 times");
    }

    [Test]
    public async Task CSharpExtension_RejectsConflictingObjectKindsWithinSubject()
    {
        const string first = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "Start.json",
              "type": "object",
              "x-strategos-action-name": "start",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "entity"
            }
            """;
        const string second = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "Complete.json",
              "type": "object",
              "x-strategos-action-name": "complete",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "process"
            }
            """;

        await AssertCodegenRejects(
            new Dictionary<string, string>
            {
                ["Start.json"] = first,
                ["Complete.json"] = second,
            },
            "action subject 'Testing/Fixture': declares conflicting object kinds \"entity\", \"process\"");
    }

    [Test]
    public async Task CSharpExtension_MergesEquivalentObjectKindCasingWithinSubject()
    {
        const string first = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "Start.json",
              "type": "object",
              "x-strategos-action-name": "start",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "entity"
            }
            """;
        const string second = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "Complete.json",
              "type": "object",
              "x-strategos-action-name": "complete",
              "x-strategos-domain": "Testing",
              "x-strategos-object": "Fixture",
              "x-strategos-object-kind": "Entity"
            }
            """;
        var input = Directory.CreateTempSubdirectory("contracts-equivalent-kind-").FullName;
        var records = Directory.CreateTempSubdirectory("contracts-equivalent-kind-records-").FullName;
        var ontology = Directory.CreateTempSubdirectory("contracts-equivalent-kind-ontology-").FullName;

        try
        {
            var canonicalSchemas = Path.Combine(
                RepoLayout.ContractsProjectDir,
                "schemas",
                "json-schema");
            foreach (var file in Directory.GetFiles(canonicalSchemas, "*.json"))
            {
                File.Copy(file, Path.Combine(input, Path.GetFileName(file)));
            }

            await File.WriteAllTextAsync(Path.Combine(input, "Start.json"), first);
            await File.WriteAllTextAsync(Path.Combine(input, "Complete.json"), second);
            var codegenProject = Path.Combine(
                RepoLayout.RepoRoot,
                "src",
                "Strategos.Contracts.Codegen",
                "Strategos.Contracts.Codegen.csproj");
            var result = await Cli.RunAsync(
                "dotnet",
                $"run --project \"{codegenProject}\" -- \"{input}\" \"{records}\" \"{ontology}\"");

            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            var generated = await File.ReadAllTextAsync(
                Path.Combine(ontology, "ContractOntology.g.cs"));
            const string subjectSymbol = "SymbolKey = \"typespec://Testing/Fixture\"";
            await Assert.That(generated.Split(subjectSymbol, StringSplitOptions.None).Length - 1)
                .IsEqualTo(1);
            await Assert.That(generated).Contains("\"complete\"");
            await Assert.That(generated).Contains("\"start\"");
        }
        finally
        {
            Directory.Delete(input, recursive: true);
            Directory.Delete(records, recursive: true);
            Directory.Delete(ontology, recursive: true);
        }
    }

    private static async Task AssertCodegenRejects(string schema, string expectedMessage)
        => await AssertCodegenRejects(
            new Dictionary<string, string> { ["InspectPositionRequest.json"] = schema },
            expectedMessage);

    private static async Task AssertCodegenRejects(
        IReadOnlyDictionary<string, string> schemas,
        string expectedMessage)
    {
        var input = Directory.CreateTempSubdirectory("contracts-invalid-predicate-").FullName;
        var records = Directory.CreateTempSubdirectory("contracts-invalid-records-").FullName;
        var ontology = Directory.CreateTempSubdirectory("contracts-invalid-ontology-").FullName;

        try
        {
            var canonicalSchemas = Path.Combine(
                RepoLayout.ContractsProjectDir,
                "schemas",
                "json-schema");
            foreach (var file in Directory.GetFiles(canonicalSchemas, "*.json"))
            {
                File.Copy(file, Path.Combine(input, Path.GetFileName(file)));
            }

            foreach (var schema in schemas)
            {
                await File.WriteAllTextAsync(Path.Combine(input, schema.Key), schema.Value);
            }

            var codegenProject = Path.Combine(
                RepoLayout.RepoRoot,
                "src",
                "Strategos.Contracts.Codegen",
                "Strategos.Contracts.Codegen.csproj");
            var result = await Cli.RunAsync(
                "dotnet",
                $"run --project \"{codegenProject}\" -- \"{input}\" \"{records}\" \"{ontology}\"");

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
            await Assert.That(result.Output).Contains(expectedMessage);
        }
        finally
        {
            Directory.Delete(input, recursive: true);
            Directory.Delete(records, recursive: true);
            Directory.Delete(ontology, recursive: true);
        }
    }
}

// =============================================================================
// <copyright file="ActionPredicateContractConformanceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

using NJsonSchema;

using Strategos.Ontology.MCP.Internal;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// Pins the MCP-local predicate DTOs to the Contracts 0.10 schema files without
/// introducing a production dependency on the contracts assembly.
/// </summary>
public sealed class ActionPredicateContractConformanceTests
{
    [Test]
    public async Task RequirementSerialization_ConformsToContractsSchema()
    {
        ActionPredicateV1 predicate = new ActionAllPredicateV1(
        [
            new ActionPropertyComparisonPredicateV1(
                new ActionPropertyReferenceV1(
                    "quantity",
                    ActionPredicateScalarKindV1.Integer,
                    IsNullable: false),
                ActionComparisonOperatorV1.GreaterThanOrEqual,
                new ActionIntegerLiteralV1("123456789012345678901234567890")),
            new ActionNotPredicateV1(new ActionLinkExistsPredicateV1("archivedBy")),
            new ActionCustomPredicateV1(
                "risk.acceptable",
                ImmutableArray.Create<ActionLiteralV1>(
                    new ActionDecimalLiteralV1("0.125"),
                    new ActionSymbolLiteralV1("intraday")),
                ImmutableArray.Create<ActionResourceV1>(
                    new ActionPropertyResourceV1("RiskScore"),
                    new ActionExternalResourceV1("market-clock"))),
        ]);
        var requirement = new ActionRequirementV1(
            predicate,
            "typed expression",
            ActionRequirementStrengthV1.Hard,
            Description: null);

        var json = JsonSerializer.Serialize(requirement);
        var errors = await Validate("ActionRequirementV1.json", json);

        await Assert.That(errors.Count).IsEqualTo(0)
            .Because(string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
        using var document = JsonDocument.Parse(json);
        await Assert.That(document.RootElement.TryGetProperty("description", out _)).IsFalse();
        await Assert.That(json).Contains("\"kind\":\"all\"");
        await Assert.That(json).Contains("\"strength\":\"hard\"");

        var roundTrip = JsonSerializer.Deserialize<ActionRequirementV1>(json);
        await Assert.That(roundTrip).IsNotNull();
        var reserialized = JsonSerializer.Serialize(roundTrip);
        await Assert.That(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(reserialized))).IsTrue();
    }

    [Test]
    public async Task GuaranteeSerialization_ConformsToContractsSchema()
    {
        var guarantee = new ActionGuaranteeV1(
            new ActionRelationHoldsPredicateV1("owner", ["Portfolio"]),
            "principal -[owner]-> Portfolio",
            "Ownership remains established.");

        var json = JsonSerializer.Serialize(guarantee);
        var errors = await Validate("ActionGuaranteeV1.json", json);

        await Assert.That(errors.Count).IsEqualTo(0)
            .Because(string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
        await Assert.That(json).Contains("\"kind\":\"relation-holds\"");
    }

    [Test]
    public async Task PredicateSerialization_RejectsUnknownKind()
    {
        const string json = """
            { "kind": "future-predicate" }
            """;
        var act = () => JsonSerializer.Deserialize<ActionPredicateV1>(json);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    public async Task PredicateSerialization_RejectsNumericEnumToken()
    {
        const string json = """
            {
              "kind": "property-comparison",
              "property": { "name": "quantity", "scalarKind": "integer", "isNullable": false },
              "operator": 99,
              "value": { "kind": "integer", "value": "1" }
            }
            """;
        var act = () => JsonSerializer.Deserialize<ActionPredicateV1>(json);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    [Arguments("\"99\"")]
    [Arguments("\"Equal\"")]
    [Arguments("\"EQUAL\"")]
    public async Task PredicateSerialization_RejectsNonCanonicalEnumToken(string json)
    {
        var act = () => JsonSerializer.Deserialize<ActionComparisonOperatorV1>(json);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    public async Task PredicateSerialization_RejectsUndefinedEnumValue()
    {
        var act = () => JsonSerializer.Serialize((ActionComparisonOperatorV1)99);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    public async Task PredicateSerialization_RejectsIncompleteKnownArm()
    {
        const string json = """
            { "kind": "link-exists" }
            """;
        var act = () => JsonSerializer.Deserialize<ActionPredicateV1>(json);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    [Arguments("{ \"kind\": \"link-exists\", \"linkName\": null }")]
    [Arguments("{ \"kind\": \"not\", \"predicate\": null }")]
    [Arguments("{ \"kind\": \"relation-holds\", \"relationName\": null, \"linkPath\": [] }")]
    [Arguments("{ \"kind\": \"relation-holds\", \"relationName\": \"owner\", \"linkPath\": [null] }")]
    [Arguments("{ \"kind\": \"all\", \"predicates\": [null] }")]
    [Arguments("{ \"kind\": \"any\", \"predicates\": [null] }")]
    [Arguments("{ \"kind\": \"custom\", \"evaluatorKey\": null, \"arguments\": [], \"readSet\": [] }")]
    [Arguments("{ \"kind\": \"custom\", \"evaluatorKey\": \"policy\", \"arguments\": [null], \"readSet\": [] }")]
    [Arguments("{ \"kind\": \"custom\", \"evaluatorKey\": \"policy\", \"arguments\": [], \"readSet\": [null] }")]
    public async Task PredicateSerialization_RejectsExplicitNullRequiredReferences(string json)
    {
        var act = () => JsonSerializer.Deserialize<ActionPredicateV1>(json);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    public async Task LiteralResourceAndWrappers_RejectExplicitNullRequiredReferences()
    {
        var literal = () => JsonSerializer.Deserialize<ActionLiteralV1>(
            "{ \"kind\": \"string\", \"value\": null }");
        var resource = () => JsonSerializer.Deserialize<ActionResourceV1>(
            "{ \"kind\": \"link\", \"name\": null }");
        var requirement = () => JsonSerializer.Deserialize<ActionRequirementV1>(
            "{ \"predicate\": null, \"expression\": \"true\", \"strength\": \"hard\" }");
        var guarantee = () => JsonSerializer.Deserialize<ActionGuaranteeV1>(
            "{ \"predicate\": { \"kind\": \"true\" }, \"expression\": null }");

        await Assert.That(literal).Throws<JsonException>();
        await Assert.That(resource).Throws<JsonException>();
        await Assert.That(requirement).Throws<JsonException>();
        await Assert.That(guarantee).Throws<JsonException>();
    }

    [Test]
    [Arguments("{ \"name\": \"status\", \"scalarKind\": \"enum\", \"isNullable\": false }")]
    [Arguments("{ \"name\": \"status\", \"scalarKind\": \"enum\", \"isNullable\": false, \"enumTypeName\": null }")]
    [Arguments("{ \"name\": \"status\", \"scalarKind\": \"enum\", \"isNullable\": false, \"enumTypeName\": \"   \" }")]
    [Arguments("{ \"name\": \"status\", \"scalarKind\": \"string\", \"isNullable\": false, \"enumTypeName\": \"OrderStatus\" }")]
    public async Task PropertyReferenceSerialization_RejectsInvalidEnumTypeIdentity(string json)
    {
        var act = () => JsonSerializer.Deserialize<ActionPropertyReferenceV1>(json);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    public async Task PredicateWireTypes_RejectNullReferencesDuringSerialization()
    {
        var predicate = () => JsonSerializer.Serialize<ActionPredicateV1>(
            new ActionLinkExistsPredicateV1(null!));
        var aggregate = () => JsonSerializer.Serialize<ActionPredicateV1>(
            new ActionAllPredicateV1([null!]));
        var literal = () => JsonSerializer.Serialize<ActionLiteralV1>(
            new ActionStringLiteralV1(null!));
        var resource = () => JsonSerializer.Serialize<ActionResourceV1>(
            new ActionLinkResourceV1(null!));
        var requirement = () => JsonSerializer.Serialize(
            new ActionRequirementV1(
                null!,
                "true",
                ActionRequirementStrengthV1.Hard,
                Description: null));
        var enumProperty = () => JsonSerializer.Serialize(
            new ActionPropertyReferenceV1(
                "status",
                ActionPredicateScalarKindV1.Enum,
                IsNullable: false));

        await Assert.That(predicate).Throws<JsonException>();
        await Assert.That(aggregate).Throws<JsonException>();
        await Assert.That(literal).Throws<JsonException>();
        await Assert.That(resource).Throws<JsonException>();
        await Assert.That(requirement).Throws<JsonException>();
        await Assert.That(enumProperty).Throws<JsonException>();
    }

    [Test]
    public async Task LiteralSerialization_RejectsUnknownKind()
    {
        const string json = """
            { "kind": "future-literal", "value": "opaque" }
            """;
        var act = () => JsonSerializer.Deserialize<ActionLiteralV1>(json);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    public async Task ResourceSerialization_RejectsUnknownKind()
    {
        const string json = """
            { "kind": "future-resource", "name": "opaque" }
            """;
        var act = () => JsonSerializer.Deserialize<ActionResourceV1>(json);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    public async Task PredicateSchemaExport_PreservesClosedDiscriminatorVocabulary()
    {
        var schema = JsonSchemaHelper.JsonSchemaFor<ActionRequirementV1>().GetRawText();

        await Assert.That(schema).Contains("property-comparison");
        await Assert.That(schema).Contains("relation-holds");
        await Assert.That(schema).Contains("custom");
        await Assert.That(schema).Contains("not-equal");
    }

    [Test]
    public async Task ProductionMcpAssembly_TakesNoContractsOrNJsonSchemaDependency()
    {
        foreach (var referenced in typeof(ActionPredicateV1).Assembly.GetReferencedAssemblies())
        {
            var name = referenced.Name ?? string.Empty;
            await Assert.That(name.Contains("Strategos.Contracts", StringComparison.OrdinalIgnoreCase)).IsFalse();
            await Assert.That(name.Contains("NJsonSchema", StringComparison.OrdinalIgnoreCase)).IsFalse();
        }
    }

    private static async Task<ICollection<NJsonSchema.Validation.ValidationError>> Validate(
        string schemaFile,
        string json)
    {
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFiles.Dir, schemaFile));
        return schema.Validate(json);
    }
}

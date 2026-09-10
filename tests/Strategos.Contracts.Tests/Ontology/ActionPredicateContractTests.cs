// =============================================================================
// <copyright file="ActionPredicateContractTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;
using Strategos.Contracts.Generated;

namespace Strategos.Contracts.Tests.Ontology;

/// <summary>Wire-shape and generated-record coverage for action predicates v1.</summary>
[Property("Category", "Ontology")]
[NotInParallel("tsp-compile")]
public sealed class ActionPredicateContractTests
{
    private static readonly string[] PredicateKinds =
    [
        "true",
        "false",
        "property-comparison",
        "link-exists",
        "relation-holds",
        "all",
        "any",
        "not",
        "custom",
    ];

    /// <summary>The versioned predicate is a closed recursive tagged union.</summary>
    [Test]
    public async Task PredicateSchema_IsClosedRecursiveTaggedUnion()
    {
        var compile = await TspToolchain.CompileAsync();
        await Assert.That(compile.ExitCode).IsEqualTo(0).Because(compile.Output);

        var predicate = await EventSchemas.LoadAsync("ActionPredicateV1");
        var armNames = predicate.GetProperty("anyOf").EnumerateArray()
            .Select(arm => Path.GetFileNameWithoutExtension(arm.GetProperty("$ref").GetString())!)
            .ToArray();
        await Assert.That(armNames.Length).IsEqualTo(PredicateKinds.Length);

        var actualKinds = new List<string>();
        foreach (var armName in armNames)
        {
            var arm = await EventSchemas.LoadAsync(armName);
            actualKinds.Add(arm.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString()!);
        }

        await Assert.That(actualKinds).IsEquivalentTo(PredicateKinds);

        var all = await EventSchemas.LoadAsync("ActionAllPredicateV1");
        var itemRef = all.GetProperty("properties").GetProperty("predicates")
            .GetProperty("items").GetProperty("$ref").GetString();
        await Assert.That(Path.GetFileNameWithoutExtension(itemRef)).IsEqualTo("ActionPredicateV1");

        var not = await EventSchemas.LoadAsync("ActionNotPredicateV1");
        var operandRef = not.GetProperty("properties").GetProperty("predicate")
            .GetProperty("$ref").GetString();
        await Assert.That(Path.GetFileNameWithoutExtension(operandRef)).IsEqualTo("ActionPredicateV1");
    }

    /// <summary>Integer and decimal values are exact strings, never JSON floating point.</summary>
    [Test]
    public async Task NumericLiteralSchemas_RequireCanonicalBase10Strings()
    {
        var integer = await EventSchemas.LoadAsync("ActionIntegerLiteralV1");
        var integerValue = integer.GetProperty("properties").GetProperty("value");
        await Assert.That(integerValue.GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(integerValue.GetProperty("pattern").GetString())
            .IsEqualTo("^(0|-?[1-9][0-9]*)$");

        var decimalSchema = await EventSchemas.LoadAsync("ActionDecimalLiteralV1");
        var decimalValue = decimalSchema.GetProperty("properties").GetProperty("value");
        await Assert.That(decimalValue.GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(decimalValue.GetProperty("pattern").GetString())
            .IsEqualTo("^(0|-?[1-9][0-9]*|-?(0|[1-9][0-9]*)\\.[0-9]*[1-9])$");
    }

    /// <summary>
    /// Relation-path elements are non-empty at the authoring, schema, and generated
    /// CLR layers, matching the runtime predicate factory's validation.
    /// </summary>
    [Test]
    public async Task RelationPathSchema_RejectsBlankSegmentsWithoutChangingClrWireType()
    {
        var relation = await EventSchemas.LoadAsync("ActionRelationHoldsPredicateV1");
        var segmentRef = relation.GetProperty("properties").GetProperty("linkPath")
            .GetProperty("items").GetProperty("$ref").GetString();
        await Assert.That(Path.GetFileNameWithoutExtension(segmentRef))
            .IsEqualTo("ActionLinkPathSegmentV1");

        var segment = await EventSchemas.LoadAsync("ActionLinkPathSegmentV1");
        await Assert.That(segment.GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(segment.GetProperty("minLength").GetInt32()).IsEqualTo(1);
        await Assert.That(segment.GetProperty("pattern").GetString()).IsEqualTo(".*\\S.*");

        var clrProperty = typeof(ActionRelationHoldsPredicateV1).GetProperty(
            nameof(ActionRelationHoldsPredicateV1.LinkPath));
        await Assert.That(clrProperty).IsNotNull();
        await Assert.That(clrProperty!.PropertyType).IsEqualTo(typeof(IReadOnlyList<string>));
    }

    /// <summary>Nested predicates and typed literals round-trip through the generated base types.</summary>
    [Test]
    public async Task GeneratedPredicate_RoundTripsWithStableDiscriminators()
    {
        ActionPredicateV1 predicate = new ActionAllPredicateV1
        {
            Predicates =
            [
                new ActionPropertyComparisonPredicateV1
                {
                    Property = new ActionPropertyReferenceV1
                    {
                        Name = "quantity",
                        ScalarKind = ActionPredicateScalarKindV1.Integer,
                        IsNullable = false,
                    },
                    Operator = ActionComparisonOperatorV1.GreaterThanOrEqual,
                    Value = new ActionIntegerLiteralV1 { Value = "123456789012345678901234567890" },
                },
                new ActionNotPredicateV1
                {
                    Predicate = new ActionLinkExistsPredicateV1 { LinkName = "archivedBy" },
                },
            ],
        };

        var json = ContractsJson.Serialize(predicate);
        var roundTrip = JsonSerializer.Deserialize<ActionPredicateV1>(json, ContractsJson.Options);

        await Assert.That(roundTrip).IsTypeOf<ActionAllPredicateV1>();
        await Assert.That(json).Contains("\"kind\": \"all\"");
        await Assert.That(json).Contains("123456789012345678901234567890");
        await Assert.That(json).Contains("\"operator\": \"greater-than-or-equal\"");
    }

    /// <summary>Unknown discriminators are rejected rather than becoming custom or true.</summary>
    [Test]
    public async Task GeneratedPredicate_RejectsUnknownDiscriminator()
    {
        const string json = """
            { "kind": "future-predicate", "payload": true }
            """;
        var act = () => JsonSerializer.Deserialize<ActionPredicateV1>(json, ContractsJson.Options);

        await Assert.That(act).Throws<JsonException>();
    }

    /// <summary>Closed wire enums reject numeric ordinals, including undefined values.</summary>
    [Test]
    public async Task GeneratedPredicate_RejectsNumericComparisonOperator()
    {
        const string json = """
            {
              "kind": "property-comparison",
              "property": { "name": "quantity", "scalarKind": "integer", "isNullable": false },
              "operator": 99,
              "value": { "kind": "integer", "value": "1" }
            }
            """;
        var act = () => JsonSerializer.Deserialize<ActionPredicateV1>(json, ContractsJson.Options);

        await Assert.That(act).Throws<JsonException>();
    }

    /// <summary>Closed operator enums accept only exact schema tokens.</summary>
    [Test]
    [Arguments("\"99\"")]
    [Arguments("\"Equal\"")]
    [Arguments("\"EQUAL\"")]
    public async Task GeneratedPredicate_RejectsNonCanonicalComparisonOperator(string json)
    {
        var act = () => JsonSerializer.Deserialize<ActionComparisonOperatorV1>(json, ContractsJson.Options);

        await Assert.That(act).Throws<JsonException>();
    }

    /// <summary>A recognized predicate arm still rejects absent schema-required fields.</summary>
    [Test]
    public async Task GeneratedPredicate_RejectsIncompleteKnownArm()
    {
        const string json = """
            { "kind": "link-exists" }
            """;
        var act = () => JsonSerializer.Deserialize<ActionPredicateV1>(json, ContractsJson.Options);

        await Assert.That(act).Throws<JsonException>();
    }

    /// <summary>Required predicate references reject explicit null, including recursive list elements.</summary>
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
    public async Task GeneratedPredicate_RejectsExplicitNullRequiredReferences(string json)
    {
        var act = () => JsonSerializer.Deserialize<ActionPredicateV1>(json, ContractsJson.Options);

        await Assert.That(act).Throws<JsonException>();
    }

    /// <summary>Required literal and resource strings reject explicit null.</summary>
    [Test]
    public async Task GeneratedLiteralAndResource_RejectExplicitNullRequiredStrings()
    {
        var literal = () => JsonSerializer.Deserialize<ActionLiteralV1>(
            "{ \"kind\": \"string\", \"value\": null }",
            ContractsJson.Options);
        var resource = () => JsonSerializer.Deserialize<ActionResourceV1>(
            "{ \"kind\": \"link\", \"name\": null }",
            ContractsJson.Options);

        await Assert.That(literal).Throws<JsonException>();
        await Assert.That(resource).Throws<JsonException>();
    }

    /// <summary>Requirement and guarantee payloads reject explicit null required references.</summary>
    [Test]
    public async Task GeneratedRequirementAndGuarantee_RejectExplicitNullRequiredReferences()
    {
        var requirement = () => JsonSerializer.Deserialize<ActionRequirementV1>(
            "{ \"predicate\": null, \"expression\": \"true\", \"strength\": \"hard\" }",
            ContractsJson.Options);
        var guarantee = () => JsonSerializer.Deserialize<ActionGuaranteeV1>(
            "{ \"predicate\": { \"kind\": \"true\" }, \"expression\": null }",
            ContractsJson.Options);

        await Assert.That(requirement).Throws<JsonException>();
        await Assert.That(guarantee).Throws<JsonException>();
    }

    /// <summary>Enum property identity is present exactly for the enum scalar domain.</summary>
    [Test]
    [Arguments("{ \"name\": \"status\", \"scalarKind\": \"enum\", \"isNullable\": false }")]
    [Arguments("{ \"name\": \"status\", \"scalarKind\": \"enum\", \"isNullable\": false, \"enumTypeName\": null }")]
    [Arguments("{ \"name\": \"status\", \"scalarKind\": \"enum\", \"isNullable\": false, \"enumTypeName\": \"   \" }")]
    [Arguments("{ \"name\": \"status\", \"scalarKind\": \"string\", \"isNullable\": false, \"enumTypeName\": \"OrderStatus\" }")]
    public async Task GeneratedPropertyReference_RejectsInvalidEnumTypeIdentity(string json)
    {
        var act = () => JsonSerializer.Deserialize<ActionPropertyReferenceV1>(
            json,
            ContractsJson.Options);

        await Assert.That(act).Throws<JsonException>();
    }

    /// <summary>Canonical serialization cannot omit invalid required references or emit null list elements.</summary>
    [Test]
    public async Task GeneratedActionContracts_RejectNullReferencesDuringSerialization()
    {
        var predicate = () => ContractsJson.Serialize<ActionPredicateV1>(
            new ActionLinkExistsPredicateV1 { LinkName = null! });
        var aggregate = () => ContractsJson.Serialize<ActionPredicateV1>(
            new ActionAllPredicateV1 { Predicates = [null!] });
        var literal = () => ContractsJson.Serialize<ActionLiteralV1>(
            new ActionStringLiteralV1 { Value = null! });
        var resource = () => ContractsJson.Serialize<ActionResourceV1>(
            new ActionLinkResourceV1 { Name = null! });
        var requirement = () => ContractsJson.Serialize(
            new ActionRequirementV1
            {
                Predicate = null!,
                Expression = "true",
                Strength = ActionRequirementStrengthV1.Hard,
            });
        var enumProperty = () => ContractsJson.Serialize(
            new ActionPropertyReferenceV1
            {
                Name = "status",
                ScalarKind = ActionPredicateScalarKindV1.Enum,
                IsNullable = false,
            });

        await Assert.That(predicate).Throws<JsonException>();
        await Assert.That(aggregate).Throws<JsonException>();
        await Assert.That(literal).Throws<JsonException>();
        await Assert.That(resource).Throws<JsonException>();
        await Assert.That(requirement).Throws<JsonException>();
        await Assert.That(enumProperty).Throws<JsonException>();
    }
}

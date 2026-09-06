// =============================================================================
// <copyright file="ActionPredicateEnumJsonConverters.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP.Internal;

/// <summary>Reads and writes only the exact comparison-operator wire tokens.</summary>
public sealed class ActionComparisonOperatorV1JsonConverter
    : JsonConverter<ActionComparisonOperatorV1>
{
    /// <inheritdoc />
    public override ActionComparisonOperatorV1 Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        RequireString(reader, nameof(ActionComparisonOperatorV1));
        return reader.GetString() switch
        {
            "equal" => ActionComparisonOperatorV1.Equal,
            "not-equal" => ActionComparisonOperatorV1.NotEqual,
            "less-than" => ActionComparisonOperatorV1.LessThan,
            "less-than-or-equal" => ActionComparisonOperatorV1.LessThanOrEqual,
            "greater-than" => ActionComparisonOperatorV1.GreaterThan,
            "greater-than-or-equal" => ActionComparisonOperatorV1.GreaterThanOrEqual,
            _ => throw new JsonException("Unknown ActionComparisonOperatorV1 wire token."),
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ActionComparisonOperatorV1 value,
        JsonSerializerOptions options)
    {
        var token = value switch
        {
            ActionComparisonOperatorV1.Equal => "equal",
            ActionComparisonOperatorV1.NotEqual => "not-equal",
            ActionComparisonOperatorV1.LessThan => "less-than",
            ActionComparisonOperatorV1.LessThanOrEqual => "less-than-or-equal",
            ActionComparisonOperatorV1.GreaterThan => "greater-than",
            ActionComparisonOperatorV1.GreaterThanOrEqual => "greater-than-or-equal",
            _ => throw new JsonException("Unknown ActionComparisonOperatorV1 value."),
        };
        writer.WriteStringValue(token);
    }

    private static void RequireString(Utf8JsonReader reader, string enumName)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"{enumName} requires an exact string wire token.");
        }
    }
}

/// <summary>Reads and writes only the exact scalar-kind wire tokens.</summary>
public sealed class ActionPredicateScalarKindV1JsonConverter
    : JsonConverter<ActionPredicateScalarKindV1>
{
    /// <inheritdoc />
    public override ActionPredicateScalarKindV1 Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        RequireString(reader);
        return reader.GetString() switch
        {
            "boolean" => ActionPredicateScalarKindV1.Boolean,
            "integer" => ActionPredicateScalarKindV1.Integer,
            "decimal" => ActionPredicateScalarKindV1.Decimal,
            "string" => ActionPredicateScalarKindV1.String,
            "enum" => ActionPredicateScalarKindV1.Enum,
            "symbol" => ActionPredicateScalarKindV1.Symbol,
            _ => throw new JsonException("Unknown ActionPredicateScalarKindV1 wire token."),
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ActionPredicateScalarKindV1 value,
        JsonSerializerOptions options)
    {
        var token = value switch
        {
            ActionPredicateScalarKindV1.Boolean => "boolean",
            ActionPredicateScalarKindV1.Integer => "integer",
            ActionPredicateScalarKindV1.Decimal => "decimal",
            ActionPredicateScalarKindV1.String => "string",
            ActionPredicateScalarKindV1.Enum => "enum",
            ActionPredicateScalarKindV1.Symbol => "symbol",
            _ => throw new JsonException("Unknown ActionPredicateScalarKindV1 value."),
        };
        writer.WriteStringValue(token);
    }

    private static void RequireString(Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("ActionPredicateScalarKindV1 requires an exact string wire token.");
        }
    }
}

/// <summary>Reads and writes only the exact requirement-strength wire tokens.</summary>
public sealed class ActionRequirementStrengthV1JsonConverter
    : JsonConverter<ActionRequirementStrengthV1>
{
    /// <inheritdoc />
    public override ActionRequirementStrengthV1 Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        RequireString(reader);
        return reader.GetString() switch
        {
            "hard" => ActionRequirementStrengthV1.Hard,
            "soft" => ActionRequirementStrengthV1.Soft,
            _ => throw new JsonException("Unknown ActionRequirementStrengthV1 wire token."),
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ActionRequirementStrengthV1 value,
        JsonSerializerOptions options)
    {
        var token = value switch
        {
            ActionRequirementStrengthV1.Hard => "hard",
            ActionRequirementStrengthV1.Soft => "soft",
            _ => throw new JsonException("Unknown ActionRequirementStrengthV1 value."),
        };
        writer.WriteStringValue(token);
    }

    private static void RequireString(Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("ActionRequirementStrengthV1 requires an exact string wire token.");
        }
    }
}

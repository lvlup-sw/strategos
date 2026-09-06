// =============================================================================
// <copyright file="ActionPredicateV1.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Collections.Immutable;
using System.Text.Json.Serialization;

using Strategos.Ontology.MCP.Internal;

namespace Strategos.Ontology.MCP;

/// <summary>Wire comparison operators shared with the Contracts 0.10 schema.</summary>
[JsonConverter(typeof(ActionComparisonOperatorV1JsonConverter))]
public enum ActionComparisonOperatorV1
{
    [JsonStringEnumMemberName("equal")]
    Equal,

    [JsonStringEnumMemberName("not-equal")]
    NotEqual,

    [JsonStringEnumMemberName("less-than")]
    LessThan,

    [JsonStringEnumMemberName("less-than-or-equal")]
    LessThanOrEqual,

    [JsonStringEnumMemberName("greater-than")]
    GreaterThan,

    [JsonStringEnumMemberName("greater-than-or-equal")]
    GreaterThanOrEqual,
}

/// <summary>Wire scalar domains shared with the Contracts 0.10 schema.</summary>
[JsonConverter(typeof(ActionPredicateScalarKindV1JsonConverter))]
public enum ActionPredicateScalarKindV1
{
    [JsonStringEnumMemberName("boolean")]
    Boolean,

    [JsonStringEnumMemberName("integer")]
    Integer,

    [JsonStringEnumMemberName("decimal")]
    Decimal,

    [JsonStringEnumMemberName("string")]
    String,

    [JsonStringEnumMemberName("enum")]
    Enum,

    [JsonStringEnumMemberName("symbol")]
    Symbol,
}

/// <summary>Wire hard/soft requirement strength.</summary>
[JsonConverter(typeof(ActionRequirementStrengthV1JsonConverter))]
public enum ActionRequirementStrengthV1
{
    [JsonStringEnumMemberName("hard")]
    Hard,

    [JsonStringEnumMemberName("soft")]
    Soft,
}

/// <summary>Version 1 of the closed, lossless predicate-literal wire vocabulary.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ActionNullLiteralV1), "null")]
[JsonDerivedType(typeof(ActionBooleanLiteralV1), "boolean")]
[JsonDerivedType(typeof(ActionIntegerLiteralV1), "integer")]
[JsonDerivedType(typeof(ActionDecimalLiteralV1), "decimal")]
[JsonDerivedType(typeof(ActionStringLiteralV1), "string")]
[JsonDerivedType(typeof(ActionEnumLiteralV1), "enum")]
[JsonDerivedType(typeof(ActionSymbolLiteralV1), "symbol")]
public abstract record ActionLiteralV1 : IJsonOnDeserialized, IJsonOnSerializing
{
    void IJsonOnDeserialized.OnDeserialized() =>
        ActionPredicateWireValidation.Validate(this);

    void IJsonOnSerializing.OnSerializing() =>
        ActionPredicateWireValidation.Validate(this);
}

/// <summary>Explicit null; distinct from a missing fact.</summary>
public sealed record ActionNullLiteralV1 : ActionLiteralV1;

/// <summary>An exact Boolean literal.</summary>
public sealed record ActionBooleanLiteralV1(
    [property: JsonPropertyName("value"), JsonRequired] bool Value) : ActionLiteralV1;

/// <summary>An arbitrary-precision integer represented by a canonical base-10 string.</summary>
public sealed record ActionIntegerLiteralV1(
    [property: JsonPropertyName("value"), JsonRequired] string Value) : ActionLiteralV1;

/// <summary>An exact decimal represented by a canonical base-10 string.</summary>
public sealed record ActionDecimalLiteralV1(
    [property: JsonPropertyName("value"), JsonRequired] string Value) : ActionLiteralV1;

/// <summary>An ordinal string literal.</summary>
public sealed record ActionStringLiteralV1(
    [property: JsonPropertyName("value"), JsonRequired] string Value) : ActionLiteralV1;

/// <summary>A member of a language-neutral named enum.</summary>
public sealed record ActionEnumLiteralV1(
    [property: JsonPropertyName("typeName"), JsonRequired] string TypeName,
    [property: JsonPropertyName("memberName"), JsonRequired] string MemberName) : ActionLiteralV1;

/// <summary>An ordinal symbolic identifier.</summary>
public sealed record ActionSymbolLiteralV1(
    [property: JsonPropertyName("value"), JsonRequired] string Value) : ActionLiteralV1;

/// <summary>Version 1 of a custom predicate's declared resource read.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ActionPropertyResourceV1), "property")]
[JsonDerivedType(typeof(ActionLinkResourceV1), "link")]
[JsonDerivedType(typeof(ActionEventResourceV1), "event")]
[JsonDerivedType(typeof(ActionExternalResourceV1), "external")]
public abstract record ActionResourceV1 : IJsonOnDeserialized, IJsonOnSerializing
{
    void IJsonOnDeserialized.OnDeserialized() =>
        ActionPredicateWireValidation.Validate(this);

    void IJsonOnSerializing.OnSerializing() =>
        ActionPredicateWireValidation.Validate(this);
}

/// <summary>A declared read of an ontology property.</summary>
public sealed record ActionPropertyResourceV1(
    [property: JsonPropertyName("name"), JsonRequired] string Name) : ActionResourceV1;

/// <summary>A declared read of an ontology link.</summary>
public sealed record ActionLinkResourceV1(
    [property: JsonPropertyName("name"), JsonRequired] string Name) : ActionResourceV1;

/// <summary>A declared read of an ontology event stream.</summary>
public sealed record ActionEventResourceV1(
    [property: JsonPropertyName("name"), JsonRequired] string Name) : ActionResourceV1;

/// <summary>A declared read of an external resource.</summary>
public sealed record ActionExternalResourceV1(
    [property: JsonPropertyName("name"), JsonRequired] string Name) : ActionResourceV1;

/// <summary>Language-neutral identity and scalar domain of an ontology property.</summary>
public sealed record ActionPropertyReferenceV1(
    [property: JsonPropertyName("name"), JsonRequired] string Name,
    [property: JsonPropertyName("scalarKind"), JsonRequired] ActionPredicateScalarKindV1 ScalarKind,
    [property: JsonPropertyName("isNullable"), JsonRequired] bool IsNullable,
    [property: JsonPropertyName("enumTypeName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EnumTypeName = null) : IJsonOnDeserialized, IJsonOnSerializing
{
    void IJsonOnDeserialized.OnDeserialized() =>
        ActionPredicateWireValidation.Validate(this);

    void IJsonOnSerializing.OnSerializing() =>
        ActionPredicateWireValidation.Validate(this);
}

/// <summary>Version 1 of the closed recursive action-predicate wire vocabulary.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ActionTruePredicateV1), "true")]
[JsonDerivedType(typeof(ActionFalsePredicateV1), "false")]
[JsonDerivedType(typeof(ActionPropertyComparisonPredicateV1), "property-comparison")]
[JsonDerivedType(typeof(ActionLinkExistsPredicateV1), "link-exists")]
[JsonDerivedType(typeof(ActionRelationHoldsPredicateV1), "relation-holds")]
[JsonDerivedType(typeof(ActionAllPredicateV1), "all")]
[JsonDerivedType(typeof(ActionAnyPredicateV1), "any")]
[JsonDerivedType(typeof(ActionNotPredicateV1), "not")]
[JsonDerivedType(typeof(ActionCustomPredicateV1), "custom")]
public abstract record ActionPredicateV1 : IJsonOnDeserialized, IJsonOnSerializing
{
    void IJsonOnDeserialized.OnDeserialized() =>
        ActionPredicateWireValidation.Validate(this);

    void IJsonOnSerializing.OnSerializing() =>
        ActionPredicateWireValidation.Validate(this);
}

/// <summary>The predicate that is satisfied in every state.</summary>
public sealed record ActionTruePredicateV1 : ActionPredicateV1;

/// <summary>The predicate that is unsatisfied in every state.</summary>
public sealed record ActionFalsePredicateV1 : ActionPredicateV1;

/// <summary>Compares an ontology property with a typed literal.</summary>
public sealed record ActionPropertyComparisonPredicateV1(
    [property: JsonPropertyName("property"), JsonRequired] ActionPropertyReferenceV1 Property,
    [property: JsonPropertyName("operator"), JsonRequired] ActionComparisonOperatorV1 Operator,
    [property: JsonPropertyName("value"), JsonRequired] ActionLiteralV1 Value) : ActionPredicateV1;

/// <summary>Requires an ontology link to exist.</summary>
public sealed record ActionLinkExistsPredicateV1(
    [property: JsonPropertyName("linkName"), JsonRequired] string LinkName) : ActionPredicateV1;

/// <summary>Requires an ontology relation to hold through an optional link path.</summary>
public sealed record ActionRelationHoldsPredicateV1(
    [property: JsonPropertyName("relationName"), JsonRequired] string RelationName,
    [property: JsonPropertyName("linkPath"), JsonRequired] ImmutableArray<string> LinkPath) : ActionPredicateV1;

/// <summary>Requires every nested predicate to hold.</summary>
public sealed record ActionAllPredicateV1(
    [property: JsonPropertyName("predicates"), JsonRequired] ImmutableArray<ActionPredicateV1> Predicates) : ActionPredicateV1;

/// <summary>Requires at least one nested predicate to hold.</summary>
public sealed record ActionAnyPredicateV1(
    [property: JsonPropertyName("predicates"), JsonRequired] ImmutableArray<ActionPredicateV1> Predicates) : ActionPredicateV1;

/// <summary>Negates one nested predicate.</summary>
public sealed record ActionNotPredicateV1(
    [property: JsonPropertyName("predicate"), JsonRequired] ActionPredicateV1 Predicate) : ActionPredicateV1;

/// <summary>Delegates evaluation to a stable registered evaluator key.</summary>
public sealed record ActionCustomPredicateV1(
    [property: JsonPropertyName("evaluatorKey"), JsonRequired] string EvaluatorKey,
    [property: JsonPropertyName("arguments"), JsonRequired] ImmutableArray<ActionLiteralV1> Arguments,
    [property: JsonPropertyName("readSet"), JsonRequired] ImmutableArray<ActionResourceV1> ReadSet) : ActionPredicateV1;

/// <summary>A typed hard or soft requirement in MCP action metadata.</summary>
public sealed record ActionRequirementV1(
    [property: JsonPropertyName("predicate"), JsonRequired] ActionPredicateV1 Predicate,
    [property: JsonPropertyName("expression"), JsonRequired] string Expression,
    [property: JsonPropertyName("strength"), JsonRequired] ActionRequirementStrengthV1 Strength,
    [property: JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Description) : IJsonOnDeserialized, IJsonOnSerializing
{
    void IJsonOnDeserialized.OnDeserialized() =>
        ActionPredicateWireValidation.Validate(this);

    void IJsonOnSerializing.OnSerializing() =>
        ActionPredicateWireValidation.Validate(this);
}

/// <summary>A typed post-state guarantee in MCP action metadata.</summary>
public sealed record ActionGuaranteeV1(
    [property: JsonPropertyName("predicate"), JsonRequired] ActionPredicateV1 Predicate,
    [property: JsonPropertyName("expression"), JsonRequired] string Expression,
    [property: JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Description) : IJsonOnDeserialized, IJsonOnSerializing
{
    void IJsonOnDeserialized.OnDeserialized() =>
        ActionPredicateWireValidation.Validate(this);

    void IJsonOnSerializing.OnSerializing() =>
        ActionPredicateWireValidation.Validate(this);
}

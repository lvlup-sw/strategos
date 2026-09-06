// =============================================================================
// <copyright file="ActionPredicateWireValidation.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Collections.Immutable;
using System.Text.Json;

namespace Strategos.Ontology.MCP.Internal;

/// <summary>
/// Keeps the standalone MCP predicate DTOs as strict as their Contracts 0.10
/// schema when callers use the default or a source-generated JSON serializer.
/// </summary>
internal static class ActionPredicateWireValidation
{
    internal static void Validate(ActionLiteralV1 literal)
    {
        switch (literal)
        {
            case ActionIntegerLiteralV1 integer:
                Require(integer.Value, "ActionIntegerLiteralV1.value");
                break;
            case ActionDecimalLiteralV1 decimalLiteral:
                Require(decimalLiteral.Value, "ActionDecimalLiteralV1.value");
                break;
            case ActionStringLiteralV1 text:
                Require(text.Value, "ActionStringLiteralV1.value");
                break;
            case ActionEnumLiteralV1 enumLiteral:
                Require(enumLiteral.TypeName, "ActionEnumLiteralV1.typeName");
                Require(enumLiteral.MemberName, "ActionEnumLiteralV1.memberName");
                break;
            case ActionSymbolLiteralV1 symbol:
                Require(symbol.Value, "ActionSymbolLiteralV1.value");
                break;
        }
    }

    internal static void Validate(ActionResourceV1 resource)
    {
        switch (resource)
        {
            case ActionPropertyResourceV1 property:
                Require(property.Name, "ActionPropertyResourceV1.name");
                break;
            case ActionLinkResourceV1 link:
                Require(link.Name, "ActionLinkResourceV1.name");
                break;
            case ActionEventResourceV1 eventResource:
                Require(eventResource.Name, "ActionEventResourceV1.name");
                break;
            case ActionExternalResourceV1 external:
                Require(external.Name, "ActionExternalResourceV1.name");
                break;
        }
    }

    internal static void Validate(ActionPropertyReferenceV1 property)
    {
        Require(property.Name, "ActionPropertyReferenceV1.name");
        if (property.ScalarKind == ActionPredicateScalarKindV1.Enum)
        {
            if (string.IsNullOrWhiteSpace(property.EnumTypeName))
            {
                throw new JsonException(
                    "ActionPropertyReferenceV1.enumTypeName is required and cannot be blank for an enum property.");
            }
        }
        else if (property.EnumTypeName is not null)
        {
            throw new JsonException(
                "ActionPropertyReferenceV1.enumTypeName must be absent for a non-enum property.");
        }
    }

    internal static void Validate(ActionPredicateV1 predicate)
    {
        switch (predicate)
        {
            case ActionPropertyComparisonPredicateV1 comparison:
                Require(comparison.Property, "ActionPropertyComparisonPredicateV1.property");
                Require(comparison.Value, "ActionPropertyComparisonPredicateV1.value");
                break;
            case ActionLinkExistsPredicateV1 link:
                Require(link.LinkName, "ActionLinkExistsPredicateV1.linkName");
                break;
            case ActionRelationHoldsPredicateV1 relation:
                Require(relation.RelationName, "ActionRelationHoldsPredicateV1.relationName");
                RequireNoNullElements(relation.LinkPath, "ActionRelationHoldsPredicateV1.linkPath");
                break;
            case ActionAllPredicateV1 all:
                RequireNoNullElements(all.Predicates, "ActionAllPredicateV1.predicates");
                break;
            case ActionAnyPredicateV1 any:
                RequireNoNullElements(any.Predicates, "ActionAnyPredicateV1.predicates");
                break;
            case ActionNotPredicateV1 not:
                Require(not.Predicate, "ActionNotPredicateV1.predicate");
                break;
            case ActionCustomPredicateV1 custom:
                Require(custom.EvaluatorKey, "ActionCustomPredicateV1.evaluatorKey");
                RequireNoNullElements(custom.Arguments, "ActionCustomPredicateV1.arguments");
                RequireNoNullElements(custom.ReadSet, "ActionCustomPredicateV1.readSet");
                break;
        }
    }

    internal static void Validate(ActionRequirementV1 requirement)
    {
        Require(requirement.Predicate, "ActionRequirementV1.predicate");
        Require(requirement.Expression, "ActionRequirementV1.expression");
    }

    internal static void Validate(ActionGuaranteeV1 guarantee)
    {
        Require(guarantee.Predicate, "ActionGuaranteeV1.predicate");
        Require(guarantee.Expression, "ActionGuaranteeV1.expression");
    }

    private static void Require<T>(T? value, string propertyName)
        where T : class
    {
        if (value is null)
        {
            throw new JsonException($"Required action contract property '{propertyName}' cannot be null.");
        }
    }

    private static void RequireNoNullElements<T>(
        ImmutableArray<T> values,
        string propertyName)
        where T : class
    {
        if (values.IsDefault)
        {
            throw new JsonException($"Required action contract property '{propertyName}' cannot be null.");
        }

        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] is null)
            {
                throw new JsonException(
                    $"Required action contract property '{propertyName}' cannot contain null at index {index}.");
            }
        }
    }
}

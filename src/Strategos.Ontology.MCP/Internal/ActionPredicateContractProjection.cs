// =============================================================================
// <copyright file="ActionPredicateContractProjection.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.MCP.Internal;

/// <summary>
/// Projects the runtime predicate model into the MCP-local wire twin of the
/// Contracts 0.10 schema. This keeps the production assembly independent of the
/// contracts package while preserving one schema-tested JSON vocabulary.
/// </summary>
internal static class ActionPredicateContractProjection
{
    internal static ActionRequirementV1 Requirement(ActionPrecondition requirement) =>
        new(
            Predicate(requirement.Predicate),
            requirement.Expression,
            requirement.Strength == ConstraintStrength.Hard
                ? ActionRequirementStrengthV1.Hard
                : ActionRequirementStrengthV1.Soft,
            requirement.Description);

    internal static ActionGuaranteeV1 Guarantee(ActionGuarantee guarantee) =>
        new(Predicate(guarantee.Predicate), guarantee.Expression, guarantee.Description);

    internal static ActionPredicateV1 Predicate(ActionPredicate predicate) => predicate switch
    {
        ConstantActionPredicate { Value: true } => new ActionTruePredicateV1(),
        ConstantActionPredicate => new ActionFalsePredicateV1(),
        PropertyComparisonPredicate comparison => new ActionPropertyComparisonPredicateV1(
            Property(comparison.PropertyReference),
            Operator(comparison.Operator),
            Literal(comparison.Value)),
        LinkExistsPredicate link => new ActionLinkExistsPredicateV1(link.LinkName),
        RelationHoldsPredicate relation => new ActionRelationHoldsPredicateV1(
            relation.RelationName,
            relation.LinkPath),
        AllPredicate all => new ActionAllPredicateV1(
            all.Operands.Select(Predicate).ToImmutableArray()),
        AnyPredicate any => new ActionAnyPredicateV1(
            any.Operands.Select(Predicate).ToImmutableArray()),
        NotPredicate not => new ActionNotPredicateV1(Predicate(not.Operand)),
        CustomPredicate custom => new ActionCustomPredicateV1(
            custom.EvaluatorKey,
            custom.Arguments.Select(Literal).ToImmutableArray(),
            custom.ReadSet.Select(Resource).ToImmutableArray()),
        _ => throw new InvalidOperationException(
            $"Unknown action predicate runtime type '{predicate.GetType().FullName}'."),
    };

    internal static ImmutableArray<ActionAuthorizationRequirement> MandatoryRelations(
        IEnumerable<ActionPrecondition> requirements)
    {
        var relations = new SortedDictionary<string, ActionAuthorizationRequirement>(StringComparer.Ordinal);
        foreach (var requirement in requirements.Where(item => item.Strength == ConstraintStrength.Hard))
        {
            foreach (var pair in MandatoryRelations(requirement.Predicate))
            {
                relations[pair.Key] = pair.Value;
            }
        }

        return relations.Values.ToImmutableArray();
    }

    private static IReadOnlyDictionary<string, ActionAuthorizationRequirement> MandatoryRelations(
        ActionPredicate predicate)
    {
        switch (predicate)
        {
            case RelationHoldsPredicate relation:
                return new Dictionary<string, ActionAuthorizationRequirement>(StringComparer.Ordinal)
                {
                    [relation.CanonicalToken] = new(
                        relation.RelationName,
                        relation.LinkPath),
                };

            case AllPredicate all:
            {
                var union = new Dictionary<string, ActionAuthorizationRequirement>(StringComparer.Ordinal);
                foreach (var operand in all.Operands)
                {
                    foreach (var pair in MandatoryRelations(operand))
                    {
                        union[pair.Key] = pair.Value;
                    }
                }

                return union;
            }

            case AnyPredicate any:
            {
                if (any.Operands.IsEmpty)
                {
                    return ImmutableDictionary<string, ActionAuthorizationRequirement>.Empty;
                }

                var intersection = MandatoryRelations(any.Operands[0])
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                foreach (var operand in any.Operands.Skip(1))
                {
                    var branch = MandatoryRelations(operand);
                    foreach (var key in intersection.Keys.Where(key => !branch.ContainsKey(key)).ToArray())
                    {
                        intersection.Remove(key);
                    }
                }

                return intersection;
            }

            // A negated relation is not a positive authorization requirement.
            // Non-relation atoms and opaque predicates add no safe flat facade.
            default:
                return ImmutableDictionary<string, ActionAuthorizationRequirement>.Empty;
        }
    }

    private static ActionPropertyReferenceV1 Property(PredicatePropertyReference property) =>
        new(
            property.Name,
            ScalarKind(property.ScalarKind),
            property.IsNullable,
            property.EnumTypeName);

    private static ActionPredicateScalarKindV1 ScalarKind(PredicateScalarKind kind) => kind switch
    {
        PredicateScalarKind.Boolean => ActionPredicateScalarKindV1.Boolean,
        PredicateScalarKind.Integer => ActionPredicateScalarKindV1.Integer,
        PredicateScalarKind.Decimal => ActionPredicateScalarKindV1.Decimal,
        PredicateScalarKind.String => ActionPredicateScalarKindV1.String,
        PredicateScalarKind.Enum => ActionPredicateScalarKindV1.Enum,
        PredicateScalarKind.Symbol => ActionPredicateScalarKindV1.Symbol,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown predicate scalar kind."),
    };

    private static ActionComparisonOperatorV1 Operator(PredicateComparisonOperator comparison) => comparison switch
    {
        PredicateComparisonOperator.Equal => ActionComparisonOperatorV1.Equal,
        PredicateComparisonOperator.NotEqual => ActionComparisonOperatorV1.NotEqual,
        PredicateComparisonOperator.LessThan => ActionComparisonOperatorV1.LessThan,
        PredicateComparisonOperator.LessThanOrEqual => ActionComparisonOperatorV1.LessThanOrEqual,
        PredicateComparisonOperator.GreaterThan => ActionComparisonOperatorV1.GreaterThan,
        PredicateComparisonOperator.GreaterThanOrEqual => ActionComparisonOperatorV1.GreaterThanOrEqual,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown predicate comparison."),
    };

    private static ActionLiteralV1 Literal(PredicateLiteral literal) => literal.Kind switch
    {
        PredicateLiteralKind.Null => new ActionNullLiteralV1(),
        PredicateLiteralKind.Boolean => new ActionBooleanLiteralV1(literal.BooleanValue),
        PredicateLiteralKind.Integer => new ActionIntegerLiteralV1(literal.CanonicalValue),
        PredicateLiteralKind.Decimal => new ActionDecimalLiteralV1(literal.CanonicalValue),
        PredicateLiteralKind.String => new ActionStringLiteralV1(literal.CanonicalValue),
        PredicateLiteralKind.Enum => new ActionEnumLiteralV1(literal.TypeName!, literal.EnumMemberName),
        PredicateLiteralKind.Symbol => new ActionSymbolLiteralV1(literal.CanonicalValue),
        _ => throw new ArgumentOutOfRangeException(nameof(literal), literal.Kind, "Unknown predicate literal kind."),
    };

    private static ActionResourceV1 Resource(ActionResource resource) => resource.Kind switch
    {
        ActionResourceKind.Property => new ActionPropertyResourceV1(resource.Name),
        ActionResourceKind.Link => new ActionLinkResourceV1(resource.Name),
        ActionResourceKind.Event => new ActionEventResourceV1(resource.Name),
        ActionResourceKind.External => new ActionExternalResourceV1(resource.Name),
        _ => throw new ArgumentOutOfRangeException(nameof(resource), resource.Kind, "Unknown action resource kind."),
    };
}

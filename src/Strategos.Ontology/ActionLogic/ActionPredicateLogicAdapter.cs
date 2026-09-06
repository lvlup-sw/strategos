using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.ActionLogic;

/// <summary>
/// Bridges the public, presentation-aware predicate model to the small proof IR.
/// The reverse map is deliberately structural: projection can only retain or
/// remove existing atoms, so no string parser is needed to reconstruct a public
/// predicate after forgetting.
/// </summary>
internal sealed class ActionPredicateLogicAdapter
{
    private readonly Dictionary<string, ActionPredicate> predicatesByFormulaKey =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImmutableArray<ActionResource>> readsByResourceKey =
        new(StringComparer.Ordinal);

    internal LogicFormula ToLogic(
        ActionPredicate predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return predicate switch
        {
            ConstantActionPredicate constant => constant.Value ? LogicFormula.True : LogicFormula.False,
            PropertyComparisonPredicate comparison => Register(
                LogicFormula.Comparison(
                    PropertyResource(comparison.PropertyReference),
                    ToLogic(comparison.Operator),
                    ToLogic(comparison.Value)),
                comparison),
            LinkExistsPredicate link => Register(
                LogicFormula.BooleanAtom(BooleanResource(LinkResourceKey(link.LinkName))),
                link),
            RelationHoldsPredicate relation => Register(
                LogicFormula.BooleanAtom(BooleanResource(RelationResourceKey(relation))),
                relation,
                GetLocallyWritableRelationReads(relation)),
            AllPredicate all => LogicFormula.All(all.Operands.Select(operand =>
                ToLogic(operand, cancellationToken))),
            AnyPredicate any => LogicFormula.Any(any.Operands.Select(operand =>
                ToLogic(operand, cancellationToken))),
            NotPredicate not => LogicFormula.Not(ToLogic(not.Operand, cancellationToken)),
            CustomPredicate custom => Register(LogicFormula.Opaque(custom.CanonicalToken), custom),
            _ => throw new InvalidOperationException(
                $"Unknown action predicate type '{predicate.GetType().FullName}'."),
        };
    }

    internal ActionPredicate ToPredicate(LogicFormula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);

        return formula.Kind switch
        {
            LogicFormulaKind.True => ActionPredicate.True,
            LogicFormulaKind.False => ActionPredicate.False,
            LogicFormulaKind.Comparison or LogicFormulaKind.BooleanAtom or LogicFormulaKind.Opaque =>
                predicatesByFormulaKey.TryGetValue(formula.StableKey, out var predicate)
                    ? predicate
                    : throw new InvalidOperationException(
                        $"The proof atom '{formula.StableKey}' did not originate in the public predicate model."),
            LogicFormulaKind.All => ActionPredicate.All(formula.Operands.Select(ToPredicate)),
            LogicFormulaKind.Any => ActionPredicate.Any(formula.Operands.Select(ToPredicate)),
            LogicFormulaKind.Not => ActionPredicate.Not(ToPredicate(formula.Operands[0])),
            _ => throw new InvalidOperationException($"Unknown proof formula kind '{formula.Kind}'."),
        };
    }

    /// <summary>
    /// Expands a public frame into proof resources. Frames are local to the
    /// action subject. For a traversed relation, only replacing the first hop
    /// can change which remote relation is observed; deeper same-named links
    /// belong to linked objects and are not writable through a local frame. A
    /// direct principal-to-target relation reads the subject's named relation
    /// link, so rewriting that link must forget the relation fact as well.
    /// </summary>
    internal ImmutableArray<string> GetWrittenLogicResourceKeys(IEnumerable<ActionResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var written = resources
            .Where(resource => resource.Kind is ActionResourceKind.Property or ActionResourceKind.Link)
            .ToHashSet();
        var result = ImmutableArray.CreateBuilder<string>();

        foreach (var pair in readsByResourceKey.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (pair.Value.Any(written.Contains))
            {
                result.Add(pair.Key);
            }
        }

        return result.ToImmutable();
    }

    internal static string PropertyResourceKey(string propertyName) => "property|" + propertyName;

    internal static string LinkResourceKey(string linkName) => "link|" + linkName;

    private LogicFormula Register(
        LogicFormula formula,
        ActionPredicate predicate,
        ImmutableArray<ActionResource>? frameReads = null)
    {
        predicatesByFormulaKey[formula.StableKey] = predicate;
        if (formula.Resource is not null)
        {
            readsByResourceKey[formula.Resource.Key] = frameReads ?? predicate.ReferencedResources;
        }

        return formula;
    }

    private static ImmutableArray<ActionResource> GetLocallyWritableRelationReads(
        RelationHoldsPredicate relation) => ImmutableArray.Create(ActionResource.Link(
            relation.LinkPath.IsEmpty
                ? relation.RelationName
                : relation.LinkPath[0]));

    private static LogicResource PropertyResource(PredicatePropertyReference property) => new(
        PropertyResourceKey(property.Name),
        property.ScalarKind switch
        {
            PredicateScalarKind.Boolean => LogicScalarKind.Boolean,
            PredicateScalarKind.Integer => LogicScalarKind.Integer,
            PredicateScalarKind.Decimal => LogicScalarKind.Decimal,
            PredicateScalarKind.String => LogicScalarKind.String,
            PredicateScalarKind.Enum => LogicScalarKind.Enum,
            PredicateScalarKind.Symbol => LogicScalarKind.Symbol,
            _ => throw new ArgumentOutOfRangeException(nameof(property), property.ScalarKind, "Unknown scalar kind."),
        },
        property.IsNullable,
        property.EnumTypeName);

    private static LogicResource BooleanResource(string key) =>
        new(key, LogicScalarKind.Boolean, isNullable: false);

    private static string RelationResourceKey(RelationHoldsPredicate relation) =>
        "relation|" + relation.CanonicalToken;

    private static LogicComparisonOperator ToLogic(PredicateComparisonOperator comparison) => comparison switch
    {
        PredicateComparisonOperator.Equal => LogicComparisonOperator.Equal,
        PredicateComparisonOperator.NotEqual => LogicComparisonOperator.NotEqual,
        PredicateComparisonOperator.LessThan => LogicComparisonOperator.LessThan,
        PredicateComparisonOperator.LessThanOrEqual => LogicComparisonOperator.LessThanOrEqual,
        PredicateComparisonOperator.GreaterThan => LogicComparisonOperator.GreaterThan,
        PredicateComparisonOperator.GreaterThanOrEqual => LogicComparisonOperator.GreaterThanOrEqual,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown comparison operator."),
    };

    private static LogicLiteral ToLogic(PredicateLiteral literal) => literal.Kind switch
    {
        PredicateLiteralKind.Null => LogicLiteral.Null,
        PredicateLiteralKind.Boolean => new LogicLiteral(LogicLiteralKind.Boolean, literal.CanonicalValue),
        PredicateLiteralKind.Integer => new LogicLiteral(LogicLiteralKind.Integer, literal.CanonicalValue),
        PredicateLiteralKind.Decimal => new LogicLiteral(LogicLiteralKind.Decimal, literal.CanonicalValue),
        PredicateLiteralKind.String => new LogicLiteral(LogicLiteralKind.String, literal.CanonicalValue),
        PredicateLiteralKind.Enum => new LogicLiteral(
            LogicLiteralKind.Enum,
            literal.CanonicalValue,
            literal.TypeName),
        PredicateLiteralKind.Symbol => new LogicLiteral(LogicLiteralKind.Symbol, literal.CanonicalValue),
        _ => throw new ArgumentOutOfRangeException(nameof(literal), literal.Kind, "Unknown literal kind."),
    };
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;

namespace Strategos.Ontology.ActionLogic;

internal enum LogicDecisionKind
{
    Satisfiable,
    Unsatisfiable,
    Opaque,
    Invalid,
}

internal sealed class LogicDecision
{
    internal LogicDecision(
        LogicDecisionKind kind,
        ImmutableArray<KeyValuePair<string, string>> witness = default,
        string? reason = null)
    {
        Kind = kind;
        Witness = witness.IsDefault ? ImmutableArray<KeyValuePair<string, string>>.Empty : witness;
        Reason = reason;
    }

    internal LogicDecisionKind Kind { get; }

    internal ImmutableArray<KeyValuePair<string, string>> Witness { get; }

    internal string? Reason { get; }
}

internal static class FiniteDomainSolver
{
    internal static LogicDecision IsSatisfiable(
        LogicFormula formula,
        CancellationToken cancellationToken = default) =>
        DecideSatisfiability(formula, cancellationToken, minimizeWitness: true);

    private static LogicDecision DecideSatisfiability(
        LogicFormula formula,
        CancellationToken cancellationToken,
        bool minimizeWitness)
    {
        if (formula is null)
        {
            throw new ArgumentNullException(nameof(formula));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (formula.ContainsOpaque)
        {
            return new LogicDecision(LogicDecisionKind.Opaque, reason: "The predicate contains an opaque custom term.");
        }

        if (!TryBuildDomains(formula, out var domains, out var reason, cancellationToken))
        {
            return new LogicDecision(LogicDecisionKind.Invalid, reason: reason);
        }

        var formulaDag = new FormulaDag(formula, cancellationToken);
        var ordered = domains.Values
            .OrderBy(domain => domain.Resource.Key, StringComparer.Ordinal)
            .ToImmutableArray();
        var assignment = new Dictionary<string, LogicCell>(StringComparer.Ordinal);
        var unsatisfiablePrefixes = new HashSet<string>(StringComparer.Ordinal);
        if (!Search(
                formulaDag,
                ordered,
                assignment,
                0,
                unsatisfiablePrefixes,
                cancellationToken,
                out var witness))
        {
            return new LogicDecision(LogicDecisionKind.Unsatisfiable);
        }

        if (minimizeWitness)
        {
            MinimizeWitness(formula, formulaDag, witness, cancellationToken);
        }

        return new LogicDecision(
            LogicDecisionKind.Satisfiable,
            witness
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Display))
                .ToImmutableArray());
    }

    internal static LogicDecision Implies(
        LogicFormula antecedent,
        LogicFormula consequent,
        CancellationToken cancellationToken = default) =>
        IsSatisfiable(LogicFormula.All(antecedent, LogicFormula.Not(consequent)), cancellationToken);

    internal static bool TryNormalizeExactDecimal(string value, out string canonicalValue)
    {
        try
        {
            canonicalValue = ExactDecimal.Parse(value).ToString();
            return true;
        }
        catch (FormatException)
        {
            canonicalValue = null!;
            return false;
        }
    }

    /// <summary>
    /// Validates that every occurrence of a named resource uses one scalar
    /// domain across several formulas. This deliberately does not conjoin the
    /// formulas: requirements describe the pre-state and guarantees describe
    /// the post-state, so their values may differ when the resource is written,
    /// while its scalar metadata may not.
    /// </summary>
    internal static bool TryValidateResourceDomains(
        IEnumerable<LogicFormula> formulas,
        out string? failureReason,
        CancellationToken cancellationToken = default)
    {
        if (formulas is null)
        {
            throw new ArgumentNullException(nameof(formulas));
        }

        return TryBuildDomains(
            formulas,
            out _,
            out failureReason,
            cancellationToken);
    }

    internal static bool TryForget(
        LogicFormula formula,
        IEnumerable<string> resourceKeys,
        out LogicFormula projected,
        out string? failureReason,
        CancellationToken cancellationToken = default)
    {
        if (formula is null)
        {
            throw new ArgumentNullException(nameof(formula));
        }

        if (resourceKeys is null)
        {
            throw new ArgumentNullException(nameof(resourceKeys));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (formula.ContainsOpaque)
        {
            projected = formula;
            failureReason = "Opaque predicates cannot be projected at build time.";
            return false;
        }

        projected = formula;
        foreach (var resourceKey in resourceKeys
                     .Where(key => !string.IsNullOrWhiteSpace(key))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(key => key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryBuildDomains(projected, out var domains, out failureReason, cancellationToken))
            {
                return false;
            }

            if (!domains.TryGetValue(resourceKey, out var domain))
            {
                continue;
            }

            var current = projected;
            var alternatives = ImmutableArray.CreateBuilder<LogicFormula>(domain.Cells.Length);
            foreach (var cell in domain.Cells)
            {
                cancellationToken.ThrowIfCancellationRequested();
                alternatives.Add(Substitute(current, resourceKey, cell, cancellationToken));
            }

            projected = LogicFormula.Any(alternatives);
        }

        failureReason = null;
        return true;
    }

    /// <summary>
    /// Replaces opaque terms with stable Boolean atoms so the decidable
    /// fragment can prove facts that hold independently of custom evaluator
    /// semantics.
    /// </summary>
    internal static LogicFormula AbstractOpaqueTerms(
        LogicFormula formula,
        CancellationToken cancellationToken = default)
    {
        if (formula is null)
        {
            throw new ArgumentNullException(nameof(formula));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return formula.Kind switch
        {
            LogicFormulaKind.Opaque => LogicFormula.BooleanAtom(new LogicResource(
                OpaqueResourceKey(formula.OpaqueKey!),
                LogicScalarKind.Boolean,
                isNullable: false)),
            LogicFormulaKind.Not => LogicFormula.Not(AbstractOpaqueTerms(
                formula.Operands[0],
                cancellationToken)),
            LogicFormulaKind.All => LogicFormula.All(formula.Operands.Select(operand =>
                AbstractOpaqueTerms(operand, cancellationToken))),
            LogicFormulaKind.Any => LogicFormula.Any(formula.Operands.Select(operand =>
                AbstractOpaqueTerms(operand, cancellationToken))),
            _ => formula,
        };
    }

    /// <summary>
    /// Searches for a frame-realizability violation that is forced by the
    /// closed fragment of an otherwise opaque contract. Opaque requirements
    /// are universally quantified and opaque guarantees are existentially
    /// quantified, so a returned witness cannot depend on guessing custom
    /// evaluator behavior.
    /// </summary>
    internal static bool TryFindDefiniteFrameViolation(
        LogicFormula requirement,
        LogicFormula guarantee,
        IEnumerable<string> writtenResourceKeys,
        out LogicDecision decision,
        out string? failureReason,
        CancellationToken cancellationToken = default)
    {
        if (requirement is null)
        {
            throw new ArgumentNullException(nameof(requirement));
        }

        if (guarantee is null)
        {
            throw new ArgumentNullException(nameof(guarantee));
        }

        if (writtenResourceKeys is null)
        {
            throw new ArgumentNullException(nameof(writtenResourceKeys));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var abstractRequirement = AbstractOpaqueTerms(requirement, cancellationToken);
        var abstractGuarantee = AbstractOpaqueTerms(guarantee, cancellationToken);
        var requirementOpaqueResources = FindOpaqueResourceKeys(requirement, cancellationToken);
        if (!TryForget(
                LogicFormula.Not(abstractRequirement),
                requirementOpaqueResources,
                out var possiblyFalseRequirement,
                out failureReason,
                cancellationToken))
        {
            decision = new LogicDecision(
                LogicDecisionKind.Invalid,
                reason: failureReason ?? "Opaque requirements could not be universally projected.");
            return false;
        }

        var definitelyRequired = LogicFormula.Not(possiblyFalseRequirement);
        var optimisticGuaranteeResources = writtenResourceKeys
            .Concat(FindOpaqueResourceKeys(guarantee, cancellationToken))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal);
        if (!TryForget(
                abstractGuarantee,
                optimisticGuaranteeResources,
                out var possiblyRealizableGuarantee,
                out failureReason,
                cancellationToken))
        {
            decision = new LogicDecision(
                LogicDecisionKind.Invalid,
                reason: failureReason ?? "Opaque guarantees could not be existentially projected.");
            return false;
        }

        decision = Implies(definitelyRequired, possiblyRealizableGuarantee, cancellationToken);
        failureReason = decision.Kind is LogicDecisionKind.Satisfiable or LogicDecisionKind.Unsatisfiable
            ? null
            : decision.Reason ?? "The opaque frame could not be classified.";
        return failureReason is null;
    }

    private static ImmutableArray<string> FindOpaqueResourceKeys(
        LogicFormula formula,
        CancellationToken cancellationToken)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        CollectOpaqueResourceKeys(formula, keys, cancellationToken);
        return keys.ToImmutableArray();
    }

    private static void CollectOpaqueResourceKeys(
        LogicFormula formula,
        ISet<string> keys,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (formula.Kind == LogicFormulaKind.Opaque)
        {
            keys.Add(OpaqueResourceKey(formula.OpaqueKey!));
            return;
        }

        foreach (var operand in formula.Operands)
        {
            CollectOpaqueResourceKeys(operand, keys, cancellationToken);
        }
    }

    private static string OpaqueResourceKey(string opaqueKey) => "opaque|" + opaqueKey;

    private static bool Search(
        FormulaDag formula,
        ImmutableArray<LogicDomain> domains,
        Dictionary<string, LogicCell> assignment,
        int index,
        HashSet<string> unsatisfiablePrefixes,
        CancellationToken cancellationToken,
        out Dictionary<string, LogicCell> witness)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var partial = formula.Evaluate(assignment, cancellationToken);
        if (partial == LogicTruthValue.False)
        {
            witness = null!;
            return false;
        }

        if (partial == LogicTruthValue.True)
        {
            witness = new Dictionary<string, LogicCell>(assignment, StringComparer.Ordinal);
            return true;
        }

        while (index < domains.Length && assignment.ContainsKey(domains[index].Resource.Key))
        {
            index++;
        }

        if (index == domains.Length)
        {
            witness = null!;
            return false;
        }

        var prefixKey = BuildPrefixKey(domains, assignment, index);
        if (unsatisfiablePrefixes.Contains(prefixKey))
        {
            witness = null!;
            return false;
        }

        var domain = domains[index];
        foreach (var cell in domain.Cells)
        {
            assignment[domain.Resource.Key] = cell;
            if (Search(
                    formula,
                    domains,
                    assignment,
                    index + 1,
                    unsatisfiablePrefixes,
                    cancellationToken,
                    out witness))
            {
                assignment.Remove(domain.Resource.Key);
                return true;
            }

            assignment.Remove(domain.Resource.Key);
        }

        unsatisfiablePrefixes.Add(prefixKey);
        witness = null!;
        return false;
    }

    private static void MinimizeWitness(
        LogicFormula formula,
        FormulaDag formulaDag,
        Dictionary<string, LogicCell> witness,
        CancellationToken cancellationToken)
    {
        foreach (var key in witness.Keys.OrderByDescending(key => key, StringComparer.Ordinal).ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = witness[key];
            witness.Remove(key);
            if (formulaDag.Evaluate(witness, cancellationToken) != LogicTruthValue.True
                && !IsTrueForAllCompletions(formula, witness, cancellationToken))
            {
                witness[key] = value;
            }
        }
    }

    private static bool IsTrueForAllCompletions(
        LogicFormula formula,
        IReadOnlyDictionary<string, LogicCell> assignment,
        CancellationToken cancellationToken)
    {
        var reduced = formula;
        foreach (var pair in assignment.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            reduced = Substitute(reduced, pair.Key, pair.Value, cancellationToken);
        }

        if (ReferenceEquals(reduced, LogicFormula.True))
        {
            return true;
        }

        if (ReferenceEquals(reduced, LogicFormula.False))
        {
            return false;
        }

        var counterexample = DecideSatisfiability(
            LogicFormula.Not(reduced),
            cancellationToken,
            minimizeWitness: false);
        return counterexample.Kind == LogicDecisionKind.Unsatisfiable;
    }

    private static string BuildPrefixKey(
        ImmutableArray<LogicDomain> domains,
        IReadOnlyDictionary<string, LogicCell> assignment,
        int index)
    {
        var pieces = new List<string> { index.ToString(CultureInfo.InvariantCulture) };
        for (var i = 0; i < index; i++)
        {
            var key = domains[i].Resource.Key;
            if (assignment.TryGetValue(key, out var cell))
            {
                pieces.Add(key.Length.ToString(CultureInfo.InvariantCulture));
                pieces.Add(key);
                pieces.Add(cell.Literal.StableKey);
            }
        }

        return string.Join("|", pieces);
    }

    private static bool EvaluateComparison(
        LogicLiteral left,
        LogicComparisonOperator comparisonOperator,
        LogicLiteral right)
    {
        if (left.Kind == LogicLiteralKind.Null || right.Kind == LogicLiteralKind.Null)
        {
            var equal = left.Kind == LogicLiteralKind.Null && right.Kind == LogicLiteralKind.Null;
            return comparisonOperator switch
            {
                LogicComparisonOperator.Equal => equal,
                LogicComparisonOperator.NotEqual => !equal,
                _ => false,
            };
        }

        int comparison;
        if (left.Kind == LogicLiteralKind.Integer && right.Kind == LogicLiteralKind.Integer)
        {
            comparison = BigInteger.Parse(left.CanonicalValue, CultureInfo.InvariantCulture)
                .CompareTo(BigInteger.Parse(right.CanonicalValue, CultureInfo.InvariantCulture));
        }
        else if (left.Kind == LogicLiteralKind.Decimal && right.Kind == LogicLiteralKind.Decimal)
        {
            comparison = ExactDecimal.Parse(left.CanonicalValue).CompareTo(ExactDecimal.Parse(right.CanonicalValue));
        }
        else
        {
            comparison = string.Compare(left.CanonicalValue, right.CanonicalValue, StringComparison.Ordinal);
        }

        return comparisonOperator switch
        {
            LogicComparisonOperator.Equal => comparison == 0,
            LogicComparisonOperator.NotEqual => comparison != 0,
            LogicComparisonOperator.GreaterThan => comparison > 0,
            LogicComparisonOperator.GreaterThanOrEqual => comparison >= 0,
            LogicComparisonOperator.LessThan => comparison < 0,
            LogicComparisonOperator.LessThanOrEqual => comparison <= 0,
            _ => throw new InvalidOperationException(
                $"Unknown comparison operator '{comparisonOperator}'."),
        };
    }

    private static LogicFormula Substitute(
        LogicFormula formula,
        string resourceKey,
        LogicCell cell,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return formula.Kind switch
        {
            LogicFormulaKind.Comparison when formula.Resource!.Key == resourceKey =>
                EvaluateComparison(cell.Literal, formula.ComparisonOperator, formula.Literal!)
                    ? LogicFormula.True
                    : LogicFormula.False,
            LogicFormulaKind.BooleanAtom when formula.Resource!.Key == resourceKey =>
                string.Equals(cell.Literal.CanonicalValue, "true", StringComparison.Ordinal)
                    ? LogicFormula.True
                    : LogicFormula.False,
            LogicFormulaKind.Not => LogicFormula.Not(Substitute(
                formula.Operands[0],
                resourceKey,
                cell,
                cancellationToken)),
            LogicFormulaKind.All => LogicFormula.All(
                formula.Operands.Select(operand => Substitute(
                    operand,
                    resourceKey,
                    cell,
                    cancellationToken))),
            LogicFormulaKind.Any => LogicFormula.Any(
                formula.Operands.Select(operand => Substitute(
                    operand,
                    resourceKey,
                    cell,
                    cancellationToken))),
            _ => formula,
        };
    }

    private static bool TryBuildDomains(
        LogicFormula formula,
        out Dictionary<string, LogicDomain> domains,
        out string? failureReason,
        CancellationToken cancellationToken) =>
        TryBuildDomains([formula], out domains, out failureReason, cancellationToken);

    private static bool TryBuildDomains(
        IEnumerable<LogicFormula> formulas,
        out Dictionary<string, LogicDomain> domains,
        out string? failureReason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var builders = new Dictionary<string, DomainBuilder>(StringComparer.Ordinal);
        foreach (var formula in formulas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (formula is null)
            {
                domains = null!;
                failureReason = "A resource-domain validation formula cannot be null.";
                return false;
            }

            if (!Collect(formula, builders, out failureReason, cancellationToken))
            {
                domains = null!;
                return false;
            }
        }

        domains = new Dictionary<string, LogicDomain>(StringComparer.Ordinal);
        foreach (var pair in builders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!pair.Value.TryBuild(out var domain, out failureReason, cancellationToken))
            {
                domains = null!;
                return false;
            }

            domains.Add(pair.Key, domain!);
        }

        failureReason = null;
        return true;
    }

    private static bool Collect(
        LogicFormula formula,
        IDictionary<string, DomainBuilder> builders,
        out string? failureReason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (formula.Kind is LogicFormulaKind.Comparison or LogicFormulaKind.BooleanAtom)
        {
            var resource = formula.Resource!;
            if (builders.TryGetValue(resource.Key, out var existing)
                && !existing.Resource.Equals(resource))
            {
                failureReason = $"Resource '{resource.Key}' is used with inconsistent scalar metadata.";
                return false;
            }

            if (existing is null)
            {
                existing = new DomainBuilder(resource);
                builders.Add(resource.Key, existing);
            }

            if (formula.Kind == LogicFormulaKind.Comparison)
            {
                if (!existing.TryAdd(formula.ComparisonOperator, formula.Literal!, out failureReason))
                {
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        foreach (var operand in formula.Operands)
        {
            if (!Collect(operand, builders, out failureReason, cancellationToken))
            {
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    private sealed class DomainBuilder
    {
        private readonly Dictionary<string, LogicLiteral> literals = new(StringComparer.Ordinal);

        internal DomainBuilder(LogicResource resource)
        {
            Resource = resource;
        }

        internal LogicResource Resource { get; }

        internal bool TryAdd(
            LogicComparisonOperator comparisonOperator,
            LogicLiteral literal,
            out string? failureReason)
        {
            if (!IsCompatible(Resource, comparisonOperator, literal, out failureReason))
            {
                return false;
            }

            literals[literal.StableKey] = literal;
            return true;
        }

        internal bool TryBuild(
            out LogicDomain? domain,
            out string? failureReason,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cells = new List<LogicCell>();
            if (Resource.IsNullable)
            {
                cells.Add(new LogicCell(LogicLiteral.Null, "null"));
            }

            switch (Resource.ScalarKind)
            {
                case LogicScalarKind.Boolean:
                    cells.Add(new LogicCell(new LogicLiteral(LogicLiteralKind.Boolean, "false"), "false"));
                    cells.Add(new LogicCell(new LogicLiteral(LogicLiteralKind.Boolean, "true"), "true"));
                    break;
                case LogicScalarKind.Integer:
                    if (!AddIntegerCells(cells, literals.Values, out failureReason, cancellationToken))
                    {
                        domain = null;
                        return false;
                    }

                    break;
                case LogicScalarKind.Decimal:
                    if (!AddDecimalCells(cells, literals.Values, out failureReason, cancellationToken))
                    {
                        domain = null;
                        return false;
                    }

                    break;
                case LogicScalarKind.String:
                    AddDiscreteCells(
                        cells,
                        literals.Values,
                        LogicLiteralKind.String,
                        "<other-string>",
                        cancellationToken);
                    break;
                case LogicScalarKind.Enum:
                    AddDiscreteCells(
                        cells,
                        literals.Values,
                        LogicLiteralKind.Enum,
                        "<other-enum>",
                        cancellationToken,
                        Resource.ScalarTypeName);
                    break;
                case LogicScalarKind.Symbol:
                    AddDiscreteCells(
                        cells,
                        literals.Values,
                        LogicLiteralKind.Symbol,
                        "<other-symbol>",
                        cancellationToken);
                    break;
                default:
                    domain = null;
                    failureReason = $"Unknown scalar kind '{Resource.ScalarKind}'.";
                    return false;
            }

            domain = new LogicDomain(Resource, cells.ToImmutableArray());
            failureReason = null;
            return true;
        }

        private static bool IsCompatible(
            LogicResource resource,
            LogicComparisonOperator comparisonOperator,
            LogicLiteral literal,
            out string? failureReason)
        {
            if (literal.Kind == LogicLiteralKind.Null)
            {
                if (!resource.IsNullable)
                {
                    failureReason = $"Non-nullable resource '{resource.Key}' cannot be compared with null.";
                    return false;
                }

                if (comparisonOperator is not LogicComparisonOperator.Equal
                    and not LogicComparisonOperator.NotEqual)
                {
                    failureReason = "Null supports equality and inequality only.";
                    return false;
                }

                failureReason = null;
                return true;
            }

            var expected = resource.ScalarKind switch
            {
                LogicScalarKind.Boolean => LogicLiteralKind.Boolean,
                LogicScalarKind.Integer => LogicLiteralKind.Integer,
                LogicScalarKind.Decimal => LogicLiteralKind.Decimal,
                LogicScalarKind.String => LogicLiteralKind.String,
                LogicScalarKind.Enum => LogicLiteralKind.Enum,
                LogicScalarKind.Symbol => LogicLiteralKind.Symbol,
                _ => literal.Kind,
            };
            if (literal.Kind != expected)
            {
                failureReason = $"Resource '{resource.Key}' expects {expected} literals, not {literal.Kind}.";
                return false;
            }

            if (resource.ScalarKind == LogicScalarKind.Enum
                && !string.Equals(
                    resource.ScalarTypeName,
                    literal.ScalarTypeName,
                    StringComparison.Ordinal))
            {
                failureReason = $"Enum resource '{resource.Key}' has type '{resource.ScalarTypeName}', "
                    + $"but the literal has type '{literal.ScalarTypeName}'.";
                return false;
            }

            if (resource.ScalarKind is not LogicScalarKind.Integer and not LogicScalarKind.Decimal
                && comparisonOperator is not LogicComparisonOperator.Equal
                and not LogicComparisonOperator.NotEqual)
            {
                failureReason = $"Resource '{resource.Key}' supports equality and inequality only.";
                return false;
            }

            failureReason = null;
            return true;
        }

        private static bool AddIntegerCells(
            ICollection<LogicCell> cells,
            IEnumerable<LogicLiteral> values,
            out string? failureReason,
            CancellationToken cancellationToken)
        {
            try
            {
                var distinct = new HashSet<BigInteger>();
                foreach (var value in values.Where(value => value.Kind == LogicLiteralKind.Integer))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    distinct.Add(BigInteger.Parse(value.CanonicalValue, CultureInfo.InvariantCulture));
                }

                var constants = distinct.OrderBy(value => value).ToArray();
                if (constants.Length == 0)
                {
                    cells.Add(IntegerCell(BigInteger.Zero, "0"));
                    failureReason = null;
                    return true;
                }

                cells.Add(IntegerCell(
                    constants[0] - BigInteger.One,
                    "<" + constants[0].ToString(CultureInfo.InvariantCulture)));
                for (var i = 0; i < constants.Length; i++)
                {
                    cells.Add(IntegerCell(constants[i], constants[i].ToString(CultureInfo.InvariantCulture)));
                    if (i + 1 < constants.Length && constants[i + 1] - constants[i] > BigInteger.One)
                    {
                        cells.Add(IntegerCell(
                            constants[i] + BigInteger.One,
                            "(" + constants[i].ToString(CultureInfo.InvariantCulture)
                                + ", " + constants[i + 1].ToString(CultureInfo.InvariantCulture) + ")"));
                    }
                }

                cells.Add(IntegerCell(
                    constants[constants.Length - 1] + BigInteger.One,
                    ">" + constants[constants.Length - 1].ToString(CultureInfo.InvariantCulture)));
                failureReason = null;
                return true;
            }
            catch (FormatException)
            {
                failureReason = "An integer literal is not in canonical base-10 form.";
                return false;
            }
        }

        private static bool AddDecimalCells(
            ICollection<LogicCell> cells,
            IEnumerable<LogicLiteral> values,
            out string? failureReason,
            CancellationToken cancellationToken)
        {
            try
            {
                var distinct = new HashSet<ExactDecimal>();
                foreach (var value in values.Where(value => value.Kind == LogicLiteralKind.Decimal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    distinct.Add(ExactDecimal.Parse(value.CanonicalValue));
                }

                var constants = distinct.OrderBy(value => value).ToArray();
                if (constants.Length == 0)
                {
                    cells.Add(DecimalCell(ExactDecimal.Zero, "0"));
                    failureReason = null;
                    return true;
                }

                cells.Add(DecimalCell(constants[0].SubtractOne(), $"<{constants[0]}"));
                for (var i = 0; i < constants.Length; i++)
                {
                    cells.Add(DecimalCell(constants[i], constants[i].ToString()));
                    if (i + 1 < constants.Length)
                    {
                        cells.Add(DecimalCell(
                            ExactDecimal.Midpoint(constants[i], constants[i + 1]),
                            $"({constants[i]}, {constants[i + 1]})"));
                    }
                }

                cells.Add(DecimalCell(
                    constants[constants.Length - 1].AddOne(),
                    $">{constants[constants.Length - 1]}"));
                failureReason = null;
                return true;
            }
            catch (FormatException)
            {
                failureReason = "A decimal literal is not in canonical base-10 form.";
                return false;
            }
        }

        private static void AddDiscreteCells(
            ICollection<LogicCell> cells,
            IEnumerable<LogicLiteral> values,
            LogicLiteralKind kind,
            string other,
            CancellationToken cancellationToken,
            string? scalarTypeName = null)
        {
            var constants = values.Where(value => value.Kind == kind).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            Array.Sort(constants, (left, right) => string.Compare(
                left.CanonicalValue,
                right.CanonicalValue,
                StringComparison.Ordinal));
            foreach (var constant in constants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cells.Add(new LogicCell(constant, DisplayDiscreteLiteral(constant)));
            }

            var otherValue = other;
            while (constants.Any(value => string.Equals(
                       value.CanonicalValue,
                       otherValue,
                       StringComparison.Ordinal)))
            {
                otherValue += "_";
            }

            cells.Add(new LogicCell(new LogicLiteral(kind, otherValue, scalarTypeName), other));
        }

        private static string DisplayDiscreteLiteral(LogicLiteral literal) => literal.Kind switch
        {
            LogicLiteralKind.String => Quote(literal.CanonicalValue),
            LogicLiteralKind.Symbol => $"symbol({Quote(literal.CanonicalValue)})",
            LogicLiteralKind.Enum => $"{literal.ScalarTypeName}.{literal.CanonicalValue}",
            _ => literal.CanonicalValue,
        };

        private static string Quote(string value)
        {
            var builder = new StringBuilder(value.Length + 2).Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default: builder.Append(character); break;
                }
            }

            return builder.Append('"').ToString();
        }

        private static LogicCell IntegerCell(BigInteger value, string display) => new(
            new LogicLiteral(LogicLiteralKind.Integer, value.ToString(CultureInfo.InvariantCulture)),
            display);

        private static LogicCell DecimalCell(ExactDecimal value, string display) => new(
            new LogicLiteral(LogicLiteralKind.Decimal, value.ToString()),
            display);
    }

    private sealed class LogicDomain
    {
        internal LogicDomain(LogicResource resource, ImmutableArray<LogicCell> cells)
        {
            Resource = resource;
            Cells = cells;
        }

        internal LogicResource Resource { get; }

        internal ImmutableArray<LogicCell> Cells { get; }
    }

    private sealed class LogicCell
    {
        internal LogicCell(LogicLiteral literal, string display)
        {
            Literal = literal;
            Display = display;
        }

        internal LogicLiteral Literal { get; }

        internal string Display { get; }
    }

    /// <summary>
    /// Per-decision canonical DAG and partial-evaluation cache. Stable structural
    /// keys hash-cons repeated sub-formulas without retaining compiler inputs in
    /// process-global state.
    /// </summary>
    private sealed class FormulaDag
    {
        private readonly Dictionary<string, DagNode> nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LogicTruthValue> evaluations = new(StringComparer.Ordinal);
        private readonly DagNode root;
        private int nextNodeId;

        internal FormulaDag(LogicFormula formula, CancellationToken cancellationToken)
        {
            root = Intern(formula, cancellationToken);
        }

        internal LogicTruthValue Evaluate(
            IReadOnlyDictionary<string, LogicCell> assignment,
            CancellationToken cancellationToken) =>
            Evaluate(root, assignment, cancellationToken);

        private DagNode Intern(LogicFormula formula, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nodes.TryGetValue(formula.StableKey, out var existing))
            {
                return existing;
            }

            var operands = formula.Operands
                .Select(operand => Intern(operand, cancellationToken))
                .ToImmutableArray();
            var resourceKeys = formula.Resource is null
                ? operands
                    .SelectMany(operand => operand.ResourceKeys)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToImmutableArray()
                : ImmutableArray.Create(formula.Resource.Key);
            var node = new DagNode(nextNodeId++, formula, operands, resourceKeys);
            nodes.Add(formula.StableKey, node);
            return node;
        }

        private LogicTruthValue Evaluate(
            DagNode node,
            IReadOnlyDictionary<string, LogicCell> assignment,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cacheKey = BuildEvaluationKey(node, assignment);
            if (evaluations.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            LogicTruthValue result;
            switch (node.Formula.Kind)
            {
                case LogicFormulaKind.False:
                    result = LogicTruthValue.False;
                    break;
                case LogicFormulaKind.True:
                    result = LogicTruthValue.True;
                    break;
                case LogicFormulaKind.Opaque:
                    result = LogicTruthValue.Unknown;
                    break;
                case LogicFormulaKind.BooleanAtom:
                    if (!assignment.TryGetValue(node.Formula.Resource!.Key, out var atomCell))
                    {
                        result = LogicTruthValue.Unknown;
                        break;
                    }

                    result = atomCell.Literal.Kind == LogicLiteralKind.Boolean
                        && string.Equals(atomCell.Literal.CanonicalValue, "true", StringComparison.Ordinal)
                            ? LogicTruthValue.True
                            : LogicTruthValue.False;
                    break;
                case LogicFormulaKind.Comparison:
                    if (!assignment.TryGetValue(node.Formula.Resource!.Key, out var comparisonCell))
                    {
                        result = LogicTruthValue.Unknown;
                        break;
                    }

                    result = EvaluateComparison(
                        comparisonCell.Literal,
                        node.Formula.ComparisonOperator,
                        node.Formula.Literal!)
                        ? LogicTruthValue.True
                        : LogicTruthValue.False;
                    break;
                case LogicFormulaKind.Not:
                    result = Evaluate(node.Operands[0], assignment, cancellationToken) switch
                    {
                        LogicTruthValue.True => LogicTruthValue.False,
                        LogicTruthValue.False => LogicTruthValue.True,
                        _ => LogicTruthValue.Unknown,
                    };
                    break;
                case LogicFormulaKind.All:
                    result = EvaluateAll(node.Operands, assignment, cancellationToken);
                    break;
                case LogicFormulaKind.Any:
                    result = EvaluateAny(node.Operands, assignment, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown logic formula kind '{node.Formula.Kind}'.");
            }

            evaluations[cacheKey] = result;
            return result;
        }

        private LogicTruthValue EvaluateAll(
            ImmutableArray<DagNode> operands,
            IReadOnlyDictionary<string, LogicCell> assignment,
            CancellationToken cancellationToken)
        {
            var sawUnknown = false;
            foreach (var operand in operands)
            {
                var value = Evaluate(operand, assignment, cancellationToken);
                if (value == LogicTruthValue.False)
                {
                    return LogicTruthValue.False;
                }

                sawUnknown |= value == LogicTruthValue.Unknown;
            }

            return sawUnknown ? LogicTruthValue.Unknown : LogicTruthValue.True;
        }

        private LogicTruthValue EvaluateAny(
            ImmutableArray<DagNode> operands,
            IReadOnlyDictionary<string, LogicCell> assignment,
            CancellationToken cancellationToken)
        {
            var sawUnknown = false;
            foreach (var operand in operands)
            {
                var value = Evaluate(operand, assignment, cancellationToken);
                if (value == LogicTruthValue.True)
                {
                    return LogicTruthValue.True;
                }

                sawUnknown |= value == LogicTruthValue.Unknown;
            }

            return sawUnknown ? LogicTruthValue.Unknown : LogicTruthValue.False;
        }

        private static string BuildEvaluationKey(
            DagNode node,
            IReadOnlyDictionary<string, LogicCell> assignment)
        {
            var builder = new StringBuilder()
                .Append(node.Id.ToString(CultureInfo.InvariantCulture))
                .Append('|');
            for (var index = 0; index < node.ResourceKeys.Length; index++)
            {
                var resourceKey = node.ResourceKeys[index];
                if (!assignment.TryGetValue(resourceKey, out var cell))
                {
                    continue;
                }

                builder.Append(index.ToString(CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(cell.Literal.StableKey.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(cell.Literal.StableKey)
                    .Append(';');
            }

            return builder.ToString();
        }

        private sealed class DagNode
        {
            internal DagNode(
                int id,
                LogicFormula formula,
                ImmutableArray<DagNode> operands,
                ImmutableArray<string> resourceKeys)
            {
                Id = id;
                Formula = formula;
                Operands = operands;
                ResourceKeys = resourceKeys;
            }

            internal int Id { get; }

            internal LogicFormula Formula { get; }

            internal ImmutableArray<DagNode> Operands { get; }

            internal ImmutableArray<string> ResourceKeys { get; }
        }
    }

    private readonly struct ExactDecimal : IComparable<ExactDecimal>, IEquatable<ExactDecimal>
    {
        private ExactDecimal(BigInteger coefficient, int scale)
        {
            while (scale > 0 && coefficient % 10 == 0)
            {
                coefficient /= 10;
                scale--;
            }

            Coefficient = coefficient;
            Scale = coefficient.IsZero ? 0 : scale;
        }

        internal static ExactDecimal Zero { get; } = new(BigInteger.Zero, 0);

        private BigInteger Coefficient { get; }

        private int Scale { get; }

        internal static ExactDecimal Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new FormatException("A decimal literal cannot be empty.");
            }

            var negative = false;
            var start = 0;
            if (value[0] is '-' or '+')
            {
                negative = value[0] == '-';
                start = 1;
            }

            if (start == value.Length)
            {
                throw new FormatException($"'{value}' is not an exact base-10 decimal.");
            }

            var dot = value.IndexOf('.', start);
            var whole = dot < 0
                ? value.Substring(start)
                : value.Substring(start, dot - start);
            var fraction = dot < 0 ? string.Empty : value.Substring(dot + 1);
            if (whole.Length == 0
                || (dot >= 0 && fraction.Length == 0)
                || !whole.All(character => character is >= '0' and <= '9')
                || !fraction.All(character => character is >= '0' and <= '9'))
            {
                throw new FormatException($"'{value}' is not an exact base-10 decimal.");
            }

            var digits = whole + fraction;
            var coefficient = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
            return new ExactDecimal(negative ? -coefficient : coefficient, fraction.Length);
        }

        internal ExactDecimal AddOne() => Add(new ExactDecimal(BigInteger.One, 0));

        internal ExactDecimal SubtractOne() => Add(new ExactDecimal(-BigInteger.One, 0));

        internal static ExactDecimal Midpoint(ExactDecimal left, ExactDecimal right)
        {
            var scale = Math.Max(left.Scale, right.Scale);
            var sum = ScaleTo(left, scale) + ScaleTo(right, scale);
            return new ExactDecimal(sum * 5, checked(scale + 1));
        }

        public int CompareTo(ExactDecimal other)
        {
            var scale = Math.Max(Scale, other.Scale);
            return ScaleTo(this, scale).CompareTo(ScaleTo(other, scale));
        }

        public bool Equals(ExactDecimal other) => CompareTo(other) == 0;

        public override bool Equals(object? obj) => obj is ExactDecimal other && Equals(other);

        public override int GetHashCode() => Coefficient.GetHashCode() ^ Scale;

        public override string ToString()
        {
            if (Scale == 0)
            {
                return Coefficient.ToString(CultureInfo.InvariantCulture);
            }

            var negative = Coefficient.Sign < 0;
            var digits = BigInteger.Abs(Coefficient).ToString(CultureInfo.InvariantCulture);
            if (digits.Length <= Scale)
            {
                digits = new string('0', Scale - digits.Length + 1) + digits;
            }

            var split = digits.Length - Scale;
            return (negative ? "-" : string.Empty)
                + digits.Substring(0, split)
                + "."
                + digits.Substring(split);
        }

        private ExactDecimal Add(ExactDecimal other)
        {
            var scale = Math.Max(Scale, other.Scale);
            return new ExactDecimal(ScaleTo(this, scale) + ScaleTo(other, scale), scale);
        }

        private static BigInteger ScaleTo(ExactDecimal value, int scale) =>
            value.Coefficient * BigInteger.Pow(10, scale - value.Scale);
    }
}

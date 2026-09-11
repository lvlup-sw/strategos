// -----------------------------------------------------------------------
// <copyright file="PredicateExpression.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Strategos.Ontology.ActionLogic;

namespace Strategos.Generators.Proof;

/// <summary>
/// Renders the canonical display projection a wire requirement or guarantee
/// carries in its <c>expression</c> member.
/// </summary>
/// <remarks>
/// <para>
/// The wire declares this field as a display projection consumers never parse,
/// and that is exactly how it is treated here: nothing reads it back. It exists
/// so a human reading a catalog, a diff, or a diagnostic sees the predicate
/// rather than a tree of tagged objects.
/// </para>
/// <para>
/// The format matches <c>Ontology/decorators.mjs</c>'s <c>predicateExpression</c>
/// and the runtime's <c>PredicateLiteral.Quote</c>, including its deliberately
/// narrow escape set. Two arms rendering the same predicate differently would
/// make a catalog diff read as a change when nothing changed.
/// </para>
/// </remarks>
internal static class PredicateExpression
{
    /// <summary>Renders a formula as its canonical display expression.</summary>
    /// <param name="formula">The formula to render.</param>
    /// <returns>The display projection.</returns>
    internal static string Render(LogicFormula formula)
    {
        if (formula is null)
        {
            throw new ArgumentNullException(nameof(formula));
        }

        return formula.Kind switch
        {
            LogicFormulaKind.True => "true",
            LogicFormulaKind.False => "false",
            LogicFormulaKind.BooleanAtom => formula.Resource!.Key,
            LogicFormulaKind.Comparison =>
                $"{formula.Resource!.Key} {OperatorSymbol(formula.ComparisonOperator)} {RenderLiteral(formula.Literal!)}",
            LogicFormulaKind.Not => $"!({Render(formula.Operands[0])})",
            LogicFormulaKind.All => Join(formula.Operands, " && "),
            LogicFormulaKind.Any => Join(formula.Operands, " || "),
            LogicFormulaKind.Opaque => $"custom({Quote(formula.OpaqueKey!)})",
            _ => throw new InvalidOperationException(
                $"Unhandled predicate kind '{formula.Kind}' in the expression renderer."),
        };
    }

    private static string Join(IEnumerable<LogicFormula> operands, string separator) =>
        "(" + string.Join(separator, operands.Select(Render)) + ")";

    private static string RenderLiteral(LogicLiteral literal) => literal.Kind switch
    {
        LogicLiteralKind.Null => "null",
        LogicLiteralKind.Boolean => literal.CanonicalValue,
        LogicLiteralKind.Integer or LogicLiteralKind.Decimal => literal.CanonicalValue,
        LogicLiteralKind.String => Quote(literal.CanonicalValue),
        LogicLiteralKind.Enum => $"{literal.ScalarTypeName}.{literal.CanonicalValue}",
        LogicLiteralKind.Symbol => $"symbol({Quote(literal.CanonicalValue)})",
        _ => throw new InvalidOperationException(
            $"Unhandled literal kind '{literal.Kind}' in the expression renderer."),
    };

    private static string OperatorSymbol(LogicComparisonOperator comparison) => comparison switch
    {
        LogicComparisonOperator.Equal => "==",
        LogicComparisonOperator.NotEqual => "!=",
        LogicComparisonOperator.LessThan => "<",
        LogicComparisonOperator.LessThanOrEqual => "<=",
        LogicComparisonOperator.GreaterThan => ">",
        LogicComparisonOperator.GreaterThanOrEqual => ">=",
        _ => throw new InvalidOperationException(
            $"Unhandled comparison operator '{comparison}' in the expression renderer."),
    };

    /// <summary>
    /// Quotes a value with the five escapes the canonical display format defines.
    /// </summary>
    /// <param name="value">The value to quote.</param>
    /// <returns>The quoted value.</returns>
    /// <remarks>
    /// Deliberately narrower than JSON string escaping. This is a display
    /// projection, not a transport encoding — widening it here would silently
    /// diverge from the two other arms that render the same format.
    /// </remarks>
    private static string Quote(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default: builder.Append(c); break;
            }
        }

        return builder.Append('"').ToString();
    }
}

// -----------------------------------------------------------------------
// <copyright file="ProofCatalogProjection.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Strategos.Ontology.ActionLogic;

namespace Strategos.Generators.Proof;

/// <summary>
/// Decides what a contract can say across an assembly boundary, and rebuilds the
/// read-set bookkeeping a contract that crossed one needs (#204).
/// </summary>
/// <remarks>
/// One rule, in one place. The writer's job is to be total over what this admits;
/// a formula kind added to the algebra and not to this gate stops the export
/// rather than being written as something it is not.
/// </remarks>
internal static class ProofCatalogProjection
{
    /// <summary>
    /// Describes why a formula cannot cross an assembly boundary, or returns
    /// <see langword="null"/> when it can.
    /// </summary>
    /// <param name="formula">The formula to classify.</param>
    /// <returns>The reason, or <see langword="null"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Opaque terms are refused.</b> An opaque contract is unprovable wherever it
    /// is read, so exporting one would let every consumer import a contract that can
    /// only ever produce "cannot be proved". Refusing at the declaring assembly puts
    /// the diagnostic in front of the author who can fix it, once, instead of in
    /// front of every consumer who cannot.
    /// </para>
    /// <para>
    /// <b>Boolean atoms are refused.</b> A link or relation predicate is a boolean
    /// atom whose atom key and read set are DIFFERENT strings — a relation atom keyed
    /// <c>relation|relation:...</c> reads <c>link|X</c> — and the wire's property
    /// reference has one name and no read set. Writing it as a comparison would
    /// silently drop the read set, and the frame check reads exactly that. A contract
    /// that lost it would be proved against a frame it never declared.
    /// </para>
    /// </remarks>
    internal static string? DescribeUnexportable(LogicFormula formula)
    {
        switch (formula.Kind)
        {
            case LogicFormulaKind.True:
            case LogicFormulaKind.False:
            case LogicFormulaKind.Comparison:
                return null;

            case LogicFormulaKind.Opaque:
                return $"its predicate '{formula.OpaqueKey}' is opaque, and an opaque contract "
                    + "cannot be proved by any compilation that reads it";

            case LogicFormulaKind.BooleanAtom:
                return $"its predicate '{formula.Resource!.Key}' is a link or relation atom, "
                    + "whose declared read set the portable contract has no slot for";

            case LogicFormulaKind.Not:
            case LogicFormulaKind.All:
            case LogicFormulaKind.Any:
                foreach (var operand in formula.Operands)
                {
                    if (DescribeUnexportable(operand) is { } reason)
                    {
                        return reason;
                    }
                }

                return null;

            default:
                return $"its predicate uses kind '{formula.Kind}', which the portable contract "
                    + "does not carry";
        }
    }

    /// <summary>
    /// Rebuilds the atom read set for a formula that arrived from a catalog.
    /// </summary>
    /// <param name="formula">The imported formula.</param>
    /// <param name="customReads">Read sets carried by custom predicates, by evaluator key.</param>
    /// <returns>The atom-to-reads map the proof's frame check consumes.</returns>
    /// <remarks>
    /// Not bookkeeping: <c>AtomReads</c> is what the frame check projects a guarantee
    /// through. An imported contract with an empty map is proved against an empty
    /// frame, which refutes every action that changes anything — the exact silent
    /// wrongness this whole path exists to avoid. A comparison's atom reads the
    /// resource it compares, which is the same rule the local parser applies when it
    /// builds one.
    /// </remarks>
    internal static ImmutableDictionary<string, ImmutableArray<string>> RebuildAtomReads(
        LogicFormula formula,
        IReadOnlyDictionary<string, ImmutableArray<string>> customReads)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(
            StringComparer.Ordinal);
        Collect(formula, customReads, builder);
        return builder.ToImmutable();
    }

    private static void Collect(
        LogicFormula formula,
        IReadOnlyDictionary<string, ImmutableArray<string>> customReads,
        ImmutableDictionary<string, ImmutableArray<string>>.Builder builder)
    {
        switch (formula.Kind)
        {
            case LogicFormulaKind.Comparison:
            case LogicFormulaKind.BooleanAtom:
                builder[formula.Resource!.Key] = ImmutableArray.Create(formula.Resource!.Key);
                return;

            case LogicFormulaKind.Opaque:
                builder[formula.OpaqueKey!] = customReads.TryGetValue(formula.OpaqueKey!, out var reads)
                    ? reads
                    : ImmutableArray<string>.Empty;
                return;

            case LogicFormulaKind.Not:
            case LogicFormulaKind.All:
            case LogicFormulaKind.Any:
                foreach (var operand in formula.Operands)
                {
                    Collect(operand, customReads, builder);
                }

                return;

            default:
                return;
        }
    }
}

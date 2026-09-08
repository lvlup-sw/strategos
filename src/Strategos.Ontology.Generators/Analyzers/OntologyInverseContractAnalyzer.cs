// -----------------------------------------------------------------------
// <copyright file="OntologyInverseContractAnalyzer.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using Strategos.Analyzers.Proof;
using Strategos.Ontology.ActionLogic;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Analyzers;

/// <summary>
/// Proves authored ontology compensation edges against their mechanically
/// derived inverse contracts.
/// </summary>
internal static class OntologyInverseContractAnalyzer
{
    internal static void Analyze(CompilationAnalysisContext context)
    {
        var catalog = OntologyActionCatalog.Build(
            context.Compilation,
            context.CancellationToken);

        foreach (var forward in catalog.Actions
            .Where(static action => action.CompensatingActionName is not null)
            .OrderBy(static action => action.Identity.DomainName, StringComparer.Ordinal)
            .ThenBy(static action => action.Identity.ObjectTypeName, StringComparer.Ordinal)
            .ThenBy(static action => action.Identity.ActionName, StringComparer.Ordinal)
            .ThenBy(static action => action.Location.SourceSpan.Start))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Dynamic or indirect authoring is owned by AONT220/AONT221. AONT216
            // is emitted only when the complete inverse obligation is statically
            // visible; this preserves the analyzer's no-false-positive contract.
            if (forward.InvalidReason is not null)
            {
                continue;
            }

            var inverseIdentity = new ActionIdentity(
                forward.Identity.DomainName,
                forward.Identity.ObjectTypeName,
                forward.CompensatingActionName!);
            var matches = catalog.Resolve(inverseIdentity);
            if (matches.Length != 1)
            {
                Report(
                    context,
                    forward,
                    $"the inverse identity resolves to {matches.Length.ToString(CultureInfo.InvariantCulture)} action declarations");
                continue;
            }

            var inverse = matches[0];
            if (inverse.InvalidReason is not null)
            {
                Report(context, forward, $"the authored inverse is not statically closed: {inverse.InvalidReason}");
                continue;
            }

            if (!TryProveContract(
                    forward,
                    context.CancellationToken,
                    out var forwardProof,
                    out var proofFailure))
            {
                Report(context, forward, $"the forward contract {proofFailure}");
                continue;
            }

            if (!TryProveContract(
                    inverse,
                    context.CancellationToken,
                    out var inverseProof,
                    out proofFailure))
            {
                Report(context, forward, $"the authored inverse contract {proofFailure}");
                continue;
            }

            var disagreement = FindDisagreement(
                catalog,
                forward,
                forwardProof,
                inverse,
                inverseProof,
                context.CancellationToken);
            if (disagreement is not null)
            {
                Report(context, forward, disagreement);
            }
        }
    }

    private static string? FindDisagreement(
        OntologyActionCatalog catalog,
        OntologyActionContract forward,
        StaticContractProof forwardProof,
        OntologyActionContract inverse,
        StaticContractProof inverseProof,
        System.Threading.CancellationToken cancellationToken)
    {
        if (!forward.Frame.ToImmutableHashSet(StringComparer.Ordinal).SetEquals(inverse.Frame))
        {
            return "the authored inverse frame differs from the forward frame";
        }

        var authorityFailure = FindAuthorityDisagreement(catalog, forward, inverse);
        if (authorityFailure is not null)
        {
            return authorityFailure;
        }

        var requirementFailure = FindFormulaInequivalence(
            inverseProof.Requirement,
            forwardProof.EffectiveGuarantee,
            "authored inverse requirement",
            "forward effective guarantee",
            cancellationToken);
        if (requirementFailure is not null)
        {
            return requirementFailure;
        }

        return FindFormulaInequivalence(
            inverseProof.EffectiveGuarantee,
            forwardProof.Requirement,
            "authored inverse effective guarantee",
            "forward requirement",
            cancellationToken);
    }

    private static string? FindAuthorityDisagreement(
        OntologyActionCatalog catalog,
        OntologyActionContract forward,
        OntologyActionContract inverse)
    {
        if (forward.RequiredAuthority is null && inverse.RequiredAuthority is null)
        {
            return null;
        }

        var lattices = catalog.ResolveLattice(forward.Identity.DomainName);
        if (lattices.Length != 1)
        {
            return $"the subject domain resolves to {lattices.Length.ToString(CultureInfo.InvariantCulture)} authority lattices";
        }

        var lattice = lattices[0];
        if (!lattice.TryJoinAtMost(
                [forward.RequiredAuthority],
                inverse.RequiredAuthority,
                out var forwardAtMostInverse,
                out var reason))
        {
            return reason ?? "forward-to-inverse authority equivalence could not be resolved";
        }

        if (!lattice.TryJoinAtMost(
                [inverse.RequiredAuthority],
                forward.RequiredAuthority,
                out var inverseAtMostForward,
                out reason))
        {
            return reason ?? "inverse-to-forward authority equivalence could not be resolved";
        }

        return forwardAtMostInverse && inverseAtMostForward
            ? null
            : $"required authority differs semantically (forward={forward.RequiredAuthority ?? "<none>"}, inverse={inverse.RequiredAuthority ?? "<none>"})";
    }

    private static string? FindFormulaInequivalence(
        LogicFormula left,
        LogicFormula right,
        string leftName,
        string rightName,
        System.Threading.CancellationToken cancellationToken)
    {
        var leftToRight = FiniteDomainSolver.Implies(left, right, cancellationToken);
        if (leftToRight.Kind != LogicDecisionKind.Unsatisfiable)
        {
            return leftToRight.Kind == LogicDecisionKind.Satisfiable
                ? $"{leftName} does not imply {rightName}; counterexample: {FormatWitness(leftToRight)}"
                : $"{leftName} -> {rightName} could not be proved: {leftToRight.Reason ?? "unknown solver result"}";
        }

        var rightToLeft = FiniteDomainSolver.Implies(right, left, cancellationToken);
        if (rightToLeft.Kind != LogicDecisionKind.Unsatisfiable)
        {
            return rightToLeft.Kind == LogicDecisionKind.Satisfiable
                ? $"{rightName} does not imply {leftName}; counterexample: {FormatWitness(rightToLeft)}"
                : $"{rightName} -> {leftName} could not be proved: {rightToLeft.Reason ?? "unknown solver result"}";
        }

        return null;
    }

    private static bool TryProveContract(
        OntologyActionContract action,
        System.Threading.CancellationToken cancellationToken,
        out StaticContractProof proof,
        out string? failureReason)
    {
        cancellationToken.ThrowIfCancellationRequested();
        failureReason = action.Requirement.InvalidReason ?? action.Guarantee.InvalidReason;
        if (failureReason is not null)
        {
            proof = null!;
            return false;
        }

        if (!FiniteDomainSolver.TryValidateResourceDomains(
                [action.Requirement.Formula, action.Guarantee.Formula],
                out failureReason,
                cancellationToken))
        {
            proof = null!;
            failureReason ??= "uses inconsistent predicate resource domains";
            return false;
        }

        var opaqueKeys = action.Requirement.OpaqueKeys
            .Concat(action.Guarantee.OpaqueKeys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToImmutableArray();
        if (!opaqueKeys.IsEmpty)
        {
            proof = null!;
            failureReason = "contains opaque custom predicate(s): " + string.Join(", ", opaqueKeys);
            return false;
        }

        var requirementDecision = FiniteDomainSolver.IsSatisfiable(
            action.Requirement.Formula,
            cancellationToken);
        if (requirementDecision.Kind != LogicDecisionKind.Satisfiable)
        {
            proof = null!;
            failureReason = requirementDecision.Kind == LogicDecisionKind.Unsatisfiable
                ? "has contradictory hard requirements"
                : requirementDecision.Reason ?? "has invalid hard requirements";
            return false;
        }

        var guaranteeDecision = FiniteDomainSolver.IsSatisfiable(
            action.Guarantee.Formula,
            cancellationToken);
        if (guaranteeDecision.Kind != LogicDecisionKind.Satisfiable)
        {
            proof = null!;
            failureReason = guaranteeDecision.Kind == LogicDecisionKind.Unsatisfiable
                ? "has contradictory guarantees"
                : guaranteeDecision.Reason ?? "has invalid guarantees";
            return false;
        }

        var frame = action.Frame.ToImmutableHashSet(StringComparer.Ordinal);
        var writtenResources = action.Requirement.AtomReads
            .Concat(action.Guarantee.AtomReads)
            .Where(pair => pair.Value.Any(frame.Contains))
            .Select(static pair => pair.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToImmutableArray();
        if (!FiniteDomainSolver.TryForget(
                action.Guarantee.Formula,
                writtenResources,
                out var realizableGuarantee,
                out failureReason,
                cancellationToken))
        {
            proof = null!;
            failureReason ??= "could not project its guarantee through the declared frame";
            return false;
        }

        var frameDecision = FiniteDomainSolver.Implies(
            action.Requirement.Formula,
            realizableGuarantee,
            cancellationToken);
        if (frameDecision.Kind != LogicDecisionKind.Unsatisfiable)
        {
            proof = null!;
            failureReason = frameDecision.Kind == LogicDecisionKind.Satisfiable
                ? "has an unrealizable frame; counterexample: " + FormatWitness(frameDecision)
                : frameDecision.Reason ?? "has a frame that could not be classified";
            return false;
        }

        if (!FiniteDomainSolver.TryForget(
                action.Requirement.Formula,
                writtenResources,
                out var preservedRequirement,
                out failureReason,
                cancellationToken))
        {
            proof = null!;
            failureReason ??= "could not project its requirements through the declared frame";
            return false;
        }

        proof = new StaticContractProof(
            action.Requirement.Formula,
            LogicFormula.All(action.Guarantee.Formula, preservedRequirement));
        failureReason = null;
        return true;
    }

    private static string FormatWitness(LogicDecision decision) => decision.Witness.IsEmpty
        ? "<none>"
        : string.Join(
            ", ",
            decision.Witness.Select(static fact => fact.Key + "=" + fact.Value));

    private static void Report(
        CompilationAnalysisContext context,
        OntologyActionContract forward,
        string reason) => context.ReportDiagnostic(Diagnostic.Create(
            OntologyDiagnostics.CompensationDisagreesWithInverse,
            forward.Location,
            forward.Identity.ActionName,
            forward.CompensatingActionName!,
            reason));

    private sealed class StaticContractProof
    {
        internal StaticContractProof(
            LogicFormula requirement,
            LogicFormula effectiveGuarantee)
        {
            Requirement = requirement;
            EffectiveGuarantee = effectiveGuarantee;
        }

        internal LogicFormula Requirement { get; }

        internal LogicFormula EffectiveGuarantee { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Strategos.Ontology.ActionLogic;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Analyzers;

/// <summary>
/// Performs exact build-time checks for statically visible typed action
/// sequences. The parser intentionally recognizes a closed construction
/// vocabulary; unresolved helpers remain a runtime concern (AONT220).
/// </summary>
internal static class ActionCompositionAnalyzer
{
    private const string DescriptorNamespace = "Strategos.Ontology.Descriptors";
    private const string NoSubjectBearingOperandReason = "no subject-bearing operand was found";

    internal static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        var symbol = symbolInfo.Symbol as IMethodSymbol
            ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (symbol is null)
        {
            return;
        }

        if (IsActionCalculusSequential(symbol))
        {
            if (!IsNestedSequential(invocation, context.SemanticModel, context.CancellationToken))
            {
                AnalyzeSequence(context, invocation);
            }

            return;
        }

        if (IsExpressionPredicateBuilderMethod(symbol))
        {
            AnalyzeExpressionPredicate(context, invocation);
        }
    }

    private static void AnalyzeSequence(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        if (!TryResolveSequence(
                invocation,
                context.SemanticModel,
                context.CancellationToken,
                new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                out var sequence,
                out var unresolvedReason))
        {
            if (string.Equals(
                    unresolvedReason,
                    NoSubjectBearingOperandReason,
                    StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.InvalidActionContract,
                    invocation.GetLocation(),
                    "a sequential composition requires an action, nested composite, or explicit identity operand"));
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.DynamicActionSequence,
                invocation.GetLocation(),
                unresolvedReason ?? "an operand is not statically immutable"));
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.ActionComposabilityCoverage,
                invocation.GetLocation(),
                "composable=0, opaque=0, vacuous=0, invalid=0, statically-unresolved=1"));
            return;
        }

        foreach (var mismatch in sequence.IdentitySubjectMismatches)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.InvalidActionContract,
                invocation.GetLocation(),
                mismatch));
        }

        if (sequence.Actions.Count == 0)
        {
            return;
        }

        var requiredAuthorities = sequence.Actions
            .Select(action => action.RequiredAuthority)
            .Where(authority => authority is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(authority => authority, StringComparer.Ordinal)
            .ToArray();
        if (requiredAuthorities.Length != 0)
        {
            var invocationOperation = context.SemanticModel.GetOperation(
                invocation,
                context.CancellationToken) as IInvocationOperation;
            var latticeExpression = FindArgumentExpression(
                invocation,
                invocationOperation,
                "authorityLattice");
            HashSet<string>? authorityNames = null;
            string? latticeFailure = null;
            var latticeStatus = latticeExpression is null
                ? StaticParseKind.Dynamic
                : TryParseAuthorityNames(
                    latticeExpression,
                    context.SemanticModel,
                    context.CancellationToken,
                    new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                    out authorityNames,
                    out latticeFailure);
            if (latticeStatus == StaticParseKind.Dynamic)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.DynamicActionSequence,
                    invocation.GetLocation(),
                    "the authority lattice is not statically constructible"));
            }
            else if (latticeStatus == StaticParseKind.Invalid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.InvalidActionContract,
                    invocation.GetLocation(),
                    latticeFailure ?? "the authority lattice is invalid"));
            }
            else
            {
                foreach (var missing in requiredAuthorities.Where(authority => !authorityNames!.Contains(authority)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        OntologyDiagnostics.InvalidActionContract,
                        invocation.GetLocation(),
                        $"required authority '{missing}' is absent from the authority lattice"));
                }
            }
        }

        var composable = 0;
        var opaque = 0;
        var vacuous = 0;
        var invalid = 0;
        var proofs = new List<StaticActionProof>();
        foreach (var action in sequence.Actions)
        {
            if (!string.Equals(action.DomainName, sequence.DomainName, StringComparison.Ordinal)
                || !string.Equals(action.ObjectTypeName, sequence.ObjectTypeName, StringComparison.Ordinal))
            {
                invalid++;
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.InvalidActionContract,
                    action.Location,
                    $"subject '{action.DomainName}/{action.ObjectTypeName}' does not match "
                    + $"'{sequence.DomainName}/{sequence.ObjectTypeName}'"));
                proofs.Add(StaticActionProof.Invalid(action, "subject mismatch"));
                continue;
            }

            var proof = ProveAction(action, context.CancellationToken);
            proofs.Add(proof);
            switch (proof.Kind)
            {
                case StaticProofKind.Invalid:
                    invalid++;
                    context.ReportDiagnostic(Diagnostic.Create(
                        OntologyDiagnostics.InvalidActionContract,
                        action.Location,
                        $"action '{action.DomainName}/{action.ObjectTypeName}/{action.Name}': {proof.Reason}"));
                    break;
                case StaticProofKind.Opaque:
                    opaque++;
                    context.ReportDiagnostic(Diagnostic.Create(
                        OntologyDiagnostics.OpaqueActionContract,
                        action.Location,
                        action.DomainName + "/" + action.ObjectTypeName,
                        action.Name,
                        string.Join(", ", proof.OpaqueKeys)));
                    break;
                case StaticProofKind.Closed when !proof.HasNontrivialEffectiveGuarantee:
                    vacuous++;
                    break;
                case StaticProofKind.Closed:
                    composable++;
                    break;
            }
        }

        for (var index = 0; index + 1 < proofs.Count; index++)
        {
            var upstream = proofs[index];
            var downstream = proofs[index + 1];
            if (upstream.Kind != StaticProofKind.Closed || downstream.Kind != StaticProofKind.Closed)
            {
                continue;
            }

            var decision = FiniteDomainSolver.Implies(
                upstream.EffectiveGuarantee,
                downstream.Requirement,
                context.CancellationToken);
            if (decision.Kind == LogicDecisionKind.Unsatisfiable)
            {
                continue;
            }

            if (decision.Kind != LogicDecisionKind.Satisfiable)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.InvalidActionContract,
                    invocation.GetLocation(),
                    $"seam '{upstream.Action.Name}' to '{downstream.Action.Name}': "
                    + (decision.Reason ?? "the seam could not be decided")));
                continue;
            }

            var witness = decision.Witness.IsEmpty
                ? "<empty state>"
                : string.Join(", ", decision.Witness.Select(pair => pair.Key + "=" + pair.Value));
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.IllegalActionSeam,
                invocation.GetLocation(),
                upstream.Action.DomainName + "/" + upstream.Action.ObjectTypeName,
                upstream.Action.Name,
                downstream.Action.DomainName + "/" + downstream.Action.ObjectTypeName,
                downstream.Action.Name,
                witness));
        }

        var total = sequence.Actions.Count;
        var percentage = composable * 100.0 / total;
        context.ReportDiagnostic(Diagnostic.Create(
            OntologyDiagnostics.ActionComposabilityCoverage,
            invocation.GetLocation(),
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1} ({2:0.##}%): composable={0}, opaque={3}, vacuous={4}, invalid={5}, statically-unresolved=0",
                composable,
                total,
                percentage,
                opaque,
                vacuous,
                invalid)));
    }

    private static StaticActionProof ProveAction(StaticAction action, System.Threading.CancellationToken cancellationToken)
    {
        if (action.InvalidReason is not null)
        {
            return StaticActionProof.Invalid(action, action.InvalidReason);
        }

        if (!FiniteDomainSolver.TryValidateResourceDomains(
                new[] { action.Requirement.Formula, action.Guarantee.Formula },
                out var domainFailure,
                cancellationToken))
        {
            return StaticActionProof.Invalid(
                action,
                domainFailure ?? "the action uses inconsistent predicate resource domains");
        }

        var writtenProofResources = action.Requirement.AtomReads
            .Concat(action.Guarantee.AtomReads)
            .Where(pair => pair.Value.Overlaps(action.Frame))
            .Select(pair => pair.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        if (action.OpaqueKeys.Count != 0)
        {
            var abstractRequirement = FiniteDomainSolver.IsSatisfiable(
                FiniteDomainSolver.AbstractOpaqueTerms(action.Requirement.Formula, cancellationToken),
                cancellationToken);
            if (abstractRequirement.Kind == LogicDecisionKind.Unsatisfiable)
            {
                return StaticActionProof.Invalid(
                    action,
                    "hard requirements are contradictory independently of their custom predicates");
            }

            if (abstractRequirement.Kind != LogicDecisionKind.Satisfiable)
            {
                return StaticActionProof.Invalid(
                    action,
                    abstractRequirement.Reason ?? "hard requirements are invalid");
            }

            var abstractGuarantee = FiniteDomainSolver.IsSatisfiable(
                FiniteDomainSolver.AbstractOpaqueTerms(action.Guarantee.Formula, cancellationToken),
                cancellationToken);
            if (abstractGuarantee.Kind == LogicDecisionKind.Unsatisfiable)
            {
                return StaticActionProof.Invalid(
                    action,
                    "guarantees are contradictory independently of their custom predicates");
            }

            if (abstractGuarantee.Kind != LogicDecisionKind.Satisfiable)
            {
                return StaticActionProof.Invalid(
                    action,
                    abstractGuarantee.Reason ?? "guarantees are invalid");
            }

            if (!FiniteDomainSolver.TryFindDefiniteFrameViolation(
                    action.Requirement.Formula,
                    action.Guarantee.Formula,
                    writtenProofResources,
                    out var opaqueFrameDecision,
                    out var opaqueProjectionFailure,
                    cancellationToken))
            {
                return StaticActionProof.Invalid(
                    action,
                    opaqueProjectionFailure ?? "opaque guarantee frame could not be projected");
            }

            if (opaqueFrameDecision.Kind == LogicDecisionKind.Satisfiable)
            {
                return StaticActionProof.Invalid(
                    action,
                    "guarantee constrains untouched state beyond the requirements independently "
                    + "of custom predicates");
            }

            return StaticActionProof.Opaque(action);
        }

        var requirementDecision = FiniteDomainSolver.IsSatisfiable(action.Requirement.Formula, cancellationToken);
        if (requirementDecision.Kind != LogicDecisionKind.Satisfiable)
        {
            return StaticActionProof.Invalid(
                action,
                requirementDecision.Kind == LogicDecisionKind.Unsatisfiable
                    ? "hard requirements are contradictory"
                    : requirementDecision.Reason ?? "hard requirements are invalid");
        }

        var guaranteeDecision = FiniteDomainSolver.IsSatisfiable(action.Guarantee.Formula, cancellationToken);
        if (guaranteeDecision.Kind != LogicDecisionKind.Satisfiable)
        {
            return StaticActionProof.Invalid(
                action,
                guaranteeDecision.Kind == LogicDecisionKind.Unsatisfiable
                    ? "guarantees are contradictory"
                    : guaranteeDecision.Reason ?? "guarantees are invalid");
        }

        if (!FiniteDomainSolver.TryForget(
                action.Guarantee.Formula,
                writtenProofResources,
                out var realizableGuarantee,
                out var reason,
                cancellationToken))
        {
            return StaticActionProof.Invalid(action, reason ?? "guarantee projection failed");
        }

        var frameDecision = FiniteDomainSolver.Implies(
            action.Requirement.Formula,
            realizableGuarantee,
            cancellationToken);
        if (frameDecision.Kind != LogicDecisionKind.Unsatisfiable)
        {
            return StaticActionProof.Invalid(
                action,
                frameDecision.Kind == LogicDecisionKind.Satisfiable
                    ? "guarantee constrains untouched state beyond the requirements"
                    : frameDecision.Reason ?? "frame is unrealizable");
        }

        if (!FiniteDomainSolver.TryForget(
                action.Requirement.Formula,
                writtenProofResources,
                out var preservedRequirement,
                out reason,
                cancellationToken))
        {
            return StaticActionProof.Invalid(action, reason ?? "requirement projection failed");
        }

        var effectiveGuarantee = LogicFormula.All(action.Guarantee.Formula, preservedRequirement);
        var negatedGuarantee = FiniteDomainSolver.IsSatisfiable(
            LogicFormula.Not(effectiveGuarantee),
            cancellationToken);
        if (negatedGuarantee.Kind is not LogicDecisionKind.Satisfiable
            and not LogicDecisionKind.Unsatisfiable)
        {
            return StaticActionProof.Invalid(
                action,
                negatedGuarantee.Reason ?? "effective guarantee could not be classified");
        }

        return StaticActionProof.Closed(
            action,
            action.Requirement.Formula,
            effectiveGuarantee,
            negatedGuarantee.Kind == LogicDecisionKind.Satisfiable);
    }

    private static bool TryResolveSequence(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out StaticSequence sequence,
        out string? unresolvedReason)
    {
        var operation = semanticModel.GetOperation(invocation, cancellationToken) as IInvocationOperation;
        if (operation is null || !IsActionCalculusSequential(operation.TargetMethod))
        {
            sequence = null!;
            unresolvedReason = "the ActionCalculus.Sequential call could not be resolved";
            return false;
        }

        var actions = new List<StaticAction>();
        string? domainName = null;
        string? objectTypeName = null;
        unresolvedReason = null;
        var identitySubjectMismatches = new List<string>();
        for (var argumentIndex = 0; argumentIndex < invocation.ArgumentList.Arguments.Count; argumentIndex++)
        {
            var argumentSyntax = invocation.ArgumentList.Arguments[argumentIndex];
            var argumentOperation = semanticModel.GetOperation(argumentSyntax, cancellationToken)
                as IArgumentOperation;
            var isOperand = string.Equals(
                    argumentOperation?.Parameter?.Name,
                    "operands",
                    StringComparison.Ordinal)
                || argumentOperation is null && argumentIndex > 0;
            if (!isOperand)
            {
                continue;
            }

            if (!TryResolveOperand(
                    argumentSyntax.Expression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var resolved,
                    out unresolvedReason))
            {
                sequence = null!;
                unresolvedReason ??= "an action operand is dynamic";
                return false;
            }

            identitySubjectMismatches.AddRange(resolved.IdentitySubjectMismatches);
            if (!MergeSubject(resolved, ref domainName, ref objectTypeName)
                && resolved.Actions.Count == 0)
            {
                identitySubjectMismatches.Add(
                    $"identity subject '{resolved.DomainName}/{resolved.ObjectTypeName}' does not match "
                    + $"'{domainName}/{objectTypeName}'");
            }

            actions.AddRange(resolved.Actions);
        }

        if (domainName is null || objectTypeName is null)
        {
            sequence = null!;
            unresolvedReason = NoSubjectBearingOperandReason;
            return false;
        }

        sequence = new StaticSequence(
            domainName,
            objectTypeName,
            actions,
            identitySubjectMismatches);
        unresolvedReason = null;
        return true;
    }

    private static bool TryResolveOperand(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out StaticSequence sequence,
        out string? unresolvedReason)
    {
        expression = UnwrapParentheses(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            var local = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol as ILocalSymbol;
            if (local is not null && IsPotentiallyMutableCollection(local.Type))
            {
                sequence = null!;
                unresolvedReason = "a mutable collection local cannot be proven stable";
                return false;
            }

            return TryResolveOperand(
                initializer,
                semanticModel,
                cancellationToken,
                resolving,
                out sequence,
                out unresolvedReason);
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            if (method is not null && IsActionCalculusSequential(method))
            {
                return TryResolveSequence(
                    invocation,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out sequence,
                    out unresolvedReason);
            }

            if (IsType(method?.ContainingType, DescriptorNamespace + ".ActionCalculus")
                && method!.Name == "Identity")
            {
                var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                if (argument is not null
                    && TryParseSubject(argument, semanticModel, cancellationToken, resolving, out var subject))
                {
                    var validation = subject.InvalidReason is null
                        ? new List<string>()
                        : new List<string> { subject.InvalidReason };
                    sequence = new StaticSequence(
                        subject.DomainName,
                        subject.ObjectTypeName,
                        new List<StaticAction>(),
                        validation);
                    unresolvedReason = null;
                    return true;
                }
            }

            if (IsType(method?.ContainingType, DescriptorNamespace + ".ActionCompositionOperand")
                && method!.Name == "From"
                && invocation.ArgumentList.Arguments.Count == 1)
            {
                return TryResolveOperand(
                    invocation.ArgumentList.Arguments[0].Expression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out sequence,
                    out unresolvedReason);
            }
        }

        if (TryParseAction(
            expression,
            semanticModel,
            cancellationToken,
            resolving,
            out var action,
            out var actionFailure))
        {
            sequence = new StaticSequence(
                action.DomainName,
                action.ObjectTypeName,
                new List<StaticAction> { action },
                new List<string>());
            unresolvedReason = null;
            return true;
        }

        if (TryResolveItems(expression, semanticModel, cancellationToken, resolving, out var items))
        {
            var actions = new List<StaticAction>();
            var identitySubjectMismatches = new List<string>();
            string? domainName = null;
            string? objectTypeName = null;
            foreach (var item in items)
            {
                if (!TryResolveOperand(
                        item,
                        semanticModel,
                        cancellationToken,
                        resolving,
                        out var resolved,
                        out unresolvedReason))
                {
                    sequence = null!;
                    return false;
                }

                identitySubjectMismatches.AddRange(resolved.IdentitySubjectMismatches);
                if (!MergeSubject(resolved, ref domainName, ref objectTypeName)
                    && resolved.Actions.Count == 0)
                {
                    identitySubjectMismatches.Add(
                        $"identity subject '{resolved.DomainName}/{resolved.ObjectTypeName}' does not match "
                        + $"'{domainName}/{objectTypeName}'");
                }

                actions.AddRange(resolved.Actions);
            }

            if (domainName is not null && objectTypeName is not null)
            {
                sequence = new StaticSequence(
                    domainName,
                    objectTypeName,
                    actions,
                    identitySubjectMismatches);
                unresolvedReason = null;
                return true;
            }
        }

        sequence = null!;
        unresolvedReason = actionFailure
            ?? "an operand comes from a helper, mutable local, or non-static collection";
        return false;
    }

    private static bool TryParseAction(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out StaticAction action,
        out string? unresolvedReason)
    {
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParseAction(
                initializer,
                semanticModel,
                cancellationToken,
                resolving,
                out action,
                out unresolvedReason);
        }

        if (expression is not BaseObjectCreationExpressionSyntax creation
            || !IsType(
                semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                DescriptorNamespace + ".ActionDescriptor")
            || creation.ArgumentList is null
            || creation.ArgumentList.Arguments.Count < 2)
        {
            action = null!;
            unresolvedReason = null;
            return false;
        }

        var operation = semanticModel.GetOperation(creation, cancellationToken) as IObjectCreationOperation;
        var subjectExpression = FindArgumentExpression(
            creation.ArgumentList,
            operation,
            "subject",
            position: 0);
        var nameExpression = FindArgumentExpression(
            creation.ArgumentList,
            operation,
            "name",
            position: 1);
        if (subjectExpression is null
            || nameExpression is null
            || !TryParseSubject(
                subjectExpression,
                semanticModel,
                cancellationToken,
                resolving,
                out var subject))
        {
            action = null!;
            unresolvedReason = "an action subject or name is dynamic";
            return false;
        }

        var nameStatus = TryParseRequiredString(
            nameExpression,
            semanticModel,
            cancellationToken,
            out var name);
        if (nameStatus == StaticParseKind.Dynamic)
        {
            action = null!;
            unresolvedReason = "an action subject or name is dynamic";
            return false;
        }

        var identityFailure = subject.InvalidReason;
        if (nameStatus == StaticParseKind.Invalid)
        {
            name = "<invalid-action-name>";
            identityFailure ??= "an action name cannot be empty";
        }

        var hardRequirements = new List<StaticPredicate>();
        var guarantees = new List<StaticPredicate>();
        var frame = new HashSet<string>(StringComparer.Ordinal);
        var contractFacts = new List<StaticPredicate>();
        string? requiredAuthority = null;
        if (creation.Initializer is not null)
        {
            var preconditions = FindInitializerValue(creation.Initializer, "Preconditions");
            if (preconditions is not null
                && !TryParsePreconditions(
                    preconditions,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    hardRequirements))
            {
                action = null!;
                unresolvedReason = "an action precondition is dynamic or outside the typed predicate grammar";
                return false;
            }

            var ensures = FindInitializerValue(creation.Initializer, "Ensures");
            if (ensures is not null
                && !TryParseGuarantees(
                    ensures,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    guarantees))
            {
                action = null!;
                unresolvedReason = "an action guarantee is dynamic or outside the typed predicate grammar";
                return false;
            }

            var touched = FindInitializerValue(creation.Initializer, "TouchedResources");
            if (touched is not null
                && !TryParseFrame(
                    touched,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    frame,
                    contractFacts))
            {
                action = null!;
                unresolvedReason = "an action frame is dynamic";
                return false;
            }

            var postconditions = FindInitializerValue(creation.Initializer, "Postconditions");
            if (postconditions is not null
                && !TryParsePostconditions(
                    postconditions,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    frame,
                    contractFacts))
            {
                action = null!;
                unresolvedReason = "an action postcondition is dynamic";
                return false;
            }

            var requiredAuthorityExpression = FindInitializerValue(
                creation.Initializer,
                "RequiredAuthority");
            if (requiredAuthorityExpression is not null
                && !Unwrap(requiredAuthorityExpression).IsKind(SyntaxKind.NullLiteralExpression))
            {
                var authorityStatus = TryParseRequiredString(
                    requiredAuthorityExpression,
                    semanticModel,
                    cancellationToken,
                    out requiredAuthority);
                if (authorityStatus == StaticParseKind.Dynamic)
                {
                    action = null!;
                    unresolvedReason = "an action required authority is dynamic";
                    return false;
                }

                if (authorityStatus == StaticParseKind.Invalid)
                {
                    requiredAuthority = null;
                    identityFailure ??= "the required authority name cannot be empty";
                }
            }
        }

        guarantees.AddRange(contractFacts);
        var requirement = StaticPredicate.All(hardRequirements);
        var guarantee = StaticPredicate.All(guarantees);
        action = new StaticAction(
            subject.DomainName,
            subject.ObjectTypeName,
            name,
            requirement,
            guarantee,
            frame,
            requiredAuthority,
            requirement.OpaqueKeys.Concat(guarantee.OpaqueKeys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList(),
            identityFailure ?? requirement.InvalidReason ?? guarantee.InvalidReason,
            creation.GetLocation());
        unresolvedReason = null;
        return true;
    }

    private static bool TryParsePreconditions(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        ICollection<StaticPredicate> hard)
    {
        if (!TryResolveItems(expression, semanticModel, cancellationToken, resolving, out var items))
        {
            return false;
        }

        foreach (var item in items)
        {
            var candidate = Unwrap(item);
            if (candidate.IsKind(SyntaxKind.NullLiteralExpression))
            {
                hard.Add(StaticPredicate.Invalid("the precondition collection contains a null entry"));
                continue;
            }

            if (candidate is IdentifierNameSyntax identifier
                && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
            {
                candidate = initializer;
            }

            if (candidate is not BaseObjectCreationExpressionSyntax creation
                || !IsType(
                    semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                    DescriptorNamespace + ".ActionPrecondition")
                || FindArgumentExpression(
                    semanticModel.GetOperation(creation, cancellationToken) as IObjectCreationOperation,
                    "predicate") is not ExpressionSyntax predicateExpression
                || !TryParsePredicate(
                    predicateExpression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var predicate))
            {
                return false;
            }

            var strengthStatus = TryParseConstraintStrength(
                creation,
                semanticModel,
                cancellationToken,
                resolving,
                out var isSoft,
                out var strengthFailure);
            if (strengthStatus == StaticParseKind.Dynamic)
            {
                return false;
            }

            if (strengthStatus == StaticParseKind.Invalid)
            {
                hard.Add(StaticPredicate.Invalid(strengthFailure ?? "invalid constraint strength"));
            }
            else if (!isSoft || predicate.InvalidReason is not null)
            {
                hard.Add(predicate);
            }
        }

        return true;
    }

    private static bool TryParseGuarantees(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        ICollection<StaticPredicate> guarantees)
    {
        if (!TryResolveItems(expression, semanticModel, cancellationToken, resolving, out var items))
        {
            return false;
        }

        foreach (var item in items)
        {
            var candidate = Unwrap(item);
            if (candidate.IsKind(SyntaxKind.NullLiteralExpression))
            {
                guarantees.Add(StaticPredicate.Invalid("the guarantee collection contains a null entry"));
                continue;
            }

            if (candidate is IdentifierNameSyntax identifier
                && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
            {
                candidate = initializer;
            }

            if (candidate is not BaseObjectCreationExpressionSyntax creation
                || !IsType(
                    semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                    DescriptorNamespace + ".ActionGuarantee")
                || FindArgumentExpression(
                    semanticModel.GetOperation(creation, cancellationToken) as IObjectCreationOperation,
                    "predicate") is not ExpressionSyntax predicateExpression
                || !TryParsePredicate(
                    predicateExpression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var predicate))
            {
                return false;
            }

            guarantees.Add(predicate);
        }

        return true;
    }

    private static bool TryParseFrame(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        ISet<string> frame,
        ICollection<StaticPredicate> validation)
    {
        if (!TryResolveItems(expression, semanticModel, cancellationToken, resolving, out var items))
        {
            return false;
        }

        foreach (var item in items)
        {
            var candidate = Unwrap(item);
            if (candidate.IsKind(SyntaxKind.NullLiteralExpression))
            {
                validation.Add(StaticPredicate.Invalid("the action frame contains a null resource"));
                continue;
            }

            var status = TryParseActionResource(
                candidate,
                semanticModel,
                cancellationToken,
                resolving,
                out var kind,
                out var name,
                out var failureReason);
            if (status == StaticParseKind.Dynamic)
            {
                return false;
            }

            if (status == StaticParseKind.Invalid)
            {
                validation.Add(StaticPredicate.Invalid(failureReason ?? "the action frame is invalid"));
                continue;
            }

            frame.Add(ResourceKey(kind, name));
        }

        return true;
    }

    private static bool TryParsePostconditions(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        ISet<string> frame,
        ICollection<StaticPredicate> facts)
    {
        if (!TryResolveItems(expression, semanticModel, cancellationToken, resolving, out var items))
        {
            return false;
        }

        foreach (var item in items)
        {
            var candidate = Unwrap(item);
            if (candidate.IsKind(SyntaxKind.NullLiteralExpression))
            {
                facts.Add(StaticPredicate.Invalid("the postcondition collection contains a null entry"));
                continue;
            }

            if (candidate is IdentifierNameSyntax identifier
                && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
            {
                candidate = initializer;
            }

            if (candidate is not BaseObjectCreationExpressionSyntax creation
                || !IsType(
                    semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                    DescriptorNamespace + ".ActionPostcondition")
                || creation.Initializer is null)
            {
                return false;
            }

            var kindExpression = FindInitializerValue(creation.Initializer, "Kind");
            if (kindExpression is null)
            {
                facts.Add(StaticPredicate.Invalid("an action postcondition must declare its kind"));
                continue;
            }

            var kindStatus = TryResolveEnumMemberName(
                kindExpression,
                semanticModel,
                cancellationToken,
                DescriptorNamespace + ".PostconditionKind",
                out var kind);
            if (kindStatus == StaticParseKind.Dynamic)
            {
                return false;
            }

            if (kindStatus == StaticParseKind.Invalid)
            {
                facts.Add(StaticPredicate.Invalid("unknown action postcondition kind"));
                continue;
            }

            StaticResourceKind resourceKind;
            string memberName;
            string missingNameFailure;
            switch (kind)
            {
                case "ModifiesProperty":
                    resourceKind = StaticResourceKind.Property;
                    memberName = "PropertyName";
                    missingNameFailure = "a ModifiesProperty postcondition must name the property";
                    break;
                case "CreatesLink":
                    resourceKind = StaticResourceKind.Link;
                    memberName = "LinkName";
                    missingNameFailure = "a CreatesLink postcondition must name the created link";
                    break;
                case "EmitsEvent":
                    resourceKind = StaticResourceKind.Event;
                    memberName = "EventTypeName";
                    missingNameFailure = "an EmitsEvent postcondition must name the event type";
                    break;
                default:
                    facts.Add(StaticPredicate.Invalid("unknown action postcondition kind"));
                    continue;
            }

            var nameExpression = FindInitializerValue(creation.Initializer, memberName);
            if (nameExpression is null)
            {
                facts.Add(StaticPredicate.Invalid(missingNameFailure));
                continue;
            }

            var nameStatus = TryParseRequiredString(
                nameExpression,
                semanticModel,
                cancellationToken,
                out var resourceName);
            if (nameStatus == StaticParseKind.Dynamic)
            {
                return false;
            }

            if (nameStatus == StaticParseKind.Invalid)
            {
                facts.Add(StaticPredicate.Invalid(missingNameFailure));
                continue;
            }

            var resourceKey = ResourceKey(resourceKind, resourceName);
            if (!frame.Contains(resourceKey))
            {
                facts.Add(StaticPredicate.Invalid(
                    $"postcondition mutates '{resourceKind}:{resourceName}' outside the declared frame"));
                continue;
            }

            if (kind == "CreatesLink")
            {
                facts.Add(StaticPredicate.BooleanAtom(resourceKey, resourceKey));
            }
        }

        return true;
    }

    private static bool TryParsePredicate(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out StaticPredicate predicate)
    {
        expression = UnwrapParentheses(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParsePredicate(initializer, semanticModel, cancellationToken, resolving, out predicate);
        }

        if (expression is MemberAccessExpressionSyntax member)
        {
            var property = semanticModel.GetSymbolInfo(member, cancellationToken).Symbol as IPropertySymbol;
            if (IsType(property?.ContainingType, DescriptorNamespace + ".ActionPredicate")
                && property!.Name is "True" or "False")
            {
                predicate = property.Name == "True" ? StaticPredicate.True : StaticPredicate.False;
                return true;
            }
        }

        if (expression is not InvocationExpressionSyntax invocation)
        {
            predicate = null!;
            return false;
        }

        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (!IsType(method?.ContainingType, DescriptorNamespace + ".ActionPredicate"))
        {
            predicate = null!;
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var operation = semanticModel.GetOperation(invocation, cancellationToken) as IInvocationOperation;
        switch (method.Name)
        {
            case "All":
            case "Any":
            {
                var operands = new List<StaticPredicate>();
                foreach (var argument in arguments)
                {
                    if (TryResolveItems(argument.Expression, semanticModel, cancellationToken, resolving, out var items))
                    {
                        foreach (var item in items)
                        {
                            if (!TryParsePredicate(item, semanticModel, cancellationToken, resolving, out var operand))
                            {
                                predicate = null!;
                                return false;
                            }

                            operands.Add(operand);
                        }
                    }
                    else if (TryParsePredicate(
                        argument.Expression,
                        semanticModel,
                        cancellationToken,
                        resolving,
                        out var operand))
                    {
                        operands.Add(operand);
                    }
                    else
                    {
                        predicate = null!;
                        return false;
                    }
                }

                predicate = method.Name == "All"
                    ? StaticPredicate.All(operands)
                    : StaticPredicate.Any(operands);
                return true;
            }

            case "Not" when FindArgumentExpression(invocation, operation, "operand") is ExpressionSyntax operandExpression:
                if (TryParsePredicate(
                    operandExpression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var negated))
                {
                    predicate = negated.Not();
                    return true;
                }

                break;
            case "LinkExists" when FindArgumentExpression(invocation, operation, "linkName") is ExpressionSyntax linkExpression:
            {
                var status = TryParseRequiredString(
                    linkExpression,
                    semanticModel,
                    cancellationToken,
                    out var linkName);
                if (status == StaticParseKind.Success)
                {
                    predicate = StaticPredicate.BooleanAtom("link|" + linkName, "link|" + linkName);
                    return true;
                }

                if (status == StaticParseKind.Invalid)
                {
                    predicate = StaticPredicate.Invalid("a link-existence predicate requires a non-empty link name");
                    return true;
                }

                break;
            }

            case "RelationHolds" when FindArgumentExpression(
                invocation,
                operation,
                "relationName") is ExpressionSyntax relationExpression:
            {
                var relationStatus = TryParseRequiredString(
                    relationExpression,
                    semanticModel,
                    cancellationToken,
                    out var relationName);
                if (relationStatus == StaticParseKind.Invalid)
                {
                    predicate = StaticPredicate.Invalid("a relation predicate requires a non-empty relation name");
                    return true;
                }

                if (relationStatus != StaticParseKind.Success)
                {
                    break;
                }

                var pathNames = new List<string>();
                foreach (var pathExpression in FindArgumentExpressions(
                    invocation,
                    "linkPath"))
                {
                    if (TryResolveItems(
                            pathExpression,
                            semanticModel,
                            cancellationToken,
                            resolving,
                            out var pathItems))
                    {
                        foreach (var pathItem in pathItems)
                        {
                            var pathStatus = TryParseRequiredString(
                                pathItem,
                                semanticModel,
                                cancellationToken,
                                out var path);
                            if (pathStatus == StaticParseKind.Dynamic)
                            {
                                predicate = null!;
                                return false;
                            }

                            if (pathStatus == StaticParseKind.Invalid)
                            {
                                predicate = StaticPredicate.Invalid(
                                    "a relation path cannot contain an empty link name");
                                return true;
                            }

                            pathNames.Add(path);
                        }
                    }
                    else
                    {
                        var pathStatus = TryParseRequiredString(
                            pathExpression,
                            semanticModel,
                            cancellationToken,
                            out var path);
                        if (pathStatus == StaticParseKind.Dynamic)
                        {
                            predicate = null!;
                            return false;
                        }

                        if (pathStatus == StaticParseKind.Invalid)
                        {
                            predicate = StaticPredicate.Invalid(
                                "a relation path cannot contain an empty link name");
                            return true;
                        }

                        pathNames.Add(path);
                    }
                }

                var token = "relation|relation:" + Segment(relationName) + ":"
                    + string.Concat(pathNames.Select(Segment));
                predicate = StaticPredicate.BooleanAtom(
                    token,
                    "link|" + (pathNames.Count == 0 ? relationName : pathNames[0]));
                return true;
            }

            case "Custom" when arguments.Count >= 1:
                var customStatus = TryParseCustomPredicate(
                    invocation,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var customToken,
                    out var customKey,
                    out var customFailure);
                if (customStatus == StaticParseKind.Success)
                {
                    predicate = StaticPredicate.Opaque(customToken, customKey);
                    return true;
                }

                if (customStatus == StaticParseKind.Invalid)
                {
                    predicate = StaticPredicate.Invalid(customFailure ?? "invalid custom predicate");
                    return true;
                }

                break;
            case "Property" when FindArgumentExpression(
                    invocation,
                    operation,
                    "property") is ExpressionSyntax propertyExpression
                && FindArgumentExpression(
                    invocation,
                    operation,
                    "comparison") is ExpressionSyntax comparisonExpression
                && FindArgumentExpression(
                    invocation,
                    operation,
                    "value") is ExpressionSyntax valueExpression:
            {
                var resourceStatus = TryParsePropertyReference(
                    propertyExpression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var resource,
                    out var resourceFailure);
                if (resourceStatus == StaticParseKind.Invalid)
                {
                    predicate = StaticPredicate.Invalid(resourceFailure ?? "invalid property reference");
                    return true;
                }

                if (resourceStatus == StaticParseKind.Dynamic)
                {
                    break;
                }

                var comparisonStatus = TryParseComparison(
                    comparisonExpression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var comparison,
                    out var comparisonFailure);
                if (comparisonStatus == StaticParseKind.Invalid)
                {
                    predicate = StaticPredicate.Invalid(comparisonFailure ?? "invalid comparison operator");
                    return true;
                }

                if (comparisonStatus == StaticParseKind.Dynamic)
                {
                    break;
                }

                var literalStatus = TryParseLiteral(
                    valueExpression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var literal,
                    out var literalFailure);
                if (literalStatus == StaticParseKind.Invalid)
                {
                    predicate = StaticPredicate.Invalid(literalFailure ?? "invalid predicate literal");
                    return true;
                }

                if (literalStatus == StaticParseKind.Success)
                {
                    predicate = StaticPredicate.Comparison(resource, comparison, literal);
                    return true;
                }

                break;
            }
        }

        predicate = null!;
        return false;
    }

    private static StaticParseKind TryParsePropertyReference(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out LogicResource resource,
        out string? failureReason)
    {
        expression = Unwrap(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParsePropertyReference(
                initializer,
                semanticModel,
                cancellationToken,
                resolving,
                out resource,
                out failureReason);
        }

        if (expression is not BaseObjectCreationExpressionSyntax creation
            || !IsType(
                semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                DescriptorNamespace + ".PredicatePropertyReference"))
        {
            resource = null!;
            failureReason = null;
            return StaticParseKind.Dynamic;
        }

        var operation = semanticModel.GetOperation(creation, cancellationToken) as IObjectCreationOperation;
        var nameExpression = FindArgumentExpression(operation, "name");
        var scalarExpression = FindArgumentExpression(operation, "scalarKind");
        if (nameExpression is null || scalarExpression is null)
        {
            resource = null!;
            failureReason = "a property reference must statically provide its name and scalar kind";
            return StaticParseKind.Invalid;
        }

        var nameConstant = semanticModel.GetConstantValue(nameExpression, cancellationToken);
        if (!nameConstant.HasValue)
        {
            resource = null!;
            failureReason = null;
            return StaticParseKind.Dynamic;
        }

        if (nameConstant.Value is not string name || string.IsNullOrWhiteSpace(name))
        {
            resource = null!;
            failureReason = "a property reference name cannot be empty";
            return StaticParseKind.Invalid;
        }

        var scalarStatus = TryParseScalarKind(
            scalarExpression,
            semanticModel,
            cancellationToken,
            resolving,
            out var scalarKind,
            out failureReason);
        if (scalarStatus != StaticParseKind.Success)
        {
            resource = null!;
            return scalarStatus;
        }

        var nullable = false;
        var nullableExpression = FindArgumentExpression(operation, "isNullable");
        if (nullableExpression is not null)
        {
            var nullableConstant = semanticModel.GetConstantValue(nullableExpression, cancellationToken);
            if (!nullableConstant.HasValue)
            {
                resource = null!;
                failureReason = null;
                return StaticParseKind.Dynamic;
            }

            if (nullableConstant.Value is not bool nullableValue)
            {
                resource = null!;
                failureReason = "property nullability must be a Boolean constant";
                return StaticParseKind.Invalid;
            }

            nullable = nullableValue;
        }

        string? scalarTypeName = null;
        var scalarTypeExpression = FindArgumentExpression(operation, "enumTypeName");
        if (scalarTypeExpression is not null)
        {
            var typeNameConstant = semanticModel.GetConstantValue(scalarTypeExpression, cancellationToken);
            if (!typeNameConstant.HasValue)
            {
                resource = null!;
                failureReason = null;
                return StaticParseKind.Dynamic;
            }

            scalarTypeName = typeNameConstant.Value as string;
        }

        if (scalarKind == LogicScalarKind.Enum && string.IsNullOrWhiteSpace(scalarTypeName))
        {
            resource = null!;
            failureReason = $"enum property '{name}' requires a stable enum type name";
            return StaticParseKind.Invalid;
        }

        if (scalarKind != LogicScalarKind.Enum && scalarTypeName is not null)
        {
            resource = null!;
            failureReason = $"non-enum property '{name}' cannot carry an enum type name";
            return StaticParseKind.Invalid;
        }

        resource = new LogicResource("property|" + name, scalarKind, nullable, scalarTypeName);
        failureReason = null;
        return StaticParseKind.Success;
    }

    private static StaticParseKind TryParseLiteral(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out LogicLiteral literal,
        out string? failureReason)
    {
        expression = Unwrap(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParseLiteral(
                initializer,
                semanticModel,
                cancellationToken,
                resolving,
                out literal,
                out failureReason);
        }

        if (expression is MemberAccessExpressionSyntax member
            && member.Name.Identifier.Text == "Null")
        {
            var nullProperty = semanticModel.GetSymbolInfo(member, cancellationToken).Symbol as IPropertySymbol;
            if (IsType(nullProperty?.ContainingType, DescriptorNamespace + ".PredicateLiteral")
                && nullProperty!.Name == "Null")
            {
                literal = LogicLiteral.Null;
                failureReason = null;
                return StaticParseKind.Success;
            }
        }

        if (expression is not InvocationExpressionSyntax invocation)
        {
            literal = null!;
            failureReason = null;
            return StaticParseKind.Dynamic;
        }

        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (!IsType(method?.ContainingType, DescriptorNamespace + ".PredicateLiteral"))
        {
            literal = null!;
            failureReason = null;
            return StaticParseKind.Dynamic;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var operation = semanticModel.GetOperation(invocation, cancellationToken) as IInvocationOperation;
        object? constant;
        switch (method.Name)
        {
            case "Boolean" when FindArgumentExpression(
                invocation,
                operation,
                "value") is ExpressionSyntax booleanExpression:
                var booleanConstant = semanticModel.GetConstantValue(booleanExpression, cancellationToken);
                if (!booleanConstant.HasValue)
                {
                    break;
                }

                constant = booleanConstant.Value;
                if (constant is bool boolean)
                {
                    literal = new LogicLiteral(LogicLiteralKind.Boolean, boolean ? "true" : "false");
                    failureReason = null;
                    return StaticParseKind.Success;
                }

                literal = null!;
                failureReason = "a Boolean predicate literal requires a Boolean constant";
                return StaticParseKind.Invalid;
            case "Integer" when FindArgumentExpression(
                invocation,
                operation,
                "value") is ExpressionSyntax integerExpression:
                if (TryParseBigIntegerExpression(
                    integerExpression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var integer,
                    out var integerWasStatic))
                {
                    literal = new LogicLiteral(LogicLiteralKind.Integer, integer);
                    failureReason = null;
                    return StaticParseKind.Success;
                }

                if (integerWasStatic)
                {
                    literal = null!;
                    failureReason = "the integer predicate literal is not a valid arbitrary-precision integer";
                    return StaticParseKind.Invalid;
                }

                break;
            case "Decimal" when FindArgumentExpression(
                invocation,
                operation,
                "value") is ExpressionSyntax decimalExpression:
                var decimalConstant = semanticModel.GetConstantValue(decimalExpression, cancellationToken);
                if (!decimalConstant.HasValue)
                {
                    break;
                }

                constant = decimalConstant.Value;
                if (constant is decimal decimalValue)
                {
                    literal = new LogicLiteral(
                        LogicLiteralKind.Decimal,
                        decimalValue.ToString("G29", CultureInfo.InvariantCulture));
                    failureReason = null;
                    return StaticParseKind.Success;
                }

                if (constant is string decimalText
                    && FiniteDomainSolver.TryNormalizeExactDecimal(
                        decimalText,
                        out var canonicalDecimal))
                {
                    literal = new LogicLiteral(LogicLiteralKind.Decimal, canonicalDecimal);
                    failureReason = null;
                    return StaticParseKind.Success;
                }

                literal = null!;
                failureReason = "an exact decimal predicate literal requires a decimal or invariant string constant";
                return StaticParseKind.Invalid;
            case "String" when FindArgumentExpression(
                invocation,
                operation,
                "value") is ExpressionSyntax stringExpression:
                var stringConstant = semanticModel.GetConstantValue(stringExpression, cancellationToken);
                if (!stringConstant.HasValue)
                {
                    break;
                }

                if (stringConstant.Value is string text)
                {
                    literal = new LogicLiteral(LogicLiteralKind.String, text);
                    failureReason = null;
                    return StaticParseKind.Success;
                }

                literal = null!;
                failureReason = "a string predicate literal cannot be null";
                return StaticParseKind.Invalid;
            case "Symbol" when FindArgumentExpression(
                invocation,
                operation,
                "value") is ExpressionSyntax symbolExpression:
                var symbolConstant = semanticModel.GetConstantValue(symbolExpression, cancellationToken);
                if (!symbolConstant.HasValue)
                {
                    break;
                }

                if (symbolConstant.Value is string symbol && !string.IsNullOrWhiteSpace(symbol))
                {
                    literal = new LogicLiteral(LogicLiteralKind.Symbol, symbol);
                    failureReason = null;
                    return StaticParseKind.Success;
                }

                literal = null!;
                failureReason = "a symbolic predicate literal requires a non-empty string constant";
                return StaticParseKind.Invalid;
            case "Enum" when !method!.IsGenericMethod
                && FindArgumentExpression(
                    invocation,
                    operation,
                    "typeName") is ExpressionSyntax enumTypeExpression
                && FindArgumentExpression(
                    invocation,
                    operation,
                    "memberName") is ExpressionSyntax memberExpression:
                var enumTypeConstant = semanticModel.GetConstantValue(enumTypeExpression, cancellationToken);
                var memberConstant = semanticModel.GetConstantValue(memberExpression, cancellationToken);
                if (!enumTypeConstant.HasValue || !memberConstant.HasValue)
                {
                    break;
                }

                if (enumTypeConstant.Value is string enumTypeName
                    && !string.IsNullOrWhiteSpace(enumTypeName)
                    && memberConstant.Value is string memberName
                    && !string.IsNullOrWhiteSpace(memberName))
                {
                    literal = new LogicLiteral(LogicLiteralKind.Enum, memberName, enumTypeName);
                    failureReason = null;
                    return StaticParseKind.Success;
                }

                literal = null!;
                failureReason = "an enum predicate literal requires non-empty type and member names";
                return StaticParseKind.Invalid;
            case "Enum" when method!.IsGenericMethod
                && FindArgumentExpression(
                    invocation,
                    operation,
                    "value") is ExpressionSyntax enumValueExpression:
                if (TryParseGenericEnumLiteral(
                    method,
                    enumValueExpression,
                    semanticModel,
                    cancellationToken,
                    out literal,
                    out var enumWasStatic))
                {
                    failureReason = null;
                    return StaticParseKind.Success;
                }

                if (enumWasStatic)
                {
                    failureReason = "a generic enum predicate literal must name one defined, unaliased enum member";
                    return StaticParseKind.Invalid;
                }

                break;
        }

        literal = null!;
        failureReason = null;
        return StaticParseKind.Dynamic;
    }

    private static StaticParseKind TryParseCustomPredicate(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out string canonicalToken,
        out string evaluatorKey,
        out string? failureReason)
    {
        var operation = semanticModel.GetOperation(invocation, cancellationToken) as IInvocationOperation;
        var keyExpression = FindArgumentExpression(invocation, operation, "evaluatorKey");
        if (keyExpression is null)
        {
            canonicalToken = null!;
            evaluatorKey = null!;
            failureReason = "a custom predicate must declare an evaluator key";
            return StaticParseKind.Invalid;
        }

        var keyConstant = semanticModel.GetConstantValue(keyExpression, cancellationToken);
        if (!keyConstant.HasValue)
        {
            canonicalToken = null!;
            evaluatorKey = null!;
            failureReason = null;
            return StaticParseKind.Dynamic;
        }

        if (keyConstant.Value is not string key || string.IsNullOrWhiteSpace(key))
        {
            canonicalToken = null!;
            evaluatorKey = null!;
            failureReason = "a custom predicate evaluator key cannot be empty";
            return StaticParseKind.Invalid;
        }

        var literalTokens = new List<string>();
        var argumentsExpression = FindArgumentExpression(invocation, operation, "arguments");
        if (!IsExplicitNull(argumentsExpression, semanticModel, cancellationToken))
        {
            var argumentItems = new List<ExpressionSyntax>();
            if (argumentsExpression is not null
                && !TryResolveItems(
                    argumentsExpression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out argumentItems))
            {
                canonicalToken = null!;
                evaluatorKey = null!;
                failureReason = null;
                return StaticParseKind.Dynamic;
            }

            foreach (var item in argumentItems)
            {
                var status = TryParseLiteral(
                    item,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var literal,
                    out failureReason);
                if (status != StaticParseKind.Success)
                {
                    canonicalToken = null!;
                    evaluatorKey = null!;
                    return status;
                }

                literalTokens.Add(LogicLiteralCanonicalToken(literal));
            }
        }

        var resources = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var readSetExpression = FindArgumentExpression(invocation, operation, "readSet");
        if (!IsExplicitNull(readSetExpression, semanticModel, cancellationToken))
        {
            var resourceItems = new List<ExpressionSyntax>();
            if (readSetExpression is not null
                && !TryResolveItems(
                    readSetExpression,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out resourceItems))
            {
                canonicalToken = null!;
                evaluatorKey = null!;
                failureReason = null;
                return StaticParseKind.Dynamic;
            }

            foreach (var item in resourceItems)
            {
                var status = TryParseActionResource(
                    item,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var kind,
                    out var name,
                    out failureReason);
                if (status != StaticParseKind.Success)
                {
                    canonicalToken = null!;
                    evaluatorKey = null!;
                    return status;
                }

                var resourceToken = ((int)kind).ToString(CultureInfo.InvariantCulture)
                    + ":" + Segment(name);
                resources[resourceToken] = resourceToken;
            }
        }

        evaluatorKey = key;
        canonicalToken = "custom:" + Segment(key) + ":"
            + string.Concat(literalTokens.Select(Segment)) + ":"
            + string.Concat(resources.Values);
        failureReason = null;
        return StaticParseKind.Success;
    }

    private static StaticParseKind TryParseActionResource(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out StaticResourceKind kind,
        out string name,
        out string? failureReason)
    {
        expression = Unwrap(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParseActionResource(
                initializer,
                semanticModel,
                cancellationToken,
                resolving,
                out kind,
                out name,
                out failureReason);
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            if (IsType(method?.ContainingType, DescriptorNamespace + ".ActionResource")
                && invocation.ArgumentList.Arguments.Count == 1)
            {
                switch (method!.Name)
                {
                    case "Property": kind = StaticResourceKind.Property; break;
                    case "Link": kind = StaticResourceKind.Link; break;
                    case "Event": kind = StaticResourceKind.Event; break;
                    case "External": kind = StaticResourceKind.External; break;
                    default:
                        kind = default;
                        name = null!;
                        failureReason = null;
                        return StaticParseKind.Dynamic;
                }

                var nameStatus = TryParseRequiredString(
                    invocation.ArgumentList.Arguments[0].Expression,
                    semanticModel,
                    cancellationToken,
                    out name);
                failureReason = nameStatus == StaticParseKind.Invalid
                    ? "an action resource must have a non-empty name"
                    : null;
                return nameStatus;
            }
        }

        if (expression is BaseObjectCreationExpressionSyntax creation
            && IsType(
                semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                DescriptorNamespace + ".ActionResource"))
        {
            var operation = semanticModel.GetOperation(creation, cancellationToken) as IObjectCreationOperation;
            var kindExpression = FindArgumentExpression(operation, "Kind")
                ?? FindArgumentExpression(operation, "kind");
            var nameExpression = FindArgumentExpression(operation, "Name")
                ?? FindArgumentExpression(operation, "name");
            if (kindExpression is null || nameExpression is null)
            {
                kind = default;
                name = null!;
                failureReason = "an action resource must declare a kind and name";
                return StaticParseKind.Invalid;
            }

            var kindStatus = TryResolveEnumMemberName(
                kindExpression,
                semanticModel,
                cancellationToken,
                DescriptorNamespace + ".ActionResourceKind",
                out var kindName);
            if (kindStatus != StaticParseKind.Success)
            {
                kind = default;
                name = null!;
                failureReason = kindStatus == StaticParseKind.Invalid
                    ? "unknown action resource kind"
                    : null;
                return kindStatus;
            }

            var nameConstant = semanticModel.GetConstantValue(nameExpression, cancellationToken);
            if (!nameConstant.HasValue)
            {
                kind = default;
                name = null!;
                failureReason = null;
                return StaticParseKind.Dynamic;
            }

            kind = default;
            if (nameConstant.Value is not string resourceName || string.IsNullOrWhiteSpace(resourceName)
                || !TryMapResourceKind(kindName, out kind))
            {
                name = null!;
                failureReason = "an action resource must have a known kind and non-empty name";
                return StaticParseKind.Invalid;
            }

            name = resourceName;
            failureReason = null;
            return StaticParseKind.Success;
        }

        kind = default;
        name = null!;
        failureReason = null;
        return StaticParseKind.Dynamic;
    }

    private static bool TryMapResourceKind(string name, out StaticResourceKind kind)
    {
        switch (name)
        {
            case "Property": kind = StaticResourceKind.Property; return true;
            case "Link": kind = StaticResourceKind.Link; return true;
            case "Event": kind = StaticResourceKind.Event; return true;
            case "External": kind = StaticResourceKind.External; return true;
            default: kind = default; return false;
        }
    }

    private static bool IsExplicitNull(
        ExpressionSyntax? expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (expression is null)
        {
            return true;
        }

        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        return constant.HasValue && constant.Value is null;
    }

    private static string LogicLiteralCanonicalToken(LogicLiteral literal) =>
        "literal:" + ((int)literal.Kind).ToString(CultureInfo.InvariantCulture) + ":"
        + Segment(literal.ScalarTypeName ?? string.Empty) + ":"
        + Segment(literal.CanonicalValue);

    private static bool TryResolveItems(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out List<ExpressionSyntax> items)
    {
        expression = Unwrap(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            var local = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol as ILocalSymbol;
            if (local is null || IsPotentiallyMutableCollection(local.Type))
            {
                items = null!;
                return false;
            }

            return TryResolveItems(initializer, semanticModel, cancellationToken, resolving, out items);
        }

        if (expression is CollectionExpressionSyntax collection)
        {
            items = collection.Elements
                .OfType<ExpressionElementSyntax>()
                .Select(element => element.Expression)
                .ToList();
            return items.Count == collection.Elements.Count;
        }

        InitializerExpressionSyntax? initializerExpression = expression switch
        {
            ArrayCreationExpressionSyntax array => array.Initializer,
            ImplicitArrayCreationExpressionSyntax array => array.Initializer,
            _ => null,
        };
        if (initializerExpression is not null)
        {
            items = initializerExpression.Expressions.ToList();
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            if (IsType(method?.ContainingType, "System.Collections.Immutable.ImmutableArray")
                && method.Name == "Create")
            {
                items = invocation.ArgumentList.Arguments.Select(argument => argument.Expression).ToList();
                return true;
            }
        }

        items = null!;
        return false;
    }

    private static bool TryParseSubject(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out StaticSubject subject)
    {
        expression = Unwrap(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParseSubject(initializer, semanticModel, cancellationToken, resolving, out subject);
        }

        if (expression is BaseObjectCreationExpressionSyntax creation
            && IsType(
                semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                DescriptorNamespace + ".ActionSubject"))
        {
            var operation = semanticModel.GetOperation(creation, cancellationToken) as IObjectCreationOperation;
            var domainExpression = FindArgumentExpression(
                creation.ArgumentList,
                operation,
                "domainName",
                position: 0);
            var objectTypeExpression = FindArgumentExpression(
                creation.ArgumentList,
                operation,
                "objectTypeName",
                position: 1);
            if (domainExpression is null || objectTypeExpression is null)
            {
                subject = null!;
                return false;
            }

            var domainStatus = TryParseRequiredString(
                domainExpression,
                semanticModel,
                cancellationToken,
                out var domainName);
            var objectTypeStatus = TryParseRequiredString(
                objectTypeExpression,
                semanticModel,
                cancellationToken,
                out var objectTypeName);
            if (domainStatus == StaticParseKind.Dynamic || objectTypeStatus == StaticParseKind.Dynamic)
            {
                subject = null!;
                return false;
            }

            var invalidReason = domainStatus == StaticParseKind.Invalid
                ? "an action subject domain name cannot be empty"
                : objectTypeStatus == StaticParseKind.Invalid
                    ? "an action subject object type name cannot be empty"
                    : null;
            subject = new StaticSubject(
                domainStatus == StaticParseKind.Success ? domainName : "<invalid-domain>",
                objectTypeStatus == StaticParseKind.Success ? objectTypeName : "<invalid-object-type>",
                invalidReason);
            return true;
        }

        subject = null!;
        return false;
    }

    private static StaticParseKind TryParseAuthorityNames(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out HashSet<string>? authorityNames,
        out string? failureReason)
    {
        expression = Unwrap(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParseAuthorityNames(
                initializer,
                semanticModel,
                cancellationToken,
                resolving,
                out authorityNames,
                out failureReason);
        }

        if (expression is not BaseObjectCreationExpressionSyntax creation
            || !IsType(
                semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                DescriptorNamespace + ".AuthorityLattice")
            || creation.ArgumentList is null)
        {
            authorityNames = null;
            failureReason = null;
            return StaticParseKind.Dynamic;
        }

        var operation = semanticModel.GetOperation(creation, cancellationToken) as IObjectCreationOperation;
        var authoritiesExpression = FindArgumentExpression(
            creation.ArgumentList,
            operation,
            "authorities",
            position: 1);
        if (authoritiesExpression is null
            || !TryResolveItems(
                authoritiesExpression,
                semanticModel,
                cancellationToken,
                resolving,
                out var items))
        {
            authorityNames = null;
            failureReason = null;
            return StaticParseKind.Dynamic;
        }

        authorityNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var candidate = Unwrap(item);
            if (candidate is IdentifierNameSyntax itemIdentifier
                && TryResolveImmutableLocal(
                    itemIdentifier,
                    semanticModel,
                    cancellationToken,
                    resolving,
                    out var itemInitializer))
            {
                candidate = Unwrap(itemInitializer);
            }

            if (candidate is not BaseObjectCreationExpressionSyntax authorityCreation
                || !IsType(
                    semanticModel.GetTypeInfo(authorityCreation, cancellationToken).Type,
                    DescriptorNamespace + ".AuthorityDescriptor")
                || authorityCreation.ArgumentList is null)
            {
                authorityNames = null;
                failureReason = null;
                return StaticParseKind.Dynamic;
            }

            var authorityOperation = semanticModel.GetOperation(
                authorityCreation,
                cancellationToken) as IObjectCreationOperation;
            var nameExpression = FindArgumentExpression(
                authorityCreation.ArgumentList,
                authorityOperation,
                "Name",
                position: 0)
                ?? FindArgumentExpression(
                    authorityCreation.ArgumentList,
                    authorityOperation,
                    "name",
                    position: 0);
            if (nameExpression is null)
            {
                authorityNames = null;
                failureReason = null;
                return StaticParseKind.Dynamic;
            }

            var nameStatus = TryParseRequiredString(
                nameExpression,
                semanticModel,
                cancellationToken,
                out var name);
            if (nameStatus != StaticParseKind.Success)
            {
                authorityNames = null;
                failureReason = nameStatus == StaticParseKind.Invalid
                    ? "an authority literal name cannot be empty"
                    : null;
                return nameStatus;
            }

            authorityNames.Add(name);
        }

        failureReason = null;
        return StaticParseKind.Success;
    }

    private static bool TryResolveImmutableLocal(
        IdentifierNameSyntax identifier,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        ISet<ISymbol> resolving,
        out ExpressionSyntax initializer)
    {
        var local = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol as ILocalSymbol;
        if (local is null || local.DeclaringSyntaxReferences.Length != 1)
        {
            initializer = null!;
            return false;
        }

        var declaration = local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken)
            as VariableDeclaratorSyntax;
        if (declaration?.Initializer?.Value is not ExpressionSyntax value
            || IsWrittenAfterDeclaration(local, declaration, semanticModel, cancellationToken)
            || value.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .Any(candidate => SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(candidate, cancellationToken).Symbol,
                    local)))
        {
            initializer = null!;
            return false;
        }

        initializer = value;
        return true;
    }

    private static bool IsWrittenAfterDeclaration(
        ILocalSymbol local,
        VariableDeclaratorSyntax declaration,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var root = declaration.SyntaxTree.GetRoot(cancellationToken);
        foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    local))
            {
                continue;
            }

            if (identifier.Ancestors()
                    .OfType<AssignmentExpressionSyntax>()
                    .Any(assignment => assignment.Left.Span.Contains(identifier.Span))
                || identifier.Parent is PrefixUnaryExpressionSyntax prefix
                    && prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || identifier.Parent is PrefixUnaryExpressionSyntax decrement
                    && decrement.IsKind(SyntaxKind.PreDecrementExpression)
                || identifier.Parent is PostfixUnaryExpressionSyntax
                || identifier.Parent is RefExpressionSyntax
                || identifier.Parent is ArgumentSyntax argument
                    && argument.RefKindKeyword.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword)
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeExpressionPredicate(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            ExpressionSyntax candidate = argument.Expression;
            if (candidate is IdentifierNameSyntax identifier
                && TryResolveImmutableLocal(
                    identifier,
                    context.SemanticModel,
                    context.CancellationToken,
                    new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                    out var initializer))
            {
                candidate = initializer;
            }

            if (candidate is not LambdaExpressionSyntax lambda
                || lambda.Body is not ExpressionSyntax body)
            {
                continue;
            }

            var parameterSyntax = lambda switch
            {
                SimpleLambdaExpressionSyntax simple => simple.Parameter,
                ParenthesizedLambdaExpressionSyntax parenthesized
                    when parenthesized.ParameterList.Parameters.Count == 1 =>
                    parenthesized.ParameterList.Parameters[0],
                _ => null,
            };
            var parameter = parameterSyntax is null
                ? null
                : context.SemanticModel.GetDeclaredSymbol(parameterSyntax, context.CancellationToken)
                    as IParameterSymbol;
            string? reason = null;
            if (parameter is null
                || !IsSupportedExpressionPredicate(
                    body,
                    parameter,
                    context.SemanticModel,
                    context.CancellationToken,
                    topLevel: true,
                    out reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.InvalidActionContract,
                    lambda.GetLocation(),
                    "unsupported action-predicate expression: "
                    + (reason ?? "use the closed property-to-literal AND/OR/NOT grammar or explicit ActionPredicate.Custom")));
            }
        }
    }

    private static bool IsSupportedExpressionPredicate(
        ExpressionSyntax expression,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        bool topLevel,
        out string? failureReason)
    {
        expression = UnwrapParentheses(expression);
        if (expression is LiteralExpressionSyntax literal)
        {
            if (literal.IsKind(SyntaxKind.TrueLiteralExpression))
            {
                failureReason = topLevel
                    ? "use ActionPredicate.True instead of a top-level '_ => true' lambda"
                    : null;
                return !topLevel;
            }

            if (literal.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                failureReason = null;
                return true;
            }

            failureReason = "a scalar literal is only valid as one side of a property comparison";
            return false;
        }

        if (expression is PrefixUnaryExpressionSyntax not
            && not.IsKind(SyntaxKind.LogicalNotExpression))
        {
            var unary = semanticModel.GetOperation(not, cancellationToken) as IUnaryOperation;
            if (unary?.OperatorMethod is not null)
            {
                failureReason = "user-defined logical operators are excluded";
                return false;
            }

            return IsSupportedExpressionPredicate(
                not.Operand,
                parameter,
                semanticModel,
                cancellationToken,
                topLevel: false,
                out failureReason);
        }

        if (expression is BinaryExpressionSyntax logical
            && logical.Kind() is SyntaxKind.LogicalAndExpression
                or SyntaxKind.BitwiseAndExpression
                or SyntaxKind.LogicalOrExpression
                or SyntaxKind.BitwiseOrExpression)
        {
            var operation = semanticModel.GetOperation(logical, cancellationToken) as IBinaryOperation;
            if (operation?.OperatorMethod is not null
                || operation?.Type?.SpecialType != SpecialType.System_Boolean)
            {
                failureReason = "logical composition must use the built-in Boolean AND/OR operators";
                return false;
            }

            return IsSupportedExpressionPredicate(
                    logical.Left,
                    parameter,
                    semanticModel,
                    cancellationToken,
                    topLevel: false,
                    out failureReason)
                && IsSupportedExpressionPredicate(
                    logical.Right,
                    parameter,
                    semanticModel,
                    cancellationToken,
                    topLevel: false,
                    out failureReason);
        }

        if (TryGetDirectProperty(
                expression,
                parameter,
                semanticModel,
                cancellationToken,
                out var directProperty,
                out var directConversionIsInvalid))
        {
            if (directConversionIsInvalid)
            {
                failureReason = "user-defined property conversions are excluded";
                return false;
            }

            if (!TryGetExpressionScalarKind(directProperty.Type, out var scalarKind, out _)
                || scalarKind != LogicScalarKind.Boolean)
            {
                failureReason = "a bare property predicate must name a Boolean property";
                return false;
            }

            failureReason = null;
            return true;
        }

        if (expression is BinaryExpressionSyntax comparison
            && comparison.Kind() is SyntaxKind.EqualsExpression
                or SyntaxKind.NotEqualsExpression
                or SyntaxKind.LessThanExpression
                or SyntaxKind.LessThanOrEqualExpression
                or SyntaxKind.GreaterThanExpression
                or SyntaxKind.GreaterThanOrEqualExpression)
        {
            var leftProperty = TryGetDirectProperty(
                comparison.Left,
                parameter,
                semanticModel,
                cancellationToken,
                out var left,
                out var leftConversionIsInvalid);
            var rightProperty = TryGetDirectProperty(
                comparison.Right,
                parameter,
                semanticModel,
                cancellationToken,
                out var right,
                out var rightConversionIsInvalid);
            if (leftConversionIsInvalid || rightConversionIsInvalid)
            {
                failureReason = "semantics-changing property conversions are excluded";
                return false;
            }

            if (leftProperty == rightProperty)
            {
                failureReason = "exactly one comparison operand must be a direct subject property";
                return false;
            }

            var binaryOperation = semanticModel.GetOperation(comparison, cancellationToken)
                as IBinaryOperation;
            var propertyOperand = leftProperty
                ? binaryOperation?.LeftOperand
                : binaryOperation?.RightOperand;
            var property = leftProperty ? left : right;
            if (HasNonIdentityConversion(propertyOperand, property.Type))
            {
                failureReason = "numeric or user-defined property conversions are excluded";
                return false;
            }

            if (!TryGetExpressionScalarKind(property.Type, out var comparisonScalarKind, out var nullable))
            {
                failureReason = $"property type '{property.Type.ToDisplayString()}' is outside the predicate scalar grammar";
                return false;
            }

            if (comparisonScalarKind is not LogicScalarKind.Integer and not LogicScalarKind.Decimal
                && comparison.Kind() is not SyntaxKind.EqualsExpression and not SyntaxKind.NotEqualsExpression)
            {
                failureReason = $"{comparisonScalarKind} properties support equality and inequality only";
                return false;
            }

            if (!IsApprovedComparisonOperator(binaryOperation?.OperatorMethod))
            {
                failureReason = "user-defined comparison operators are excluded";
                return false;
            }

            return IsCompatibleExpressionLiteral(
                leftProperty ? comparison.Right : comparison.Left,
                property.Type,
                comparisonScalarKind,
                nullable,
                comparison.Kind(),
                semanticModel,
                cancellationToken,
                out failureReason);
        }

        failureReason = "method calls, arithmetic, captures, fields, and computed values are excluded";
        return false;
    }

    private static bool TryGetDirectProperty(
        ExpressionSyntax expression,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out IPropertySymbol property,
        out bool conversionIsInvalid)
    {
        expression = UnwrapParentheses(expression);
        conversionIsInvalid = false;
        if (expression is CheckedExpressionSyntax checkedExpression)
        {
            return TryGetDirectProperty(
                checkedExpression.Expression,
                parameter,
                semanticModel,
                cancellationToken,
                out property,
                out conversionIsInvalid);
        }

        if (expression is CastExpressionSyntax cast)
        {
            var conversion = semanticModel.GetOperation(cast, cancellationToken)
                as IConversionOperation;
            if (!TryGetDirectProperty(
                    cast.Expression,
                    parameter,
                    semanticModel,
                    cancellationToken,
                    out property,
                    out conversionIsInvalid))
            {
                property = null!;
                return false;
            }

            if (conversion is { OperatorMethod: null }
                && !conversionIsInvalid
                && IsRepresentationPreservingPropertyConversion(
                    property.Type,
                    conversion.Type,
                    conversion.IsChecked))
            {
                return true;
            }

            property = null!;
            conversionIsInvalid = true;
            return false;
        }

        if (expression is MemberAccessExpressionSyntax member
            && member.Expression is IdentifierNameSyntax owner
            && semanticModel.GetSymbolInfo(owner, cancellationToken).Symbol is IParameterSymbol ownerSymbol
            && SymbolEqualityComparer.Default.Equals(ownerSymbol, parameter)
            && semanticModel.GetSymbolInfo(member, cancellationToken).Symbol is IPropertySymbol { IsIndexer: false } propertySymbol)
        {
            property = propertySymbol;
            return true;
        }

        property = null!;
        return false;
    }

    private static bool HasNonIdentityConversion(
        IOperation? operation,
        ITypeSymbol propertyType)
    {
        while (operation is IConversionOperation conversion)
        {
            if (!conversion.Conversion.IsIdentity
                && !IsRepresentationPreservingPropertyConversion(
                    propertyType,
                    conversion.Type,
                    conversion.IsChecked))
            {
                return true;
            }

            operation = conversion.Operand;
        }

        return false;
    }

    private static bool IsEnumUnderlyingConversion(
        ITypeSymbol source,
        ITypeSymbol? target)
    {
        if (target is null)
        {
            return false;
        }

        var sourceType = UnwrapNullable(source, out var sourceNullable);
        var targetType = UnwrapNullable(target, out var targetNullable);
        var underlyingType = (sourceType as INamedTypeSymbol)?.EnumUnderlyingType;
        var comparisonType = underlyingType?.SpecialType is
            SpecialType.System_SByte or
            SpecialType.System_Byte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16
                ? SpecialType.System_Int32
                : underlyingType?.SpecialType;
        return IsCompatibleNullableConversion(sourceNullable, targetNullable)
            && sourceType.TypeKind == TypeKind.Enum
            && comparisonType == targetType.SpecialType;
    }

    private static bool IsRepresentationPreservingPropertyConversion(
        ITypeSymbol source,
        ITypeSymbol? target,
        bool isChecked) =>
        target is not null
        && (IsValuePreservingIntegralConversion(source, target)
            || !isChecked
                && (IsNullableLift(source, target)
                    || IsEnumUnderlyingConversion(source, target)));

    private static bool IsRepresentationPreservingLiteralConversion(
        IConversionOperation conversion)
    {
        if (conversion.Operand.Type is not { } source
            || conversion.Type is not { } target)
        {
            return false;
        }

        if (conversion.IsChecked)
        {
            return conversion.OperatorMethod is null
                && IsValuePreservingIntegralConversion(source, target);
        }

        if (IsNullableLift(source, target)
            || IsValuePreservingIntegralConversion(source, target)
            || IsEnumUnderlyingConversion(source, target))
        {
            return true;
        }

        var targetType = UnwrapNullable(target, out _);
        if (!IsExactIntegralConversion(source, target))
        {
            return false;
        }

        if (targetType.SpecialType == SpecialType.System_Decimal)
        {
            return conversion.OperatorMethod is null
                || conversion.OperatorMethod.Name == "op_Implicit"
                    && conversion.OperatorMethod.ContainingType.SpecialType == SpecialType.System_Decimal;
        }

        return IsType(targetType, "System.Numerics.BigInteger")
            && conversion.OperatorMethod is { Name: "op_Implicit" } method
            && IsType(method.ContainingType, "System.Numerics.BigInteger");
    }

    private static bool IsNullableLift(ITypeSymbol source, ITypeSymbol target)
    {
        var sourceType = UnwrapNullable(source, out var sourceNullable);
        var targetType = UnwrapNullable(target, out var targetNullable);
        return !sourceNullable
            && targetNullable
            && SymbolEqualityComparer.Default.Equals(sourceType, targetType);
    }

    private static bool IsExactIntegralConversion(ITypeSymbol source, ITypeSymbol target)
    {
        var sourceType = UnwrapNullable(source, out var sourceNullable);
        var targetType = UnwrapNullable(target, out var targetNullable);
        return IsCompatibleNullableConversion(sourceNullable, targetNullable)
            && IsIntegralType(sourceType)
            && (targetType.SpecialType == SpecialType.System_Decimal
                || IsType(targetType, "System.Numerics.BigInteger"));
    }

    private static bool IsValuePreservingIntegralConversion(ITypeSymbol source, ITypeSymbol target)
    {
        var sourceType = UnwrapNullable(source, out var sourceNullable);
        var targetType = UnwrapNullable(target, out var targetNullable);
        if (!IsCompatibleNullableConversion(sourceNullable, targetNullable))
        {
            return false;
        }

        return sourceType.SpecialType switch
        {
            SpecialType.System_SByte => targetType.SpecialType is
                SpecialType.System_SByte or
                SpecialType.System_Int16 or
                SpecialType.System_Int32 or
                SpecialType.System_Int64,
            SpecialType.System_Byte => targetType.SpecialType is
                SpecialType.System_Byte or
                SpecialType.System_Int16 or
                SpecialType.System_UInt16 or
                SpecialType.System_Int32 or
                SpecialType.System_UInt32 or
                SpecialType.System_Int64 or
                SpecialType.System_UInt64,
            SpecialType.System_Int16 => targetType.SpecialType is
                SpecialType.System_Int16 or
                SpecialType.System_Int32 or
                SpecialType.System_Int64,
            SpecialType.System_UInt16 => targetType.SpecialType is
                SpecialType.System_UInt16 or
                SpecialType.System_Int32 or
                SpecialType.System_UInt32 or
                SpecialType.System_Int64 or
                SpecialType.System_UInt64,
            SpecialType.System_Int32 => targetType.SpecialType is
                SpecialType.System_Int32 or
                SpecialType.System_Int64,
            SpecialType.System_UInt32 => targetType.SpecialType is
                SpecialType.System_UInt32 or
                SpecialType.System_Int64 or
                SpecialType.System_UInt64,
            SpecialType.System_Int64 => targetType.SpecialType == SpecialType.System_Int64,
            SpecialType.System_UInt64 => targetType.SpecialType == SpecialType.System_UInt64,
            _ => false,
        };
    }

    private static bool IsIntegralType(ITypeSymbol type) => type.SpecialType is
        SpecialType.System_SByte or
        SpecialType.System_Byte or
        SpecialType.System_Int16 or
        SpecialType.System_UInt16 or
        SpecialType.System_Int32 or
        SpecialType.System_UInt32 or
        SpecialType.System_Int64 or
        SpecialType.System_UInt64;

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type, out bool nullable)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            nullable = true;
            return named.TypeArguments[0];
        }

        nullable = false;
        return type;
    }

    private static bool IsCompatibleNullableConversion(bool sourceNullable, bool targetNullable) =>
        sourceNullable == targetNullable || !sourceNullable && targetNullable;

    private static bool TryGetExpressionScalarKind(
        ITypeSymbol propertyType,
        out LogicScalarKind scalarKind,
        out bool nullable)
    {
        nullable = false;
        var type = propertyType;
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            nullable = true;
            type = named.TypeArguments[0];
        }
        else if (type.IsReferenceType)
        {
            nullable = type.NullableAnnotation != NullableAnnotation.NotAnnotated;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
                scalarKind = LogicScalarKind.Boolean;
                return true;
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                scalarKind = LogicScalarKind.Integer;
                return true;
            case SpecialType.System_Decimal:
                scalarKind = LogicScalarKind.Decimal;
                return true;
            case SpecialType.System_String:
                scalarKind = LogicScalarKind.String;
                return true;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            scalarKind = LogicScalarKind.Enum;
            return true;
        }

        if (IsType(type, "System.Numerics.BigInteger"))
        {
            scalarKind = LogicScalarKind.Integer;
            return true;
        }

        if (IsType(type, "System.Guid"))
        {
            scalarKind = LogicScalarKind.Symbol;
            return true;
        }

        scalarKind = default;
        return false;
    }

    private static bool IsApprovedComparisonOperator(IMethodSymbol? method)
    {
        if (method is null)
        {
            return true;
        }

        return method.ContainingType.SpecialType is SpecialType.System_String
            or SpecialType.System_Decimal
            || IsType(method.ContainingType, "System.Numerics.BigInteger")
            || IsType(method.ContainingType, "System.Guid");
    }

    private static bool IsCompatibleExpressionLiteral(
        ExpressionSyntax expression,
        ITypeSymbol propertyType,
        LogicScalarKind scalarKind,
        bool nullable,
        SyntaxKind comparisonKind,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out string? failureReason)
    {
        expression = UnwrapParentheses(expression);
        if (expression is CheckedExpressionSyntax checkedExpression)
        {
            return IsCompatibleExpressionLiteral(
                checkedExpression.Expression,
                propertyType,
                scalarKind,
                nullable,
                comparisonKind,
                semanticModel,
                cancellationToken,
                out failureReason);
        }

        if (expression is CastExpressionSyntax cast)
        {
            var conversion = semanticModel.GetOperation(cast, cancellationToken) as IConversionOperation;
            if (conversion is not null
                && IsUniqueNamedEnumLiteralCast(
                    cast,
                    conversion,
                    propertyType,
                    semanticModel,
                    cancellationToken))
            {
                failureReason = null;
                return true;
            }

            if (conversion is null || !IsRepresentationPreservingLiteralConversion(conversion))
            {
                failureReason = "semantics-changing literal conversions are excluded";
                return false;
            }

            return IsCompatibleExpressionLiteral(
                cast.Expression,
                propertyType,
                scalarKind,
                nullable,
                comparisonKind,
                semanticModel,
                cancellationToken,
                out failureReason);
        }

        if (!IsDirectExpressionLiteralSyntax(
                expression,
                propertyType,
                scalarKind,
                semanticModel,
                cancellationToken))
        {
            failureReason = "the comparison value must be a direct literal; arithmetic, const aliases, fields, calls, and computed values are excluded";
            return false;
        }

        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (!constant.HasValue)
        {
            failureReason = "the comparison value must be a literal, not a capture, field, static property, call, or computed value";
            return false;
        }

        if (constant.Value is null)
        {
            var equality = comparisonKind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression;
            failureReason = !nullable
                ? "a non-nullable property cannot be compared with null"
                : !equality
                    ? "null supports equality and inequality only"
                    : null;
            return nullable && equality;
        }

        var valid = scalarKind switch
        {
            LogicScalarKind.Boolean => constant.Value is bool,
            LogicScalarKind.Integer => TryInvariantInteger(constant.Value, out _),
            LogicScalarKind.Decimal => constant.Value is decimal
                || TryInvariantInteger(constant.Value, out _),
            LogicScalarKind.String => constant.Value is string,
            LogicScalarKind.Enum => IsMatchingEnumLiteral(
                expression,
                propertyType,
                semanticModel,
                cancellationToken),
            LogicScalarKind.Symbol => false,
            _ => false,
        };
        failureReason = valid
            ? null
            : $"the literal is not representable in the property's {scalarKind} domain";
        return valid;
    }

    private static bool IsUniqueNamedEnumLiteralCast(
        CastExpressionSyntax cast,
        IConversionOperation conversion,
        ITypeSymbol propertyType,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (conversion.Type is null
            || conversion.OperatorMethod is not null
            || !conversion.Conversion.Exists
            || conversion.Conversion.IsUserDefined)
        {
            return false;
        }

        var expectedType = UnwrapNullable(propertyType, out _);
        var targetType = UnwrapNullable(conversion.Type, out _);
        return expectedType.TypeKind == TypeKind.Enum
            && SymbolEqualityComparer.Default.Equals(expectedType, targetType)
            && IsDirectExpressionLiteralSyntax(
                cast.Expression,
                propertyType,
                LogicScalarKind.Integer,
                semanticModel,
                cancellationToken)
            && IsMatchingEnumLiteral(cast, propertyType, semanticModel, cancellationToken);
    }

    private static bool IsDirectExpressionLiteralSyntax(
        ExpressionSyntax expression,
        ITypeSymbol propertyType,
        LogicScalarKind scalarKind,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);
        if (expression is PostfixUnaryExpressionSyntax suppression
            && suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            return IsDirectExpressionLiteralSyntax(
                suppression.Operand,
                propertyType,
                scalarKind,
                semanticModel,
                cancellationToken);
        }

        if (expression is PrefixUnaryExpressionSyntax unary
            && unary.Kind() is SyntaxKind.UnaryMinusExpression or SyntaxKind.UnaryPlusExpression)
        {
            return scalarKind is LogicScalarKind.Integer or LogicScalarKind.Decimal
                && UnwrapParentheses(unary.Operand) is LiteralExpressionSyntax;
        }

        if (expression is LiteralExpressionSyntax)
        {
            return true;
        }

        if (scalarKind != LogicScalarKind.Enum)
        {
            return false;
        }

        var expected = propertyType is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1
                ? named.TypeArguments[0]
                : propertyType;
        var field = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol as IFieldSymbol;
        return field is { HasConstantValue: true }
            && field.ContainingType.TypeKind == TypeKind.Enum
            && SymbolEqualityComparer.Default.Equals(expected, field.ContainingType);
    }

    private static bool IsMatchingEnumLiteral(
        ExpressionSyntax expression,
        ITypeSymbol propertyType,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var expected = propertyType is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1
                ? named.TypeArguments[0]
                : propertyType;
        if (expected is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            return false;
        }

        var actual = semanticModel.GetTypeInfo(expression, cancellationToken).Type;
        if (actual?.TypeKind == TypeKind.Enum
            && !SymbolEqualityComparer.Default.Equals(expected, actual))
        {
            return false;
        }

        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (!constant.HasValue
            || constant.Value is null
            || !TryInvariantInteger(constant.Value, out var expectedValue))
        {
            return false;
        }

        return enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Count(field => field.HasConstantValue
                && field.ConstantValue is not null
                && TryInvariantInteger(field.ConstantValue, out var fieldValue)
                && fieldValue == expectedValue) == 1;
    }

    private static bool IsNestedSequential(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var ancestor in invocation.Ancestors().OfType<InvocationExpressionSyntax>())
        {
            var method = semanticModel.GetSymbolInfo(ancestor, cancellationToken).Symbol as IMethodSymbol;
            if (method is not null && IsActionCalculusSequential(method))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActionCalculusSequential(IMethodSymbol method) =>
        method.Name == "Sequential"
        && IsType(method.ContainingType, DescriptorNamespace + ".ActionCalculus");

    private static bool IsExpressionPredicateBuilderMethod(IMethodSymbol method)
    {
        if (method.Name is not ("Requires" or "RequiresSoft" or "Ensures")
            || method.Parameters.Length != 1
            || method.ContainingNamespace.ToDisplayString() != "Strategos.Ontology.Builder"
            || method.ContainingType.Arity != 1
            || method.ContainingType.Name is not ("ActionBuilder" or "IActionBuilder"))
        {
            return false;
        }

        return method.Parameters[0].Type is INamedTypeSymbol expression
            && expression.Name == "Expression"
            && expression.Arity == 1
            && expression.ContainingNamespace.ToDisplayString() == "System.Linq.Expressions";
    }

    private static bool IsPotentiallyMutableCollection(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        if (string.Equals(
                type.ContainingNamespace?.ToDisplayString(),
                "System.Collections.Immutable",
                StringComparison.Ordinal)
            && type.Name.StartsWith("Immutable", StringComparison.Ordinal))
        {
            return false;
        }

        return type.AllInterfaces.Any(@interface =>
            @interface.SpecialType == SpecialType.System_Collections_IEnumerable
            || string.Equals(
                @interface.OriginalDefinition.ToDisplayString(),
                "System.Collections.Generic.IEnumerable<T>",
                StringComparison.Ordinal));
    }

    private static bool IsType(ITypeSymbol? type, string metadataName) =>
        type is not null
        && string.Equals(type.ToDisplayString(), metadataName, StringComparison.Ordinal);

    private static ExpressionSyntax? FindInitializerValue(
        InitializerExpressionSyntax initializer,
        string propertyName) => initializer.Expressions
        .OfType<AssignmentExpressionSyntax>()
        .Where(assignment => assignment.Left is IdentifierNameSyntax identifier
            && identifier.Identifier.Text == propertyName)
        .Select(assignment => assignment.Right)
        .FirstOrDefault();

    private static StaticParseKind TryParseRequiredString(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out string value)
    {
        expression = Unwrap(expression);
        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (!constant.HasValue)
        {
            value = null!;
            return StaticParseKind.Dynamic;
        }

        if (constant.Value is not string text || string.IsNullOrWhiteSpace(text))
        {
            value = null!;
            return StaticParseKind.Invalid;
        }

        value = text;
        return StaticParseKind.Success;
    }

    private static StaticParseKind TryParseComparison(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out LogicComparisonOperator comparison,
        out string? failureReason)
    {
        expression = UnwrapParentheses(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParseComparison(
                initializer,
                semanticModel,
                cancellationToken,
                resolving,
                out comparison,
                out failureReason);
        }

        var status = TryResolveEnumMemberName(
            expression,
            semanticModel,
            cancellationToken,
            DescriptorNamespace + ".PredicateComparisonOperator",
            out var name);
        if (status != StaticParseKind.Success)
        {
            comparison = default;
            failureReason = status == StaticParseKind.Invalid
                ? "unknown predicate comparison operator"
                : null;
            return status;
        }

        switch (name)
        {
            case "Equal": comparison = LogicComparisonOperator.Equal; break;
            case "NotEqual": comparison = LogicComparisonOperator.NotEqual; break;
            case "LessThan": comparison = LogicComparisonOperator.LessThan; break;
            case "LessThanOrEqual": comparison = LogicComparisonOperator.LessThanOrEqual; break;
            case "GreaterThan": comparison = LogicComparisonOperator.GreaterThan; break;
            case "GreaterThanOrEqual": comparison = LogicComparisonOperator.GreaterThanOrEqual; break;
            default:
                comparison = default;
                failureReason = "unknown predicate comparison operator";
                return StaticParseKind.Invalid;
        }

        failureReason = null;
        return StaticParseKind.Success;
    }

    private static StaticParseKind TryParseScalarKind(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out LogicScalarKind scalarKind,
        out string? failureReason)
    {
        expression = UnwrapParentheses(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParseScalarKind(
                initializer,
                semanticModel,
                cancellationToken,
                resolving,
                out scalarKind,
                out failureReason);
        }

        var status = TryResolveEnumMemberName(
            expression,
            semanticModel,
            cancellationToken,
            DescriptorNamespace + ".PredicateScalarKind",
            out var name);
        if (status != StaticParseKind.Success)
        {
            scalarKind = default;
            failureReason = status == StaticParseKind.Invalid
                ? "unknown predicate scalar kind"
                : null;
            return status;
        }

        foreach (LogicScalarKind candidate in Enum.GetValues(typeof(LogicScalarKind)))
        {
            if (string.Equals(name, candidate.ToString(), StringComparison.Ordinal))
            {
                scalarKind = candidate;
                failureReason = null;
                return StaticParseKind.Success;
            }
        }

        scalarKind = default;
        failureReason = "unknown predicate scalar kind";
        return StaticParseKind.Invalid;
    }

    private static bool TryInvariantInteger(object value, out string text)
    {
        switch (value)
        {
            case sbyte number: text = number.ToString(CultureInfo.InvariantCulture); return true;
            case byte number: text = number.ToString(CultureInfo.InvariantCulture); return true;
            case short number: text = number.ToString(CultureInfo.InvariantCulture); return true;
            case ushort number: text = number.ToString(CultureInfo.InvariantCulture); return true;
            case int number: text = number.ToString(CultureInfo.InvariantCulture); return true;
            case uint number: text = number.ToString(CultureInfo.InvariantCulture); return true;
            case long number: text = number.ToString(CultureInfo.InvariantCulture); return true;
            case ulong number: text = number.ToString(CultureInfo.InvariantCulture); return true;
            case BigInteger number: text = number.ToString(CultureInfo.InvariantCulture); return true;
            default: text = null!; return false;
        }
    }

    private static ExpressionSyntax? FindArgumentExpression(
        IObjectCreationOperation? operation,
        string parameterName)
    {
        var argument = operation?.Arguments.FirstOrDefault(candidate =>
            !candidate.IsImplicit
            && string.Equals(candidate.Parameter?.Name, parameterName, StringComparison.Ordinal));
        return argument?.Value.Syntax as ExpressionSyntax
            ?? (argument?.Syntax as ArgumentSyntax)?.Expression;
    }

    private static ExpressionSyntax? FindArgumentExpression(
        BaseArgumentListSyntax? argumentList,
        IObjectCreationOperation? operation,
        string parameterName,
        int position)
    {
        var explicitlyNamed = argumentList?.Arguments
            .Where(argument => string.Equals(
                argument.NameColon?.Name.Identifier.ValueText,
                parameterName,
                StringComparison.Ordinal))
            .Select(argument => argument.Expression)
            .FirstOrDefault();
        if (explicitlyNamed is not null)
        {
            return explicitlyNamed;
        }

        if (argumentList is not null
            && position < argumentList.Arguments.Count
            && argumentList.Arguments[position].NameColon is null)
        {
            return argumentList.Arguments[position].Expression;
        }

        return FindArgumentExpression(operation, parameterName);
    }

    private static ExpressionSyntax? FindArgumentExpression(
        IInvocationOperation? operation,
        string parameterName)
    {
        var argument = operation?.Arguments.FirstOrDefault(candidate =>
            !candidate.IsImplicit
            && string.Equals(candidate.Parameter?.Name, parameterName, StringComparison.Ordinal));
        return argument?.Value.Syntax as ExpressionSyntax
            ?? (argument?.Syntax as ArgumentSyntax)?.Expression;
    }

    private static ExpressionSyntax? FindArgumentExpression(
        InvocationExpressionSyntax invocation,
        IInvocationOperation? operation,
        string parameterName) => invocation.ArgumentList.Arguments
        .Where(argument => string.Equals(
            argument.NameColon?.Name.Identifier.ValueText,
            parameterName,
            StringComparison.Ordinal))
        .Select(argument => argument.Expression)
        .FirstOrDefault()
        ?? FindArgumentExpression(operation, parameterName);

    private static IEnumerable<ExpressionSyntax> FindArgumentExpressions(
        InvocationExpressionSyntax invocation,
        string parameterName)
    {
        for (var index = 0; index < invocation.ArgumentList.Arguments.Count; index++)
        {
            var syntax = invocation.ArgumentList.Arguments[index];
            var explicitName = syntax.NameColon?.Name.Identifier.ValueText;
            if (string.Equals(explicitName, parameterName, StringComparison.Ordinal)
                || explicitName is null && parameterName == "linkPath" && index > 0)
            {
                yield return syntax.Expression;
            }
        }
    }

    private static StaticParseKind TryParseConstraintStrength(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out bool isSoft,
        out string? failureReason)
    {
        var operation = semanticModel.GetOperation(creation, cancellationToken) as IObjectCreationOperation;
        var expression = FindArgumentExpression(operation, "strength");
        if (expression is null)
        {
            isSoft = false;
            failureReason = null;
            return StaticParseKind.Success;
        }

        expression = UnwrapParentheses(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return ParseConstraintStrengthExpression(
                initializer,
                semanticModel,
                cancellationToken,
                out isSoft,
                out failureReason);
        }

        return ParseConstraintStrengthExpression(
            expression,
            semanticModel,
            cancellationToken,
            out isSoft,
            out failureReason);
    }

    private static StaticParseKind ParseConstraintStrengthExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out bool isSoft,
        out string? failureReason)
    {
        var status = TryResolveEnumMemberName(
            expression,
            semanticModel,
            cancellationToken,
            DescriptorNamespace + ".ConstraintStrength",
            out var name);
        if (status != StaticParseKind.Success)
        {
            isSoft = false;
            failureReason = status == StaticParseKind.Invalid
                ? "unknown constraint strength"
                : null;
            return status;
        }

        switch (name)
        {
            case "Hard":
                isSoft = false;
                failureReason = null;
                return StaticParseKind.Success;
            case "Soft":
                isSoft = true;
                failureReason = null;
                return StaticParseKind.Success;
            default:
                isSoft = false;
                failureReason = "unknown constraint strength";
                return StaticParseKind.Invalid;
        }
    }

    private static StaticParseKind TryResolveEnumMemberName(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        string expectedTypeName,
        out string name)
    {
        expression = UnwrapParentheses(expression);
        var typeInfo = semanticModel.GetTypeInfo(expression, cancellationToken);
        var enumType = IsType(typeInfo.Type, expectedTypeName)
            ? typeInfo.Type as INamedTypeSymbol
            : IsType(typeInfo.ConvertedType, expectedTypeName)
                ? typeInfo.ConvertedType as INamedTypeSymbol
                : null;
        if (enumType is null)
        {
            name = null!;
            return StaticParseKind.Dynamic;
        }

        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (!constant.HasValue || constant.Value is null)
        {
            name = null!;
            return StaticParseKind.Dynamic;
        }

        var directField = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol as IFieldSymbol;
        if (directField is { HasConstantValue: true }
            && IsType(directField.ContainingType, expectedTypeName)
            && Equals(directField.ConstantValue, constant.Value))
        {
            name = directField.Name;
            return StaticParseKind.Success;
        }

        var matchingField = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => field.HasConstantValue && Equals(field.ConstantValue, constant.Value))
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        if (matchingField is null)
        {
            name = null!;
            return StaticParseKind.Invalid;
        }

        name = matchingField.Name;
        return StaticParseKind.Success;
    }

    private static bool TryParseBigIntegerExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        HashSet<ISymbol> resolving,
        out string text,
        out bool wasStatic)
    {
        expression = UnwrapParentheses(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(identifier, semanticModel, cancellationToken, resolving, out var initializer))
        {
            return TryParseBigIntegerExpression(
                initializer,
                semanticModel,
                cancellationToken,
                resolving,
                out text,
                out wasStatic);
        }

        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (constant.HasValue)
        {
            wasStatic = true;
            text = null!;
            return constant.Value is not null && TryInvariantInteger(constant.Value, out text);
        }

        if (expression is PrefixUnaryExpressionSyntax unary
            && unary.IsKind(SyntaxKind.UnaryMinusExpression)
            && TryParseBigIntegerExpression(
                unary.Operand,
                semanticModel,
                cancellationToken,
                resolving,
                out var magnitude,
                out wasStatic)
            && BigInteger.TryParse(
                magnitude,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var magnitudeValue))
        {
            text = (-magnitudeValue).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        var symbol = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol;
        if (symbol is IPropertySymbol property
            && IsType(property.ContainingType, "System.Numerics.BigInteger")
            && property.Name is "Zero" or "One" or "MinusOne")
        {
            wasStatic = true;
            text = property.Name switch
            {
                "Zero" => "0",
                "One" => "1",
                _ => "-1",
            };
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation
            && symbol is IMethodSymbol method
            && IsType(method.ContainingType, "System.Numerics.BigInteger")
            && method.Name == "Parse"
            && invocation.ArgumentList.Arguments.Count == 1)
        {
            var argument = semanticModel.GetConstantValue(
                invocation.ArgumentList.Arguments[0].Expression,
                cancellationToken);
            if (!argument.HasValue)
            {
                text = null!;
                wasStatic = false;
                return false;
            }

            wasStatic = true;
            if (argument.Value is string integerText
                && BigInteger.TryParse(
                    integerText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var integer))
            {
                text = integer.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            text = null!;
            return false;
        }

        if (expression is BaseObjectCreationExpressionSyntax creation
            && IsType(
                semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                "System.Numerics.BigInteger")
            && creation.ArgumentList is { Arguments.Count: 1 })
        {
            var argument = semanticModel.GetConstantValue(
                creation.ArgumentList.Arguments[0].Expression,
                cancellationToken);
            wasStatic = argument.HasValue;
            text = null!;
            return argument.HasValue
                && argument.Value is not null
                && TryInvariantInteger(argument.Value, out text);
        }

        text = null!;
        wasStatic = false;
        return false;
    }

    private static bool TryParseGenericEnumLiteral(
        IMethodSymbol method,
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out LogicLiteral literal,
        out bool wasStatic)
    {
        if (method.TypeArguments.Length != 1
            || method.TypeArguments[0] is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            literal = null!;
            wasStatic = true;
            return false;
        }

        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (!constant.HasValue || constant.Value is null)
        {
            literal = null!;
            wasStatic = false;
            return false;
        }

        wasStatic = true;
        var fields = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(candidate => candidate.HasConstantValue
                && Equals(candidate.ConstantValue, constant.Value))
            .ToArray();
        if (fields.Length != 1)
        {
            literal = null!;
            return false;
        }

        literal = new LogicLiteral(LogicLiteralKind.Enum, fields[0].Name, enumType.Name);
        return true;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax suppression
                    when suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = suppression.Operand;
                    continue;
                default:
                    return expression;
            }
        }
    }

    private static bool MergeSubject(
        StaticSequence sequence,
        ref string? domainName,
        ref string? objectTypeName)
    {
        if (domainName is null)
        {
            domainName = sequence.DomainName;
            objectTypeName = sequence.ObjectTypeName;
            return true;
        }

        return string.Equals(domainName, sequence.DomainName, StringComparison.Ordinal)
            && string.Equals(objectTypeName, sequence.ObjectTypeName, StringComparison.Ordinal);
    }

    private static string Segment(string value) =>
        value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;

    private static string ResourceKey(StaticResourceKind kind, string name) => kind switch
    {
        StaticResourceKind.Property => "property|" + name,
        StaticResourceKind.Link => "link|" + name,
        StaticResourceKind.Event => "event|" + name,
        StaticResourceKind.External => "external|" + name,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private sealed class StaticSubject
    {
        internal StaticSubject(string domainName, string objectTypeName, string? invalidReason = null)
        {
            DomainName = domainName;
            ObjectTypeName = objectTypeName;
            InvalidReason = invalidReason;
        }

        internal string DomainName { get; }

        internal string ObjectTypeName { get; }

        internal string? InvalidReason { get; }
    }

    private sealed class StaticSequence
    {
        internal StaticSequence(
            string domainName,
            string objectTypeName,
            List<StaticAction> actions,
            List<string> identitySubjectMismatches)
        {
            DomainName = domainName;
            ObjectTypeName = objectTypeName;
            Actions = actions;
            IdentitySubjectMismatches = identitySubjectMismatches;
        }

        internal string DomainName { get; }

        internal string ObjectTypeName { get; }

        internal List<StaticAction> Actions { get; }

        internal List<string> IdentitySubjectMismatches { get; }
    }

    private sealed class StaticAction
    {
        internal StaticAction(
            string domainName,
            string objectTypeName,
            string name,
            StaticPredicate requirement,
            StaticPredicate guarantee,
            HashSet<string> frame,
            string? requiredAuthority,
            List<string> opaqueKeys,
            string? invalidReason,
            Location location)
        {
            DomainName = domainName;
            ObjectTypeName = objectTypeName;
            Name = name;
            Requirement = requirement;
            Guarantee = guarantee;
            Frame = frame;
            RequiredAuthority = requiredAuthority;
            OpaqueKeys = opaqueKeys;
            InvalidReason = invalidReason;
            Location = location;
        }

        internal string DomainName { get; }

        internal string ObjectTypeName { get; }

        internal string Name { get; }

        internal StaticPredicate Requirement { get; }

        internal StaticPredicate Guarantee { get; }

        internal HashSet<string> Frame { get; }

        internal string? RequiredAuthority { get; }

        internal List<string> OpaqueKeys { get; }

        internal string? InvalidReason { get; }

        internal Location Location { get; }
    }

    private sealed class StaticPredicate
    {
        private StaticPredicate(
            LogicFormula formula,
            Dictionary<string, HashSet<string>> atomReads,
            List<string> opaqueKeys,
            string? invalidReason = null)
        {
            Formula = formula;
            AtomReads = atomReads;
            OpaqueKeys = opaqueKeys;
            InvalidReason = invalidReason;
        }

        internal static StaticPredicate True { get; } = new(
            LogicFormula.True,
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
            new List<string>());

        internal static StaticPredicate False { get; } = new(
            LogicFormula.False,
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
            new List<string>());

        internal LogicFormula Formula { get; }

        internal Dictionary<string, HashSet<string>> AtomReads { get; }

        internal List<string> OpaqueKeys { get; }

        internal string? InvalidReason { get; }

        internal static StaticPredicate Invalid(string reason) => new(
            LogicFormula.True,
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
            new List<string>(),
            reason);

        internal static StaticPredicate Comparison(
            LogicResource resource,
            LogicComparisonOperator comparison,
            LogicLiteral literal)
        {
            var reads = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                [resource.Key] = new HashSet<string>(new[] { resource.Key }, StringComparer.Ordinal),
            };
            return new StaticPredicate(
                LogicFormula.Comparison(resource, comparison, literal),
                reads,
                new List<string>());
        }

        internal static StaticPredicate BooleanAtom(string key, params string[] reads)
        {
            var resource = new LogicResource(key, LogicScalarKind.Boolean, false);
            return new StaticPredicate(
                LogicFormula.BooleanAtom(resource),
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
                {
                    [key] = new HashSet<string>(reads, StringComparer.Ordinal),
                },
                new List<string>());
        }

        internal static StaticPredicate Opaque(string semanticKey, string evaluatorKey) => new(
            LogicFormula.Opaque(semanticKey),
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
            new List<string> { evaluatorKey });

        internal static StaticPredicate All(IEnumerable<StaticPredicate> operands) =>
            Junction(operands, all: true);

        internal static StaticPredicate Any(IEnumerable<StaticPredicate> operands) =>
            Junction(operands, all: false);

        internal StaticPredicate Not() => new(
            LogicFormula.Not(Formula),
            CloneReads(AtomReads),
            new List<string>(OpaqueKeys),
            InvalidReason);

        private static StaticPredicate Junction(IEnumerable<StaticPredicate> operands, bool all)
        {
            var array = operands.ToArray();
            var reads = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var operand in array)
            {
                foreach (var pair in operand.AtomReads)
                {
                    if (!reads.TryGetValue(pair.Key, out var existing))
                    {
                        existing = new HashSet<string>(StringComparer.Ordinal);
                        reads.Add(pair.Key, existing);
                    }

                    existing.UnionWith(pair.Value);
                }
            }

            var formula = all
                ? LogicFormula.All(array.Select(operand => operand.Formula))
                : LogicFormula.Any(array.Select(operand => operand.Formula));
            return new StaticPredicate(
                formula,
                reads,
                formula.ContainsOpaque
                    ? array.SelectMany(operand => operand.OpaqueKeys)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(key => key, StringComparer.Ordinal)
                        .ToList()
                    : new List<string>(),
                array.Select(operand => operand.InvalidReason)
                    .FirstOrDefault(reason => reason is not null));
        }

        private static Dictionary<string, HashSet<string>> CloneReads(
            Dictionary<string, HashSet<string>> source) => source.ToDictionary(
                pair => pair.Key,
                pair => new HashSet<string>(pair.Value, StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private enum StaticProofKind
    {
        Closed,
        Opaque,
        Invalid,
    }

    private enum StaticParseKind
    {
        Success,
        Dynamic,
        Invalid,
    }

    private enum StaticResourceKind
    {
        Property,
        Link,
        Event,
        External,
    }

    private sealed class StaticActionProof
    {
        private StaticActionProof(
            StaticAction action,
            StaticProofKind kind,
            LogicFormula requirement,
            LogicFormula effectiveGuarantee,
            string? reason,
            ImmutableArray<string> opaqueKeys,
            bool hasNontrivialEffectiveGuarantee = false)
        {
            Action = action;
            Kind = kind;
            Requirement = requirement;
            EffectiveGuarantee = effectiveGuarantee;
            Reason = reason;
            OpaqueKeys = opaqueKeys;
            HasNontrivialEffectiveGuarantee = hasNontrivialEffectiveGuarantee;
        }

        internal StaticAction Action { get; }

        internal StaticProofKind Kind { get; }

        internal LogicFormula Requirement { get; }

        internal LogicFormula EffectiveGuarantee { get; }

        internal string? Reason { get; }

        internal ImmutableArray<string> OpaqueKeys { get; }

        internal bool HasNontrivialEffectiveGuarantee { get; }

        internal static StaticActionProof Closed(
            StaticAction action,
            LogicFormula requirement,
            LogicFormula effectiveGuarantee,
            bool hasNontrivialEffectiveGuarantee) => new(
                action,
                StaticProofKind.Closed,
                requirement,
                effectiveGuarantee,
                null,
                ImmutableArray<string>.Empty,
                hasNontrivialEffectiveGuarantee);

        internal static StaticActionProof Opaque(StaticAction action) => new(
            action,
            StaticProofKind.Opaque,
            action.Requirement.Formula,
            LogicFormula.Opaque(string.Join("|", action.OpaqueKeys)),
            "custom predicate",
            action.OpaqueKeys.ToImmutableArray());

        internal static StaticActionProof Invalid(StaticAction action, string reason) => new(
            action,
            StaticProofKind.Invalid,
            action.Requirement.Formula,
            LogicFormula.True,
            reason,
            ImmutableArray<string>.Empty);
    }
}

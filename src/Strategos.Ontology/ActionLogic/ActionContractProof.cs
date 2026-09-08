using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.ActionLogic;

internal enum ActionContractProofKind
{
    Closed,
    Opaque,
    Invalid,
}

internal sealed class ActionContractProof
{
    internal ActionContractProof(
        ActionDescriptor action,
        ActionContractProofKind kind,
        ActionPredicate requirement,
        ActionPredicate declaredGuarantee,
        ActionPredicate effectiveGuarantee,
        LogicFormula requirementFormula,
        LogicFormula declaredGuaranteeFormula,
        LogicFormula effectiveGuaranteeFormula,
        ImmutableArray<string> opaqueKeys = default,
        string? reason = null,
        ImmutableArray<KeyValuePair<string, string>> witness = default,
        bool hasNontrivialEffectiveGuarantee = false)
    {
        Action = action;
        Kind = kind;
        Requirement = requirement;
        DeclaredGuarantee = declaredGuarantee;
        EffectiveGuarantee = effectiveGuarantee;
        RequirementFormula = requirementFormula;
        DeclaredGuaranteeFormula = declaredGuaranteeFormula;
        EffectiveGuaranteeFormula = effectiveGuaranteeFormula;
        OpaqueKeys = opaqueKeys.IsDefault ? ImmutableArray<string>.Empty : opaqueKeys;
        Reason = reason;
        Witness = witness.IsDefault
            ? ImmutableArray<KeyValuePair<string, string>>.Empty
            : witness;
        HasNontrivialEffectiveGuarantee = hasNontrivialEffectiveGuarantee;
    }

    internal ActionDescriptor Action { get; }

    internal ActionContractProofKind Kind { get; }

    internal ActionPredicate Requirement { get; }

    internal ActionPredicate DeclaredGuarantee { get; }

    internal ActionPredicate EffectiveGuarantee { get; }

    internal LogicFormula RequirementFormula { get; }

    internal LogicFormula DeclaredGuaranteeFormula { get; }

    internal LogicFormula EffectiveGuaranteeFormula { get; }

    internal ImmutableArray<string> OpaqueKeys { get; }

    internal string? Reason { get; }

    internal ImmutableArray<KeyValuePair<string, string>> Witness { get; }

    internal bool HasNontrivialEffectiveGuarantee { get; }
}

internal static class ActionContractProofEngine
{
    internal static ActionContractProof Analyze(
        ActionDescriptor action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryValidateContractShape(action, cancellationToken, out var shapeFailure))
        {
            return Invalid(
                action,
                ActionPredicate.True,
                ActionPredicate.True,
                shapeFailure!);
        }

        var requirement = ActionPredicate.All(
            action.Preconditions
                .Where(precondition => precondition.Strength == ConstraintStrength.Hard)
                .Select(precondition => precondition.Predicate));
        var createdLinks = action.Postconditions
            .Where(postcondition => postcondition.Kind == PostconditionKind.CreatesLink)
            .Select(postcondition => postcondition.LinkName)
            .ToArray();
        if (createdLinks.Any(string.IsNullOrWhiteSpace))
        {
            return Invalid(
                action,
                requirement,
                ActionPredicate.True,
                "A CreatesLink postcondition must name the created link.");
        }

        var derivedFacts = createdLinks
            .Select(linkName => ActionPredicate.LinkExists(linkName!));
        var declaredGuarantee = ActionPredicate.All(
            action.Ensures.Select(guarantee => guarantee.Predicate)
                .Concat(derivedFacts));
        var opaqueKeys = action.Preconditions
            .Where(precondition => precondition.Strength == ConstraintStrength.Hard)
            .SelectMany(precondition => FindOpaqueKeys(precondition.Predicate, cancellationToken))
            .Concat(action.Ensures.SelectMany(guarantee =>
                FindOpaqueKeys(guarantee.Predicate, cancellationToken)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToImmutableArray();
        var adapter = new ActionPredicateLogicAdapter();
        var requirementFormula = adapter.ToLogic(requirement, cancellationToken);
        var declaredGuaranteeFormula = adapter.ToLogic(declaredGuarantee, cancellationToken);

        if (!FiniteDomainSolver.TryValidateResourceDomains(
                [requirementFormula, declaredGuaranteeFormula],
                out var domainFailure,
                cancellationToken))
        {
            return Invalid(
                action,
                requirement,
                declaredGuarantee,
                domainFailure ?? "The action uses inconsistent predicate resource domains.",
                requirementFormula);
        }

        var writtenResourceKeys = adapter.GetWrittenLogicResourceKeys(action.TouchedResources);

        if (!opaqueKeys.IsEmpty)
        {
            var abstractRequirementDecision = FiniteDomainSolver.IsSatisfiable(
                FiniteDomainSolver.AbstractOpaqueTerms(requirementFormula, cancellationToken),
                cancellationToken);
            if (abstractRequirementDecision.Kind == LogicDecisionKind.Unsatisfiable)
            {
                return InvalidFromDecision(
                    action,
                    requirement,
                    declaredGuarantee,
                    requirementFormula,
                    abstractRequirementDecision,
                    "The hard requirements are contradictory independently of their custom predicates.");
            }

            if (abstractRequirementDecision.Kind != LogicDecisionKind.Satisfiable)
            {
                return InvalidFromDecision(
                    action,
                    requirement,
                    declaredGuarantee,
                    requirementFormula,
                    abstractRequirementDecision,
                    "The hard requirements could not be validated independently of their custom predicates.");
            }

            var abstractGuaranteeDecision = FiniteDomainSolver.IsSatisfiable(
                FiniteDomainSolver.AbstractOpaqueTerms(declaredGuaranteeFormula, cancellationToken),
                cancellationToken);
            if (abstractGuaranteeDecision.Kind == LogicDecisionKind.Unsatisfiable)
            {
                return InvalidFromDecision(
                    action,
                    requirement,
                    declaredGuarantee,
                    requirementFormula,
                    abstractGuaranteeDecision,
                    "The declared guarantees are contradictory independently of their custom predicates.");
            }

            if (abstractGuaranteeDecision.Kind != LogicDecisionKind.Satisfiable)
            {
                return InvalidFromDecision(
                    action,
                    requirement,
                    declaredGuarantee,
                    requirementFormula,
                    abstractGuaranteeDecision,
                    "The declared guarantees could not be validated independently of their custom predicates.");
            }

            if (!FiniteDomainSolver.TryFindDefiniteFrameViolation(
                    requirementFormula,
                    declaredGuaranteeFormula,
                    writtenResourceKeys,
                    out var opaqueFrameDecision,
                    out var opaqueProjectionFailure,
                    cancellationToken))
            {
                return Invalid(
                    action,
                    requirement,
                    declaredGuarantee,
                    opaqueProjectionFailure ?? "The opaque guarantee frame could not be projected.",
                    requirementFormula,
                    opaqueFrameDecision.Witness);
            }

            if (opaqueFrameDecision.Kind == LogicDecisionKind.Satisfiable)
            {
                return Invalid(
                    action,
                    requirement,
                    declaredGuarantee,
                    "The guarantee constrains untouched state beyond what the requirements establish, "
                    + "independently of custom predicates.",
                    requirementFormula,
                    opaqueFrameDecision.Witness);
            }

            return new ActionContractProof(
                action,
                ActionContractProofKind.Opaque,
                requirement,
                declaredGuarantee,
                ActionPredicate.True,
                requirementFormula,
                declaredGuaranteeFormula,
                LogicFormula.Opaque(string.Join("|", opaqueKeys)),
                opaqueKeys,
                "The action contains one or more custom predicates and is excluded from static proof.");
        }

        var requirementSatisfiability = FiniteDomainSolver.IsSatisfiable(
            requirementFormula,
            cancellationToken);
        if (requirementSatisfiability.Kind != LogicDecisionKind.Satisfiable)
        {
            return InvalidFromDecision(
                action,
                requirement,
                declaredGuarantee,
                requirementFormula,
                requirementSatisfiability,
                requirementSatisfiability.Kind == LogicDecisionKind.Unsatisfiable
                    ? "The hard requirements are contradictory."
                    : "The hard requirements are invalid.");
        }

        var guaranteeSatisfiability = FiniteDomainSolver.IsSatisfiable(
            declaredGuaranteeFormula,
            cancellationToken);
        if (guaranteeSatisfiability.Kind != LogicDecisionKind.Satisfiable)
        {
            return InvalidFromDecision(
                action,
                requirement,
                declaredGuarantee,
                requirementFormula,
                guaranteeSatisfiability,
                guaranteeSatisfiability.Kind == LogicDecisionKind.Unsatisfiable
                    ? "The declared guarantees are contradictory."
                    : "The declared guarantees are invalid.");
        }

        if (!FiniteDomainSolver.TryForget(
                declaredGuaranteeFormula,
                writtenResourceKeys,
                out var realizableGuarantee,
                out var projectionFailure,
                cancellationToken))
        {
            return Invalid(
                action,
                requirement,
                declaredGuarantee,
                projectionFailure ?? "The guarantee frame could not be projected.",
                requirementFormula);
        }

        var frameDecision = FiniteDomainSolver.Implies(
            requirementFormula,
            realizableGuarantee,
            cancellationToken);
        if (frameDecision.Kind != LogicDecisionKind.Unsatisfiable)
        {
            var reason = frameDecision.Kind == LogicDecisionKind.Satisfiable
                ? "The guarantee constrains untouched state beyond what the requirements establish."
                : frameDecision.Reason ?? "The action frame is not realizable.";
            return Invalid(
                action,
                requirement,
                declaredGuarantee,
                reason,
                requirementFormula,
                frameDecision.Witness);
        }

        if (!FiniteDomainSolver.TryForget(
                requirementFormula,
                writtenResourceKeys,
                out var preservedRequirement,
                out projectionFailure,
                cancellationToken))
        {
            return Invalid(
                action,
                requirement,
                declaredGuarantee,
                projectionFailure ?? "The requirement frame could not be projected.",
                requirementFormula);
        }

        var effectiveGuaranteeFormula = LogicFormula.All(
            declaredGuaranteeFormula,
            preservedRequirement);
        var negatedEffectiveGuarantee = FiniteDomainSolver.IsSatisfiable(
            LogicFormula.Not(effectiveGuaranteeFormula),
            cancellationToken);
        if (negatedEffectiveGuarantee.Kind is not LogicDecisionKind.Satisfiable
            and not LogicDecisionKind.Unsatisfiable)
        {
            return InvalidFromDecision(
                action,
                requirement,
                declaredGuarantee,
                requirementFormula,
                negatedEffectiveGuarantee,
                "The effective guarantee could not be classified for composability coverage.");
        }

        return new ActionContractProof(
            action,
            ActionContractProofKind.Closed,
            requirement,
            declaredGuarantee,
            adapter.ToPredicate(effectiveGuaranteeFormula),
            requirementFormula,
            declaredGuaranteeFormula,
            effectiveGuaranteeFormula,
            hasNontrivialEffectiveGuarantee:
                negatedEffectiveGuarantee.Kind == LogicDecisionKind.Satisfiable);
    }

    private static bool TryValidateContractShape(
        ActionDescriptor action,
        CancellationToken cancellationToken,
        out string? failureReason)
    {
        if (action.Preconditions.Any(precondition => precondition is null))
        {
            failureReason = "The precondition collection contains a null entry.";
            return false;
        }

        if (action.Ensures.Any(guarantee => guarantee is null))
        {
            failureReason = "The guarantee collection contains a null entry.";
            return false;
        }

        if (action.Postconditions.Any(postcondition => postcondition is null))
        {
            failureReason = "The postcondition collection contains a null entry.";
            return false;
        }

        if (action.TouchedResources.Any(resource => resource is null))
        {
            failureReason = "The action frame contains a null resource.";
            return false;
        }

        foreach (var precondition in action.Preconditions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(typeof(ConstraintStrength), precondition.Strength))
            {
                failureReason = $"Precondition '{precondition.Expression}' has unknown constraint strength "
                    + $"'{(int)precondition.Strength}'.";
                return false;
            }
        }

        if (action.RequiredAuthority is not null && string.IsNullOrWhiteSpace(action.RequiredAuthority))
        {
            failureReason = "The required authority name cannot be empty.";
            return false;
        }

        var frame = new HashSet<ActionResource>();
        foreach (var resource in action.TouchedResources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(typeof(ActionResourceKind), resource.Kind))
            {
                failureReason = $"The action frame contains unknown resource kind '{(int)resource.Kind}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(resource.Name))
            {
                failureReason = $"The declared {resource.Kind} resource name is empty.";
                return false;
            }

            frame.Add(resource);
        }

        foreach (var postcondition in action.Postconditions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(typeof(PostconditionKind), postcondition.Kind))
            {
                failureReason = $"The action declares unknown postcondition kind '{(int)postcondition.Kind}'.";
                return false;
            }

            var mutated = postcondition.Kind switch
            {
                PostconditionKind.ModifiesProperty when !string.IsNullOrWhiteSpace(postcondition.PropertyName) =>
                    ActionResource.Property(postcondition.PropertyName),
                PostconditionKind.CreatesLink when !string.IsNullOrWhiteSpace(postcondition.LinkName) =>
                    ActionResource.Link(postcondition.LinkName),
                PostconditionKind.EmitsEvent when !string.IsNullOrWhiteSpace(postcondition.EventTypeName) =>
                    ActionResource.Event(postcondition.EventTypeName),
                _ => null,
            };
            if (mutated is null)
            {
                failureReason = postcondition.Kind switch
                {
                    PostconditionKind.ModifiesProperty => "A ModifiesProperty postcondition must name the property.",
                    PostconditionKind.CreatesLink => "A CreatesLink postcondition must name the created link.",
                    PostconditionKind.EmitsEvent => "An EmitsEvent postcondition must name the event type.",
                    _ => "The postcondition is invalid.",
                };
                return false;
            }

            if (!frame.Contains(mutated))
            {
                failureReason = $"Postcondition mutates '{mutated.Kind}:{mutated.Name}' outside the declared frame.";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    private static ActionContractProof InvalidFromDecision(
        ActionDescriptor action,
        ActionPredicate requirement,
        ActionPredicate declaredGuarantee,
        LogicFormula requirementFormula,
        LogicDecision decision,
        string fallbackReason) => Invalid(
            action,
            requirement,
            declaredGuarantee,
            decision.Reason ?? fallbackReason,
            requirementFormula,
            decision.Witness);

    private static ActionContractProof Invalid(
        ActionDescriptor action,
        ActionPredicate requirement,
        ActionPredicate declaredGuarantee,
        string reason,
        LogicFormula? requirementFormula = null,
        ImmutableArray<KeyValuePair<string, string>> witness = default) => new(
            action,
            ActionContractProofKind.Invalid,
            requirement,
            declaredGuarantee,
            ActionPredicate.True,
            requirementFormula ?? LogicFormula.True,
            LogicFormula.True,
            LogicFormula.True,
            reason: reason,
            witness: witness);

    private static IEnumerable<string> FindOpaqueKeys(
        ActionPredicate predicate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (predicate)
        {
            case CustomPredicate custom:
                yield return custom.EvaluatorKey;
                break;
            case AllPredicate all:
                foreach (var key in all.Operands.SelectMany(operand =>
                             FindOpaqueKeys(operand, cancellationToken)))
                {
                    yield return key;
                }

                break;
            case AnyPredicate any:
                foreach (var key in any.Operands.SelectMany(operand =>
                             FindOpaqueKeys(operand, cancellationToken)))
                {
                    yield return key;
                }

                break;
            case NotPredicate not:
                foreach (var key in FindOpaqueKeys(not.Operand, cancellationToken))
                {
                    yield return key;
                }

                break;
        }
    }
}

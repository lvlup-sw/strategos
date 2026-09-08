using System.Collections.Immutable;

using Strategos.Ontology.ActionLogic;

namespace Strategos.Ontology.Descriptors;

/// <summary>Pure composition operations over action contracts.</summary>
public static class ActionCalculus
{
    /// <summary>
    /// Proves that an executable action is a behavioral refinement of a declared
    /// action specification.
    /// </summary>
    public static ActionRefinementAnalysis AnalyzeRefinement(
        ActionDescriptor specification,
        ActionDescriptor implementation,
        AuthorityLattice authorityLattice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(implementation);
        ArgumentNullException.ThrowIfNull(authorityLattice);
        cancellationToken.ThrowIfCancellationRequested();
        var proof = ActionContractProofEngine.Analyze(implementation, cancellationToken);
        AuthorityRequirement implementationAuthority;
        try
        {
            implementationAuthority = implementation.RequiredAuthority is null
                ? authorityLattice.Join([])
                : authorityLattice.Join(implementation.RequiredAuthority);
        }
        catch (KeyNotFoundException exception)
        {
            return RefinementResult(
                ActionRefinementStatus.Invalid,
                ActionRefinementObligation.ImplementationContract,
                exception.Message);
        }

        return AnalyzeRefinement(
            specification,
            proof.Action.Subject,
            proof.Requirement,
            proof.Kind == ActionContractProofKind.Closed ? proof.EffectiveGuarantee : null,
            implementationAuthority,
            new ActionFrame(implementation.TouchedResources),
            proof.Kind switch
            {
                ActionContractProofKind.Closed => ActionContractVerificationStatus.Proven,
                ActionContractProofKind.Opaque => ActionContractVerificationStatus.PartiallyVerified,
                _ => ActionContractVerificationStatus.Refuted,
            },
            proof.Reason,
            authorityLattice,
            cancellationToken);
    }

    /// <summary>
    /// Proves that a composed workflow contract is a behavioral refinement of a
    /// declared action specification.
    /// </summary>
    public static ActionRefinementAnalysis AnalyzeRefinement(
        ActionDescriptor specification,
        CompositeActionContract implementation,
        AuthorityLattice authorityLattice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(implementation);
        ArgumentNullException.ThrowIfNull(authorityLattice);
        cancellationToken.ThrowIfCancellationRequested();
        return AnalyzeRefinement(
            specification,
            implementation.Subject,
            implementation.FirstRequirement,
            implementation.FinalGuarantee,
            implementation.RequiredAuthority,
            implementation.Frame,
            implementation.VerificationStatus,
            implementation.VerificationStatus == ActionContractVerificationStatus.Refuted
                ? "The implementation composite contains a refuted seam."
                : null,
            authorityLattice,
            cancellationToken);
    }

    /// <summary>Creates the distinct empty sequential operand for a subject.</summary>
    public static ActionCompositionOperand Identity(ActionSubject subject) =>
        ActionCompositionOperand.Identity(subject);

    /// <summary>Analyzes a flattened sequence without throwing for contract failures.</summary>
    public static ActionCompositionAnalysis AnalyzeSequential(
        AuthorityLattice authorityLattice,
        params ActionCompositionOperand[] operands) =>
        AnalyzeSequential(authorityLattice, CancellationToken.None, operands);

    /// <summary>Analyzes a statically materializable operand sequence without throwing.</summary>
    public static ActionCompositionAnalysis AnalyzeSequential(
        AuthorityLattice authorityLattice,
        IEnumerable<ActionCompositionOperand> operands) =>
        AnalyzeSequential(authorityLattice, CancellationToken.None, operands);

    /// <summary>Analyzes an operand sequence with cooperative proof cancellation.</summary>
    public static ActionCompositionAnalysis AnalyzeSequential(
        AuthorityLattice authorityLattice,
        CancellationToken cancellationToken,
        IEnumerable<ActionCompositionOperand> operands)
    {
        ArgumentNullException.ThrowIfNull(operands);
        cancellationToken.ThrowIfCancellationRequested();
        var materialized = new List<ActionCompositionOperand>();
        foreach (var operand in operands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            materialized.Add(operand);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return AnalyzeSequential(authorityLattice, cancellationToken, materialized.ToArray());
    }

    /// <summary>Analyzes a flattened sequence with cooperative proof cancellation.</summary>
    public static ActionCompositionAnalysis AnalyzeSequential(
        AuthorityLattice authorityLattice,
        CancellationToken cancellationToken,
        params ActionCompositionOperand[] operands)
    {
        ArgumentNullException.ThrowIfNull(authorityLattice);
        ArgumentNullException.ThrowIfNull(operands);
        cancellationToken.ThrowIfCancellationRequested();

        if (operands.Length == 0)
        {
            return Invalid("A sequential composition requires an action, nested composite, or explicit identity operand.");
        }

        var actions = ImmutableArray.CreateBuilder<ActionDescriptor>();
        ActionSubject? subject = null;
        foreach (var operand in operands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operand is null)
            {
                return Invalid("A sequential composition cannot contain a null operand.");
            }

            var operandSubject = GetSubject(operand);
            if (subject is null)
            {
                subject = operandSubject;
            }
            else if (!subject.Equals(operandSubject))
            {
                return Invalid(
                    $"Sequential composition is restricted to one subject: '{subject}' and '{operandSubject}' do not match.");
            }

            if (operand.Action is not null)
            {
                actions.Add(operand.Action);
            }
            else if (operand.Composite is { IsIdentity: false } composite)
            {
                actions.AddRange(composite.Actions);
            }
        }

        if (subject is null)
        {
            return Invalid("A sequential composition operand did not declare a subject.");
        }

        if (actions.Count == 0)
        {
            var identityContract = new CompositeActionContract(
                subject,
                [],
                ActionPredicate.True,
                ActionPredicate.True,
                authorityLattice.Join([]),
                ActionFrame.Empty,
                ActionContractVerificationStatus.Identity,
                [],
                [],
                isIdentity: true);
            return new ActionCompositionAnalysis(
                ActionCompositionAnalysisStatus.Proven,
                identityContract,
                [],
                []);
        }

        var proofs = actions
            .Select(action => ActionContractProofEngine.Analyze(action, cancellationToken))
            .ToImmutableArray();
        var invalidProofs = proofs
            .Where(proof => proof.Kind == ActionContractProofKind.Invalid)
            .ToImmutableArray();
        if (!invalidProofs.IsEmpty)
        {
            return new ActionCompositionAnalysis(
                ActionCompositionAnalysisStatus.Invalid,
                null,
                [],
                invalidProofs.Select(proof =>
                    $"Invalid contract for '{proof.Action.Subject}/{proof.Action.Name}': {proof.Reason}"));
        }

        var exclusions = proofs
            .Where(proof => proof.Kind == ActionContractProofKind.Opaque)
            .Select(proof => new ActionCompositionExclusion(
                proof.Action.Subject,
                proof.Action.Name,
                proof.OpaqueKeys))
            .ToImmutableArray();
        var seams = ImmutableArray.CreateBuilder<ActionCompositionSeamResult>();
        var proofErrors = ImmutableArray.CreateBuilder<string>();
        for (var index = 0; index + 1 < proofs.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var upstream = proofs[index];
            var downstream = proofs[index + 1];
            if (upstream.Kind == ActionContractProofKind.Opaque
                || downstream.Kind == ActionContractProofKind.Opaque)
            {
                seams.Add(new ActionCompositionSeamResult(
                    upstream.Action,
                    downstream.Action,
                    ActionCompositionSeamStatus.Opaque,
                    message: "A custom predicate makes this seam runtime-only."));
                continue;
            }

            var decision = FiniteDomainSolver.Implies(
                upstream.EffectiveGuaranteeFormula,
                downstream.RequirementFormula,
                cancellationToken);
            if (decision.Kind == LogicDecisionKind.Unsatisfiable)
            {
                seams.Add(new ActionCompositionSeamResult(
                    upstream.Action,
                    downstream.Action,
                    ActionCompositionSeamStatus.Proven));
                continue;
            }

            if (decision.Kind == LogicDecisionKind.Satisfiable)
            {
                var facts = decision.Witness
                    .Select(pair => new ActionCounterexampleFact(pair.Key, pair.Value))
                    .ToImmutableArray();
                var message = IllegalSeamMessage(upstream.Action, downstream.Action, facts);
                seams.Add(new ActionCompositionSeamResult(
                    upstream.Action,
                    downstream.Action,
                    ActionCompositionSeamStatus.Refuted,
                    facts,
                    message));
                proofErrors.Add(message);
                continue;
            }

            return Invalid(
                decision.Reason
                ?? $"The seam '{upstream.Action.Name}' to '{downstream.Action.Name}' could not be decided.");
        }

        var frame = actions.Aggregate(
            ActionFrame.Empty,
            (current, action) => current.Union(new ActionFrame(action.TouchedResources)));
        var requiredAuthorities = actions
            .Select(action => action.RequiredAuthority)
            .Where(authority => authority is not null)
            .Cast<string>()
            .ToImmutableArray();
        AuthorityRequirement requiredAuthority;
        try
        {
            foreach (var authority in requiredAuthorities)
            {
                _ = authorityLattice.Resolve(authority);
            }

            requiredAuthority = authorityLattice.Join(requiredAuthorities);
        }
        catch (KeyNotFoundException exception)
        {
            return Invalid(exception.Message);
        }

        var hasOpaque = !exclusions.IsEmpty;
        var contract = new CompositeActionContract(
            subject,
            actions,
            proofs[0].Requirement,
            proofs[proofs.Length - 1].Kind == ActionContractProofKind.Closed
                ? proofs[proofs.Length - 1].EffectiveGuarantee
                : null,
            requiredAuthority,
            frame,
            proofErrors.Count != 0
                ? ActionContractVerificationStatus.Refuted
                : hasOpaque
                ? ActionContractVerificationStatus.PartiallyVerified
                : ActionContractVerificationStatus.Proven,
            seams,
            exclusions,
            isIdentity: false);

        if (proofErrors.Count != 0)
        {
            return new ActionCompositionAnalysis(
                ActionCompositionAnalysisStatus.Refuted,
                contract,
                seams,
                proofErrors);
        }

        return new ActionCompositionAnalysis(
            hasOpaque
                ? ActionCompositionAnalysisStatus.PartiallyVerified
                : ActionCompositionAnalysisStatus.Proven,
            contract,
            seams,
            []);
    }

    /// <summary>
    /// Constructs a sequential composite, throwing only for invalid contracts
    /// or refuted seams. Opaque seams produce a partially verified contract.
    /// </summary>
    public static CompositeActionContract Sequential(
        AuthorityLattice authorityLattice,
        params ActionCompositionOperand[] operands)
    {
        var analysis = AnalyzeSequential(authorityLattice, operands);
        if (!analysis.CanCompose || analysis.Contract is null)
        {
            throw new ActionCompositionException(analysis);
        }

        return analysis.Contract;
    }

    /// <summary>
    /// Constructs a sequential composite from a materialized operand sequence.
    /// </summary>
    public static CompositeActionContract Sequential(
        AuthorityLattice authorityLattice,
        IEnumerable<ActionCompositionOperand> operands)
    {
        var analysis = AnalyzeSequential(authorityLattice, operands);
        if (!analysis.CanCompose || analysis.Contract is null)
        {
            throw new ActionCompositionException(analysis);
        }

        return analysis.Contract;
    }

    /// <summary>
    /// Derives the rollback order for a completed forward prefix.
    /// </summary>
    public static ImmutableArray<string> DeriveRollbackPlan(
        IEnumerable<ActionDescriptor> completedForwardPrefix)
    {
        ArgumentNullException.ThrowIfNull(completedForwardPrefix);
        var actions = completedForwardPrefix.ToArray();
        if (actions.Any(action => string.IsNullOrWhiteSpace(action.CompensatingActionName)))
        {
            throw new InvalidOperationException(
                "Every completed action must name a compensating action before a rollback plan can be derived.");
        }

        return actions
            .Reverse()
            .Select(action => action.CompensatingActionName!)
            .ToImmutableArray();
    }

    /// <summary>Checks an authored rollback sequence against the derived plan.</summary>
    public static bool AuthoredRollbackAgrees(
        IEnumerable<ActionDescriptor> completedForwardPrefix,
        IEnumerable<string> authoredRollback)
    {
        ArgumentNullException.ThrowIfNull(authoredRollback);
        return DeriveRollbackPlan(completedForwardPrefix)
            .SequenceEqual(authoredRollback, StringComparer.Ordinal);
    }

    private static ActionSubject GetSubject(ActionCompositionOperand operand)
    {
        if (operand.Action is not null)
        {
            return operand.Action.Subject;
        }

        if (operand.Composite is not null)
        {
            return operand.Composite.Subject;
        }

        return operand.IdentitySubject
            ?? throw new InvalidOperationException("Unknown composition operand variant.");
    }

    private static ActionCompositionAnalysis Invalid(string message) => new(
        ActionCompositionAnalysisStatus.Invalid,
        null,
        [],
        [message]);

    private static ActionRefinementAnalysis AnalyzeRefinement(
        ActionDescriptor specification,
        ActionSubject implementationSubject,
        ActionPredicate implementationRequirement,
        ActionPredicate? implementationGuarantee,
        AuthorityRequirement implementationAuthority,
        ActionFrame implementationFrame,
        ActionContractVerificationStatus implementationStatus,
        string? implementationFailure,
        AuthorityLattice authorityLattice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(implementationSubject);
        ArgumentNullException.ThrowIfNull(implementationRequirement);
        ArgumentNullException.ThrowIfNull(implementationAuthority);
        ArgumentNullException.ThrowIfNull(implementationFrame);
        ArgumentNullException.ThrowIfNull(authorityLattice);
        cancellationToken.ThrowIfCancellationRequested();

        var specificationProof = ActionContractProofEngine.Analyze(specification, cancellationToken);
        if (specificationProof.Kind == ActionContractProofKind.Invalid)
        {
            return RefinementResult(
                ActionRefinementStatus.Invalid,
                ActionRefinementObligation.SpecificationContract,
                specificationProof.Reason ?? "The specification contract is invalid.");
        }

        if (implementationStatus == ActionContractVerificationStatus.Refuted)
        {
            return RefinementResult(
                ActionRefinementStatus.Invalid,
                ActionRefinementObligation.ImplementationContract,
                implementationFailure ?? "The implementation contract is invalid or refuted.");
        }

        if (!Enum.IsDefined(implementationStatus))
        {
            return RefinementResult(
                ActionRefinementStatus.Invalid,
                ActionRefinementObligation.ImplementationContract,
                $"The implementation contract has unknown verification status '{implementationStatus}'.");
        }

        var failures = ImmutableArray.CreateBuilder<ActionRefinementFailure>();
        var hasInvalidObligation = false;
        var hasRefutedObligation = false;
        var hasOpaqueObligation = false;
        if (implementationStatus == ActionContractVerificationStatus.PartiallyVerified)
        {
            failures.Add(new ActionRefinementFailure(
                ActionRefinementObligation.ImplementationContract,
                "A custom predicate leaves part of the implementation contract runtime-only."));
            hasOpaqueObligation = true;
        }

        if (!specification.Subject.Equals(implementationSubject))
        {
            failures.Add(new ActionRefinementFailure(
                ActionRefinementObligation.Subject,
                $"Implementation subject '{implementationSubject}' does not match specification subject "
                + $"'{specification.Subject}'."));
            hasRefutedObligation = true;
        }

        var adapter = new ActionPredicateLogicAdapter();
        var implementationRequirementFormula = adapter.ToLogic(
            implementationRequirement,
            cancellationToken);
        if (specificationProof.RequirementFormula.ContainsOpaque
            || implementationRequirementFormula.ContainsOpaque)
        {
            failures.Add(new ActionRefinementFailure(
                ActionRefinementObligation.Requirements,
                "A custom predicate prevents a complete requirements-refinement proof."));
            hasOpaqueObligation = true;
        }
        else
        {
            AddImplicationFailure(
                failures,
                ActionRefinementObligation.Requirements,
                FiniteDomainSolver.Implies(
                    specificationProof.RequirementFormula,
                    implementationRequirementFormula,
                    cancellationToken),
                "The specification requirement does not imply the implementation requirement.",
                ref hasInvalidObligation,
                ref hasRefutedObligation,
                ref hasOpaqueObligation);
        }

        if (implementationGuarantee is null
            || specificationProof.DeclaredGuaranteeFormula.ContainsOpaque)
        {
            failures.Add(new ActionRefinementFailure(
                ActionRefinementObligation.Guarantees,
                "A custom predicate prevents a complete guarantees-refinement proof."));
            hasOpaqueObligation = true;
        }
        else
        {
            var implementationGuaranteeFormula = adapter.ToLogic(
                implementationGuarantee,
                cancellationToken);
            if (implementationGuaranteeFormula.ContainsOpaque)
            {
                failures.Add(new ActionRefinementFailure(
                    ActionRefinementObligation.Guarantees,
                    "A custom predicate prevents a complete guarantees-refinement proof."));
                hasOpaqueObligation = true;
            }
            else
            {
                AddImplicationFailure(
                    failures,
                    ActionRefinementObligation.Guarantees,
                    FiniteDomainSolver.Implies(
                        implementationGuaranteeFormula,
                        specificationProof.DeclaredGuaranteeFormula,
                        cancellationToken),
                    "The implementation guarantee does not imply the specification guarantee.",
                    ref hasInvalidObligation,
                    ref hasRefutedObligation,
                    ref hasOpaqueObligation);
            }
        }

        AuthorityRequirement? specificationAuthority = null;
        try
        {
            specificationAuthority = specification.RequiredAuthority is null
                ? authorityLattice.Join([])
                : authorityLattice.Join(specification.RequiredAuthority);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or ArgumentException)
        {
            failures.Add(new ActionRefinementFailure(
                ActionRefinementObligation.SpecificationContract,
                exception.Message));
            hasInvalidObligation = true;
        }

        if (specificationAuthority is not null)
        {
            try
            {
                if (!authorityLattice.IsAtMost(implementationAuthority, specificationAuthority))
                {
                    failures.Add(new ActionRefinementFailure(
                        ActionRefinementObligation.Authority,
                        "The implementation requires more authority than the specification permits."));
                    hasRefutedObligation = true;
                }
            }
            catch (ArgumentException exception)
            {
                failures.Add(new ActionRefinementFailure(
                    ActionRefinementObligation.ImplementationContract,
                    exception.Message));
                hasInvalidObligation = true;
            }
        }

        var specificationFrame = new ActionFrame(specification.TouchedResources);
        var outsideFrame = implementationFrame.Resources
            .Where(resource => !specificationFrame.Contains(resource))
            .ToArray();
        if (outsideFrame.Length != 0)
        {
            failures.Add(new ActionRefinementFailure(
                ActionRefinementObligation.Frame,
                "The implementation writes outside the specification frame: "
                + string.Join(", ", outsideFrame.Select(resource => $"{resource.Kind}:{resource.Name}"))
                + "."));
            hasRefutedObligation = true;
        }

        return new ActionRefinementAnalysis(
            hasInvalidObligation
                ? ActionRefinementStatus.Invalid
                : hasRefutedObligation
                    ? ActionRefinementStatus.Refuted
                    : hasOpaqueObligation
                        ? ActionRefinementStatus.Opaque
                        : ActionRefinementStatus.Proven,
            failures);
    }

    private static ActionRefinementAnalysis RefinementResult(
        ActionRefinementStatus status,
        ActionRefinementObligation obligation,
        string message) => new(
            status,
            [new ActionRefinementFailure(obligation, message)]);

    private static void AddImplicationFailure(
        ICollection<ActionRefinementFailure> failures,
        ActionRefinementObligation obligation,
        LogicDecision decision,
        string fallbackMessage,
        ref bool hasInvalidObligation,
        ref bool hasRefutedObligation,
        ref bool hasOpaqueObligation)
    {
        if (decision.Kind == LogicDecisionKind.Unsatisfiable)
        {
            return;
        }

        failures.Add(new ActionRefinementFailure(
            obligation,
            decision.Kind == LogicDecisionKind.Satisfiable
                ? fallbackMessage
                : decision.Reason ?? "The implication could not be decided.",
            decision.Witness.Select(pair => new ActionCounterexampleFact(pair.Key, pair.Value))));
        switch (decision.Kind)
        {
            case LogicDecisionKind.Satisfiable:
                hasRefutedObligation = true;
                break;
            case LogicDecisionKind.Opaque:
                hasOpaqueObligation = true;
                break;
            default:
                hasInvalidObligation = true;
                break;
        }
    }

    private static string IllegalSeamMessage(
        ActionDescriptor upstream,
        ActionDescriptor downstream,
        ImmutableArray<ActionCounterexampleFact> facts)
    {
        var witness = facts.IsEmpty
            ? "<empty state>"
            : string.Join(", ", facts.Select(fact => $"{fact.Resource}={fact.Value}"));
        return $"Illegal action seam '{upstream.Subject}/{upstream.Name}' -> "
            + $"'{downstream.Subject}/{downstream.Name}'; counterexample: {witness}.";
    }
}

using System.Collections.Immutable;

using Strategos.Ontology.ActionLogic;

namespace Strategos.Ontology.Descriptors;

/// <summary>Pure composition operations over action contracts.</summary>
public static class ActionCalculus
{
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

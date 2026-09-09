using System.Collections.Immutable;

using Strategos.Ontology.ActionLogic;

namespace Strategos.Ontology.Descriptors;

/// <summary>Pure composition operations over action contracts.</summary>
public static class ActionCalculus
{
    /// <summary>Derives the inverse of a forward action without an authored inverse.</summary>
    public static ActionInverseAnalysis AnalyzeInverse(
        ActionDescriptor forwardAction,
        AuthorityLattice authorityLattice) =>
        AnalyzeInverse(forwardAction, null, authorityLattice, default);

    /// <summary>Derives the inverse of a forward action without an authored inverse.</summary>
    public static ActionInverseAnalysis AnalyzeInverse(
        ActionDescriptor forwardAction,
        AuthorityLattice authorityLattice,
        CancellationToken cancellationToken) =>
        AnalyzeInverse(forwardAction, null, authorityLattice, cancellationToken);

    /// <summary>Derives and proves an authored inverse using the supplied authority lattice.</summary>
    public static ActionInverseAnalysis AnalyzeInverse(
        ActionDescriptor forwardAction,
        ActionDescriptor? authoredInverse,
        AuthorityLattice authorityLattice) =>
        AnalyzeInverse(forwardAction, authoredInverse, authorityLattice, default);

    /// <summary>
    /// Derives <c>A^-1</c> and proves that an optional authored inverse is semantically equivalent.
    /// </summary>
    public static ActionInverseAnalysis AnalyzeInverse(
        ActionDescriptor forwardAction,
        ActionDescriptor? authoredInverse,
        AuthorityLattice authorityLattice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(forwardAction);
        ArgumentNullException.ThrowIfNull(authorityLattice);
        cancellationToken.ThrowIfCancellationRequested();

        var forwardProof = ActionContractProofEngine.Analyze(forwardAction, cancellationToken);
        if (forwardProof.Kind == ActionContractProofKind.Invalid)
        {
            return InverseResult(
                ActionInverseAnalysisStatus.Invalid,
                forwardAction,
                authoredInverse,
                null,
                ActionInverseObligation.ForwardContract,
                forwardProof.Reason ?? "The forward action contract is invalid.");
        }

        if (forwardProof.Kind == ActionContractProofKind.Opaque)
        {
            return InverseResult(
                ActionInverseAnalysisStatus.Opaque,
                forwardAction,
                authoredInverse,
                null,
                ActionInverseObligation.ForwardContract,
                "Custom predicate evaluator(s) prevent derivation of a closed inverse: "
                + string.Join(", ", forwardProof.OpaqueKeys)
                + ".");
        }

        if (!TryResolveAuthority(
                forwardAction,
                authorityLattice,
                out var forwardAuthority,
                out var authorityFailure))
        {
            return InverseResult(
                ActionInverseAnalysisStatus.Invalid,
                forwardAction,
                authoredInverse,
                null,
                ActionInverseObligation.ForwardContract,
                authorityFailure!);
        }

        var frame = new ActionFrame(forwardAction.TouchedResources);
        var derivedContract = new ActionInverseContract(
            new ActionContractIdentity(forwardAction.Subject, forwardAction.Name),
            forwardProof.EffectiveGuarantee,
            forwardProof.Requirement,
            forwardAuthority!,
            frame);
        var derivedDescriptor = new ActionDescriptor(
            forwardAction.Subject,
            $"{forwardAction.Name}^-1",
            $"Mechanically derived inverse contract for {forwardAction.Name}.")
        {
            RequiredAuthority = forwardAction.RequiredAuthority,
            Preconditions =
            [
                new ActionPrecondition(
                    derivedContract.Requirement,
                    derivedContract.Requirement.Expression),
            ],
            Ensures = [new ActionGuarantee(derivedContract.Guarantee)],
            TouchedResources = frame.Resources,
        };
        var derivedProof = ActionContractProofEngine.Analyze(derivedDescriptor, cancellationToken);
        if (derivedProof.Kind != ActionContractProofKind.Closed)
        {
            return InverseResult(
                ActionInverseAnalysisStatus.Invalid,
                forwardAction,
                authoredInverse,
                derivedContract,
                ActionInverseObligation.ForwardContract,
                derivedProof.Reason ?? "The mechanically derived inverse contract is invalid.");
        }

        if (authoredInverse is null)
        {
            if (frame.Resources.IsEmpty)
            {
                if (forwardAction.CompensatingActionName is not null)
                {
                    return InverseResult(
                        ActionInverseAnalysisStatus.Refuted,
                        forwardAction,
                        null,
                        derivedContract,
                        ActionInverseObligation.ExecutableInverse,
                        $"The declared inverse action '{forwardAction.CompensatingActionName}' was not supplied; "
                        + "remove the broken declaration to use the empty identity inverse.");
                }

                return new ActionInverseAnalysis(
                    ActionInverseAnalysisStatus.Proven,
                    forwardAction,
                    null,
                    derivedContract,
                    [],
                    usesIdentityInverse: true);
            }

            var expected = forwardAction.CompensatingActionName is null
                ? "The non-empty action frame requires an authored inverse."
                : $"The declared inverse action '{forwardAction.CompensatingActionName}' was not supplied.";
            return InverseResult(
                ActionInverseAnalysisStatus.Missing,
                forwardAction,
                null,
                derivedContract,
                ActionInverseObligation.ExecutableInverse,
                expected);
        }

        var failures = ImmutableArray.CreateBuilder<ActionInverseFailure>();
        var hasInvalid = false;
        var hasRefuted = false;
        var hasOpaque = false;

        if (forwardAction.CompensatingActionName is not null
            && !string.Equals(
                forwardAction.CompensatingActionName,
                authoredInverse.Name,
                StringComparison.Ordinal))
        {
            failures.Add(new ActionInverseFailure(
                ActionInverseObligation.ExecutableInverse,
                $"Forward action '{forwardAction.Subject}/{forwardAction.Name}' names inverse "
                + $"'{forwardAction.CompensatingActionName}', but '{authoredInverse.Name}' was supplied."));
            hasRefuted = true;
        }

        var authoredProof = ActionContractProofEngine.Analyze(authoredInverse, cancellationToken);
        if (authoredProof.Kind == ActionContractProofKind.Invalid)
        {
            failures.Add(new ActionInverseFailure(
                ActionInverseObligation.AuthoredContract,
                authoredProof.Reason ?? "The authored inverse contract is invalid.",
                ToCounterexample(authoredProof.Witness)));
            hasInvalid = true;
        }

        if (!forwardAction.Subject.Equals(authoredInverse.Subject))
        {
            failures.Add(new ActionInverseFailure(
                ActionInverseObligation.Subject,
                $"Authored inverse subject '{authoredInverse.Subject}' does not match forward subject "
                + $"'{forwardAction.Subject}'."));
            hasRefuted = true;
        }

        AuthorityRequirement? authoredAuthority = null;
        if (!TryResolveAuthority(
                authoredInverse,
                authorityLattice,
                out authoredAuthority,
                out authorityFailure))
        {
            failures.Add(new ActionInverseFailure(
                ActionInverseObligation.AuthoredContract,
                authorityFailure!));
            hasInvalid = true;
        }
        else if (!AuthoritiesEqual(forwardAuthority!, authoredAuthority!, authorityLattice))
        {
            failures.Add(new ActionInverseFailure(
                ActionInverseObligation.Authority,
                "The authored inverse authority is not semantically equal to the forward authority."));
            hasRefuted = true;
        }

        var authoredFrame = new ActionFrame(
            authoredInverse.TouchedResources.Where(resource => resource is not null));
        if (!frame.Resources.SequenceEqual(authoredFrame.Resources))
        {
            var expected = FormatFrame(frame);
            var actual = FormatFrame(authoredFrame);
            failures.Add(new ActionInverseFailure(
                ActionInverseObligation.Frame,
                $"The authored inverse declares a different frame: [{actual}] does not equal "
                + $"the forward frame [{expected}]."));
            hasRefuted = true;
        }

        if (authoredProof.Kind == ActionContractProofKind.Opaque)
        {
            failures.Add(new ActionInverseFailure(
                ActionInverseObligation.AuthoredContract,
                "Custom predicate evaluator(s) prevent a complete inverse proof: "
                + string.Join(", ", authoredProof.OpaqueKeys)
                + "."));
            hasOpaque = true;
        }
        else if (authoredProof.Kind == ActionContractProofKind.Closed)
        {
            AddEquivalenceFailures(
                failures,
                ActionInverseObligation.Requirements,
                forwardProof.EffectiveGuaranteeFormula,
                authoredProof.RequirementFormula,
                "Derived inverse requirement does not imply the authored inverse requirement.",
                "Authored inverse requirement does not imply the derived inverse requirement.",
                cancellationToken,
                ref hasInvalid,
                ref hasRefuted,
                ref hasOpaque);
            AddEquivalenceFailures(
                failures,
                ActionInverseObligation.Guarantees,
                forwardProof.RequirementFormula,
                authoredProof.EffectiveGuaranteeFormula,
                "Derived inverse guarantee does not imply the authored inverse guarantee.",
                "Authored inverse guarantee does not imply the derived inverse guarantee.",
                cancellationToken,
                ref hasInvalid,
                ref hasRefuted,
                ref hasOpaque);
        }

        var status = hasInvalid
            ? ActionInverseAnalysisStatus.Invalid
            : hasRefuted
                ? ActionInverseAnalysisStatus.Refuted
                : hasOpaque
                    ? ActionInverseAnalysisStatus.Opaque
                    : ActionInverseAnalysisStatus.Proven;
        return new ActionInverseAnalysis(
            status,
            forwardAction,
            authoredInverse,
            derivedContract,
            failures,
            usesIdentityInverse: false);
    }

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

    /// <summary>Creates the distinct empty rollback identity for one subject.</summary>
    public static ActionRollbackPlan RollbackIdentity(ActionSubject subject) =>
        new(ActionRollbackPlanKind.Identity, subject, null, []);

    /// <summary>Derives one rollback leaf from a completed forward action.</summary>
    public static ActionRollbackPlan DeriveRollbackPlan(ActionInverseAnalysis inverse)
    {
        ArgumentNullException.ThrowIfNull(inverse);
        var leaf = new ActionRollbackLeaf(inverse);
        return new ActionRollbackPlan(
            ActionRollbackPlanKind.Leaf,
            inverse.ForwardAction.Subject,
            leaf,
            []);
    }

    /// <summary>
    /// Derives rollback for a completed forward prefix. Only supplied completed
    /// leaves participate, and their execution order is reversed mechanically.
    /// </summary>
    public static ActionRollbackPlan DeriveRollbackPlan(
        ActionSubject subject,
        IEnumerable<ActionInverseAnalysis> completedForwardPrefix)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(completedForwardPrefix);
        var children = completedForwardPrefix
            .Select(DeriveRollbackPlan)
            .ToArray();
        return DeriveSequentialRollbackPlan(subject, children);
    }

    /// <summary>
    /// Derives sequential rollback by reversing the completed forward children.
    /// Nested sequential plans are flattened after reversal; scope boundaries are retained.
    /// </summary>
    public static ActionRollbackPlan DeriveSequentialRollbackPlan(
        ActionSubject subject,
        IEnumerable<ActionRollbackPlan> completedForwardChildren)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var children = MaterializeRollbackChildren(subject, completedForwardChildren);
        var rollbackOrder = children
            .Reverse()
            .SelectMany(child => child.Kind == ActionRollbackPlanKind.Sequence
                ? child.Children
                : [child])
            .Where(child => child.Kind != ActionRollbackPlanKind.Identity)
            .ToImmutableArray();
        return rollbackOrder.Length switch
        {
            0 => RollbackIdentity(subject),
            1 => rollbackOrder[0],
            _ => new ActionRollbackPlan(
                ActionRollbackPlanKind.Sequence,
                subject,
                null,
                rollbackOrder),
        };
    }

    /// <summary>
    /// Derives parallel rollback without imposing a sequential order on independent branches.
    /// </summary>
    public static ActionRollbackPlan DeriveParallelRollbackPlan(
        ActionSubject subject,
        IEnumerable<ActionRollbackPlan> completedForwardBranches)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var branches = MaterializeRollbackChildren(subject, completedForwardBranches)
            .Where(child => child.Kind != ActionRollbackPlanKind.Identity)
            .ToImmutableArray();
        var interference = FindParallelRollbackInterference(branches);
        if (interference is not null)
        {
            throw new ArgumentException(
                interference,
                nameof(completedForwardBranches));
        }

        return branches.IsEmpty
            ? RollbackIdentity(subject)
            : new ActionRollbackPlan(
                ActionRollbackPlanKind.Parallel,
                subject,
                null,
                branches);
    }

    private static string? FindParallelRollbackInterference(
        ImmutableArray<ActionRollbackPlan> branches)
    {
        var conflicts = new List<(
            ActionResource Resource,
            bool IsWriteWrite,
            int WriterIndex,
            int OtherIndex)>();
        for (var leftIndex = 0; leftIndex < branches.Length; leftIndex++)
        {
            var left = branches[leftIndex];
            for (var rightIndex = leftIndex + 1; rightIndex < branches.Length; rightIndex++)
            {
                var right = branches[rightIndex];
                conflicts.AddRange(left.Frame.Resources
                    .Intersect(right.Frame.Resources)
                    .Select(resource => (resource, true, leftIndex, rightIndex)));
                conflicts.AddRange(left.Frame.Resources
                    .Intersect(right.ReadFootprint.Resources)
                    .Select(resource => (resource, false, leftIndex, rightIndex)));
                conflicts.AddRange(right.Frame.Resources
                    .Intersect(left.ReadFootprint.Resources)
                    .Select(resource => (resource, false, rightIndex, leftIndex)));
            }
        }

        if (conflicts.Count == 0)
        {
            return null;
        }

        var conflict = conflicts
            .OrderBy(static item => item.Resource.Kind)
            .ThenBy(static item => item.Resource.Name, StringComparer.Ordinal)
            .ThenBy(static item => item.IsWriteWrite ? 0 : 1)
            .ThenBy(static item => item.WriterIndex)
            .ThenBy(static item => item.OtherIndex)
            .First();
        return conflict.IsWriteWrite
            ? "Parallel rollback branches must have pairwise-disjoint frames; resource "
                + $"'{conflict.Resource.Kind}:{conflict.Resource.Name}' is written during rollback by more than one branch."
            : "Parallel rollback branches must be noninterfering; resource "
                + $"'{conflict.Resource.Kind}:{conflict.Resource.Name}' is written during rollback by branch "
                + $"{conflict.WriterIndex} and read by branch {conflict.OtherIndex}.";
    }

    /// <summary>Preserves a nested compensation boundary around a derived body plan.</summary>
    public static ActionRollbackPlan DeriveScopedRollbackPlan(ActionRollbackPlan completedBody)
    {
        ArgumentNullException.ThrowIfNull(completedBody);
        return new ActionRollbackPlan(
            ActionRollbackPlanKind.Scope,
            completedBody.Subject,
            null,
            [completedBody]);
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

    private static ActionInverseAnalysis InverseResult(
        ActionInverseAnalysisStatus status,
        ActionDescriptor forwardAction,
        ActionDescriptor? authoredInverse,
        ActionInverseContract? derivedContract,
        ActionInverseObligation obligation,
        string message) => new(
            status,
            forwardAction,
            authoredInverse,
            derivedContract,
            [new ActionInverseFailure(obligation, message)],
            usesIdentityInverse: false);

    private static bool TryResolveAuthority(
        ActionDescriptor action,
        AuthorityLattice authorityLattice,
        out AuthorityRequirement? authority,
        out string? failure)
    {
        try
        {
            authority = action.RequiredAuthority is null
                ? authorityLattice.Join([])
                : authorityLattice.Join(action.RequiredAuthority);
            failure = null;
            return true;
        }
        catch (Exception exception) when (exception is KeyNotFoundException or ArgumentException)
        {
            authority = null;
            failure = exception.Message;
            return false;
        }
    }

    private static bool AuthoritiesEqual(
        AuthorityRequirement left,
        AuthorityRequirement right,
        AuthorityLattice authorityLattice) =>
        authorityLattice.IsAtMost(left, right)
        && authorityLattice.IsAtMost(right, left);

    private static string FormatFrame(ActionFrame frame) => string.Join(
        ", ",
        frame.Resources.Select(resource => $"{resource.Kind}:{resource.Name}"));

    private static void AddEquivalenceFailures(
        ICollection<ActionInverseFailure> failures,
        ActionInverseObligation obligation,
        LogicFormula derived,
        LogicFormula authored,
        string derivedToAuthoredMessage,
        string authoredToDerivedMessage,
        CancellationToken cancellationToken,
        ref bool hasInvalid,
        ref bool hasRefuted,
        ref bool hasOpaque)
    {
        AddInverseImplicationFailure(
            failures,
            obligation,
            FiniteDomainSolver.Implies(derived, authored, cancellationToken),
            derivedToAuthoredMessage,
            ref hasInvalid,
            ref hasRefuted,
            ref hasOpaque);
        AddInverseImplicationFailure(
            failures,
            obligation,
            FiniteDomainSolver.Implies(authored, derived, cancellationToken),
            authoredToDerivedMessage,
            ref hasInvalid,
            ref hasRefuted,
            ref hasOpaque);
    }

    private static void AddInverseImplicationFailure(
        ICollection<ActionInverseFailure> failures,
        ActionInverseObligation obligation,
        LogicDecision decision,
        string refutationMessage,
        ref bool hasInvalid,
        ref bool hasRefuted,
        ref bool hasOpaque)
    {
        if (decision.Kind == LogicDecisionKind.Unsatisfiable)
        {
            return;
        }

        failures.Add(new ActionInverseFailure(
            obligation,
            decision.Kind == LogicDecisionKind.Satisfiable
                ? refutationMessage
                : decision.Reason ?? "The inverse equivalence could not be decided.",
            ToCounterexample(decision.Witness)));
        switch (decision.Kind)
        {
            case LogicDecisionKind.Satisfiable:
                hasRefuted = true;
                break;
            case LogicDecisionKind.Opaque:
                hasOpaque = true;
                break;
            default:
                hasInvalid = true;
                break;
        }
    }

    private static ImmutableArray<ActionCounterexampleFact> ToCounterexample(
        ImmutableArray<KeyValuePair<string, string>> witness) => witness
        .Select(pair => new ActionCounterexampleFact(pair.Key, pair.Value))
        .ToImmutableArray();

    private static ImmutableArray<ActionRollbackPlan> MaterializeRollbackChildren(
        ActionSubject subject,
        IEnumerable<ActionRollbackPlan> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        var materialized = children.ToImmutableArray();
        if (materialized.Any(child => child is null))
        {
            throw new ArgumentException(
                "Rollback plan children cannot contain null entries.",
                nameof(children));
        }

        var foreign = materialized.FirstOrDefault(child => !subject.Equals(child.Subject));
        if (foreign is not null)
        {
            throw new ArgumentException(
                $"Rollback composition is restricted to one subject: '{subject}' and "
                + $"'{foreign.Subject}' do not match.",
                nameof(children));
        }

        return materialized;
    }

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

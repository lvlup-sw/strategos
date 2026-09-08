// -----------------------------------------------------------------------
// <copyright file="WorkflowBindingProofAnalyzer.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using Strategos.Analyzers.Proof;
using Strategos.Generators.Diagnostics;
using Strategos.Generators.Models;
using Strategos.Ontology.ActionLogic;

namespace Strategos.Generators.Proof;

/// <summary>Proves compilation-local workflow action refinements.</summary>
internal static class WorkflowBindingProofAnalyzer
{
    /// <summary>
    /// Test-only fault seam. When set, it runs before the proof with the compilation under
    /// analysis so a test can prove that an internal failure of the proof itself is reported
    /// as a build error rather than as the Roslyn generator-crash warning. The delegate
    /// receives the compilation so a test can throw only for its own fixture: the seam is a
    /// process-wide static, and other generator tests run concurrently. Production never
    /// assigns it.
    /// </summary>
    internal static Action<Compilation>? ProofFaultInjection { get; set; }

    /// <summary>
    /// Runs <see cref="Analyze"/> and converts any internal failure into one
    /// <see cref="WorkflowDiagnostics.WorkflowContractUnprovable"/> error.
    /// </summary>
    /// <remarks>
    /// Roslyn reports an exception escaping a source-output node as CS8785, a warning, and
    /// discards every diagnostic the node would have produced. For a proof whose entire value
    /// is that a refuted binding fails the build, that path would turn "could not prove" into
    /// "nothing to report". The catch keeps the fail-closed contract: an unproved binding is an
    /// error whether the analyzer refuted it or could not run.
    /// </remarks>
    /// <summary>
    /// The builder method whose textual presence marks a compilation as one that binds; used
    /// only on the fail-closed path, where the semantic binding set is unavailable.
    /// </summary>
    private const string BindingMethodName = "BoundToWorkflow";

    internal static void AnalyzeFailClosed(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<WorkflowModel> workflows)
    {
        try
        {
            ProofFaultInjection?.Invoke(compilation);
            Analyze(context, compilation, workflows);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // An internal failure is an Error only for a compilation that has something to
            // prove. A compilation with nothing to prove keeps building.
            if (!HasSomethingToProve(compilation, workflows, context.CancellationToken))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                WorkflowDiagnostics.WorkflowContractUnprovable,
                Location.None,
                "(every bound workflow)",
                "(every bound action)",
                $"the workflow binding proof failed internally with {exception.GetType().Name}: {exception.Message}"));
        }
    }

    /// <summary>
    /// Decides whether a compilation whose proof failed internally has an obligation the proof
    /// would otherwise have discharged.
    /// </summary>
    /// <remarks>
    /// Two disjunct sources, because either alone is unsound. The ontology binding set is
    /// unknown once the scan has thrown, so bindings use a conservative syntactic
    /// over-approximation: any tree that mentions the binding method. That scan is blind to the
    /// other obligation, typed derived compensation, which <see
    /// cref="ReportTypedCompensationBindingBoundaries"/> rejects precisely when nothing binds the
    /// workflow — so a compilation that declares a typed inverse and never writes
    /// <c>BoundToWorkflow</c> is exactly the compilation the text scan calls vacuous and the
    /// proof calls refuted. That obligation is read from the workflow models, which the catch
    /// path still holds intact, using the same predicate the proof itself applies.
    /// </remarks>
    /// <param name="compilation">The compilation under analysis.</param>
    /// <param name="workflows">The workflow models the failed proof was given.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns><see langword="true"/> when an internal failure must fail the build.</returns>
    private static bool HasSomethingToProve(
        Compilation compilation,
        ImmutableArray<WorkflowModel> workflows,
        CancellationToken cancellationToken)
    {
        if (MentionsWorkflowBinding(compilation, cancellationToken))
        {
            return true;
        }

        try
        {
            return workflows.Any(workflow =>
                BuildOccurrenceMap(workflow).Values.Any(DeclaresTypedOrDynamicCompensation));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // If the model walk fails too, assume the obligation exists: silence is the wrong default.
            return true;
        }
    }

    /// <summary>
    /// Identifies a step occurrence whose compensation carries a typed or dynamic inverse, the
    /// obligation <see cref="ReportTypedCompensationBindingBoundaries"/> discharges.
    /// </summary>
    /// <param name="step">The step occurrence to classify.</param>
    /// <returns><see langword="true"/> when the occurrence declares such a compensation.</returns>
    private static bool DeclaresTypedOrDynamicCompensation(StepModel step) =>
        step.Compensation is { } compensation
        && (compensation.HasTypedOrDynamicDeclaration
            || compensation.InverseActionResolution
                is WorkflowActionReferenceResolution.Resolved
                or WorkflowActionReferenceResolution.DynamicOrInvalid);

    private static bool MentionsWorkflowBinding(Compilation compilation, CancellationToken cancellationToken)
    {
        try
        {
            return compilation.SyntaxTrees.Any(tree =>
                tree.GetText(cancellationToken).ToString().Contains(BindingMethodName, StringComparison.Ordinal));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // If even the text scan fails, assume the compilation binds: silence is the wrong default.
            return true;
        }
    }

    internal static void Analyze(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<WorkflowModel> workflows)
    {
        var catalog = OntologyActionCatalog.Build(compilation, context.CancellationToken);
        ReportEmissionIdentityCollisions(context, catalog, workflows);
        var workflowGroups = workflows
            .OrderBy(model => model.WorkflowName, StringComparer.Ordinal)
            .GroupBy(model => model.WorkflowName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToImmutableArray(),
                StringComparer.Ordinal);
        var typedCompensationBoundaryFailures = ReportTypedCompensationBindingBoundaries(
            context,
            catalog,
            workflowGroups);
        var boundRollbackClaims = catalog.Actions
            .Where(action => action.HasWorkflowBinding
                && action.BoundWorkflowName is not null
                && action.CompensatingActionName is not null)
            .Select(action => action.BoundWorkflowName!)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var compensationProofResults = new Dictionary<string, bool>(StringComparer.Ordinal);
        var cycleReasons = FindRecursiveBindings(catalog, workflowGroups, context.CancellationToken);

        foreach (var boundAction in catalog.Actions
            .Where(action => action.HasWorkflowBinding)
            .OrderBy(action => action.Identity.DomainName, StringComparer.Ordinal)
            .ThenBy(action => action.Identity.ObjectTypeName, StringComparer.Ordinal)
            .ThenBy(action => action.Identity.ActionName, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (boundAction.BoundWorkflowName is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    WorkflowDiagnostics.WorkflowContractUnprovable,
                    boundAction.Location,
                    "<dynamic>",
                    boundAction.Identity.ToString(),
                    boundAction.InvalidReason ?? "the workflow binding name is not statically closed"));
                continue;
            }

            var workflowName = boundAction.BoundWorkflowName;
            if (typedCompensationBoundaryFailures.Contains(workflowName))
            {
                continue;
            }

            if (cycleReasons.TryGetValue(boundAction, out var cycleReason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    WorkflowDiagnostics.WorkflowContractUnprovable,
                    boundAction.Location,
                    workflowName,
                    boundAction.Identity.ToString(),
                    cycleReason));
                continue;
            }

            var matches = workflowGroups.TryGetValue(workflowName, out var named)
                ? named
                : ImmutableArray<WorkflowModel>.Empty;
            if (matches.Length != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    WorkflowDiagnostics.BoundWorkflowNotFound,
                    boundAction.Location,
                    boundAction.Identity.ToString(),
                    workflowName,
                    matches.Length.ToString(CultureInfo.InvariantCulture)));
                continue;
            }

            AnalyzeBinding(
                context,
                catalog,
                boundAction,
                matches[0],
                boundRollbackClaims.Contains(workflowName),
                compensationProofResults);
        }
    }

    private static ImmutableHashSet<string> ReportTypedCompensationBindingBoundaries(
        SourceProductionContext context,
        OntologyActionCatalog catalog,
        IReadOnlyDictionary<string, ImmutableArray<WorkflowModel>> workflowGroups)
    {
        var failures = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var workflowGroup in workflowGroups.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var typedOccurrences = workflowGroup.Value
                .SelectMany(workflow => BuildOccurrenceMap(workflow))
                .Where(item => DeclaresTypedOrDynamicCompensation(item.Value))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToImmutableArray();
            if (typedOccurrences.IsEmpty)
            {
                continue;
            }

            var boundMatches = catalog.Actions
                .Where(action => action.HasWorkflowBinding
                    && string.Equals(
                        action.BoundWorkflowName,
                        workflowGroup.Key,
                        StringComparison.Ordinal))
                .OrderBy(action => action.Identity.DomainName, StringComparer.Ordinal)
                .ThenBy(action => action.Identity.ObjectTypeName, StringComparer.Ordinal)
                .ThenBy(action => action.Identity.ActionName, StringComparer.Ordinal)
                .ThenBy(action => action.Location.SourceSpan.Start)
                .ToImmutableArray();

            var closureFailures = workflowGroup.Value
                .SelectMany(static workflow => workflow.TopologyClosureFailures)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static failure => failure, StringComparer.Ordinal)
                .ToImmutableArray();
            string? reason = !closureFailures.IsEmpty
                ? "the authored workflow topology is not statically closed: "
                    + string.Join("; ", closureFailures)
                : workflowGroup.Value.Any(static workflow => workflow.IsEventSourced)
                    ? "typed derived compensation is not supported for EventSourced persistence: "
                    + "the generated rollback event cannot guarantee that a consumer-defined "
                    + "ApplyEvent fold applies UpdatedState during live handling and Marten replay"
                    : null;
            if (reason is null && boundMatches.IsEmpty)
            {
                reason = "typed compensation requires at least one closed BoundToWorkflow action, but no declaration binds this workflow";
            }
            else if (reason is null)
            {
                foreach (var boundMatch in boundMatches)
                {
                    if (TryProveContract(
                        boundMatch,
                        context.CancellationToken,
                        out _,
                        out var proofFailure))
                    {
                        continue;
                    }

                    reason = $"BoundToWorkflow action '{boundMatch.Identity}' is not a closed, provable contract: "
                        + (proofFailure ?? "unknown proof failure");
                    break;
                }

                var subjects = boundMatches
                    .Select(match => Subject(match.Identity))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(subject => subject, StringComparer.Ordinal)
                    .ToImmutableArray();
                if (reason is null && subjects.Length != 1)
                {
                    reason = "BoundToWorkflow actions do not share one ontology subject: "
                        + string.Join(", ", subjects.Select(subject => $"'{subject}'"));
                }
            }

            if (reason is null)
            {
                continue;
            }

            failures.Add(workflowGroup.Key);
            var workflow = workflowGroup.Value[0];
            ReportNonCompensableScope(
                context,
                workflow,
                boundMatches.IsEmpty ? Location.None : boundMatches[0].Location,
                "workflow:" + workflow.WorkflowName,
                typedOccurrences[0].Key,
                reason);
        }

        return failures.ToImmutable();
    }

    private static void ReportEmissionIdentityCollisions(
        SourceProductionContext context,
        OntologyActionCatalog catalog,
        ImmutableArray<WorkflowModel> workflows)
    {
        foreach (var collision in workflows
            .GroupBy(workflow => workflow.PascalName, StringComparer.Ordinal)
            .Where(group => group.Skip(1).Any())
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var names = collision.Select(workflow => workflow.WorkflowName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToImmutableArray();
            var exactBoundDuplicate = names.Length == 1
                && catalog.Actions.Any(action => action.HasWorkflowBinding
                    && string.Equals(
                        action.BoundWorkflowName,
                        names[0],
                        StringComparison.Ordinal));
            if (exactBoundDuplicate)
            {
                // The binding lookup below reports the more specific ambiguity diagnostic.
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                WorkflowDiagnostics.WorkflowEmissionIdentityCollision,
                Location.None,
                string.Join(", ", names.Select(name => $"'{name}'")),
                collision.Key));
        }
    }

    private static Dictionary<OntologyActionContract, string> FindRecursiveBindings(
        OntologyActionCatalog catalog,
        IReadOnlyDictionary<string, ImmutableArray<WorkflowModel>> workflowGroups,
        CancellationToken cancellationToken)
    {
        var boundActions = catalog.Actions
            .Where(action => action.HasWorkflowBinding && action.BoundWorkflowName is not null)
            .ToImmutableArray();
        var dependencies = new Dictionary<OntologyActionContract, ImmutableArray<OntologyActionContract>>();
        foreach (var action in boundActions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!workflowGroups.TryGetValue(action.BoundWorkflowName!, out var workflows)
                || workflows.Length != 1)
            {
                dependencies.Add(action, ImmutableArray<OntologyActionContract>.Empty);
                continue;
            }

            var occurrenceMap = BuildOccurrenceMap(workflows[0]);
            var targets = occurrenceMap.Values
                .Where(step => step.ActionResolution == WorkflowActionReferenceResolution.Resolved)
                .Select(step => step.Action)
                .Where(reference => reference is not null)
                .Select(reference => new ActionIdentity(
                    reference!.DomainName,
                    reference.ObjectTypeName,
                    reference.ActionName))
                .Select(identity => catalog.Resolve(identity))
                .Where(matches => matches.Length == 1)
                .Select(matches => matches[0])
                .Where(candidate => candidate.HasWorkflowBinding)
                .Distinct()
                .OrderBy(candidate => candidate.Identity.DomainName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Identity.ObjectTypeName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Identity.ActionName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Location.SourceSpan.Start)
                .ToImmutableArray();
            dependencies.Add(action, targets);
        }

        var result = new Dictionary<OntologyActionContract, string>();
        foreach (var start in boundActions)
        {
            var path = new List<OntologyActionContract> { start };
            if (TryFindBindingCycle(start, start, dependencies, path, cancellationToken, out var cycle))
            {
                result.Add(
                    start,
                    "recursive workflow binding cycle: "
                        + string.Join(" -> ", cycle.Select(action => action.Identity.ToString())));
            }
        }

        return result;
    }

    private static bool TryFindBindingCycle(
        OntologyActionContract start,
        OntologyActionContract current,
        IReadOnlyDictionary<OntologyActionContract, ImmutableArray<OntologyActionContract>> dependencies,
        List<OntologyActionContract> path,
        CancellationToken cancellationToken,
        out ImmutableArray<OntologyActionContract> cycle)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!dependencies.TryGetValue(current, out var nextActions))
        {
            cycle = default;
            return false;
        }

        foreach (var next in nextActions)
        {
            if (ReferenceEquals(next, start))
            {
                cycle = path.Append(start).ToImmutableArray();
                return true;
            }

            if (path.Contains(next))
            {
                continue;
            }

            path.Add(next);
            if (TryFindBindingCycle(
                    start,
                    next,
                    dependencies,
                    path,
                    cancellationToken,
                    out cycle))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        cycle = default;
        return false;
    }

    private static void AnalyzeBinding(
        SourceProductionContext context,
        OntologyActionCatalog catalog,
        OntologyActionContract boundAction,
        WorkflowModel workflow,
        bool hasBoundRollbackClaim,
        IDictionary<string, bool> compensationProofResults)
    {
        var cancellationToken = context.CancellationToken;
        if (!workflow.TopologyClosureFailures.IsDefaultOrEmpty)
        {
            ReportUnprovable(
                context,
                workflow,
                boundAction,
                "the authored workflow topology is not statically closed: "
                    + string.Join("; ", workflow.TopologyClosureFailures));
            return;
        }

        if (!TryProveContract(boundAction, cancellationToken, out var boundProof, out var failure))
        {
            ReportUnprovable(context, workflow, boundAction, failure!);
            return;
        }

        var occurrenceModels = BuildOccurrenceMap(workflow);
        var resolved = new Dictionary<string, ProvenOccurrence>(StringComparer.Ordinal);
        var hasReferenceFailure = false;
        foreach (var phaseName in workflow.StepNames.Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!occurrenceModels.TryGetValue(phaseName, out var step))
            {
                ReportInvalidReference(
                    context,
                    workflow,
                    boundAction,
                    phaseName,
                    "no executable StepModel occurrence");
                hasReferenceFailure = true;
                continue;
            }

            if (step.ActionResolution == WorkflowActionReferenceResolution.Missing)
            {
                ReportInvalidReference(context, workflow, boundAction, phaseName, "no action reference");
                hasReferenceFailure = true;
                continue;
            }

            if (step.ActionResolution == WorkflowActionReferenceResolution.DynamicOrInvalid)
            {
                ReportInvalidReference(
                    context,
                    workflow,
                    boundAction,
                    phaseName,
                    "a dynamic or invalid action reference");
                hasReferenceFailure = true;
                continue;
            }

            if (step.Action is null)
            {
                ReportInvalidReference(
                    context,
                    workflow,
                    boundAction,
                    phaseName,
                    "a resolved marker without a three-part action identity");
                hasReferenceFailure = true;
                continue;
            }

            var identity = new ActionIdentity(
                step.Action.DomainName,
                step.Action.ObjectTypeName,
                step.Action.ActionName);
            var actionMatches = catalog.Resolve(identity);
            if (actionMatches.Length != 1)
            {
                ReportInvalidReference(
                    context,
                    workflow,
                    boundAction,
                    phaseName,
                    $"action reference '{identity}' resolving to {actionMatches.Length.ToString(CultureInfo.InvariantCulture)} declarations");
                hasReferenceFailure = true;
                continue;
            }

            if (!TryProveContract(
                actionMatches[0],
                cancellationToken,
                out var actionProof,
                out failure))
            {
                ReportUnprovable(
                    context,
                    workflow,
                    boundAction,
                    $"step '{phaseName}' action '{identity}' {failure}");
                hasReferenceFailure = true;
                continue;
            }

            resolved.Add(phaseName, new ProvenOccurrence(step, actionMatches[0], actionProof));
        }

        if (hasReferenceFailure)
        {
            return;
        }

        var unsupportedForkFailure = workflow.Forks?
            .SelectMany(fork => fork.Paths.Select(path => (fork, path)))
            .FirstOrDefault(item => item.path.HasFailureHandler);
        if (unsupportedForkFailure is { } unsupported
            && unsupported.path is not null
            && unsupported.path.HasFailureHandler)
        {
            ReportUnprovable(
                context,
                workflow,
                boundAction,
                $"fork '{unsupported.fork.ForkId}' path {unsupported.path.PathIndex.ToString(CultureInfo.InvariantCulture)} declares OnFailure, but fork-path failure handlers are not emitted by the current runtime lowering");
            return;
        }

        var unresolvedBranch = EnumerateBranches(workflow)
            .FirstOrDefault(branch => branch.HasUnresolvedCases);
        if (unresolvedBranch is not null)
        {
            ReportUnprovable(
                context,
                workflow,
                boundAction,
                $"branch '{unresolvedBranch.BranchId}' contains a case outside the statically closed branch grammar");
            return;
        }

        if (resolved.Count == 0 && workflow.StepNames.Count != 0)
        {
            ReportUnprovable(
                context,
                workflow,
                boundAction,
                "the topology contains no executable StepModel occurrences");
            return;
        }

        var subjectMismatch = resolved.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .FirstOrDefault(pair => !SameSubject(pair.Value.Action.Identity, boundAction.Identity));
        if (!string.IsNullOrEmpty(subjectMismatch.Key))
        {
            ReportRefinementFailure(
                context,
                workflow,
                boundAction,
                $"step '{subjectMismatch.Key}' has subject '{Subject(subjectMismatch.Value.Action.Identity)}', expected the single bound subject '{Subject(boundAction.Identity)}'");
            return;
        }

        if (!compensationProofResults.TryGetValue(workflow.WorkflowName, out var compensationProved))
        {
            compensationProved = ProveCompensationContracts(
                context,
                catalog,
                workflow,
                boundAction,
                resolved,
                hasBoundRollbackClaim,
                cancellationToken);
            compensationProofResults.Add(workflow.WorkflowName, compensationProved);
        }

        if (!compensationProved)
        {
            return;
        }

        var graph = PhaseGraph.Build(workflow);
        if (!ProveEntry(context, workflow, boundAction, boundProof, graph, resolved)
            || !ProveGraphSeams(context, workflow, boundAction, graph, resolved)
            || !ProveFailureAndApprovalIngress(
                context,
                workflow,
                boundAction,
                resolved,
                cancellationToken)
            || !ProveFrame(context, workflow, boundAction, resolved.Values)
            || !ProveAuthority(context, catalog, workflow, boundAction, resolved.Values)
            || !ProveForkNoninterference(context, workflow, boundAction, resolved))
        {
            return;
        }
    }

    private static bool ProveEntry(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        ProvenContract boundProof,
        PhaseGraph graph,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved)
    {
        foreach (var target in ExpandTransparentTarget(graph.EntryPhaseName, graph, resolved))
        {
            var consequent = target == PhaseGraph.CompletedPhase
                ? boundAction.Guarantee.Formula
                : resolved.TryGetValue(target, out var entry)
                    ? entry.Proof.Requirement
                    : null;
            if (consequent is null)
            {
                ReportUnprovable(
                    context,
                    workflow,
                    boundAction,
                    $"entry topology target '{target}' has no StepModel contract");
                return false;
            }

            var obligation = target == PhaseGraph.CompletedPhase
                ? "empty workflow entry does not establish the bound guarantee"
                : $"bound requirement does not imply entry step '{target}' requirement";
            if (!CheckImplication(
                context,
                workflow,
                boundAction,
                boundProof.Requirement,
                consequent,
                obligation))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryProveContract(
        OntologyActionContract action,
        CancellationToken cancellationToken,
        out ProvenContract proof,
        out string? failureReason)
    {
        cancellationToken.ThrowIfCancellationRequested();
        failureReason = action.InvalidReason
            ?? action.Requirement.InvalidReason
            ?? action.Guarantee.InvalidReason;
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
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToImmutableArray();
        if (!opaqueKeys.IsEmpty)
        {
            proof = null!;
            failureReason = "contains opaque custom predicate(s): "
                + string.Join(", ", opaqueKeys);
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
        var writtenProofResources = action.Requirement.AtomReads
            .Concat(action.Guarantee.AtomReads)
            .Where(pair => pair.Value.Any(frame.Contains))
            .Select(pair => pair.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToImmutableArray();

        if (!FiniteDomainSolver.TryForget(
                action.Guarantee.Formula,
                writtenProofResources,
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
                ? "has an unrealizable frame: its guarantee constrains untouched state; counterexample: "
                    + FormatWitness(frameDecision)
                : frameDecision.Reason ?? "has a frame that could not be classified";
            return false;
        }

        if (!FiniteDomainSolver.TryForget(
                action.Requirement.Formula,
                writtenProofResources,
                out var preservedRequirement,
                out failureReason,
                cancellationToken))
        {
            proof = null!;
            failureReason ??= "could not project its requirements through the declared frame";
            return false;
        }

        var reads = action.Requirement.AtomReads.Values
            .Concat(action.Guarantee.AtomReads.Values)
            .SelectMany(resources => resources)
            .Where(IsStateResource)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(resource => resource, StringComparer.Ordinal)
            .ToImmutableArray();
        proof = new ProvenContract(
            action.Requirement.Formula,
            LogicFormula.All(action.Guarantee.Formula, preservedRequirement),
            preservedRequirement,
            action.Frame,
            reads);
        failureReason = null;
        return true;
    }

    private static bool ProveCompensationContracts(
        SourceProductionContext context,
        OntologyActionCatalog catalog,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        IReadOnlyDictionary<string, ProvenOccurrence> occurrences,
        bool hasBoundRollbackClaim,
        CancellationToken cancellationToken)
    {
        var orderedOccurrences = occurrences
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToImmutableArray();

        // Validate every compensation an author explicitly supplied before checking scope
        // completeness. This keeps the authored-inverse disagreement witness stable even when an
        // earlier sibling in the same scope is wholly uncompensated.
        foreach (var pair in orderedOccurrences.Where(item => item.Value.Step.Compensation is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var occurrence = pair.Value;
            var compensation = occurrence.Step.Compensation!;

            if (!compensation.RequiredOnFailure
                && compensation.InverseActionResolution
                    is WorkflowActionReferenceResolution.Resolved
                    or WorkflowActionReferenceResolution.DynamicOrInvalid)
            {
                ReportInverseDisagreement(
                    context,
                    workflow,
                    occurrence.Action,
                    pair.Key,
                    compensation.InverseIdentity ?? "<unresolved>",
                    "typed compensation is a mandatory rollback program and cannot set RequiredOnFailure to false");
                return false;
            }

            if (compensation.InverseActionResolution != WorkflowActionReferenceResolution.Resolved
                || compensation.InverseAction is null)
            {
                var reason = compensation.InverseActionResolution
                    == WorkflowActionReferenceResolution.Missing
                        ? "the legacy Compensate<T>() form has no typed inverse identity"
                        : "the inverse action identity is dynamic or invalid";
                ReportInverseDisagreement(
                    context,
                    workflow,
                    occurrence.Action,
                    pair.Key,
                    compensation.InverseIdentity ?? "<unresolved>",
                    reason);
                return false;
            }

            var inverseIdentity = new ActionIdentity(
                compensation.InverseAction.DomainName,
                compensation.InverseAction.ObjectTypeName,
                compensation.InverseAction.ActionName);
            var inverseMatches = catalog.Resolve(inverseIdentity);
            if (inverseMatches.Length != 1)
            {
                ReportInverseDisagreement(
                    context,
                    workflow,
                    occurrence.Action,
                    pair.Key,
                    inverseIdentity.ToString(),
                    $"the inverse identity resolves to {inverseMatches.Length.ToString(CultureInfo.InvariantCulture)} action declarations");
                return false;
            }

            var inverse = inverseMatches[0];
            if (!TryProveContract(inverse, cancellationToken, out var inverseProof, out var proofFailure))
            {
                ReportInverseDisagreement(
                    context,
                    workflow,
                    occurrence.Action,
                    pair.Key,
                    inverseIdentity.ToString(),
                    proofFailure ?? "the inverse contract is not statically provable");
                return false;
            }

            var disagreement = FindInverseDisagreement(
                catalog,
                occurrence.Action,
                occurrence.Proof,
                inverse,
                inverseProof,
                cancellationToken);
            if (disagreement is not null)
            {
                ReportInverseDisagreement(
                    context,
                    workflow,
                    occurrence.Action,
                    pair.Key,
                    inverseIdentity.ToString(),
                    disagreement);
                return false;
            }
        }

        var hasRollbackClaim = hasBoundRollbackClaim
            || orderedOccurrences.Any(item => item.Value.Step.Compensation is not null);
        if (!hasRollbackClaim)
        {
            return true;
        }

        var topology = CompensationTopology.Build(workflow);
        var hasTypedProgram = orderedOccurrences.Any(item =>
            item.Value.Step.Compensation is not null);
        if (hasTypedProgram && !topology.IsClosed)
        {
            var issue = topology.Issues
                .OrderBy(value => value, StringComparer.Ordinal)
                .FirstOrDefault() ?? "unknown compensation topology ambiguity";
            var phaseName = orderedOccurrences
                .First(item => item.Value.Step.Compensation is not null)
                .Key;
            ReportNonCompensableScope(
                context,
                workflow,
                boundAction.Location,
                "workflow:" + workflow.WorkflowName,
                phaseName,
                "the typed compensation topology is not closed: " + issue);
            return false;
        }

        if (hasTypedProgram)
        {
            var journaledPhases = topology.Occurrences
                .Select(static occurrence => occurrence.PhaseName)
                .ToImmutableHashSet(StringComparer.Ordinal);
            var unjournaledOccurrence = orderedOccurrences
                .FirstOrDefault(item => !journaledPhases.Contains(item.Key));
            if (!string.IsNullOrEmpty(unjournaledOccurrence.Key))
            {
                ReportNonCompensableScope(
                    context,
                    workflow,
                    boundAction.Location,
                    "workflow:" + workflow.WorkflowName,
                    unjournaledOccurrence.Key,
                    "the executable action occurrence is outside the closed compensation topology; "
                    + "its failure metadata cannot identify a journal or rollback scope");
                return false;
            }

            foreach (var approval in EnumerateApprovals(workflow.ApprovalPoints)
                .Where(static candidate => !candidate.HasRejection || !candidate.HasEscalation)
                .OrderBy(static candidate => candidate.ApprovalPointName, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.PrecedingStepName, StringComparer.Ordinal))
            {
                var reason = FindTerminalApprovalAnchorFailure(topology, approval);
                if (reason is null)
                {
                    continue;
                }

                ReportNonCompensableScope(
                    context,
                    workflow,
                    boundAction.Location,
                    "workflow:" + workflow.WorkflowName,
                    approval.PhaseName,
                    reason);
                return false;
            }
        }

        foreach (var pair in orderedOccurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var occurrence = pair.Value;
            if (occurrence.Action.Frame.Length == 0
                || occurrence.Step.Compensation is not null)
            {
                continue;
            }

            ReportNonCompensableScope(
                context,
                workflow,
                boundAction.Location,
                CompensationScopeFor(topology, workflow, pair.Key),
                pair.Key,
                "no compensation step or inverse action is declared");
            return false;
        }

        return true;
    }

    internal static string? FindTerminalApprovalAnchorFailure(
        CompensationTopology topology,
        ApprovalModel approval)
    {
        var anchors = topology.Occurrences
            .Where(occurrence => string.Equals(
                occurrence.PhaseName,
                approval.PrecedingStepName,
                StringComparison.Ordinal))
            .ToImmutableArray();
        if (anchors.Length != 1)
        {
            return "terminal approval failure cannot select one compensation anchor: "
                + $"preceding phase '{approval.PrecedingStepName}' maps to "
                + $"{anchors.Length.ToString(CultureInfo.InvariantCulture)} compiled occurrences";
        }

        return anchors[0].Scope.Kind == CompensationScopeKind.Fork
            ? "terminal approval failure is anchored inside a fork scope, but approval "
                + "resume messages carry neither the fork occurrence identity nor an "
                + "authoritative per-lane phase"
            : null;
    }

    private static IEnumerable<ApprovalModel> EnumerateApprovals(
        IEnumerable<ApprovalModel>? approvals)
    {
        if (approvals is null)
        {
            yield break;
        }

        foreach (var approval in approvals)
        {
            yield return approval;
            foreach (var nested in EnumerateApprovals(approval.NestedEscalationApprovals))
            {
                yield return nested;
            }
        }
    }

    private static string? FindInverseDisagreement(
        OntologyActionCatalog catalog,
        OntologyActionContract forward,
        ProvenContract forwardProof,
        OntologyActionContract inverse,
        ProvenContract inverseProof,
        CancellationToken cancellationToken)
    {
        if (!SameSubject(forward.Identity, inverse.Identity))
        {
            return $"subject '{Subject(inverse.Identity)}' does not match forward subject '{Subject(forward.Identity)}'";
        }

        if (forward.CompensatingActionName is not null
            && !string.Equals(
                forward.CompensatingActionName,
                inverse.Identity.ActionName,
                StringComparison.Ordinal))
        {
            return $"the forward contract names '{forward.CompensatingActionName}' as its compensating action";
        }

        if (!forward.Frame.ToImmutableHashSet(StringComparer.Ordinal)
            .SetEquals(inverse.Frame))
        {
            return "the inverse frame differs from the forward frame";
        }

        var authorityDisagreement = FindAuthorityDisagreement(catalog, forward, inverse);
        if (authorityDisagreement is not null)
        {
            return authorityDisagreement;
        }

        var requirementDisagreement = FindFormulaInequivalence(
            inverseProof.Requirement,
            forwardProof.EffectiveGuarantee,
            "inverse requirement",
            "forward effective guarantee",
            cancellationToken);
        if (requirementDisagreement is not null)
        {
            return requirementDisagreement;
        }

        return FindFormulaInequivalence(
            inverseProof.EffectiveGuarantee,
            forwardProof.Requirement,
            "inverse effective guarantee",
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
            return $"subject domain '{forward.Identity.DomainName}' resolves to {lattices.Length.ToString(CultureInfo.InvariantCulture)} authority lattices";
        }

        var lattice = lattices[0];
        if (!lattice.TryJoinAtMost(
                [forward.RequiredAuthority],
                inverse.RequiredAuthority,
                out var forwardAtMostInverse,
                out var failureReason))
        {
            return failureReason ?? "forward-to-inverse authority equivalence could not be resolved";
        }

        if (!lattice.TryJoinAtMost(
                [inverse.RequiredAuthority],
                forward.RequiredAuthority,
                out var inverseAtMostForward,
                out failureReason))
        {
            return failureReason ?? "inverse-to-forward authority equivalence could not be resolved";
        }

        return forwardAtMostInverse && inverseAtMostForward
            ? null
            : $"required authority differs semantically (forward={forward.RequiredAuthority ?? "<none>"}, inverse={inverse.RequiredAuthority ?? "<none>"})";
    }

    private static string CompensationScopeFor(
        CompensationTopology topology,
        WorkflowModel workflow,
        string phaseName)
    {
        var scopes = topology.Occurrences
            .Where(occurrence => string.Equals(
                occurrence.PhaseName,
                phaseName,
                StringComparison.Ordinal))
            .Select(occurrence => occurrence.Scope)
            .Distinct()
            .ToImmutableArray();
        return scopes.Length == 1 && scopes[0].Kind != CompensationScopeKind.Root
            ? scopes[0].TemplateKey
            : "workflow:" + workflow.WorkflowName;
    }

    private static string? FindFormulaInequivalence(
        LogicFormula left,
        LogicFormula right,
        string leftName,
        string rightName,
        CancellationToken cancellationToken)
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

    private static void ReportInverseDisagreement(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract forward,
        string phaseName,
        string inverseIdentity,
        string reason) => context.ReportDiagnostic(Diagnostic.Create(
            WorkflowDiagnostics.AuthoredInverseDisagrees,
            forward.Location,
            phaseName,
            workflow.WorkflowName,
            inverseIdentity,
            forward.Identity.ToString(),
            reason));

    private static void ReportNonCompensableScope(
        SourceProductionContext context,
        WorkflowModel workflow,
        Location location,
        string scopeName,
        string phaseName,
        string reason) => context.ReportDiagnostic(Diagnostic.Create(
            WorkflowDiagnostics.CompensationScopeNotDerivable,
            location,
            workflow.WorkflowName,
            scopeName,
            phaseName,
            reason));

    private static Dictionary<string, StepModel> BuildOccurrenceMap(WorkflowModel workflow)
    {
        var phaseNames = workflow.StepNames.ToImmutableHashSet(StringComparer.Ordinal);
        var occurrences = new List<StepModel>();

        void Add(IEnumerable<StepModel>? steps)
        {
            if (steps is null)
            {
                return;
            }

            foreach (var step in steps)
            {
                if (phaseNames.Contains(step.PhaseName))
                {
                    occurrences.Add(step);
                }

                Add(step.Confidence?.OnLowConfidenceHandlerChain?.Steps);
            }
        }

        void AddApprovals(IEnumerable<ApprovalModel>? approvals)
        {
            if (approvals is null)
            {
                return;
            }

            foreach (var approval in approvals)
            {
                Add(approval.RejectionSteps);
                Add(approval.EscalationSteps);
                AddApprovals(approval.NestedEscalationApprovals);
            }
        }

        Add(workflow.Steps);
        if (workflow.Loops is not null)
        {
            foreach (var loop in workflow.Loops)
            {
                Add(loop.BodySteps);
            }
        }

        if (workflow.Forks is not null)
        {
            foreach (var path in workflow.Forks.SelectMany(fork => fork.Paths))
            {
                Add(path.Steps);
            }
        }

        if (workflow.FailureHandlers is not null)
        {
            foreach (var handler in workflow.FailureHandlers)
            {
                Add(handler.Steps);
            }
        }

        AddApprovals(workflow.ApprovalPoints);

        var result = new Dictionary<string, StepModel>(StringComparer.Ordinal);
        foreach (var group in occurrences.GroupBy(step => step.PhaseName, StringComparer.Ordinal))
        {
            var first = group.First();
            var hasConflictingIdentity = group.Skip(1).Any(step =>
                step.ActionResolution != first.ActionResolution
                || !Equals(step.Action, first.Action));
            result.Add(
                group.Key,
                hasConflictingIdentity
                    ? first with
                    {
                        Action = null,
                        ActionResolution = WorkflowActionReferenceResolution.DynamicOrInvalid,
                    }
                    : first);
        }

        return result;
    }

    private static IEnumerable<string> ExpandTransparentTarget(
        string target,
        PhaseGraph graph,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved)
    {
        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var terminals = new SortedSet<string>(StringComparer.Ordinal);
        pending.Enqueue(target);

        while (pending.Count != 0)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == PhaseGraph.CompletedPhase
                || current == PhaseGraph.FailedPhase
                || resolved.ContainsKey(current))
            {
                terminals.Add(current);
                continue;
            }

            var successors = graph.SuccessorsOf(current);
            if (successors.Count == 0)
            {
                terminals.Add(current);
                continue;
            }

            foreach (var successor in successors.OrderBy(value => value, StringComparer.Ordinal))
            {
                pending.Enqueue(successor);
            }
        }

        return terminals;
    }

    private static bool ProveFailureAndApprovalIngress(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved,
        CancellationToken cancellationToken)
    {
        if (!ProveFailureHandlerIngress(
                context,
                workflow,
                boundAction,
                resolved,
                cancellationToken))
        {
            return false;
        }

        if (workflow.ApprovalPoints is null)
        {
            return true;
        }

        foreach (var approval in workflow.ApprovalPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!resolved.TryGetValue(approval.PrecedingStepName, out var source))
            {
                ReportUnprovable(
                    context,
                    workflow,
                    boundAction,
                    $"approval '{approval.ApprovalPointName}' has no proven preceding action '{approval.PrecedingStepName}'");
                return false;
            }

            if (!ProveApprovalIngress(
                    context,
                    workflow,
                    boundAction,
                    approval,
                    source.Proof.EffectiveGuarantee,
                    resolved,
                    cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ProveFailureHandlerIngress(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved,
        CancellationToken cancellationToken)
    {
        if (workflow.FailureHandlers is null)
        {
            return true;
        }

        var handlerSteps = workflow.FailureHandlers
            .SelectMany(handler => handler.StepPhaseNames)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var workflowSources = resolved
            .Where(pair => !handlerSteps.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToImmutableArray();

        foreach (var handler in workflow.FailureHandlers
            .OrderBy(item => item.HandlerId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (handler.StepPhaseNames.Count == 0
                || !resolved.TryGetValue(handler.FirstStepPhaseName, out var entry))
            {
                ReportUnprovable(
                    context,
                    workflow,
                    boundAction,
                    $"failure handler '{handler.HandlerId}' has no proven entry action");
                return false;
            }

            ImmutableArray<ProvenOccurrence> sources;
            if (handler.IsWorkflowScoped)
            {
                sources = workflowSources;
            }
            else if (handler.TriggerStepName is not null
                && resolved.TryGetValue(handler.TriggerStepName, out var trigger))
            {
                sources = [trigger];
            }
            else
            {
                ReportUnprovable(
                    context,
                    workflow,
                    boundAction,
                    $"step-scoped failure handler '{handler.HandlerId}' has no proven trigger action '{handler.TriggerStepName ?? "<missing>"}'");
                return false;
            }

            foreach (var source in sources)
            {
                if (!CheckImplication(
                        context,
                        workflow,
                        boundAction,
                        source.Proof.FailureGuarantee,
                        entry.Proof.Requirement,
                        $"failure ingress from '{source.Step.PhaseName}' to handler '{handler.HandlerId}' entry '{entry.Step.PhaseName}' is not composable"))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ProveApprovalIngress(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        ApprovalModel approval,
        LogicFormula sourceGuarantee,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved,
        CancellationToken cancellationToken)
    {
        if (!ProveApprovalPathIngress(
                context,
                workflow,
                boundAction,
                approval,
                "rejection",
                approval.RejectionSteps,
                sourceGuarantee,
                resolved)
            || !ProveApprovalPathIngress(
                context,
                workflow,
                boundAction,
                approval,
                "escalation",
                approval.EscalationSteps,
                sourceGuarantee,
                resolved))
        {
            return false;
        }

        // The emitted timeout handler chooses authored escalation steps before a
        // nested approval. A nested chain is reachable only when that path is empty.
        if (approval.EscalationSteps is not null && approval.EscalationSteps.Count != 0)
        {
            return true;
        }

        if (approval.NestedEscalationApprovals is null)
        {
            return true;
        }

        foreach (var nested in approval.NestedEscalationApprovals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ProveApprovalIngress(
                    context,
                    workflow,
                    boundAction,
                    nested,
                    sourceGuarantee,
                    resolved,
                    cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ProveApprovalPathIngress(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        ApprovalModel approval,
        string pathKind,
        IReadOnlyList<StepModel>? path,
        LogicFormula sourceGuarantee,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved)
    {
        if (path is null || path.Count == 0)
        {
            return true;
        }

        var firstPhase = path[0].PhaseName;
        if (!resolved.TryGetValue(firstPhase, out var entry))
        {
            ReportUnprovable(
                context,
                workflow,
                boundAction,
                $"approval '{approval.ApprovalPointName}' {pathKind} path has no proven entry action '{firstPhase}'");
            return false;
        }

        return CheckImplication(
            context,
            workflow,
            boundAction,
            sourceGuarantee,
            entry.Proof.Requirement,
            $"approval '{approval.ApprovalPointName}' {pathKind} ingress to '{firstPhase}' is not composable");
    }

    private static bool ProveFrame(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        IEnumerable<ProvenOccurrence> occurrences)
    {
        var allowed = boundAction.Frame.ToImmutableHashSet(StringComparer.Ordinal);
        var escaped = occurrences
            .SelectMany(occurrence => occurrence.Proof.Frame)
            .Where(resource => !allowed.Contains(resource))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(resource => resource, StringComparer.Ordinal)
            .ToImmutableArray();
        if (escaped.IsEmpty)
        {
            return true;
        }

        ReportRefinementFailure(
            context,
            workflow,
            boundAction,
            "workflow frame escapes the bound action frame: " + string.Join(", ", escaped));
        return false;
    }

    private static bool ProveAuthority(
        SourceProductionContext context,
        OntologyActionCatalog catalog,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        IEnumerable<ProvenOccurrence> occurrences)
    {
        var candidates = occurrences
            .Select(occurrence => occurrence.Action.RequiredAuthority)
            .ToImmutableArray();
        if (boundAction.RequiredAuthority is null && candidates.All(candidate => candidate is null))
        {
            return true;
        }

        var lattices = catalog.ResolveLattice(boundAction.Identity.DomainName);
        if (lattices.Length != 1)
        {
            ReportUnprovable(
                context,
                workflow,
                boundAction,
                $"subject domain '{boundAction.Identity.DomainName}' resolves to {lattices.Length.ToString(CultureInfo.InvariantCulture)} authority lattices");
            return false;
        }

        if (!lattices[0].TryJoinAtMost(
                candidates,
                boundAction.RequiredAuthority,
                out var isAtMost,
                out var failureReason))
        {
            ReportUnprovable(
                context,
                workflow,
                boundAction,
                failureReason ?? "the authority join could not be resolved");
            return false;
        }

        if (isAtMost)
        {
            return true;
        }

        var joinedNames = candidates
            .Where(candidate => candidate is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(candidate => candidate, StringComparer.Ordinal);
        ReportRefinementFailure(
            context,
            workflow,
            boundAction,
            "workflow authority join ["
                + string.Join(", ", joinedNames)
                + "] exceeds bound authority '"
                + (boundAction.RequiredAuthority ?? "<none>")
                + "'");
        return false;
    }

    private static bool ProveForkNoninterference(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved)
    {
        if (workflow.Forks is null)
        {
            return true;
        }

        // A workflow-level OnFailure chain is a reachable diversion of every fork path:
        // each path worker publishes the root failure trigger on terminal failure while its
        // sibling workers may still be running. Include that chain in every path footprint so
        // recovery work cannot race a sibling path (or a second concurrently failing path)
        // behind an otherwise-disjoint fork proof.
        var rootFailureDiversions = BuildRootFailureDiversions(workflow, resolved);
        foreach (var fork in workflow.Forks.OrderBy(item => item.ForkId, StringComparer.Ordinal))
        {
            var paths = fork.Paths
                .OrderBy(path => path.PathIndex)
                .Select(path => BuildForkFootprint(path, resolved, rootFailureDiversions))
                .ToImmutableArray();

            for (var leftIndex = 0; leftIndex < paths.Length; leftIndex++)
            {
                for (var rightIndex = leftIndex + 1; rightIndex < paths.Length; rightIndex++)
                {
                    var left = paths[leftIndex];
                    var right = paths[rightIndex];
                    var writeWrite = left.Writes.Intersect(right.Writes)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToImmutableArray();
                    var leftWriteRightRead = left.Writes.Intersect(right.Reads)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToImmutableArray();
                    var rightWriteLeftRead = right.Writes.Intersect(left.Reads)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToImmutableArray();
                    if (writeWrite.IsEmpty
                        && leftWriteRightRead.IsEmpty
                        && rightWriteLeftRead.IsEmpty)
                    {
                        continue;
                    }

                    var conflicts = new List<string>();
                    if (!writeWrite.IsEmpty)
                    {
                        conflicts.Add("write/write=" + string.Join(",", writeWrite));
                    }

                    if (!leftWriteRightRead.IsEmpty)
                    {
                        conflicts.Add(
                            $"path {left.PathIndex} write/path {right.PathIndex} read="
                            + string.Join(",", leftWriteRightRead));
                    }

                    if (!rightWriteLeftRead.IsEmpty)
                    {
                        conflicts.Add(
                            $"path {right.PathIndex} write/path {left.PathIndex} read="
                            + string.Join(",", rightWriteLeftRead));
                    }

                    ReportRefinementFailure(
                        context,
                        workflow,
                        boundAction,
                        $"fork '{fork.ForkId}' paths {left.PathIndex} and {right.PathIndex} interfere: "
                            + string.Join("; ", conflicts));
                    return false;
                }
            }
        }

        return true;
    }

    private static ForkFootprint BuildForkFootprint(
        ForkPathModel path,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved,
        ImmutableArray<ProvenOccurrence> rootFailureDiversions)
    {
        var occurrences = EnumerateForkPathOccurrences(path.Steps)
            .Select(step => step.PhaseName)
            .Distinct(StringComparer.Ordinal)
            .Where(resolved.ContainsKey)
            .Select(phaseName => resolved[phaseName])
            .Concat(rootFailureDiversions)
            .ToImmutableArray();

        return new ForkFootprint(
            path.PathIndex,
            occurrences
                .SelectMany(occurrence => occurrence.Proof.Frame)
                .ToImmutableHashSet(StringComparer.Ordinal),
            occurrences
                .SelectMany(occurrence => occurrence.Proof.Reads)
                .Where(IsStateResource)
                .ToImmutableHashSet(StringComparer.Ordinal));
    }

    private static ImmutableArray<ProvenOccurrence> BuildRootFailureDiversions(
        WorkflowModel workflow,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved)
    {
        if (workflow.FailureHandlers is null)
        {
            return ImmutableArray<ProvenOccurrence>.Empty;
        }

        return workflow.FailureHandlers
            .Where(handler => handler.IsWorkflowScoped)
            .SelectMany(handler => handler.StepPhaseNames)
            .Distinct(StringComparer.Ordinal)
            .Where(resolved.ContainsKey)
            .Select(phaseName => resolved[phaseName])
            .ToImmutableArray();
    }

    private static IEnumerable<StepModel> EnumerateForkPathOccurrences(
        IReadOnlyList<StepModel> pathSteps)
    {
        var pending = new Queue<StepModel>(pathSteps);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count != 0)
        {
            var step = pending.Dequeue();
            if (!visited.Add(step.PhaseName))
            {
                continue;
            }

            yield return step;

            var handlerChain = step.Confidence?.OnLowConfidenceHandlerChain;
            if (handlerChain is null)
            {
                continue;
            }

            foreach (var handlerStep in handlerChain.Steps)
            {
                pending.Enqueue(handlerStep);
            }
        }
    }

    private static bool CheckImplication(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        LogicFormula antecedent,
        LogicFormula consequent,
        string obligation)
    {
        var decision = FiniteDomainSolver.Implies(
            antecedent,
            consequent,
            context.CancellationToken);
        if (decision.Kind == LogicDecisionKind.Unsatisfiable)
        {
            return true;
        }

        if (decision.Kind == LogicDecisionKind.Satisfiable)
        {
            ReportRefinementFailure(
                context,
                workflow,
                boundAction,
                obligation + "; counterexample: " + FormatWitness(decision));
            return false;
        }

        ReportUnprovable(
            context,
            workflow,
            boundAction,
            obligation + "; " + (decision.Reason ?? "proof returned no closed decision"));
        return false;
    }

    private static string FormatWitness(LogicDecision decision) => decision.Witness.IsEmpty
        ? "<empty state>"
        : string.Join(", ", decision.Witness.Select(pair => pair.Key + "=" + pair.Value));

    private static bool SameSubject(ActionIdentity left, ActionIdentity right) =>
        string.Equals(left.DomainName, right.DomainName, StringComparison.Ordinal)
        && string.Equals(left.ObjectTypeName, right.ObjectTypeName, StringComparison.Ordinal);

    private static string Subject(ActionIdentity identity) =>
        identity.DomainName + "/" + identity.ObjectTypeName;

    private static bool IsStateResource(string resource) =>
        resource.StartsWith("property|", StringComparison.Ordinal)
        || resource.StartsWith("link|", StringComparison.Ordinal);

    private static IEnumerable<BranchModel> EnumerateBranches(WorkflowModel workflow)
    {
        static IEnumerable<BranchModel> Chain(BranchModel? branch)
        {
            while (branch is not null)
            {
                yield return branch;
                branch = branch.NextConsecutiveBranch;
            }
        }

        if (workflow.Branches is not null)
        {
            foreach (var branch in workflow.Branches)
            {
                foreach (var item in Chain(branch))
                {
                    yield return item;
                }
            }
        }

        if (workflow.Loops is not null)
        {
            foreach (var loop in workflow.Loops)
            {
                foreach (var item in Chain(loop.BranchOnExit))
                {
                    yield return item;
                }
            }
        }
    }

    private static void ReportInvalidReference(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        string phaseName,
        string description) => context.ReportDiagnostic(Diagnostic.Create(
            WorkflowDiagnostics.WorkflowActionReferenceInvalid,
            boundAction.Location,
            phaseName,
            workflow.WorkflowName,
            description));

    private static void ReportRefinementFailure(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        string reason) => context.ReportDiagnostic(Diagnostic.Create(
            WorkflowDiagnostics.WorkflowBindingRefinementFailed,
            boundAction.Location,
            workflow.WorkflowName,
            boundAction.Identity.ToString(),
            reason));

    private static void ReportUnprovable(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        string reason) => context.ReportDiagnostic(Diagnostic.Create(
            WorkflowDiagnostics.WorkflowContractUnprovable,
            boundAction.Location,
            workflow.WorkflowName,
            boundAction.Identity.ToString(),
            reason));

    private sealed record ProvenContract(
        LogicFormula Requirement,
        LogicFormula EffectiveGuarantee,
        LogicFormula FailureGuarantee,
        ImmutableArray<string> Frame,
        ImmutableArray<string> Reads);

    private sealed record ProvenOccurrence(
        StepModel Step,
        OntologyActionContract Action,
        ProvenContract Proof);

    private sealed record ForkFootprint(
        int PathIndex,
        ImmutableHashSet<string> Writes,
        ImmutableHashSet<string> Reads);

    private static bool ProveGraphSeams(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        PhaseGraph graph,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved)
    {
        var forkTailEdges = ForkTailEdges(workflow);
        foreach (var source in resolved.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var immediateTarget in graph.SuccessorsOf(source.Key)
                .Where(target => target != PhaseGraph.FailedPhase))
            {
                if (forkTailEdges.Contains(source.Key + "\u001f" + immediateTarget))
                {
                    continue;
                }

                foreach (var target in ExpandTransparentTarget(immediateTarget, graph, resolved))
                {
                    var consequent = target == PhaseGraph.CompletedPhase
                        ? boundAction.Guarantee.Formula
                        : resolved.TryGetValue(target, out var downstream)
                            ? downstream.Proof.Requirement
                            : null;
                    if (consequent is null)
                    {
                        ReportUnprovable(
                            context,
                            workflow,
                            boundAction,
                            $"transition from '{source.Key}' to '{target}' has no downstream step contract");
                        return false;
                    }

                    var obligation = target == PhaseGraph.CompletedPhase
                        ? $"successful completion after '{source.Key}' does not establish the bound guarantee"
                        : $"internal seam '{source.Key}' -> '{target}' is not composable";
                    if (!CheckImplication(
                        context,
                        workflow,
                        boundAction,
                        source.Value.Proof.EffectiveGuarantee,
                        consequent,
                        obligation))
                    {
                        return false;
                    }
                }
            }
        }

        return ProveForkJoins(context, workflow, boundAction, resolved);
    }

    private static HashSet<string> ForkTailEdges(WorkflowModel workflow)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (workflow.Forks is null)
        {
            return result;
        }

        foreach (var fork in workflow.Forks)
        {
            foreach (var path in fork.Paths.Where(path => path.Steps.Count > 0))
            {
                result.Add(path.LastStepName + "\u001f" + fork.JoinStepName);
            }
        }

        return result;
    }

    private static bool ProveForkJoins(
        SourceProductionContext context,
        WorkflowModel workflow,
        OntologyActionContract boundAction,
        IReadOnlyDictionary<string, ProvenOccurrence> resolved)
    {
        if (workflow.Forks is null)
        {
            return true;
        }

        foreach (var fork in workflow.Forks.OrderBy(item => item.ForkId, StringComparer.Ordinal))
        {
            var tails = fork.Paths
                .Where(path => path.Steps.Count > 0)
                .Select(path => resolved.TryGetValue(path.LastStepName, out var tail) ? tail : null)
                .ToImmutableArray();
            if (tails.Any(tail => tail is null)
                || !resolved.TryGetValue(fork.JoinStepName, out var join))
            {
                ReportUnprovable(
                    context,
                    workflow,
                    boundAction,
                    $"fork '{fork.ForkId}' has a path tail or join without a StepModel contract");
                return false;
            }

            var jointGuarantee = LogicFormula.All(tails.Select(tail => tail!.Proof.EffectiveGuarantee));
            if (!CheckImplication(
                context,
                workflow,
                boundAction,
                jointGuarantee,
                join.Proof.Requirement,
                $"parallel join for fork '{fork.ForkId}' does not establish '{fork.JoinStepName}' requirement"))
            {
                return false;
            }
        }

        return true;
    }
}

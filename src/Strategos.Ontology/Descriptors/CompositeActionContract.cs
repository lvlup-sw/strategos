using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>Verification state of a sequential composite contract.</summary>
public enum ActionContractVerificationStatus
{
    /// <summary>The operand is the distinct empty identity for its subject.</summary>
    Identity,

    /// <summary>Every action contract and adjacent seam was proved.</summary>
    Proven,

    /// <summary>Closed portions were proved, but custom predicates remain runtime-only.</summary>
    PartiallyVerified,

    /// <summary>At least one adjacent seam was refuted.</summary>
    Refuted,
}

/// <summary>Result of checking one adjacent pair in a sequential composition.</summary>
public enum ActionCompositionSeamStatus
{
    /// <summary>The upstream effective guarantee implies the downstream requirement.</summary>
    Proven,

    /// <summary>A concrete symbolic state refutes the implication.</summary>
    Refuted,

    /// <summary>A custom predicate prevents a complete static proof.</summary>
    Opaque,
}

/// <summary>A stable symbolic assignment demonstrating an illegal seam.</summary>
public sealed record ActionCounterexampleFact(string Resource, string Value);

/// <summary>The proof result for an adjacent pair of actions.</summary>
public sealed record ActionCompositionSeamResult
{
    /// <summary>Initializes an immutable seam result.</summary>
    public ActionCompositionSeamResult(
        ActionDescriptor upstream,
        ActionDescriptor downstream,
        ActionCompositionSeamStatus status,
        IEnumerable<ActionCounterexampleFact>? counterexample = null,
        string? message = null)
    {
        Upstream = upstream ?? throw new ArgumentNullException(nameof(upstream));
        Downstream = downstream ?? throw new ArgumentNullException(nameof(downstream));
        Status = status;
        Counterexample = counterexample?.ToImmutableArray()
            ?? ImmutableArray<ActionCounterexampleFact>.Empty;
        Message = message;
    }

    /// <summary>Gets the upstream action.</summary>
    public ActionDescriptor Upstream { get; }

    /// <summary>Gets the downstream action.</summary>
    public ActionDescriptor Downstream { get; }

    /// <summary>Gets the proof outcome.</summary>
    public ActionCompositionSeamStatus Status { get; }

    /// <summary>Gets the minimal symbolic counterexample for a refuted seam.</summary>
    public ImmutableArray<ActionCounterexampleFact> Counterexample { get; }

    /// <summary>Gets an explanatory message.</summary>
    public string? Message { get; }
}

/// <summary>One custom evaluator that excluded an action from complete static proof.</summary>
public sealed record ActionCompositionExclusion
{
    /// <summary>Initializes an immutable opaque exclusion.</summary>
    public ActionCompositionExclusion(
        ActionSubject subject,
        string actionName,
        IEnumerable<string> evaluatorKeys)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        ArgumentNullException.ThrowIfNull(evaluatorKeys);
        ActionName = actionName;
        EvaluatorKeys = evaluatorKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>Gets the action subject.</summary>
    public ActionSubject Subject { get; }

    /// <summary>Gets the action name.</summary>
    public string ActionName { get; }

    /// <summary>Gets the stable custom evaluator keys.</summary>
    public ImmutableArray<string> EvaluatorKeys { get; }
}

/// <summary>Contract computed from a flattened ordered action composition.</summary>
public sealed record CompositeActionContract
{
    internal CompositeActionContract(
        ActionSubject subject,
        IEnumerable<ActionDescriptor> actions,
        ActionPredicate firstRequirement,
        ActionPredicate? finalGuarantee,
        AuthorityRequirement requiredAuthority,
        ActionFrame frame,
        ActionContractVerificationStatus verificationStatus,
        IEnumerable<ActionCompositionSeamResult> seams,
        IEnumerable<ActionCompositionExclusion> opaqueExclusions,
        bool isIdentity)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        ArgumentNullException.ThrowIfNull(actions);
        FirstRequirement = firstRequirement ?? throw new ArgumentNullException(nameof(firstRequirement));
        RequiredAuthority = requiredAuthority ?? throw new ArgumentNullException(nameof(requiredAuthority));
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        ArgumentNullException.ThrowIfNull(seams);
        ArgumentNullException.ThrowIfNull(opaqueExclusions);

        Actions = actions.ToImmutableArray();
        FinalGuarantee = finalGuarantee;
        VerificationStatus = verificationStatus;
        Seams = seams.ToImmutableArray();
        OpaqueExclusions = opaqueExclusions.ToImmutableArray();
        IsIdentity = isIdentity;
    }

    /// <summary>Gets the single ontology subject shared by every action.</summary>
    public ActionSubject Subject { get; }

    /// <summary>Gets the recursively flattened actions in execution order.</summary>
    public ImmutableArray<ActionDescriptor> Actions { get; }

    /// <summary>Gets the hard requirement of the first action, or true for identity.</summary>
    public ActionPredicate FirstRequirement { get; }

    /// <summary>
    /// Gets the last action's effective post-state guarantee. This is null when
    /// an opaque last action prevents a sound static projection.
    /// </summary>
    public ActionPredicate? FinalGuarantee { get; }

    /// <summary>Gets the pointwise join of all action authority requirements.</summary>
    public AuthorityRequirement RequiredAuthority { get; }

    /// <summary>Gets the union of all action frames.</summary>
    public ActionFrame Frame { get; }

    /// <summary>Gets the aggregate verification state.</summary>
    public ActionContractVerificationStatus VerificationStatus { get; }

    /// <summary>Gets every adjacent seam proof in flattened order.</summary>
    public ImmutableArray<ActionCompositionSeamResult> Seams { get; }

    /// <summary>Gets actions excluded from complete static proof by custom predicates.</summary>
    public ImmutableArray<ActionCompositionExclusion> OpaqueExclusions { get; }

    /// <summary>Gets whether this is the distinct empty identity operand.</summary>
    public bool IsIdentity { get; }
}

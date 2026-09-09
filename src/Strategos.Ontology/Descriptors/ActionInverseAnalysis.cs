using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>The outcome of deriving and, when supplied, checking an executable action inverse.</summary>
public enum ActionInverseAnalysisStatus
{
    /// <summary>The inverse is executable and every static obligation was proved.</summary>
    Proven,

    /// <summary>A non-empty frame has no executable authored inverse.</summary>
    Missing,

    /// <summary>An authored inverse contradicts at least one derived obligation.</summary>
    Refuted,

    /// <summary>A custom predicate prevents a complete static inverse proof.</summary>
    Opaque,

    /// <summary>The forward, derived, or authored contract is invalid.</summary>
    Invalid,
}

/// <summary>The closed set of obligations checked for an action inverse.</summary>
public enum ActionInverseObligation
{
    /// <summary>The forward contract must be valid and closed.</summary>
    ForwardContract,

    /// <summary>A non-empty frame must name an executable inverse.</summary>
    ExecutableInverse,

    /// <summary>The authored inverse contract must be valid and closed.</summary>
    AuthoredContract,

    /// <summary>The forward and authored inverse must operate on the same subject.</summary>
    Subject,

    /// <summary>The authored requirement must equal the forward effective guarantee.</summary>
    Requirements,

    /// <summary>The authored effective guarantee must equal the forward requirement.</summary>
    Guarantees,

    /// <summary>The forward and authored inverse must require semantically equal authority.</summary>
    Authority,

    /// <summary>The forward and authored inverse must have exactly the same frame.</summary>
    Frame,
}

/// <summary>Stable ontology identity of one executable action contract.</summary>
public sealed record ActionContractIdentity
{
    /// <summary>Initializes an action identity from its subject and subject-local action name.</summary>
    public ActionContractIdentity(ActionSubject subject, string actionName)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        ActionName = actionName;
    }

    /// <summary>Gets the action subject.</summary>
    public ActionSubject Subject { get; }

    /// <summary>Gets the action name within the subject.</summary>
    public string ActionName { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Subject}/{ActionName}";
}

/// <summary>The contract computed for <c>A^-1</c> from a valid, closed forward action.</summary>
public sealed record ActionInverseContract
{
    internal ActionInverseContract(
        ActionContractIdentity forwardAction,
        ActionPredicate requirement,
        ActionPredicate guarantee,
        AuthorityRequirement requiredAuthority,
        ActionFrame frame)
    {
        ForwardAction = forwardAction ?? throw new ArgumentNullException(nameof(forwardAction));
        Requirement = requirement ?? throw new ArgumentNullException(nameof(requirement));
        Guarantee = guarantee ?? throw new ArgumentNullException(nameof(guarantee));
        RequiredAuthority = requiredAuthority ?? throw new ArgumentNullException(nameof(requiredAuthority));
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    /// <summary>Gets the forward action whose inverse this contract specifies.</summary>
    public ActionContractIdentity ForwardAction { get; }

    /// <summary>Gets the forward action's effective guarantee, required before rollback.</summary>
    public ActionPredicate Requirement { get; }

    /// <summary>Gets the forward action's hard requirement set, re-entered after rollback.</summary>
    public ActionPredicate Guarantee { get; }

    /// <summary>Gets the forward action's semantic authority requirement.</summary>
    public AuthorityRequirement RequiredAuthority { get; }

    /// <summary>Gets the exact may-change frame the inverse must declare.</summary>
    public ActionFrame Frame { get; }
}

/// <summary>One failed or unprovable action-inverse obligation.</summary>
public sealed record ActionInverseFailure
{
    /// <summary>Initializes an immutable inverse failure.</summary>
    public ActionInverseFailure(
        ActionInverseObligation obligation,
        string message,
        IEnumerable<ActionCounterexampleFact>? counterexample = null)
    {
        if (!Enum.IsDefined(obligation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(obligation),
                obligation,
                "The inverse obligation is not defined.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var facts = counterexample?.ToImmutableArray()
            ?? ImmutableArray<ActionCounterexampleFact>.Empty;
        if (facts.Any(fact => fact is null))
        {
            throw new ArgumentException(
                "Inverse counterexamples cannot contain null entries.",
                nameof(counterexample));
        }

        Obligation = obligation;
        Message = message;
        Counterexample = facts;
    }

    /// <summary>Gets the obligation that did not prove.</summary>
    public ActionInverseObligation Obligation { get; }

    /// <summary>Gets the stable explanation.</summary>
    public string Message { get; }

    /// <summary>Gets a symbolic counterexample when an equivalence was refuted.</summary>
    public ImmutableArray<ActionCounterexampleFact> Counterexample { get; }
}

/// <summary>Immutable result of deriving and checking one action inverse.</summary>
public sealed record ActionInverseAnalysis
{
    internal ActionInverseAnalysis(
        ActionInverseAnalysisStatus status,
        ActionDescriptor forwardAction,
        ActionDescriptor? authoredInverse,
        ActionInverseContract? derivedContract,
        IEnumerable<ActionInverseFailure> failures,
        bool usesIdentityInverse)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The inverse analysis status is not defined.");
        }

        ForwardAction = forwardAction ?? throw new ArgumentNullException(nameof(forwardAction));
        ArgumentNullException.ThrowIfNull(failures);
        var failureArray = failures.ToImmutableArray();
        if (failureArray.Any(failure => failure is null))
        {
            throw new ArgumentException(
                "Inverse failures cannot contain null entries.",
                nameof(failures));
        }

        if (status == ActionInverseAnalysisStatus.Proven && !failureArray.IsEmpty)
        {
            throw new ArgumentException(
                "A proven inverse cannot contain failed or unprovable obligations.",
                nameof(failures));
        }

        if (status != ActionInverseAnalysisStatus.Proven && failureArray.IsEmpty)
        {
            throw new ArgumentException(
                "A non-proven inverse must identify a failed or unprovable obligation.",
                nameof(failures));
        }

        if (usesIdentityInverse
            && (status != ActionInverseAnalysisStatus.Proven || authoredInverse is not null))
        {
            throw new ArgumentException(
                "Only a proven inverse without an authored action can use the empty identity.",
                nameof(usesIdentityInverse));
        }

        Status = status;
        AuthoredInverse = authoredInverse;
        DerivedContract = derivedContract;
        Failures = failureArray;
        UsesIdentityInverse = usesIdentityInverse;
    }

    /// <summary>Gets the forward action.</summary>
    public ActionDescriptor ForwardAction { get; }

    /// <summary>Gets the authored inverse supplied for proof, when any.</summary>
    public ActionDescriptor? AuthoredInverse { get; }

    /// <summary>Gets the mechanically derived inverse contract when the forward contract is closed.</summary>
    public ActionInverseContract? DerivedContract { get; }

    /// <summary>Gets the aggregate proof outcome.</summary>
    public ActionInverseAnalysisStatus Status { get; }

    /// <summary>Gets deterministic failed or unprovable obligations.</summary>
    public ImmutableArray<ActionInverseFailure> Failures { get; }

    /// <summary>Gets whether the empty-frame action is undone by the distinct identity inverse.</summary>
    public bool UsesIdentityInverse { get; }

    /// <summary>Gets whether this leaf has an executable, proved inverse.</summary>
    public bool IsCompensable => Status == ActionInverseAnalysisStatus.Proven;
}

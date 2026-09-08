using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>The outcome of checking whether one action contract refines another.</summary>
public enum ActionRefinementStatus
{
    /// <summary>Every refinement obligation was proved.</summary>
    Proven,

    /// <summary>At least one refinement obligation has a concrete counterexample.</summary>
    Refuted,

    /// <summary>A custom predicate prevents a complete static proof.</summary>
    Opaque,

    /// <summary>One of the contracts is malformed or already refuted.</summary>
    Invalid,
}

/// <summary>The closed set of obligations checked by action refinement.</summary>
public enum ActionRefinementObligation
{
    /// <summary>The implementation and specification operate on the same subject.</summary>
    Subject,

    /// <summary>The specification requirement implies the implementation requirement.</summary>
    Requirements,

    /// <summary>The implementation guarantee implies the specification guarantee.</summary>
    Guarantees,

    /// <summary>The implementation requires no more authority than the specification permits.</summary>
    Authority,

    /// <summary>The implementation writes only resources in the specification frame.</summary>
    Frame,

    /// <summary>The specification contract itself is valid and closed.</summary>
    SpecificationContract,

    /// <summary>The implementation contract itself is valid and closed.</summary>
    ImplementationContract,
}

/// <summary>One failed or unprovable refinement obligation.</summary>
public sealed record ActionRefinementFailure
{
    /// <summary>Initializes an immutable failure.</summary>
    public ActionRefinementFailure(
        ActionRefinementObligation obligation,
        string message,
        IEnumerable<ActionCounterexampleFact>? counterexample = null)
    {
        if (!Enum.IsDefined(obligation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(obligation),
                obligation,
                "The refinement obligation is not defined.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var counterexampleArray = counterexample?.ToImmutableArray()
            ?? ImmutableArray<ActionCounterexampleFact>.Empty;
        if (counterexampleArray.Any(fact => fact is null))
        {
            throw new ArgumentException(
                "Refinement counterexamples cannot contain null entries.",
                nameof(counterexample));
        }

        Obligation = obligation;
        Message = message;
        Counterexample = counterexampleArray;
    }

    /// <summary>Gets the obligation that did not prove.</summary>
    public ActionRefinementObligation Obligation { get; }

    /// <summary>Gets the stable explanation.</summary>
    public string Message { get; }

    /// <summary>Gets a symbolic counterexample when the obligation was refuted.</summary>
    public ImmutableArray<ActionCounterexampleFact> Counterexample { get; }
}

/// <summary>Immutable result of a behavioral-subtyping check.</summary>
public sealed record ActionRefinementAnalysis
{
    /// <summary>Initializes an immutable analysis result.</summary>
    public ActionRefinementAnalysis(
        ActionRefinementStatus status,
        IEnumerable<ActionRefinementFailure>? failures = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The refinement status is not defined.");
        }

        var failureArray = failures?.ToImmutableArray()
            ?? ImmutableArray<ActionRefinementFailure>.Empty;
        if (failureArray.Any(failure => failure is null))
        {
            throw new ArgumentException(
                "Refinement failures cannot contain null entries.",
                nameof(failures));
        }

        if (status == ActionRefinementStatus.Proven && !failureArray.IsEmpty)
        {
            throw new ArgumentException(
                "A proven refinement cannot contain failed or unprovable obligations.",
                nameof(failures));
        }

        if (status != ActionRefinementStatus.Proven && failureArray.IsEmpty)
        {
            throw new ArgumentException(
                "A non-proven refinement must identify a failed or unprovable obligation.",
                nameof(failures));
        }

        Status = status;
        Failures = failureArray;
    }

    /// <summary>Gets the aggregate outcome.</summary>
    public ActionRefinementStatus Status { get; }

    /// <summary>Gets the failed or unprovable obligations in deterministic order.</summary>
    public ImmutableArray<ActionRefinementFailure> Failures { get; }

    /// <summary>Gets whether the implementation is a proved substitute.</summary>
    public bool IsRefinement => Status == ActionRefinementStatus.Proven && Failures.IsEmpty;
}

using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>Overall result of nonthrowing sequential contract analysis.</summary>
public enum ActionCompositionAnalysisStatus
{
    /// <summary>Every contract and seam was proved.</summary>
    Proven,

    /// <summary>Closed portions were proved, with custom predicates deferred to runtime.</summary>
    PartiallyVerified,

    /// <summary>At least one adjacent seam was refuted.</summary>
    Refuted,

    /// <summary>An action contract or composition operand was invalid.</summary>
    Invalid,
}

/// <summary>Nonthrowing result returned by <see cref="ActionCalculus.AnalyzeSequential"/>.</summary>
public sealed record ActionCompositionAnalysis
{
    internal ActionCompositionAnalysis(
        ActionCompositionAnalysisStatus status,
        CompositeActionContract? contract,
        IEnumerable<ActionCompositionSeamResult> seams,
        IEnumerable<string> errors)
    {
        Status = status;
        Contract = contract;
        ArgumentNullException.ThrowIfNull(seams);
        ArgumentNullException.ThrowIfNull(errors);
        Seams = seams.ToImmutableArray();
        Errors = errors.ToImmutableArray();
    }

    /// <summary>Gets the overall result.</summary>
    public ActionCompositionAnalysisStatus Status { get; }

    /// <summary>Gets the computed contract when the operands were structurally valid.</summary>
    public CompositeActionContract? Contract { get; }

    /// <summary>Gets all adjacent seam results available from the analysis.</summary>
    public ImmutableArray<ActionCompositionSeamResult> Seams { get; }

    /// <summary>Gets deterministic validation or proof errors.</summary>
    public ImmutableArray<string> Errors { get; }

    /// <summary>Gets whether construction may proceed.</summary>
    public bool CanCompose => Status is ActionCompositionAnalysisStatus.Proven
        or ActionCompositionAnalysisStatus.PartiallyVerified;
}

/// <summary>Thrown when sequential construction encounters an invalid contract or refuted seam.</summary>
public sealed class ActionCompositionException : Exception
{
    /// <summary>Initializes an exception from a failed nonthrowing analysis.</summary>
    public ActionCompositionException(ActionCompositionAnalysis analysis)
        : base(CreateMessage(analysis))
    {
        Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
    }

    /// <summary>Gets the complete failed analysis.</summary>
    public ActionCompositionAnalysis Analysis { get; }

    private static string CreateMessage(ActionCompositionAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (!analysis.Errors.IsEmpty)
        {
            return string.Join(" ", analysis.Errors);
        }

        var refuted = analysis.Seams.FirstOrDefault(
            seam => seam.Status == ActionCompositionSeamStatus.Refuted);
        return refuted?.Message ?? "The sequential action composition is invalid.";
    }
}

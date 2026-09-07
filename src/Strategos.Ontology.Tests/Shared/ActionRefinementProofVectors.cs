namespace Strategos.Ontology.Testing;

/// <summary>
/// Dependency-neutral action-refinement vectors compiled into the runtime and
/// workflow-generator test assemblies. Each consumer independently lowers the
/// same abstract contracts into its own production representation.
/// </summary>
internal static class ActionRefinementProofVectors
{
    internal static IReadOnlyList<ActionRefinementProofVector> All { get; } =
    [
        new(
            "contravariant-requirement-covariant-guarantee",
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageNotEqualsZero,
                authority: RefinementAuthority.Writer),
            Contract(
                requirement: RefinementPredicate.StageEqualsZeroOrOne,
                guarantee: RefinementPredicate.StageEqualsTwo,
                authority: RefinementAuthority.Reader),
            RefinementStatus.Proven,
            RefinementObligation.None,
            ExpectedGeneratorDiagnosticId: null,
            ExpectedGeneratorMessageFragment: null),
        new(
            "stronger-implementation-requirement",
            Contract(
                requirement: RefinementPredicate.StageEqualsZeroOrOne,
                guarantee: RefinementPredicate.StageEqualsTwo),
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo),
            RefinementStatus.Refuted,
            RefinementObligation.Requirements,
            "AGWF041",
            "internal seam 'RequirementStep' -> 'ImplementationStep' is not composable"),
        new(
            "weaker-implementation-guarantee",
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo),
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsOneOrTwo),
            RefinementStatus.Refuted,
            RefinementObligation.Guarantees,
            "AGWF041",
            "successful completion after 'ImplementationStep' does not establish the bound guarantee"),
        new(
            "implementation-frame-expansion",
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo),
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo,
                frame: RefinementFrame.StageAndLedger),
            RefinementStatus.Refuted,
            RefinementObligation.Frame,
            "AGWF041",
            "workflow frame escapes the bound action frame: external|ledger"),
        new(
            "implementation-authority-expansion",
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo,
                authority: RefinementAuthority.Reader),
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo,
                authority: RefinementAuthority.Writer),
            RefinementStatus.Refuted,
            RefinementObligation.Authority,
            "AGWF041",
            "workflow authority join [writer] exceeds bound authority 'reader'"),
        new(
            "implementation-subject-mismatch",
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo),
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo,
                subject: RefinementSubject.OtherDocument),
            RefinementStatus.Refuted,
            RefinementObligation.Subject,
            "AGWF041",
            "step 'ImplementationStep' has subject 'publication/OtherDocument'"),
        new(
            "opaque-implementation-requirement",
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo),
            Contract(
                requirement: RefinementPredicate.CustomReady,
                guarantee: RefinementPredicate.StageEqualsTwo),
            RefinementStatus.Opaque,
            RefinementObligation.ImplementationContract,
            "AGWF042",
            "contains opaque custom predicate(s): publication.ready.v1"),
        new(
            "invalid-implementation-contract",
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo),
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.ContradictoryStage),
            RefinementStatus.Invalid,
            RefinementObligation.ImplementationContract,
            "AGWF042",
            "has contradictory guarantees"),
        new(
            "invalid-specification-contract",
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.ContradictoryStage),
            Contract(
                requirement: RefinementPredicate.StageEqualsZero,
                guarantee: RefinementPredicate.StageEqualsTwo),
            RefinementStatus.Invalid,
            RefinementObligation.SpecificationContract,
            "AGWF042",
            "has contradictory guarantees"),
    ];

    private static RefinementContract Contract(
        RefinementPredicate requirement,
        RefinementPredicate guarantee,
        RefinementSubject subject = RefinementSubject.Document,
        RefinementFrame frame = RefinementFrame.Stage,
        RefinementAuthority authority = RefinementAuthority.None) =>
        new(subject, requirement, guarantee, frame, authority);
}

internal sealed record ActionRefinementProofVector(
    string Name,
    RefinementContract Specification,
    RefinementContract Implementation,
    RefinementStatus ExpectedRuntimeStatus,
    RefinementObligation ExpectedPrimaryRuntimeObligation,
    string? ExpectedGeneratorDiagnosticId,
    string? ExpectedGeneratorMessageFragment);

internal sealed record RefinementContract(
    RefinementSubject Subject,
    RefinementPredicate Requirement,
    RefinementPredicate Guarantee,
    RefinementFrame Frame,
    RefinementAuthority Authority);

internal enum RefinementSubject
{
    Document,
    OtherDocument,
}

internal enum RefinementPredicate
{
    StageEqualsZero,
    StageEqualsTwo,
    StageNotEqualsZero,
    StageEqualsZeroOrOne,
    StageEqualsOneOrTwo,
    ContradictoryStage,
    CustomReady,
}

internal enum RefinementFrame
{
    Stage,
    StageAndLedger,
}

internal enum RefinementAuthority
{
    None,
    Reader,
    Writer,
}

internal enum RefinementStatus
{
    Proven,
    Refuted,
    Opaque,
    Invalid,
}

internal enum RefinementObligation
{
    None,
    SpecificationContract,
    ImplementationContract,
    Subject,
    Requirements,
    Guarantees,
    Authority,
    Frame,
}

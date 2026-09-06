namespace Strategos.Ontology.Testing;

/// <summary>
/// Neutral proof vectors compiled into both the runtime and analyzer test
/// assemblies. Keeping the corpus free of product references lets both
/// surfaces interpret the same contracts independently.
/// </summary>
internal static class ActionCompositionProofVectors
{
    internal static IReadOnlyList<ActionCompositionProofVector> All { get; } =
    [
        new("equal-implies-greater", ProofComparison.Equal, 2, ProofComparison.GreaterThan, 1, true),
        new("equal-refutes-greater", ProofComparison.Equal, 1, ProofComparison.GreaterThan, 1, false),
        new("strict-implies-inclusive", ProofComparison.GreaterThan, 5, ProofComparison.GreaterThanOrEqual, 5, true),
        new("inclusive-does-not-imply-strict", ProofComparison.GreaterThanOrEqual, 5, ProofComparison.GreaterThan, 5, false),
        new("less-implies-not-equal", ProofComparison.LessThan, 0, ProofComparison.NotEqual, 0, true),
        new("not-equal-does-not-imply-positive", ProofComparison.NotEqual, 0, ProofComparison.GreaterThan, 0, false),
        new("upper-bound-strengthening", ProofComparison.LessThanOrEqual, -2, ProofComparison.LessThan, 0, true),
        new("upper-bound-weakening-refuted", ProofComparison.LessThan, 0, ProofComparison.LessThanOrEqual, -2, false),
    ];
}

internal sealed record ActionCompositionProofVector(
    string Name,
    ProofComparison UpstreamComparison,
    int UpstreamValue,
    ProofComparison DownstreamComparison,
    int DownstreamValue,
    bool IsLegal);

internal enum ProofComparison
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
}

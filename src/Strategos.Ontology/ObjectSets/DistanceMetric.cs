namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Distance metric for similarity search. Maps to pgvector operators.
/// </summary>
public enum DistanceMetric
{
    /// <summary>Cosine distance (&lt;=&gt;).</summary>
    Cosine = 0,

    /// <summary>L2 (Euclidean) distance (&lt;-&gt;).</summary>
    L2 = 1,

    /// <summary>Inner product distance (&lt;#&gt;).</summary>
    InnerProduct = 2,
}

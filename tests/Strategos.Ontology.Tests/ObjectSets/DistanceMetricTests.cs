using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class DistanceMetricTests
{
    [Test]
    public async Task DistanceMetric_Cosine_HasValueZero()
    {
        await Assert.That((int)DistanceMetric.Cosine).IsEqualTo(0);
    }

    [Test]
    public async Task DistanceMetric_L2_HasValueOne()
    {
        await Assert.That((int)DistanceMetric.L2).IsEqualTo(1);
    }

    [Test]
    public async Task DistanceMetric_InnerProduct_HasValueTwo()
    {
        await Assert.That((int)DistanceMetric.InnerProduct).IsEqualTo(2);
    }

    [Test]
    public async Task DistanceMetric_Default_IsCosine()
    {
        DistanceMetric metric = default;
        await Assert.That(metric).IsEqualTo(DistanceMetric.Cosine);
    }
}

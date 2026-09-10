using Strategos.Ontology.Extensions;

namespace Strategos.Ontology.Tests.Extensions;

public class TradeOrder
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
}

public class ProducesExtensionTests
{
    [Test]
    public async Task Produces_RegistersProducedTypeOnWorkflow()
    {
        var builder = new WorkflowMetadataBuilder("execute-trade");

        builder.Produces<TradeOrder>();

        await Assert.That(builder.ProducedTypeName).IsEqualTo(nameof(TradeOrder));
    }

    [Test]
    public async Task Produces_ReturnsBuilderForChaining()
    {
        var builder = new WorkflowMetadataBuilder("execute-trade");

        var result = builder.Produces<TradeOrder>();

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Produces_EnablesWorkflowChainInference()
    {
        var builder = new WorkflowMetadataBuilder("execute-trade");

        builder.Consumes<TestPosition>().Produces<TradeOrder>();

        await Assert.That(builder.ConsumedTypeName).IsEqualTo(nameof(TestPosition));
        await Assert.That(builder.ProducedTypeName).IsEqualTo(nameof(TradeOrder));
    }
}

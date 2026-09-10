using Strategos.Ontology.Extensions;

namespace Strategos.Ontology.Tests.Extensions;

public class WorkflowOntologyExtensionsTests
{
    [Test]
    public async Task Consumes_RegistersConsumedTypeOnWorkflow()
    {
        var builder = new WorkflowMetadataBuilder("execute-trade");

        builder.Consumes<TestPosition>();

        await Assert.That(builder.ConsumedTypeName).IsEqualTo(nameof(TestPosition));
    }

    [Test]
    public async Task Consumes_ReturnsBuilderForChaining()
    {
        var builder = new WorkflowMetadataBuilder("execute-trade");

        var result = builder.Consumes<TestPosition>();

        await Assert.That(result).IsSameReferenceAs(builder);
    }
}

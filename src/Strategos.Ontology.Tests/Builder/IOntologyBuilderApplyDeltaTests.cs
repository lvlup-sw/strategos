using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

/// <summary>
/// DR-5 (Task 10 + Task 11): exercises the <c>IOntologyBuilder.ApplyDelta</c>
/// switch across each <see cref="OntologyDelta"/> variant. Task 10 covers
/// <see cref="OntologyDelta.AddObjectType"/>; Task 11 covers the remaining
/// seven variants plus the unknown-variant fallthrough.
/// </summary>
public class IOntologyBuilderApplyDeltaTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    private const string SourceId = "marten-typescript";

    [Test]
    public async Task ApplyDelta_AddObjectType_RegistersDescriptor()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
        };

        OntologyDelta delta = new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        builder.ApplyDelta(delta);

        var built = ((OntologyBuilder)builder).ObjectTypes;
        await Assert.That(built.Count).IsEqualTo(1);
        await Assert.That(built[0].Name).IsEqualTo("Position");
        await Assert.That(built[0].Source).IsEqualTo(DescriptorSource.Ingested);
    }

    [Test]
    public async Task ApplyDelta_NullDelta_Throws()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        ArgumentNullException? caught = null;
        try
        {
            builder.ApplyDelta(null!);
        }
        catch (ArgumentNullException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
    }
}

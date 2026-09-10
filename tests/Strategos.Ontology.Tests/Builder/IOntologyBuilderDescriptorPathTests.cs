using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

/// <summary>
/// DR-5 (Task 9): the <see cref="IOntologyBuilder.ObjectTypeFromDescriptor"/>
/// path lets ingestion sources register a fully-specified
/// <see cref="ObjectTypeDescriptor"/> without going through the
/// expression-tree DSL — necessary because ingested types may only be
/// known by <c>SymbolKey</c>, with no loaded CLR type.
/// </summary>
public class IOntologyBuilderDescriptorPathTests
{
    [Test]
    public async Task ObjectTypeFromDescriptor_IngestedDescriptor_AppearsInBuiltGraph()
    {
        IOntologyBuilder builder = new OntologyBuilder("Identity");

        var ingested = new ObjectTypeDescriptor
        {
            Name = "User",
            DomainName = "Identity",
            ClrType = null,
            SymbolKey = "scip-typescript . ./src/user.ts#User",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript",
        };

        builder.ObjectTypeFromDescriptor(ingested);

        var built = ((OntologyBuilder)builder).ObjectTypes;
        await Assert.That(built.Count).IsEqualTo(1);
        await Assert.That(built[0].Name).IsEqualTo("User");
        await Assert.That(built[0].Source).IsEqualTo(DescriptorSource.Ingested);
        await Assert.That(built[0].SymbolKey).IsEqualTo("scip-typescript . ./src/user.ts#User");
        await Assert.That(built[0].ClrType).IsNull();
    }

    [Test]
    public async Task ObjectTypeFromDescriptor_HandAuthoredDescriptor_PreservesSourceHandAuthored()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        builder.ObjectTypeFromDescriptor(descriptor);

        var built = ((OntologyBuilder)builder).ObjectTypes;
        await Assert.That(built.Count).IsEqualTo(1);
        await Assert.That(built[0].Source).IsEqualTo(DescriptorSource.HandAuthored);
        await Assert.That(built[0].ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task ObjectTypeFromDescriptor_NullDescriptor_Throws()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        ArgumentNullException? caught = null;
        try
        {
            builder.ObjectTypeFromDescriptor(null!);
        }
        catch (ArgumentNullException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
    }
}

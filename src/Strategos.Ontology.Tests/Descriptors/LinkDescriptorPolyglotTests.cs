using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class LinkDescriptorPolyglotTests
{
    [Test]
    public async Task TargetSymbolKey_DefaultValue_IsNull()
    {
        var descriptor = new LinkDescriptor("Orders", "TradeOrder", LinkCardinality.OneToMany);

        await Assert.That(descriptor.TargetSymbolKey).IsNull();
    }

    [Test]
    public async Task Source_DefaultValue_IsHandAuthored()
    {
        var descriptor = new LinkDescriptor("Orders", "TradeOrder", LinkCardinality.OneToMany);

        await Assert.That(descriptor.Source).IsEqualTo(DescriptorSource.HandAuthored);
    }

    [Test]
    public async Task Ctor_SymbolKeyTargetOnly_TargetTypeNameStillPresent()
    {
        // A link descriptor with a SymbolKey target (ingestion path).
        // TargetTypeName remains required (used by hand-authored paths and as
        // a string-keyed display); TargetSymbolKey rides alongside for SCIP
        // identity per DR-1.
        var descriptor = new LinkDescriptor("Authors", "User", LinkCardinality.OneToMany)
        {
            TargetSymbolKey = "scip-typescript . ./src/user.ts#User",
            Source = DescriptorSource.Ingested,
        };

        await Assert.That(descriptor.TargetTypeName).IsEqualTo("User");
        await Assert.That(descriptor.TargetSymbolKey).IsEqualTo("scip-typescript . ./src/user.ts#User");
        await Assert.That(descriptor.Source).IsEqualTo(DescriptorSource.Ingested);
    }
}

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Merge;

namespace Strategos.Ontology.Tests.Merge;

/// <summary>
/// MergeTwo lattice coverage for the DR-4 association surface. The reconstructed
/// descriptor must carry <see cref="ObjectTypeDescriptor.AssociationEndpoints"/>
/// through the fold (hand wins; fall back to ingested when hand's is empty),
/// mirroring the other identity-field lattice rules. Dropping it would leave a
/// merged association descriptor with no endpoints.
/// </summary>
public class MergeTwoAssociationTests
{
    private sealed class HandModel
    {
        public Guid Id { get; set; }
    }

    [Test]
    public async Task MergeTwo_AssociationEndpoints_PreservedFromHand()
    {
        var endpoints = new[]
        {
            new AssociationEndpoint("From", "Person"),
            new AssociationEndpoint("To", "Company"),
        };

        var hand = new ObjectTypeDescriptor
        {
            Name = "Employment",
            DomainName = "assoc",
            ClrType = typeof(HandModel),
            LanguageId = "dotnet",
            Source = DescriptorSource.HandAuthored,
            Kind = ObjectKind.Association,
            AssociationEndpoints = endpoints,
        };

        var ingested = new ObjectTypeDescriptor
        {
            Name = "Employment",
            DomainName = "assoc",
            SymbolKey = "scip-typescript ./mod#Employment",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
        };

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.AssociationEndpoints).HasCount().EqualTo(2);
        await Assert.That(merged.AssociationEndpoints[0].Role).IsEqualTo("From");
        await Assert.That(merged.AssociationEndpoints[0].DescriptorName).IsEqualTo("Person");
        await Assert.That(merged.AssociationEndpoints[1].Role).IsEqualTo("To");
        await Assert.That(merged.AssociationEndpoints[1].DescriptorName).IsEqualTo("Company");
    }

    [Test]
    public async Task MergeTwo_AssociationEndpoints_FallsBackToIngestedWhenHandEmpty()
    {
        var ingestedEndpoints = new[]
        {
            new AssociationEndpoint("Src", "Node"),
            new AssociationEndpoint("Tgt", "Node"),
        };

        // Hand contributes no endpoints (e.g. a symbol-only hand registration);
        // the ingested side supplied them. Mirrors the ClrType lattice: hand
        // wins, fall back to ingested.
        var hand = new ObjectTypeDescriptor
        {
            Name = "Link",
            DomainName = "assoc",
            ClrType = typeof(HandModel),
            Source = DescriptorSource.HandAuthored,
            Kind = ObjectKind.Association,
        };

        var ingested = new ObjectTypeDescriptor
        {
            Name = "Link",
            DomainName = "assoc",
            SymbolKey = "scip-typescript ./mod#Link",
            Source = DescriptorSource.Ingested,
            Kind = ObjectKind.Association,
            AssociationEndpoints = ingestedEndpoints,
        };

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.AssociationEndpoints).HasCount().EqualTo(2);
        await Assert.That(merged.AssociationEndpoints[0].Role).IsEqualTo("Src");
        await Assert.That(merged.AssociationEndpoints[1].Role).IsEqualTo("Tgt");
    }
}

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Merge;

namespace Strategos.Ontology.Tests.Merge;

/// <summary>
/// Task 15 — MergeTwo property + link union tests.
/// </summary>
/// <remarks>
/// Per-name union with hand-on-conflict precedence. Ingested-only entries
/// arrive with <see cref="DescriptorSource.Ingested"/> tagging so callers
/// (and downstream AONT201–AONT206 diagnostics) can distinguish origins.
/// </remarks>
public class MergeTwoPropertyLinkTests
{
    private sealed class HandModel { public Guid Id { get; set; } }

    private static ObjectTypeDescriptor BaseHand(params PropertyDescriptor[] props) => new()
    {
        Name = "TradeOrder",
        DomainName = "trading",
        ClrType = typeof(HandModel),
        Properties = props,
        Source = DescriptorSource.HandAuthored,
    };

    private static ObjectTypeDescriptor BaseHandWithLinks(params LinkDescriptor[] links) => new()
    {
        Name = "TradeOrder",
        DomainName = "trading",
        ClrType = typeof(HandModel),
        Links = links,
        Source = DescriptorSource.HandAuthored,
    };

    private static ObjectTypeDescriptor BaseIngested(params PropertyDescriptor[] props) => new()
    {
        Name = "TradeOrder",
        DomainName = "trading",
        SymbolKey = "scip-typescript ./mod#TradeOrder",
        Properties = props,
        Source = DescriptorSource.Ingested,
    };

    private static ObjectTypeDescriptor BaseIngestedWithLinks(params LinkDescriptor[] links) => new()
    {
        Name = "TradeOrder",
        DomainName = "trading",
        SymbolKey = "scip-typescript ./mod#TradeOrder",
        Links = links,
        Source = DescriptorSource.Ingested,
    };

    // --- Properties ---

    [Test]
    public async Task MergeTwo_HandPropertyMissingFromIngested_BothPresent()
    {
        var handOnly = new PropertyDescriptor("Quantity", typeof(int))
        { Source = DescriptorSource.HandAuthored };
        var ingestedOnly = new PropertyDescriptor("Price", typeof(decimal))
        { Source = DescriptorSource.Ingested };

        var hand = BaseHand(handOnly);
        var ingested = BaseIngested(ingestedOnly);

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.Properties.Count).IsEqualTo(2);
        await Assert.That(merged.Properties.Any(p => p.Name == "Quantity")).IsTrue();
        await Assert.That(merged.Properties.Any(p => p.Name == "Price")).IsTrue();
    }

    [Test]
    public async Task MergeTwo_PropertyConflict_HandWins()
    {
        var handProp = new PropertyDescriptor("Quantity", typeof(int), IsRequired: true)
        { Source = DescriptorSource.HandAuthored };
        var ingestedProp = new PropertyDescriptor("Quantity", typeof(long), IsRequired: false)
        { Source = DescriptorSource.Ingested };

        var hand = BaseHand(handProp);
        var ingested = BaseIngested(ingestedProp);

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.Properties.Count).IsEqualTo(1);
        var quantity = merged.Properties.Single(p => p.Name == "Quantity");
        await Assert.That(quantity.PropertyType).IsEqualTo(typeof(int));
        await Assert.That(quantity.IsRequired).IsTrue();
        await Assert.That(quantity.Source).IsEqualTo(DescriptorSource.HandAuthored);
    }

    [Test]
    public async Task MergeTwo_IngestedOnlyProperty_TaggedIngested()
    {
        // Even if the caller hands us an ingested-only PropertyDescriptor
        // whose Source defaulted to HandAuthored (e.g. raw constructor),
        // MergeTwo must restamp it as Ingested so downstream diagnostics
        // can distinguish origin.
        var ingestedRaw = new PropertyDescriptor("Notes", typeof(string));
        // ^ Source defaults to HandAuthored

        var hand = BaseHand();
        var ingested = BaseIngested(ingestedRaw);

        var merged = MergeTwo.Merge(hand, ingested);

        var notes = merged.Properties.Single(p => p.Name == "Notes");
        await Assert.That(notes.Source).IsEqualTo(DescriptorSource.Ingested);
    }

    // --- Links ---

    [Test]
    public async Task MergeTwo_HandLinkMissingFromIngested_BothPresent()
    {
        var handOnly = new LinkDescriptor("Lines", "OrderLine", LinkCardinality.OneToMany)
        { Source = DescriptorSource.HandAuthored };
        var ingestedOnly = new LinkDescriptor("Payments", "Payment", LinkCardinality.OneToMany)
        { Source = DescriptorSource.Ingested };

        var hand = BaseHandWithLinks(handOnly);
        var ingested = BaseIngestedWithLinks(ingestedOnly);

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.Links.Count).IsEqualTo(2);
        await Assert.That(merged.Links.Any(l => l.Name == "Lines")).IsTrue();
        await Assert.That(merged.Links.Any(l => l.Name == "Payments")).IsTrue();
    }

    [Test]
    public async Task MergeTwo_LinkConflict_HandWins()
    {
        var handLink = new LinkDescriptor("Lines", "OrderLine", LinkCardinality.OneToMany)
        { Source = DescriptorSource.HandAuthored };
        var ingestedLink = new LinkDescriptor("Lines", "DifferentTarget", LinkCardinality.OneToOne)
        { Source = DescriptorSource.Ingested };

        var hand = BaseHandWithLinks(handLink);
        var ingested = BaseIngestedWithLinks(ingestedLink);

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.Links.Count).IsEqualTo(1);
        var lines = merged.Links.Single(l => l.Name == "Lines");
        await Assert.That(lines.TargetTypeName).IsEqualTo("OrderLine");
        await Assert.That(lines.Cardinality).IsEqualTo(LinkCardinality.OneToMany);
        await Assert.That(lines.Source).IsEqualTo(DescriptorSource.HandAuthored);
    }

    [Test]
    public async Task MergeTwo_IngestedOnlyLink_TaggedIngested()
    {
        var ingestedRaw = new LinkDescriptor("Payments", "Payment", LinkCardinality.OneToMany);
        // ^ Source defaults to HandAuthored

        var hand = BaseHandWithLinks();
        var ingested = BaseIngestedWithLinks(ingestedRaw);

        var merged = MergeTwo.Merge(hand, ingested);

        var payments = merged.Links.Single(l => l.Name == "Payments");
        await Assert.That(payments.Source).IsEqualTo(DescriptorSource.Ingested);
    }

    [Test]
    public async Task MergeTwo_DuplicateIngestedProperty_CollapsesToSingleEntry()
    {
        // Two ingested entries with the same Name — mechanical noise from
        // a misbehaving ingester. The seen-set bookkeeping must collapse
        // these to a single output so downstream callers don't observe
        // a property twice.
        var dup1 = new PropertyDescriptor("Notional", typeof(decimal))
        { Source = DescriptorSource.Ingested };
        var dup2 = new PropertyDescriptor("Notional", typeof(decimal))
        { Source = DescriptorSource.Ingested };

        var hand = BaseHand();
        var ingested = BaseIngested(dup1, dup2);

        var merged = MergeTwo.Merge(hand, ingested);

        var hits = merged.Properties.Where(p => p.Name == "Notional").ToList();
        await Assert.That(hits.Count).IsEqualTo(1);
        await Assert.That(hits[0].Source).IsEqualTo(DescriptorSource.Ingested);
    }

    [Test]
    public async Task MergeTwo_DuplicateIngestedLink_CollapsesToSingleEntry()
    {
        var dup1 = new LinkDescriptor("Payments", "Payment", LinkCardinality.OneToMany);
        var dup2 = new LinkDescriptor("Payments", "Payment", LinkCardinality.OneToMany);

        var hand = BaseHandWithLinks();
        var ingested = BaseIngestedWithLinks(dup1, dup2);

        var merged = MergeTwo.Merge(hand, ingested);

        var hits = merged.Links.Where(l => l.Name == "Payments").ToList();
        await Assert.That(hits.Count).IsEqualTo(1);
        await Assert.That(hits[0].Source).IsEqualTo(DescriptorSource.Ingested);
    }
}

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Merge;

namespace Strategos.Ontology.Tests.Merge;

/// <summary>
/// Task 14 — MergeTwo lattice identity field tests (DR-6 lateral rule).
/// </summary>
/// <remarks>
/// Per basileus ADR §9.2 the identity-field lattice rule is:
/// <list type="bullet">
/// <item><description><c>ClrType</c>: hand wins, fallback to ingested.</description></item>
/// <item><description><c>SymbolKey</c>: ingested wins (SCIP authoritative), fallback to hand.</description></item>
/// <item><description><c>SymbolFqn</c>: ingested wins, fallback to hand.</description></item>
/// <item><description><c>LanguageId</c>: hand wins.</description></item>
/// <item><description><c>Source</c>: HandAuthored (record-level — hand wins on composition).</description></item>
/// <item><description><c>Actions</c>, <c>Events</c>, <c>Lifecycle</c>: hand only.</description></item>
/// </list>
/// </remarks>
public class MergeTwoIdentityTests
{
    private sealed class HandModel { public Guid Id { get; set; } }

    private static ObjectTypeDescriptor HandDescriptor() => new()
    {
        Name = "TradeOrder",
        DomainName = "trading",
        ClrType = typeof(HandModel),
        LanguageId = "dotnet",
        Source = DescriptorSource.HandAuthored,
    };

    private static ObjectTypeDescriptor IngestedDescriptor() => new()
    {
        Name = "TradeOrder",
        DomainName = "trading",
        SymbolKey = "scip-typescript ./mod#TradeOrder",
        SymbolFqn = "Mod.TradeOrder",
        LanguageId = "typescript",
        Source = DescriptorSource.Ingested,
        SourceId = "scip-source",
    };

    [Test]
    public async Task MergeTwo_ClrType_HandWinsOverIngested()
    {
        var hand = HandDescriptor();
        var ingested = IngestedDescriptor() with { ClrType = typeof(string) };

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.ClrType).IsEqualTo(typeof(HandModel));
    }

    [Test]
    public async Task MergeTwo_ClrType_FallsBackToIngestedWhenHandNull()
    {
        // Hand-authored descriptor with no ClrType (e.g. constructed via the
        // descriptor-by-name overload with only SymbolKey). The lattice rule
        // says hand wins, fallback to ingested.
        var hand = new ObjectTypeDescriptor
        {
            Name = "TradeOrder",
            DomainName = "trading",
            SymbolKey = "hand-only-symbol",
            Source = DescriptorSource.HandAuthored,
        };
        var ingested = IngestedDescriptor() with { ClrType = typeof(HandModel) };

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.ClrType).IsEqualTo(typeof(HandModel));
    }

    [Test]
    public async Task MergeTwo_SymbolKey_IngestedWinsOverHand()
    {
        var hand = HandDescriptor() with { SymbolKey = "hand-key" };
        var ingested = IngestedDescriptor();

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.SymbolKey).IsEqualTo("scip-typescript ./mod#TradeOrder");
    }

    [Test]
    public async Task MergeTwo_SymbolKey_FallsBackToHandWhenIngestedNull()
    {
        var hand = HandDescriptor() with { SymbolKey = "hand-key" };
        var ingested = new ObjectTypeDescriptor
        {
            Name = "TradeOrder",
            DomainName = "trading",
            ClrType = typeof(HandModel),
            Source = DescriptorSource.Ingested,
        };

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.SymbolKey).IsEqualTo("hand-key");
    }

    [Test]
    public async Task MergeTwo_LanguageId_HandWins()
    {
        var hand = HandDescriptor();
        var ingested = IngestedDescriptor();

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.LanguageId).IsEqualTo("dotnet");
    }

    [Test]
    public async Task MergeTwo_SymbolFqn_IngestedWinsOverHand()
    {
        var hand = HandDescriptor() with { SymbolFqn = "Hand.TradeOrder" };
        var ingested = IngestedDescriptor();

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.SymbolFqn).IsEqualTo("Mod.TradeOrder");
    }

    [Test]
    public async Task MergeTwo_Source_AlwaysHandAuthoredOnComposition()
    {
        var hand = HandDescriptor();
        var ingested = IngestedDescriptor();

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.Source).IsEqualTo(DescriptorSource.HandAuthored);
    }

    [Test]
    public async Task MergeTwo_NameAndDomain_TakenFromHand()
    {
        var hand = HandDescriptor();
        var ingested = IngestedDescriptor() with { Name = "OtherName", DomainName = "other" };

        var merged = MergeTwo.Merge(hand, ingested);

        await Assert.That(merged.Name).IsEqualTo("TradeOrder");
        await Assert.That(merged.DomainName).IsEqualTo("trading");
    }
}

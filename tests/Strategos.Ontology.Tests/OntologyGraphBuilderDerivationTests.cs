using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

public record DerivTestPosition(
    Guid Id,
    string Symbol,
    decimal Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal UnrealizedPnL,
    decimal PortfolioWeight);

public record DerivCycleA(Guid Id, decimal X, decimal Y);

public class DerivationDomainOntology : DomainOntology
{
    public override string DomainName => "deriv-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<DerivTestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
            obj.Property(p => p.Quantity);
            obj.Property(p => p.AverageCost);
            obj.Property(p => p.CurrentPrice);
            obj.Property(p => p.UnrealizedPnL)
                .Computed()
                .DerivedFrom(p => p.Quantity, p => p.AverageCost, p => p.CurrentPrice);
            obj.Property(p => p.PortfolioWeight)
                .Computed()
                .DerivedFrom(p => p.UnrealizedPnL);
        });
    }
}

public class CyclicDerivationDomainOntology : DomainOntology
{
    public override string DomainName => "cycle-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<DerivCycleA>(obj =>
        {
            obj.Key(a => a.Id);
            obj.Property(a => a.X)
                .Computed()
                .DerivedFrom(a => a.Y);
            obj.Property(a => a.Y)
                .Computed()
                .DerivedFrom(a => a.X);
        });
    }
}

public class OntologyGraphBuilderDerivationTests
{
    [Test]
    public async Task TransitiveDerivation_ComputedCorrectly()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new DerivationDomainOntology());
        var graph = graphBuilder.Build();

        var position = graph.GetObjectType("deriv-test", "DerivTestPosition")!;
        var pnl = position.Properties.First(p => p.Name == "UnrealizedPnL");
        var weight = position.Properties.First(p => p.Name == "PortfolioWeight");

        // UnrealizedPnL directly depends on Quantity, AverageCost, CurrentPrice
        await Assert.That(pnl.DerivedFrom.Count).IsEqualTo(3);
        // Transitive also includes those same sources (they are leaf nodes)
        await Assert.That(pnl.TransitiveDerivedFrom.Count).IsEqualTo(3);

        // PortfolioWeight directly depends on UnrealizedPnL
        await Assert.That(weight.DerivedFrom.Count).IsEqualTo(1);
        // Transitively depends on UnrealizedPnL + Quantity + AverageCost + CurrentPrice
        await Assert.That(weight.TransitiveDerivedFrom.Count).IsEqualTo(4);
    }

    [Test]
    public async Task TransitiveDerivation_IncludesExternalSources()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new DerivationDomainOntology());
        var graph = graphBuilder.Build();

        var position = graph.GetObjectType("deriv-test", "DerivTestPosition")!;
        var pnl = position.Properties.First(p => p.Name == "UnrealizedPnL");

        var localSources = pnl.TransitiveDerivedFrom
            .Where(s => s.Kind == DerivationSourceKind.Local)
            .Select(s => s.PropertyName)
            .ToList();

        await Assert.That(localSources).Contains("Quantity");
        await Assert.That(localSources).Contains("AverageCost");
        await Assert.That(localSources).Contains("CurrentPrice");
    }

    [Test]
    public async Task CycleDetection_ThrowsOntologyCompositionException()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new CyclicDerivationDomainOntology());

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("cycle");
    }

    [Test]
    public async Task NonComputedProperties_NotAffectedByDerivation()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new DerivationDomainOntology());
        var graph = graphBuilder.Build();

        var position = graph.GetObjectType("deriv-test", "DerivTestPosition")!;
        var symbol = position.Properties.First(p => p.Name == "Symbol");

        await Assert.That(symbol.DerivedFrom.Count).IsEqualTo(0);
        await Assert.That(symbol.TransitiveDerivedFrom.Count).IsEqualTo(0);
    }
}

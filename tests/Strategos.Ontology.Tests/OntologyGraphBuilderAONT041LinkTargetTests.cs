using System.Collections.Immutable;

using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests;

public class TradeOrder
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
}

public class Portfolio
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class TradeOrderExplicitNameOntology : DomainOntology
{
    public override string DomainName => "trading-explicit";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TradeOrder>("open_orders", obj =>
        {
            obj.Key(t => t.Id);
        });

        builder.Object<Portfolio>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<TradeOrder>("Orders");
        });
    }
}

public class TradeOrderDefaultNameOntology : DomainOntology
{
    public override string DomainName => "trading-default";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TradeOrder>(obj =>
        {
            obj.Key(t => t.Id);
        });

        builder.Object<Portfolio>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<TradeOrder>("Orders");
        });
    }
}

// DR-8 Task 31: CLR-keyed multi-registration of TradeOrder under two names ("orders" and
// "open_orders") in the same domain. Portfolio links to the simple CLR name "TradeOrder".
// This exercises the *core* multi-registration check (not the explicit-name sub-rule), and
// is what the polyglot retarget must preserve — same identity under multiple names trips
// AONT041 whether the identity is keyed by ClrType (this fixture) or SymbolKey (Task 31's
// new test below).
public class TradeOrderClrMultiRegOntology : DomainOntology
{
    public override string DomainName => "trading-clr-multireg";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TradeOrder>("orders", obj =>
        {
            obj.Key(t => t.Id);
        });

        builder.Object<TradeOrder>("open_orders", obj =>
        {
            obj.Key(t => t.Id);
        });

        builder.Object<Portfolio>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<TradeOrder>("Orders");
        });
    }
}

public class OntologyGraphBuilderAONT041LinkTargetTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task AONT041_LinkTargetWithExplicitDescriptorName_ThrowsCompositionException()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TradeOrderExplicitNameOntology>();

        var exception = await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));

        await Assert.That(exception!.Message).Contains("AONT041");
        await Assert.That(exception.Message).Contains("open_orders");
    }

    [Test]
    public async Task AONT041_LinkTargetWithDefaultDescriptorName_DoesNotThrow()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TradeOrderDefaultNameOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph).IsNotNull();
        await Assert.That(graph.ObjectTypes.Any(ot => ot.Name == "TradeOrder")).IsTrue();
    }

    // DR-8 Task 31: ClrType-keyed multi-registration still fires after the retarget.
    // TradeOrder is registered under two distinct names in the same domain; Portfolio has
    // HasMany<TradeOrder>("Orders") which writes TargetTypeName="TradeOrder" — the polyglot
    // (DomainName, Name)-keyed lookup must resolve through that simple-name link to find
    // a multi-registered identity and trip AONT041.
    [Test]
    public async Task AONT041_ClrTypeMultiRegistrationInLink_StillFires()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TradeOrderClrMultiRegOntology>();

        var exception = await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));

        await Assert.That(exception!.Message).Contains("AONT041");
        // Both registered names should appear in the diagnostic so operators can locate
        // the conflicting registrations.
        await Assert.That(exception.Message).Contains("orders");
        await Assert.That(exception.Message).Contains("open_orders");
    }

    // DR-8 Task 31: SymbolKey-keyed multi-registration in a link participant trips AONT041.
    // Two ingested descriptors share the same SymbolKey but register under different names
    // ("User" and "Account") in the same domain — the polyglot identity reverse index keys
    // these by SymbolKey since ClrType is null. A separate hand-authored "Portfolio"-style
    // descriptor declares a link with TargetTypeName="User", which after retarget resolves
    // (Domain, "User") → the SymbolKey-keyed multi-registered descriptor.
    [Test]
    public async Task AONT041_SymbolKeyKeyedMultiRegistration_InLinkParticipant_Fires()
    {
        const string sharedSymbolKey = "scip-typescript ./mod#User";
        const string domain = "polyglot-trading";

        var userDescriptor = new ObjectTypeDescriptor
        {
            Name = "User",
            DomainName = domain,
            ClrType = null,
            SymbolKey = sharedSymbolKey,
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript-a",
        };

        var accountDescriptor = new ObjectTypeDescriptor
        {
            Name = "Account",
            DomainName = domain,
            ClrType = null,
            SymbolKey = sharedSymbolKey,
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript-b",
        };

        // Holder is an ingested-only descriptor that declares a link to "User" by name.
        // Carrying the link directly on the descriptor (rather than via AddLink delta)
        // lets the AONT041 source-side scan reach it without additional fixture wiring.
        var holderDescriptor = new ObjectTypeDescriptor
        {
            Name = "Holder",
            DomainName = domain,
            ClrType = null,
            SymbolKey = "scip-typescript ./mod#Holder",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript-holder",
            Links = new List<LinkDescriptor>
            {
                new LinkDescriptor("Users", "User", LinkCardinality.OneToMany)
                {
                    Source = DescriptorSource.Ingested,
                },
            }.AsReadOnly(),
        };

        var source = new TestOntologySource
        {
            SourceId = "marten-typescript-multireg",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(userDescriptor)
                {
                    SourceId = "marten-typescript-a",
                    Timestamp = Timestamp,
                },
                new OntologyDelta.AddObjectType(accountDescriptor)
                {
                    SourceId = "marten-typescript-b",
                    Timestamp = Timestamp,
                },
                new OntologyDelta.AddObjectType(holderDescriptor)
                {
                    SourceId = "marten-typescript-holder",
                    Timestamp = Timestamp,
                }),
        };

        var graphBuilder = new OntologyGraphBuilder()
            .AddSources(new IOntologySource[] { source });

        var exception = await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));

        await Assert.That(exception!.Message).Contains("AONT041");
        // The diagnostic must surface both same-SymbolKey registrations and the link
        // that introduced the participation.
        await Assert.That(exception.Message).Contains("User");
        await Assert.That(exception.Message).Contains("Account");
    }
}

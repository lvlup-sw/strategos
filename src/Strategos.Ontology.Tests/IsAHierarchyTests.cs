// =============================================================================
// <copyright file="IsAHierarchyTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests;

// --- Test domain models for IS-A hierarchy tests ---

public record IsAFinancialTransaction(Guid Id, string Type, decimal Amount);

public record IsAPayment(Guid Id, string Type, decimal Amount, string Recipient);

public record IsARefund(Guid Id, string Type, decimal Amount, string OriginalPaymentId);

public record IsADeposit(Guid Id, string Type, decimal Amount, string AccountId);

public record IsAUnregisteredParentChild(Guid Id, string Name);

// --- Ontology with valid IS-A hierarchy ---

public class IsAValidOntology : DomainOntology
{
    public override string DomainName => "finance";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<IsAFinancialTransaction>(obj =>
        {
            obj.Key(t => t.Id);
            obj.Property(t => t.Type).Required();
            obj.Property(t => t.Amount);
        });

        builder.Object<IsAPayment>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Type).Required();
            obj.Property(p => p.Amount);
            obj.Property(p => p.Recipient);
            obj.IsA<IsAFinancialTransaction>();
        });

        builder.Object<IsARefund>(obj =>
        {
            obj.Key(r => r.Id);
            obj.Property(r => r.Type).Required();
            obj.Property(r => r.Amount);
            obj.Property(r => r.OriginalPaymentId);
            obj.IsA<IsAFinancialTransaction>();
        });
    }
}

// --- Ontology with transitive IS-A chain ---

public class IsATransitiveOntology : DomainOntology
{
    public override string DomainName => "finance";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<IsAFinancialTransaction>(obj =>
        {
            obj.Key(t => t.Id);
            obj.Property(t => t.Type).Required();
            obj.Property(t => t.Amount);
        });

        builder.Object<IsAPayment>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Type).Required();
            obj.Property(p => p.Amount);
            obj.Property(p => p.Recipient);
            obj.IsA<IsAFinancialTransaction>();
        });

        builder.Object<IsADeposit>(obj =>
        {
            obj.Key(d => d.Id);
            obj.Property(d => d.Type).Required();
            obj.Property(d => d.Amount);
            obj.Property(d => d.AccountId);
            obj.IsA<IsAPayment>();
        });
    }
}

// --- Ontology with unregistered parent ---

public class IsAUnregisteredParentOntology : DomainOntology
{
    public override string DomainName => "test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<IsAUnregisteredParentChild>(obj =>
        {
            obj.Key(c => c.Id);
            obj.Property(c => c.Name);
            obj.IsA<IsAFinancialTransaction>();  // FinancialTransaction not registered
        });
    }
}

// --- Ontology with cyclic hierarchy ---

public record IsACycleA(Guid Id, string Name);

public record IsACycleB(Guid Id, string Name);

public class IsACyclicOntology : DomainOntology
{
    public override string DomainName => "test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<IsACycleA>(obj =>
        {
            obj.Key(a => a.Id);
            obj.Property(a => a.Name);
            obj.IsA<IsACycleB>();
        });

        builder.Object<IsACycleB>(obj =>
        {
            obj.Key(b => b.Id);
            obj.Property(b => b.Name);
            obj.IsA<IsACycleA>();
        });
    }
}

// --- Tests ---

public class IsAHierarchyTests
{
    [Test]
    public async Task IsA_RegisteredParent_SetsParentTypeName()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsAValidOntology>();

        var graph = graphBuilder.Build();
        var payment = graph.ObjectTypes.First(ot => ot.Name == "IsAPayment");

        await Assert.That(payment.ParentTypeName).IsEqualTo("IsAFinancialTransaction");
    }

    [Test]
    public async Task IsA_UnregisteredParent_ThrowsOnBuild()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsAUnregisteredParentOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));
    }

    [Test]
    public async Task IsA_CyclicHierarchy_ThrowsOnBuild()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsACyclicOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));
    }

    [Test]
    public async Task IsA_TransitiveChain_Validates()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsATransitiveOntology>();

        var graph = graphBuilder.Build();
        var deposit = graph.ObjectTypes.First(ot => ot.Name == "IsADeposit");

        await Assert.That(deposit.ParentTypeName).IsEqualTo("IsAPayment");
    }

    [Test]
    public async Task GetSubtypes_ReturnsDirectChildren()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsAValidOntology>();

        var graph = graphBuilder.Build();
        var subtypes = graph.GetSubtypes("IsAFinancialTransaction");

        await Assert.That(subtypes).HasCount().EqualTo(2);
        var names = subtypes.Select(s => s.Name).OrderBy(n => n).ToList();
        await Assert.That(names[0]).IsEqualTo("IsAPayment");
        await Assert.That(names[1]).IsEqualTo("IsARefund");
    }

    [Test]
    public async Task GetObjectTypes_IncludeSubtypes_ReturnsHierarchy()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsAValidOntology>();
        var graph = graphBuilder.Build();
        var query = new OntologyQueryService(graph);

        var types = query.GetObjectTypes(includeSubtypes: true);

        // Should return all 3 types
        await Assert.That(types).HasCount().EqualTo(3);
    }

    [Test]
    public async Task GetObjectTypes_IncludeSubtypesFalse_ReturnsOnlyExact()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsAValidOntology>();
        var graph = graphBuilder.Build();
        var query = new OntologyQueryService(graph);

        var types = query.GetObjectTypes(includeSubtypes: false);

        // Should return all 3 types (no filtering without specific type filter)
        await Assert.That(types).HasCount().EqualTo(3);
    }

    [Test]
    public async Task IsA_ParentTypeName_MatchesClrTypeName()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsAValidOntology>();

        var graph = graphBuilder.Build();
        var payment = graph.ObjectTypes.First(ot => ot.Name == "IsAPayment");

        await Assert.That(payment.ParentType).IsEqualTo(typeof(IsAFinancialTransaction));
        await Assert.That(payment.ParentTypeName).IsEqualTo(typeof(IsAFinancialTransaction).Name);
    }

    [Test]
    public async Task IsA_MultipleChildren_AllHaveSameParent()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsAValidOntology>();

        var graph = graphBuilder.Build();
        var payment = graph.ObjectTypes.First(ot => ot.Name == "IsAPayment");
        var refund = graph.ObjectTypes.First(ot => ot.Name == "IsARefund");

        await Assert.That(payment.ParentTypeName).IsEqualTo("IsAFinancialTransaction");
        await Assert.That(refund.ParentTypeName).IsEqualTo("IsAFinancialTransaction");
    }

    [Test]
    public async Task IsA_NoParent_ParentTypeIsNull()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<IsAValidOntology>();

        var graph = graphBuilder.Build();
        var transaction = graph.ObjectTypes.First(ot => ot.Name == "IsAFinancialTransaction");

        await Assert.That(transaction.ParentType).IsNull();
        await Assert.That(transaction.ParentTypeName).IsNull();
    }
}

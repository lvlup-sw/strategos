using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class AONT036Tests
{
    [Test]
    public async Task AONT036_ReadOnlyThenModifies_FiresDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } public decimal Balance { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Balance);
            obj.Action(""GetBalance"").ReadOnly().Modifies(p => p.Balance);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ReadOnlyConflictsWithMutation);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT036_ReadOnlyThenCreatesLinked_FiresDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } }
public class Order { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<Order>(""Orders"");
            obj.Action(""GetOrders"").ReadOnly().CreatesLinked<Order>(""Orders"");
        });
        builder.Object<Order>(obj => { obj.Key(p => p.Id); });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ReadOnlyConflictsWithMutation);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT036_ReadOnlyThenEmitsEvent_FiresDiagnostic()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public Guid Id { get; set; } }
public record BalanceRead(Guid Id);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Event<BalanceRead>(e => { });
            obj.Action(""GetBalance"").ReadOnly().EmitsEvent<BalanceRead>();
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ReadOnlyConflictsWithMutation);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT036_ReadOnlyAlone_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } public decimal Balance { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Balance);
            obj.Action(""GetBalance"").ReadOnly().BoundToWorkflow(""get-balance"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ReadOnlyConflictsWithMutation);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT036_ModifiesAlone_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } public decimal Balance { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Balance);
            obj.Action(""UpdateBalance"").Modifies(p => p.Balance).BoundToWorkflow(""update-balance"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ReadOnlyConflictsWithMutation);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT036_TwoActionsOneReadOnlyOneMutating_OnlyMutatingAttributesDoNotFireOnReadOnlyAction()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } public decimal Balance { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Balance);
            obj.Action(""GetBalance"").ReadOnly().BoundToWorkflow(""get-balance"");
            obj.Action(""UpdateBalance"").Modifies(p => p.Balance).BoundToWorkflow(""update-balance"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ReadOnlyConflictsWithMutation);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}

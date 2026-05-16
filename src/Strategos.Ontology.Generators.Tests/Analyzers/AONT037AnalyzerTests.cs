using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

/// <summary>
/// Task 19 — AONT037 analyzer trigger tests.
/// </summary>
/// <remarks>
/// AONT037 fires when the non-generic descriptor-by-name overload
/// <c>obj.ObjectType(name, …)</c> is invoked without providing either
/// a <c>Type</c> argument (e.g. <c>typeof(T)</c>) or a <c>symbolKey:</c>
/// named argument. The generic overload <c>obj.ObjectType&lt;T&gt;(…)</c>
/// always carries a CLR type, so it must NOT fire.
/// </remarks>
public class AONT037AnalyzerTests
{
    [Test]
    public async Task Analyze_DescriptorOverloadWithoutSymbolKey_FiresAONT037()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.ObjectType(""Foo"", domainName: ""trading"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.PolyglotInvariantViolated);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Analyze_GenericObjectTypeCall_DoesNotFireAONT037()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TradeOrder { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.ObjectType<TradeOrder>();
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.PolyglotInvariantViolated);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_DescriptorOverloadWithSymbolKeyNamedArg_DoesNotFireAONT037()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.ObjectType(""Foo"", symbolKey: ""scip-typescript ./mod#User"", domainName: ""trading"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.PolyglotInvariantViolated);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_DescriptorOverloadWithTypeArgument_DoesNotFireAONT037()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TradeOrder { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.ObjectType(""Foo"", typeof(TradeOrder), ""trading"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.PolyglotInvariantViolated);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_DescriptorOverloadWithClrTypeNamedArg_DoesNotFireAONT037()
    {
        // Regression: a `clrType:` named argument satisfies the identity
        // invariant regardless of the expression form (typeof, variable,
        // factory call). Previously the matcher only recognised
        // `typeof(...)` positional/named — a `clrType: someVariable`
        // pattern false-fired AONT037.
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TradeOrder { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        Type t = typeof(TradeOrder);
        builder.ObjectType(""Foo"", clrType: t, domainName: ""trading"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.PolyglotInvariantViolated);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_ObjectTypeOnNonBuilderReceiver_DoesNotFireAONT037()
    {
        // Regression: AONT037 must only fire on IOntologyBuilder
        // receivers. A `.ObjectType(...)` call on any other type is not
        // a builder registration and must not false-fire.
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class Bystander
{
    public void ObjectType(string name) { }
}

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        var b = new Bystander();
        b.ObjectType(""Foo"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.PolyglotInvariantViolated);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}

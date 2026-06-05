using Microsoft.CodeAnalysis;

using Strategos.Ontology.Generators.Analyzers;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

/// <summary>
/// DR-10 / DR-6 (#128, #121) — AONT211 ambiguous-traversal-without-override guard.
/// </summary>
/// <remarks>
/// AONT211 is the COMPILE-TIME half of the DR-10 identity-flow fix (INV-5:
/// earliest-tier — the analyzer fires before the runtime ever resolves the
/// traversal). It fires at a <c>TraverseLink&lt;TLinked&gt;("role")</c> call site
/// when the traversal target CLR type <c>TLinked</c> is <b>ambiguously
/// multi-registered</b> (registered under two or more distinct descriptor names
/// in the same compilation, mirroring the AONT041 multi-registration detection)
/// AND no <c>descriptorName</c> override argument is supplied to disambiguate it.
///
/// Supplying the two-arg <c>TraverseLink&lt;TLinked&gt;("role", "descriptor")</c>
/// override silences the diagnostic; so does a single-registered target, which is
/// already unambiguous. INV-2: this is analyzer-only — there is no runtime
/// counterpart in this task.
/// </remarks>
public class AONT211AmbiguousTraversalTests
{
    [Test]
    public async Task Diagnostic_AONT211_IsRegistered()
    {
        // INV-5: the id is stable and documented in the diagnostic vocabulary;
        // this pins id/severity so a later edit can't silently renumber it and
        // can't disturb the sibling AONT209/AONT210 allocations.
        await Assert.That(OntologyDiagnosticIds.AmbiguousTraversalWithoutDescriptor)
            .IsEqualTo("AONT211");

        var descriptor = OntologyDiagnostics.AmbiguousTraversalWithoutDescriptor;
        await Assert.That(descriptor.Id).IsEqualTo("AONT211");
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();

        var analyzer = new OntologyDefinitionAnalyzer();
        await Assert.That(analyzer.SupportedDiagnostics.Any(d => d.Id == "AONT211")).IsTrue();
    }

    [Test]
    public async Task Analyze_TraverseLinkToMultiRegisteredTargetWithoutOverride_FiresAONT211()
    {
        // Linked is registered under TWO distinct descriptor names ("orders_a"
        // and "orders_b") in the same compilation — ambiguously multi-registered.
        // A single-arg TraverseLink<Linked>("Orders") cannot resolve which
        // registration the hop lands on, so AONT211 must fire.
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.ObjectSets;

public sealed record Source(string Id);
public sealed record Linked(string Id);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Source>(obj => obj.Key(s => s.Id));
        builder.Object<Linked>(""orders_a"", obj => obj.Key(l => l.Id));
        builder.Object<Linked>(""orders_b"", obj => obj.Key(l => l.Id));
    }
}

public class Query
{
    public void Run(ObjectSet<Source> set)
    {
        var hop = set.TraverseLink<Linked>(""Orders"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.AmbiguousTraversalWithoutDescriptor);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Analyze_TraverseLinkWithDescriptorNameOverride_DoesNotFireAONT211()
    {
        // Same ambiguous multi-registration as above, but the call supplies the
        // two-arg override TraverseLink<Linked>("Orders", "orders_a"). The
        // override disambiguates the hop, so AONT211 must stay silent.
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.ObjectSets;

public sealed record Source(string Id);
public sealed record Linked(string Id);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Source>(obj => obj.Key(s => s.Id));
        builder.Object<Linked>(""orders_a"", obj => obj.Key(l => l.Id));
        builder.Object<Linked>(""orders_b"", obj => obj.Key(l => l.Id));
    }
}

public class Query
{
    public void Run(ObjectSet<Source> set)
    {
        var hop = set.TraverseLink<Linked>(""Orders"", ""orders_a"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.AmbiguousTraversalWithoutDescriptor);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_TraverseLinkToSingleRegisteredTarget_DoesNotFireAONT211()
    {
        // Linked is registered exactly ONCE — the target is unambiguous, so a
        // single-arg TraverseLink<Linked>("Orders") needs no override and
        // AONT211 must stay silent.
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.ObjectSets;

public sealed record Source(string Id);
public sealed record Linked(string Id);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Source>(obj => obj.Key(s => s.Id));
        builder.Object<Linked>(obj => obj.Key(l => l.Id));
    }
}

public class Query
{
    public void Run(ObjectSet<Source> set)
    {
        var hop = set.TraverseLink<Linked>(""Orders"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.AmbiguousTraversalWithoutDescriptor);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}

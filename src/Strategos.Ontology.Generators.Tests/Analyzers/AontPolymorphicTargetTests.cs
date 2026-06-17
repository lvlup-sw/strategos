using Microsoft.CodeAnalysis;

using Strategos.Ontology.Generators.Analyzers;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

/// <summary>
/// DR-11 (junction posture, #128) — AONT212 "declared target descriptor has no
/// junction table" guard.
/// </summary>
/// <remarks>
/// Under the per-<c>(link, target-descriptor)</c> posture, a link to a
/// POLYMORPHIC target (a registered <c>Interface&lt;T&gt;</c>) fans out into one
/// junction table per IMPLEMENTOR descriptor. When the interface has ZERO
/// implementor object descriptors in the compilation, the fan-out set is empty —
/// no junction table can ever be provisioned, and any relate/traverse along the
/// link is dead. AONT212 is the compile-time guard (INV-5: earliest-tier) that
/// flags that link declaration.
///
/// The negative case — an interface link target WITH at least one implementor —
/// is conformant (junction tables are provisionable) and must stay silent. A
/// valid interface-typed link must ALSO not trip AONT003
/// (<c>LinkTargetNotRegistered</c>), which is scoped to concrete Object targets.
/// </remarks>
public class AontPolymorphicTargetTests
{
    [Test]
    public async Task Diagnostic_AONT212_IsRegistered()
    {
        // INV-5: the id is stable and documented; this pins id/severity so a later
        // edit can't silently renumber it or disturb the sibling AONT211 slot.
        await Assert.That(OntologyDiagnosticIds.PolymorphicTargetNoJunctionTable)
            .IsEqualTo("AONT212");

        var descriptor = OntologyDiagnostics.PolymorphicTargetNoJunctionTable;
        await Assert.That(descriptor.Id).IsEqualTo("AONT212");
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();

        var analyzer = new OntologyDefinitionAnalyzer();
        await Assert.That(analyzer.SupportedDiagnostics.Any(d => d.Id == "AONT212")).IsTrue();
    }

    [Test]
    public async Task Analyze_LinkToInterfaceWithNoImplementors_FiresAONT212()
    {
        // Account.HasMany<ISecurity>("Holdings") targets a registered interface
        // that NO object descriptor implements. The polymorphic link can resolve
        // to no descriptor, so no junction table is provisionable — AONT212 fires
        // at the link declaration.
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISecurity { System.Guid Id { get; set; } }
public class Account { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""portfolio"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISecurity>(""ISecurity"", iface => { });
        builder.Object<Account>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<ISecurity>(""Holdings"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.PolymorphicTargetNoJunctionTable);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Analyze_LinkToInterfaceWithImplementor_DoesNotFireAONT212()
    {
        // Same polymorphic link, but now Stock implements ISecurity — the fan-out
        // resolves to one junction table (account_holdings_stock), so the link is
        // conformant and AONT212 stays silent.
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISecurity { System.Guid Id { get; set; } }
public class Account { public System.Guid Id { get; set; } }
public class Stock : ISecurity { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""portfolio"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISecurity>(""ISecurity"", iface => { });
        builder.Object<Account>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<ISecurity>(""Holdings"");
        });
        builder.Object<Stock>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Implements<ISecurity>(m => { });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.PolymorphicTargetNoJunctionTable);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_LinkToInterfaceWithImplementor_DoesNotFireAONT003()
    {
        // A valid interface-typed link target must NOT trip AONT003
        // (LinkTargetNotRegistered) — that diagnostic is scoped to concrete Object
        // targets. The interface IS registered and HAS an implementor, so the link
        // is fully resolvable; neither the polymorphic guard nor the concrete
        // guard should fire.
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISecurity { System.Guid Id { get; set; } }
public class Account { public System.Guid Id { get; set; } }
public class Stock : ISecurity { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""portfolio"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISecurity>(""ISecurity"", iface => { });
        builder.Object<Account>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<ISecurity>(""Holdings"");
        });
        builder.Object<Stock>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Implements<ISecurity>(m => { });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.LinkTargetNotRegistered);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}

using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Diagnostics;

namespace Strategos.Ontology.Tests.Exceptions;

public class OntologyCompositionExceptionTests
{
    [Test]
    public async Task Ctor_LegacyMessageOnly_ExposesEmptyDiagnostics()
    {
        // Preserves backward compatibility with the pre-DR-10 single-message ctor.
        var ex = new OntologyCompositionException("AONT040: duplicate type");

        await Assert.That(ex.Message).IsEqualTo("AONT040: duplicate type");
        await Assert.That(ex.Diagnostics).IsEmpty();
        await Assert.That(ex.NonFatalDiagnostics).IsEmpty();
    }

    [Test]
    public async Task Ctor_LegacyMessageAndInner_PreservesInner()
    {
        var inner = new InvalidOperationException("boom");
        var ex = new OntologyCompositionException("AONT040: duplicate type", inner);

        await Assert.That(ex.InnerException).IsEqualTo(inner);
        await Assert.That(ex.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task Ctor_WithDiagnostics_ExposesDiagnosticsProperty()
    {
        var diagnostics = ImmutableArray.Create(
            new OntologyDiagnostic(
                Id: "AONT201",
                Message: "Hand-declared property 'Symbol' does not exist on ingested descriptor",
                Severity: OntologyDiagnosticSeverity.Error,
                DomainName: "Trading",
                TypeName: "TradeOrder",
                PropertyName: "Symbol"),
            new OntologyDiagnostic(
                Id: "AONT208",
                Message: "LanguageId disagreement between origins",
                Severity: OntologyDiagnosticSeverity.Error,
                DomainName: "Trading",
                TypeName: "TradeOrder",
                PropertyName: null));

        var ex = new OntologyCompositionException(diagnostics);

        await Assert.That(ex.Diagnostics.Length).IsEqualTo(2);
        await Assert.That(ex.Diagnostics[0].Id).IsEqualTo("AONT201");
        await Assert.That(ex.Diagnostics[1].Id).IsEqualTo("AONT208");
        await Assert.That(ex.NonFatalDiagnostics).IsEmpty();
    }

    [Test]
    public async Task Ctor_WithDiagnosticsAndNonFatal_ExposesBoth()
    {
        var fatal = ImmutableArray.Create(
            new OntologyDiagnostic("AONT201", "fatal 1", OntologyDiagnosticSeverity.Error, null, null, null));
        var nonFatal = ImmutableArray.Create(
            new OntologyDiagnostic("AONT202", "warn 1", OntologyDiagnosticSeverity.Warning, null, null, null),
            new OntologyDiagnostic("AONT204", "info 1", OntologyDiagnosticSeverity.Info, null, null, null));

        var ex = new OntologyCompositionException(fatal, nonFatal);

        await Assert.That(ex.Diagnostics.Length).IsEqualTo(1);
        await Assert.That(ex.NonFatalDiagnostics.Length).IsEqualTo(2);
        await Assert.That(ex.NonFatalDiagnostics[0].Id).IsEqualTo("AONT202");
    }

    [Test]
    public async Task Ctor_WithDiagnostics_MessageContainsFirstDiagnosticId()
    {
        var diagnostics = ImmutableArray.Create(
            new OntologyDiagnostic(
                "AONT205",
                "Ingested descriptor contributes to Actions",
                OntologyDiagnosticSeverity.Error,
                "Trading",
                "TradeOrder",
                null));

        var ex = new OntologyCompositionException(diagnostics);

        await Assert.That(ex.Message).Contains("AONT205");
    }
}

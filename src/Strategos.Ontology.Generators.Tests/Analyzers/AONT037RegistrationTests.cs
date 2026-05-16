using Microsoft.CodeAnalysis;
using Strategos.Ontology.Generators.Analyzers;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

/// <summary>
/// Task 18 — AONT037 PolyglotInvariantViolated diagnostic registration tests.
/// </summary>
/// <remarks>
/// Verifies that the diagnostic id, descriptor metadata, and analyzer's
/// <see cref="OntologyDefinitionAnalyzer.SupportedDiagnostics"/> array
/// all expose AONT037 with severity Error and a helper message that
/// names both the <c>ClrType</c> and <c>SymbolKey</c> escape hatches.
/// </remarks>
public class AONT037RegistrationTests
{
    [Test]
    public async Task AONT037_DiagnosticId_IsPolyglotInvariantViolated()
    {
        await Assert.That(OntologyDiagnosticIds.PolyglotInvariantViolated).IsEqualTo("AONT037");
    }

    [Test]
    public async Task AONT037_DescriptorRegistered_HasErrorSeverity()
    {
        var descriptor = OntologyDiagnostics.PolyglotInvariantViolated;

        await Assert.That(descriptor.Id).IsEqualTo("AONT037");
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
    }

    [Test]
    public async Task AONT037_HelperMessage_NamesClrTypeAndSymbolKey()
    {
        var descriptor = OntologyDiagnostics.PolyglotInvariantViolated;
        var message = descriptor.MessageFormat.ToString();

        await Assert.That(message).Contains("ClrType");
        await Assert.That(message).Contains("SymbolKey");
    }

    [Test]
    public async Task AONT037_RegisteredInAnalyzerSupportedDiagnostics()
    {
        var analyzer = new OntologyDefinitionAnalyzer();

        var supported = analyzer.SupportedDiagnostics;

        await Assert.That(supported.Any(d => d.Id == "AONT037")).IsTrue();
    }
}

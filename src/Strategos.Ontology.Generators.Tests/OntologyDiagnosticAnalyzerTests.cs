using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Strategos.Ontology.Generators.Tests;

public class OntologyDiagnosticAnalyzerTests
{
    [Test]
    public async Task SupportedDiagnostics_ReturnsAllOntoDiagnostics()
    {
        // Arrange
        var analyzer = new OntologyDiagnosticAnalyzer();

        // Act
        var diagnostics = analyzer.SupportedDiagnostics;

        // Assert — all 10 ONTO diagnostics are present
        var ids = diagnostics.Select(d => d.Id).OrderBy(id => id).ToArray();

        await Assert.That(ids).HasCount().EqualTo(10);
        await Assert.That(ids[0]).IsEqualTo("ONTO001");
        await Assert.That(ids[1]).IsEqualTo("ONTO002");
        await Assert.That(ids[2]).IsEqualTo("ONTO003");
        await Assert.That(ids[3]).IsEqualTo("ONTO004");
        await Assert.That(ids[4]).IsEqualTo("ONTO005");
        await Assert.That(ids[5]).IsEqualTo("ONTO006");
        await Assert.That(ids[6]).IsEqualTo("ONTO007");
        await Assert.That(ids[7]).IsEqualTo("ONTO008");
        await Assert.That(ids[8]).IsEqualTo("ONTO009");
        await Assert.That(ids[9]).IsEqualTo("ONTO010");
    }
}

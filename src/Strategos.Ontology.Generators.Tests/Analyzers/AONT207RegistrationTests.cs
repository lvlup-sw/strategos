using Microsoft.CodeAnalysis;

using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

/// <summary>
/// DR-7 (Task 29): AONT207 (branch-hand vs main-hand conflict) is
/// registration-only in 2.5.0 — its trigger requires the four-input
/// fold (branch-hand stream) which is deferred. This fixture asserts
/// the diagnostic id and descriptor are present in the vocabulary so
/// downstream consumers (and the basileus ADR alignment) can reference
/// the identifier; the trigger test is preserved as a `[Skip]` marker
/// for the v2.6.0 follow-up.
/// </summary>
public sealed class AONT207RegistrationTests
{
    [Test]
    public async Task Diagnostic_AONT207_IsRegistered()
    {
        await Assert.That(OntologyDiagnosticIds.BranchHandConflict).IsEqualTo("AONT207");

        var descriptor = OntologyDiagnostics.BranchHandConflict;
        await Assert.That(descriptor.Id).IsEqualTo("AONT207");
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
    }

    [Test]
    [Skip("requires four-input fold")]
    public async Task Build_BranchHandConflict_FiresAONT207()
    {
        // Placeholder for the trigger test exercised once branch-hand
        // stream support lands and OntologyGraphBuilder gains a
        // four-input fold over (main-hand, branch-hand, main-ingested,
        // branch-ingested). Until then, AONT207 is vocabulary-only.
        await Task.CompletedTask;
    }
}

using System.Text.Json;

using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyValidateRegistrationTests
{
    private static OntologyToolDescriptor GetValidateDescriptor()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        return new OntologyToolDiscovery(graph).Discover()
            .First(d => d.Name == "ontology_validate");
    }

    [Test]
    public async Task Discover_IncludesOntologyValidateDescriptor()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var descriptors = new OntologyToolDiscovery(graph).Discover();

        var match = descriptors.Where(d => d.Name == "ontology_validate").ToList();
        await Assert.That(match).HasCount().EqualTo(1);
    }

    [Test]
    public async Task OntologyValidateDescriptor_Annotations_AreReadOnlyIdempotentNonDestructive()
    {
        var descriptor = GetValidateDescriptor();

        await Assert.That(descriptor.Annotations.ReadOnlyHint).IsTrue();
        await Assert.That(descriptor.Annotations.IdempotentHint).IsTrue();
        await Assert.That(descriptor.Annotations.DestructiveHint).IsFalse();
        await Assert.That(descriptor.Annotations.OpenWorldHint).IsFalse();
    }

    [Test]
    public async Task OntologyValidateDescriptor_Title_IsHumanReadable()
    {
        var descriptor = GetValidateDescriptor();

        await Assert.That(descriptor.Title).IsNotNull();
        await Assert.That(descriptor.Title!.ToLowerInvariant()).Contains("validate");
    }

    [Test]
    public async Task OntologyValidateDescriptor_OutputSchemaMatchesValidationVerdict()
    {
        var descriptor = GetValidateDescriptor();

        await Assert.That(descriptor.OutputSchema.HasValue).IsTrue();
        var raw = descriptor.OutputSchema!.Value.GetRawText();
        await Assert.That(raw).Contains("\"type\"");

        var verdict = new ValidationVerdict(
            Passed: true,
            HardViolations: Array.Empty<ConstraintEvaluation>(),
            SoftWarnings: Array.Empty<ConstraintEvaluation>(),
            BlastRadius: new BlastRadius(
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<CrossDomainHop>(),
                BlastRadiusScope.Local),
            PatternViolations: Array.Empty<PatternViolation>(),
            Coverage: new CoverageReport(0, 0, Array.Empty<OntologyNodeRef>()));

        var json = JsonSerializer.Serialize(verdict);
        var roundTripped = JsonSerializer.Deserialize<ValidationVerdict>(json);

        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped!.Passed).IsTrue();
    }

    [Test]
    public async Task OntologyValidateDescriptor_OutputSchema_HandlesNullCoverage()
    {
        var descriptor = GetValidateDescriptor();
        await Assert.That(descriptor.OutputSchema.HasValue).IsTrue();

        var verdict = new ValidationVerdict(
            Passed: true,
            HardViolations: Array.Empty<ConstraintEvaluation>(),
            SoftWarnings: Array.Empty<ConstraintEvaluation>(),
            BlastRadius: new BlastRadius(
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<CrossDomainHop>(),
                BlastRadiusScope.Local),
            PatternViolations: Array.Empty<PatternViolation>(),
            Coverage: null);

        var json = JsonSerializer.Serialize(verdict);
        var roundTripped = JsonSerializer.Deserialize<ValidationVerdict>(json);

        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped!.Coverage).IsNull();
    }

    [Test]
    public async Task OntologyValidateResponse_HasMetaOntologyVersion()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var verdict = new ValidationVerdict(
            Passed: true,
            HardViolations: Array.Empty<ConstraintEvaluation>(),
            SoftWarnings: Array.Empty<ConstraintEvaluation>(),
            BlastRadius: new BlastRadius(
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<CrossDomainHop>(),
                BlastRadiusScope.Local),
            PatternViolations: Array.Empty<PatternViolation>(),
            Coverage: null);

        var meta = ResponseMeta.ForGraph(graph);
        var result = new ValidateResult(verdict, meta);

        await Assert.That(result.Verdict).IsEqualTo(verdict);
        await Assert.That(result.Meta).IsNotNull();
        await Assert.That(result.Meta.OntologyVersion).IsEqualTo("sha256:" + graph.Version);

        var json = JsonSerializer.Serialize(result);
        await Assert.That(json).Contains("\"_meta\"");
    }
}

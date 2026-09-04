using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Diagnostics;

public sealed class AONT213Tests
{
    [Test]
    public async Task Build_ReadOnlyNonIdempotentDescriptor_FailsGraphFreeze()
    {
        OntologyCompositionException? caught = null;
        try
        {
            new OntologyGraphBuilder()
                .AddDomain<InvalidRetrySafetyOntology>()
                .Build();
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        var diagnostic = caught!.Diagnostics.Single(item => item.Id == "AONT213");
        await Assert.That(diagnostic.Message).Contains("read-only but not idempotent");
        await Assert.That(diagnostic.PropertyName).IsEqualTo("read");
    }

    private sealed class InvalidRetrySafetyOntology : DomainOntology
    {
        public override string DomainName => "retry-safety";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Document",
                DomainName = DomainName,
                ClrType = typeof(object),
                Source = DescriptorSource.HandAuthoredContract,
                Actions =
                [
                    new ActionDescriptor("read", "Read a document")
                    {
                        IsReadOnly = true,
                        Idempotent = false,
                    },
                ],
            });
        }
    }
}

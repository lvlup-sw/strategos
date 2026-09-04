using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public sealed class AuthorityLatticeTests
{
    [Test]
    public async Task ProductOrder_PreservesIndependentAxesAndComputesJoin()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<AuthorityProductOntology>()
            .Build();
        var lattice = graph.GetAuthorityLattice("authority-product");

        await Assert.That(lattice.Satisfies("internal.writer", "public.reader")).IsTrue();
        await Assert.That(lattice.Satisfies("public.writer", "internal.reader")).IsFalse();

        var joined = lattice.Join("public.writer", "internal.reader");
        await Assert.That(joined.Coordinates["access"]).IsEqualTo("write");
        await Assert.That(joined.Coordinates["sensitivity"]).IsEqualTo("internal");
        await Assert.That(lattice.Satisfies("internal.writer", joined)).IsTrue();
        await Assert.That(lattice.Satisfies("public.writer", joined)).IsFalse();
    }

    [Test]
    public async Task Build_AuthorityMissingAxisCoordinate_FailsAont214()
    {
        var exception = BuildFailure<MissingCoordinateOntology>();

        var diagnostic = exception.Diagnostics.Single(item => item.Id == "AONT214");
        await Assert.That(diagnostic.Message).Contains("exactly one level on every axis");
    }

    [Test]
    public async Task Build_UnusedAuthority_FailsAont214()
    {
        var exception = BuildFailure<UnusedAuthorityOntology>();

        var diagnostic = exception.Diagnostics.Single(item => item.Id == "AONT214");
        await Assert.That(diagnostic.Message).Contains("not required by any action");
    }

    [Test]
    public async Task Build_ProductInconsistentImplication_FailsAont214()
    {
        var exception = BuildFailure<InconsistentImplicationOntology>();

        var diagnostic = exception.Diagnostics.Single(item => item.Id == "AONT214");
        await Assert.That(diagnostic.Message).Contains("not at least as strong on every axis");
    }

    [Test]
    public async Task Build_ActionRequiringUnknownAuthority_FailsAont214()
    {
        var exception = BuildFailure<UnknownRequiredAuthorityOntology>();

        var diagnostic = exception.Diagnostics.Single(item => item.Id == "AONT214");
        await Assert.That(diagnostic.Message).Contains("requires unknown authority");
    }

    private static OntologyCompositionException BuildFailure<T>()
        where T : DomainOntology, new()
    {
        try
        {
            new OntologyGraphBuilder().AddDomain<T>().Build();
        }
        catch (OntologyCompositionException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected graph construction to fail.");
    }

    private static ObjectTypeDescriptor DocumentWithActions(params ActionDescriptor[] actions) => new()
    {
        Name = "Document",
        DomainName = "authority-test",
        ClrType = typeof(object),
        Source = DescriptorSource.HandAuthoredContract,
        Actions = actions,
    };

    private sealed class AuthorityProductOntology : DomainOntology
    {
        public override string DomainName => "authority-product";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.AuthorityAxis("access", "none", "read", "write", "owner");
            builder.AuthorityAxis("sensitivity", "public", "internal", "restricted");

            builder.Authority("public.reader")
                .At("access", "read")
                .At("sensitivity", "public");
            builder.Authority("internal.reader")
                .At("access", "read")
                .At("sensitivity", "internal")
                .Implies("public.reader");
            builder.Authority("public.writer")
                .At("access", "write")
                .At("sensitivity", "public")
                .Implies("public.reader");
            builder.Authority("internal.writer")
                .At("access", "write")
                .At("sensitivity", "internal")
                .Implies("internal.reader")
                .Implies("public.writer");

            builder.Object<AuthorityDocument>(document =>
            {
                document.Key(item => item.Id);
                document.Action("read-public").RequiresAuthority("public.reader");
                document.Action("read-internal").RequiresAuthority("internal.reader");
                document.Action("write-public").RequiresAuthority("public.writer");
                document.Action("write-internal").RequiresAuthority("internal.writer");
            });
        }
    }

    private sealed class MissingCoordinateOntology : DomainOntology
    {
        public override string DomainName => "authority-test";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.AuthorityAxis("access", "read", "write");
            builder.AuthorityAxis("sensitivity", "public", "restricted");
            builder.Authority("reader").At("access", "read");
            builder.ObjectTypeFromDescriptor(DocumentWithActions(
                new ActionDescriptor("read", "read") { RequiredAuthority = "reader" }));
        }
    }

    private sealed class UnusedAuthorityOntology : DomainOntology
    {
        public override string DomainName => "authority-test";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.AuthorityAxis("access", "read", "write");
            builder.Authority("reader").At("access", "read");
            builder.Authority("writer").At("access", "write");
            builder.ObjectTypeFromDescriptor(DocumentWithActions(
                new ActionDescriptor("read", "read") { RequiredAuthority = "reader" }));
        }
    }

    private sealed class InconsistentImplicationOntology : DomainOntology
    {
        public override string DomainName => "authority-test";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.AuthorityAxis("access", "read", "write");
            builder.Authority("reader").At("access", "read").Implies("writer");
            builder.Authority("writer").At("access", "write");
            builder.ObjectTypeFromDescriptor(DocumentWithActions(
                new ActionDescriptor("read", "read") { RequiredAuthority = "reader" },
                new ActionDescriptor("write", "write") { RequiredAuthority = "writer" }));
        }
    }

    private sealed class UnknownRequiredAuthorityOntology : DomainOntology
    {
        public override string DomainName => "authority-test";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.AuthorityAxis("access", "read");
            builder.Authority("reader").At("access", "read");
            builder.ObjectTypeFromDescriptor(DocumentWithActions(
                new ActionDescriptor("read", "read") { RequiredAuthority = "unknown" },
                new ActionDescriptor("use-reader", "use reader") { RequiredAuthority = "reader" }));
        }
    }

    private sealed class AuthorityDocument
    {
        public string Id { get; init; } = string.Empty;
    }
}

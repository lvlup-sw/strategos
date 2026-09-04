using Strategos.Ontology.Builder;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Actions;

public sealed class AuthorityAuthorizationTests
{
    [Test]
    public async Task Dispatch_WeakerGrant_DeniesBeforeInnerHandler()
    {
        var inner = new RecordingDispatcher();
        var dispatcher = new AuthorityAuthorizationActionDispatcher(inner, CreateGraph());
        var context = Context("write", "reader");

        var result = await dispatcher.DispatchAsync(context, new object());

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("writer");
        await Assert.That(inner.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Dispatch_StrongerGrant_SatisfiesWeakerRequirement()
    {
        var inner = new RecordingDispatcher();
        var dispatcher = new AuthorityAuthorizationActionDispatcher(inner, CreateGraph());
        var context = Context("read", "writer");

        var result = await dispatcher.DispatchAsync(context, new object());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(inner.Calls).IsEqualTo(1);
        await Assert.That(inner.LastContext!.ActionDescriptor!.Name).IsEqualTo("read");
    }

    [Test]
    public async Task Dispatch_CallerSuppliedUnprotectedDescriptor_CannotBypassGraphAuthority()
    {
        var inner = new RecordingDispatcher();
        var dispatcher = new AuthorityAuthorizationActionDispatcher(inner, CreateGraph());
        var context = Context("write", "reader") with
        {
            ActionDescriptor = new ActionDescriptor("write", "spoofed"),
        };

        var result = await dispatcher.DispatchAsync(context, new object());

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(inner.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Dispatch_UnknownDomain_ReturnsDenial()
    {
        var inner = new RecordingDispatcher();
        var dispatcher = new AuthorityAuthorizationActionDispatcher(inner, CreateGraph());
        var context = Context("write", "writer") with { Domain = "unknown" };

        var result = await dispatcher.DispatchAsync(context, new object());

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not present");
        await Assert.That(inner.Calls).IsEqualTo(0);
    }

    private static ActionContext Context(string action, string grant) => new(
        new ActionPrincipal("User", "user-1") { GrantedAuthorities = [grant] },
        "authority-dispatch",
        nameof(SecuredDocument),
        "document-1",
        action);

    private static OntologyGraph CreateGraph() => new OntologyGraphBuilder()
        .AddDomain<AuthorityDispatchOntology>()
        .Build();

    private sealed class AuthorityDispatchOntology : DomainOntology
    {
        public override string DomainName => "authority-dispatch";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.AuthorityAxis("access", "read", "write");
            builder.Authority("reader").At("access", "read");
            builder.Authority("writer").At("access", "write").Implies("reader");
            builder.Object<SecuredDocument>(document =>
            {
                document.Key(item => item.Id);
                document.Action("read").RequiresAuthority("reader");
                document.Action("write").RequiresAuthority("writer");
            });
        }
    }

    private sealed class SecuredDocument
    {
        public string Id { get; init; } = string.Empty;
    }

    private sealed class RecordingDispatcher : IActionDispatcher
    {
        public int Calls { get; private set; }

        public ActionContext? LastContext { get; private set; }

        public Task<ActionResult> DispatchAsync(
            ActionContext context,
            object request,
            CancellationToken ct = default)
        {
            Calls++;
            LastContext = context;
            return Task.FromResult(new ActionResult(true));
        }
    }
}

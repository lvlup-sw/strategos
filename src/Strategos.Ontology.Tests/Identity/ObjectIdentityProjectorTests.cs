using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Identity;

namespace Strategos.Ontology.Tests.Identity;

public class ObjectIdentityProjectorTests
{
    private sealed record ClrThing(Guid Id, string Name);

    private static ObjectTypeDescriptor ClrDescriptor() =>
        new("ClrThing", typeof(ClrThing), "test")
        {
            IdAccessor = o => ((ClrThing)o).Id,
        };

    [Test]
    public async Task ProjectId_ClrDescriptorWithKey_ReturnsDeterministicId()
    {
        var projector = new ObjectIdentityProjector();
        var id = Guid.NewGuid();
        var instance = new ClrThing(id, "alpha");

        var projected = projector.ProjectId(ClrDescriptor(), instance);

        await Assert.That(projected).IsEqualTo(id.ToString());
    }

    [Test]
    public async Task ProjectId_SameKeyValue_ReturnsSameId()
    {
        var projector = new ObjectIdentityProjector();
        var id = Guid.NewGuid();
        var descriptor = ClrDescriptor();

        var first = projector.ProjectId(descriptor, new ClrThing(id, "alpha"));
        var second = projector.ProjectId(descriptor, new ClrThing(id, "beta-different-name"));

        await Assert.That(first).IsEqualTo(second);
    }

    [Test]
    public async Task ProjectId_NullKeyValue_ThrowsNamingDescriptor()
    {
        var projector = new ObjectIdentityProjector();
        var descriptor = new ObjectTypeDescriptor("NullableThing", typeof(NullableThing), "test")
        {
            IdAccessor = o => ((NullableThing)o).Id,
        };

        await Assert.That(() => projector.ProjectId(descriptor, new NullableThing(null)))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));

        await Assert.That(() => projector.ProjectId(descriptor, new NullableThing(null)))
            .ThrowsException()
            .WithMessageContaining("NullableThing");
    }

    private sealed record NullableThing(string? Id);

    [Test]
    public async Task ProjectId_SymbolKeyOnlyDescriptor_ResolvesWithoutReflection()
    {
        var projector = new ObjectIdentityProjector();

        // Polyglot descriptor: no CLR type at all, only a SCIP moniker. The
        // IdAccessor is supplied externally (as an IOntologySource would),
        // reading a dictionary-shaped instance by key. The instance has no
        // CLR property named "Id", so any reflection fallback would fail —
        // proving the accessor is the only resolution path (INV-8).
        var descriptor = new ObjectTypeDescriptor
        {
            Name = "Foo",
            DomainName = "py",
            ClrType = null,
            SymbolKey = "py::Foo",
            LanguageId = "python",
            IdAccessor = o => ((IReadOnlyDictionary<string, object>)o)["pk"],
        };

        var instance = new Dictionary<string, object> { ["pk"] = "row-42", ["other"] = 99 };

        var first = projector.ProjectId(descriptor, instance);
        var second = projector.ProjectId(
            descriptor,
            new Dictionary<string, object> { ["pk"] = "row-42", ["other"] = 7 });

        await Assert.That(first).IsEqualTo("row-42");
        await Assert.That(first).IsEqualTo(second);
    }

    [Test]
    public async Task ProjectId_DescriptorWithoutIdAccessor_ThrowsNamingDescriptor()
    {
        var projector = new ObjectIdentityProjector();
        var descriptor = new ObjectTypeDescriptor
        {
            Name = "Bar",
            DomainName = "py",
            SymbolKey = "py::Bar",
            IdAccessor = null,
        };

        await Assert.That(() => projector.ProjectId(descriptor, new object()))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));

        await Assert.That(() => projector.ProjectId(descriptor, new object()))
            .ThrowsException()
            .WithMessageContaining("Bar");
    }
}

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
    public async Task ProjectId_CompositeKey_FormatsDeterministicallyWithSeparator()
    {
        var projector = new ObjectIdentityProjector();

        // Composite key whose accessor yields a ValueTuple (ITuple). Each element
        // is length-prefixed (V<len>:<text>) and joined by the reserved Unit
        // Separator so the encoding is injective; single-value keys are plain
        // ToString(). Determinism: same elements => same id.
        var descriptor = new ObjectTypeDescriptor
        {
            Name = "CompositeThing",
            DomainName = "test",
            SymbolKey = "py::CompositeThing",
            IdAccessor = o => ((IReadOnlyDictionary<string, object>)o)["key"],
        };

        var instance = new Dictionary<string, object> { ["key"] = (tenant: "acme", id: 7) };

        var projected = projector.ProjectId(descriptor, instance);
        var again = projector.ProjectId(
            descriptor,
            new Dictionary<string, object> { ["key"] = (tenant: "acme", id: 7) });

        var expected = $"V4:acme{ObjectIdentityProjector.CompositeKeySeparator}V1:7";
        await Assert.That(projected).IsEqualTo(expected);
        await Assert.That(projected).IsEqualTo(again);
    }

    [Test]
    public async Task ProjectId_CompositeKey_IsCollisionFreeAcrossSeparatorBoundaries()
    {
        var projector = new ObjectIdentityProjector();
        var descriptor = new ObjectTypeDescriptor
        {
            Name = "CompositeThing",
            DomainName = "test",
            SymbolKey = "py::CompositeThing",
            IdAccessor = o => ((IReadOnlyDictionary<string, object>)o)["key"],
        };

        // Both tuples join to "a<US>b<US>c" under the naive join-on-separator
        // encoding: the first sneaks the separator INTO a component, the second
        // splits it ACROSS components. A collision-free (injective) encoding must
        // keep them distinct so the relate-store never merges two instances.
        var sep = ObjectIdentityProjector.CompositeKeySeparator;
        var leftHasSeparator = new Dictionary<string, object> { ["key"] = ($"a{sep}b", "c") };
        var rightSplitAcross = new Dictionary<string, object> { ["key"] = ("a", $"b{sep}c") };

        var left = projector.ProjectId(descriptor, leftHasSeparator);
        var right = projector.ProjectId(descriptor, rightSplitAcross);

        await Assert.That(left).IsNotEqualTo(right);
    }

    [Test]
    public async Task ProjectId_CompositeKey_NullElement_DistinctFromEmpty()
    {
        var projector = new ObjectIdentityProjector();
        var descriptor = new ObjectTypeDescriptor
        {
            Name = "CompositeThing",
            DomainName = "test",
            SymbolKey = "py::CompositeThing",
            IdAccessor = o => ((IReadOnlyDictionary<string, object>)o)["key"],
        };

        // ("a", null) and ("a", "") must project to DIFFERENT ids: a null element
        // is semantically distinct from the empty string and the two tuples are
        // distinct instances. The naive encoding collapses null to string.Empty,
        // merging them.
        var withNull = new Dictionary<string, object> { ["key"] = ("a", (string?)null) };
        var withEmpty = new Dictionary<string, object> { ["key"] = ("a", string.Empty) };

        var nullId = projector.ProjectId(descriptor, withNull);
        var emptyId = projector.ProjectId(descriptor, withEmpty);

        await Assert.That(nullId).IsNotEqualTo(emptyId);
    }

    [Test]
    public async Task ProjectId_SingleValueKey_IsPlainToString()
    {
        var projector = new ObjectIdentityProjector();
        var descriptor = ClrDescriptor();
        var id = Guid.NewGuid();

        var projected = projector.ProjectId(descriptor, new ClrThing(id, "x"));

        await Assert.That(projected).IsEqualTo(id.ToString());
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

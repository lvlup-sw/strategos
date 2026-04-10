using System.Reflection;

using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

/// <summary>
/// Test domain type used by the F2 explicit-name dispatch tests. Top-level so its
/// CLR name is exactly <c>"Foo"</c> (i.e. <c>typeof(Foo).Name == "Foo"</c>), which
/// the default-overload partition test asserts against.
/// </summary>
public sealed record Foo(string Name);

public class StubObjectSetWriter : IObjectSetWriter
{
    public List<object> StoredItems { get; } = [];

    public Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class
    {
        StoredItems.Add(item);
        return Task.CompletedTask;
    }

    public Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class
    {
        StoredItems.AddRange(items);
        return Task.CompletedTask;
    }

    // Explicit-name overloads added for Task F1 (Strategos 2.4.1 Ontology Descriptor-Name Dispatch).
    // Real behavior is exercised by InMemoryObjectSetProvider in Task E4; this stub is only here
    // so the test fixture continues to satisfy the interface.
    public Task StoreAsync<T>(string descriptorName, T item, CancellationToken ct = default) where T : class
    {
        StoredItems.Add(item);
        return Task.CompletedTask;
    }

    public Task StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, CancellationToken ct = default) where T : class
    {
        StoredItems.AddRange(items);
        return Task.CompletedTask;
    }
}

public class IObjectSetWriterTests
{
    [Test]
    public async Task StoreAsync_ImplementationCanBeCalled()
    {
        // Arrange
        var writer = new StubObjectSetWriter();

        // Act & Assert — no exception thrown
        await writer.StoreAsync("test item");

        await Assert.That(writer.StoredItems).HasCount().EqualTo(1);
        await Assert.That(writer.StoredItems[0]).IsEqualTo("test item");
    }

    [Test]
    public async Task StoreBatchAsync_ImplementationCanBeCalled()
    {
        // Arrange
        var writer = new StubObjectSetWriter();
        var items = new List<string> { "item1", "item2", "item3" };

        // Act & Assert — no exception thrown
        await writer.StoreBatchAsync(items);

        await Assert.That(writer.StoredItems).HasCount().EqualTo(3);
    }

    /// <summary>
    /// Verifies <see cref="IObjectSetWriter"/> exposes both the default and the
    /// explicit-descriptor-name overloads of <c>StoreAsync</c> and <c>StoreBatchAsync</c>.
    /// The explicit-name overloads are required for descriptor-name dispatch
    /// (Strategos 2.4.1 Ontology Descriptor-Name Dispatch, bug #31) so callers can
    /// target a specific descriptor partition on the write path when a domain type
    /// is registered against multiple descriptors.
    /// </summary>
    [Test]
    public async Task IObjectSetWriter_HasExplicitNameOverloads_ForStoreAsyncAndStoreBatchAsync()
    {
        // Arrange
        var interfaceType = typeof(IObjectSetWriter);
        var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        // Act — locate each of the four required overloads by signature
        var defaultStoreAsync = methods.SingleOrDefault(m =>
            m.Name == nameof(IObjectSetWriter.StoreAsync)
            && m.IsGenericMethodDefinition
            && HasParameterShape(m, expectDescriptorName: false, batched: false));

        var defaultStoreBatchAsync = methods.SingleOrDefault(m =>
            m.Name == nameof(IObjectSetWriter.StoreBatchAsync)
            && m.IsGenericMethodDefinition
            && HasParameterShape(m, expectDescriptorName: false, batched: true));

        var namedStoreAsync = methods.SingleOrDefault(m =>
            m.Name == nameof(IObjectSetWriter.StoreAsync)
            && m.IsGenericMethodDefinition
            && HasParameterShape(m, expectDescriptorName: true, batched: false));

        var namedStoreBatchAsync = methods.SingleOrDefault(m =>
            m.Name == nameof(IObjectSetWriter.StoreBatchAsync)
            && m.IsGenericMethodDefinition
            && HasParameterShape(m, expectDescriptorName: true, batched: true));

        // Assert — all four overloads exist
        await Assert.That(defaultStoreAsync)
            .IsNotNull()
            .Because("IObjectSetWriter must keep the default StoreAsync<T>(T, CancellationToken) overload");
        await Assert.That(defaultStoreBatchAsync)
            .IsNotNull()
            .Because("IObjectSetWriter must keep the default StoreBatchAsync<T>(IReadOnlyList<T>, CancellationToken) overload");
        await Assert.That(namedStoreAsync)
            .IsNotNull()
            .Because("IObjectSetWriter must expose StoreAsync<T>(string descriptorName, T, CancellationToken) for explicit descriptor dispatch");
        await Assert.That(namedStoreBatchAsync)
            .IsNotNull()
            .Because("IObjectSetWriter must expose StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T>, CancellationToken) for explicit descriptor dispatch");
    }

    /// <summary>
    /// Verifies the explicit-name <c>StoreAsync</c> overload routes the item into the
    /// supplied descriptor partition. A subsequent query under that descriptor name
    /// must see the item, while a query under any other descriptor name must not
    /// (Strategos 2.4.1 Ontology Descriptor-Name Dispatch, bug #31).
    /// </summary>
    [Test]
    public async Task InMemoryWriter_StoreAsync_ExplicitName_UsesSuppliedName()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        IObjectSetWriter writer = provider;
        var item = new Foo("alpha");

        // Act — write under an explicit descriptor partition that does NOT match typeof(Foo).Name
        await writer.StoreAsync<Foo>("my_partition", item);

        // Assert — query under "my_partition" finds the item
        var hit = await provider.ExecuteAsync<Foo>(
            new RootExpression(typeof(Foo), "my_partition"));
        await Assert.That(hit.Items).HasCount().EqualTo(1);
        await Assert.That(hit.Items[0].Name).IsEqualTo("alpha");

        // Assert — query under a different descriptor name returns nothing
        var miss = await provider.ExecuteAsync<Foo>(
            new RootExpression(typeof(Foo), "other_partition"));
        await Assert.That(miss.Items).IsEmpty();
    }

    /// <summary>
    /// Verifies the explicit-name <c>StoreBatchAsync</c> overload routes every item in
    /// the batch into the supplied descriptor partition (and only that partition).
    /// </summary>
    [Test]
    public async Task InMemoryWriter_StoreBatchAsync_ExplicitName_PartitionsByName()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        IObjectSetWriter writer = provider;
        var items = new List<Foo> { new("one"), new("two") };

        // Act
        await writer.StoreBatchAsync<Foo>("my_partition", items);

        // Assert — both items found under "my_partition"
        var hit = await provider.ExecuteAsync<Foo>(
            new RootExpression(typeof(Foo), "my_partition"));
        await Assert.That(hit.Items).HasCount().EqualTo(2);

        // Assert — nothing leaked into "other"
        var miss = await provider.ExecuteAsync<Foo>(
            new RootExpression(typeof(Foo), "other"));
        await Assert.That(miss.Items).IsEmpty();
    }

    /// <summary>
    /// Regression / interaction test confirming the default <c>StoreAsync&lt;T&gt;(T, ct)</c>
    /// overload partitions on <c>typeof(T).Name</c> and does not bleed across an
    /// explicit-named partition seeded under a different descriptor. After F2 wires the
    /// default overload to delegate into the explicit-name path, both code paths must
    /// continue to partition cleanly.
    /// </summary>
    [Test]
    public async Task InMemoryWriter_StoreAsync_DefaultOverload_UsesTypeofTName_AfterExplicitSeedIntoDifferentPartition()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        IObjectSetWriter writer = provider;
        var seeded = new Foo("seeded");
        provider.Seed(seeded, "seeded content", descriptorName: "other_partition");

        var defaultStored = new Foo("default");

        // Act — default overload (no descriptor name) should land under typeof(Foo).Name
        await writer.StoreAsync(defaultStored);

        // Assert — default partition (typeof(Foo).Name == "Foo") sees only the default-stored item
        var defaultHit = await provider.ExecuteAsync<Foo>(
            new RootExpression(typeof(Foo), nameof(Foo)));
        await Assert.That(defaultHit.Items).HasCount().EqualTo(1);
        await Assert.That(defaultHit.Items[0].Name).IsEqualTo("default");

        // Assert — "other_partition" still sees only the explicitly-seeded item
        var otherHit = await provider.ExecuteAsync<Foo>(
            new RootExpression(typeof(Foo), "other_partition"));
        await Assert.That(otherHit.Items).HasCount().EqualTo(1);
        await Assert.That(otherHit.Items[0].Name).IsEqualTo("seeded");
    }

    /// <summary>
    /// Returns true when <paramref name="method"/> matches one of the four expected
    /// <see cref="IObjectSetWriter"/> overload shapes.
    /// </summary>
    private static bool HasParameterShape(MethodInfo method, bool expectDescriptorName, bool batched)
    {
        var parameters = method.GetParameters();
        var expectedCount = expectDescriptorName ? 3 : 2;
        if (parameters.Length != expectedCount)
        {
            return false;
        }

        var index = 0;
        if (expectDescriptorName)
        {
            if (parameters[index].ParameterType != typeof(string)
                || parameters[index].Name != "descriptorName")
            {
                return false;
            }

            index++;
        }

        // The item/items parameter is expressed in terms of the method's generic parameter T.
        var genericArgs = method.GetGenericArguments();
        if (genericArgs.Length != 1)
        {
            return false;
        }

        var t = genericArgs[0];
        var itemParam = parameters[index];

        if (batched)
        {
            if (!itemParam.ParameterType.IsGenericType
                || itemParam.ParameterType.GetGenericTypeDefinition() != typeof(IReadOnlyList<>)
                || itemParam.ParameterType.GetGenericArguments()[0] != t)
            {
                return false;
            }
        }
        else
        {
            if (itemParam.ParameterType != t)
            {
                return false;
            }
        }

        index++;

        // Final parameter must be an optional CancellationToken.
        var ctParam = parameters[index];
        if (ctParam.ParameterType != typeof(CancellationToken) || !ctParam.IsOptional)
        {
            return false;
        }

        return true;
    }
}

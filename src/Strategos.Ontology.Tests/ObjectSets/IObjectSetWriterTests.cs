using System.Reflection;

using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

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

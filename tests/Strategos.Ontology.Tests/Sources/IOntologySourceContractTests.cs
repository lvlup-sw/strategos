using System.Runtime.CompilerServices;

using Strategos.Ontology;

namespace Strategos.Ontology.Tests.Sources;

/// <summary>
/// DR-3 (Task 7): contract tests for the <see cref="IOntologySource"/>
/// extension point. Asserts the public interface shape — <c>SourceId</c>,
/// <c>LoadAsync</c>, <c>SubscribeAsync</c> — and basic implementability.
/// </summary>
public class IOntologySourceContractTests
{
    [Test]
    public async Task IOntologySource_ImplementedByTestSource_ExposesSourceId()
    {
        IOntologySource source = new ContractTestSource("marten-typescript");

        await Assert.That(source.SourceId).IsEqualTo("marten-typescript");
    }

    [Test]
    public async Task IOntologySource_LoadAsync_ReturnsAsyncEnumerable()
    {
        IOntologySource source = new ContractTestSource("source-a");

        var count = 0;
        await foreach (var _ in source.LoadAsync(CancellationToken.None))
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task IOntologySource_SubscribeAsync_CompletesImmediately()
    {
        IOntologySource source = new ContractTestSource("source-b");

        var count = 0;
        await foreach (var _ in source.SubscribeAsync(CancellationToken.None))
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    private sealed class ContractTestSource(string sourceId) : IOntologySource
    {
        public string SourceId { get; } = sourceId;

        public async IAsyncEnumerable<OntologyDelta> LoadAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }

        public async IAsyncEnumerable<OntologyDelta> SubscribeAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }
}

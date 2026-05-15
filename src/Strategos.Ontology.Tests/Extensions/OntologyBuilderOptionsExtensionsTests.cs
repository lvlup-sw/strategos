using System.Collections.Immutable;

using Microsoft.Extensions.DependencyInjection;

using Strategos.Ontology;
using Strategos.Ontology.Configuration;

namespace Strategos.Ontology.Tests.Extensions;

/// <summary>
/// DR-3 (Task 12): DI extension <c>OntologyOptions.AddSource&lt;T&gt;()</c>
/// registers an <see cref="IOntologySource"/> implementation as transient
/// so the source-drain in <see cref="OntologyGraphBuilder"/> can resolve
/// it from <see cref="IServiceProvider"/>. Resolution as
/// <see cref="IEnumerable{T}"/> is the production wiring contract.
/// </summary>
public class OntologyBuilderOptionsExtensionsTests
{
    [Test]
    public async Task AddSource_TestSource_RegistersAsTransient()
    {
        var services = new ServiceCollection();

        services.AddOntology(opts =>
        {
            opts.AddSource<DiTestOntologySource>();
        });

        var provider = services.BuildServiceProvider();

        var resolved = provider.GetServices<IOntologySource>().ToList();

        await Assert.That(resolved).HasCount().EqualTo(1);
        await Assert.That(resolved[0]).IsTypeOf<DiTestOntologySource>();
    }

    [Test]
    public async Task AddSource_ResolvedTwice_YieldsDistinctInstances()
    {
        // Transient lifetime contract — each resolution is a fresh instance.
        var services = new ServiceCollection();

        services.AddOntology(opts =>
        {
            opts.AddSource<DiTestOntologySource>();
        });

        var provider = services.BuildServiceProvider();

        var first = provider.GetServices<IOntologySource>().Single();
        var second = provider.GetServices<IOntologySource>().Single();

        await Assert.That(first).IsNotSameReferenceAs(second);
    }

    [Test]
    public async Task AddSource_MultipleSources_AllRegistered()
    {
        // AddSource<T> must accumulate registrations — registering two
        // distinct source types both surface in IEnumerable<IOntologySource>.
        var services = new ServiceCollection();

        services.AddOntology(opts =>
        {
            opts.AddSource<DiTestOntologySource>();
            opts.AddSource<SecondDiTestOntologySource>();
        });

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetServices<IOntologySource>().ToList();

        await Assert.That(resolved).HasCount().EqualTo(2);
        await Assert.That(resolved.Any(s => s is DiTestOntologySource)).IsTrue();
        await Assert.That(resolved.Any(s => s is SecondDiTestOntologySource)).IsTrue();
    }

    /// <summary>
    /// Parameterless <see cref="IOntologySource"/> for DI activation. The
    /// production <c>TestOntologySource</c> fixture uses <c>required</c>
    /// init properties so it cannot be activated by the container without
    /// a factory; this minimal variant exists solely for DI-resolution
    /// coverage. Real ingester wiring will follow the same pattern by
    /// supplying a parameterless ctor or a factory registration.
    /// </summary>
    private sealed class DiTestOntologySource : IOntologySource
    {
        public string SourceId => "di-test";

        public async IAsyncEnumerable<OntologyDelta> LoadAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }

        public async IAsyncEnumerable<OntologyDelta> SubscribeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class SecondDiTestOntologySource : IOntologySource
    {
        public string SourceId => "di-test-second";

        public async IAsyncEnumerable<OntologyDelta> LoadAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }

        public async IAsyncEnumerable<OntologyDelta> SubscribeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }
}

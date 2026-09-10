using System.Reflection;
using global::Npgsql;
using Strategos.Ontology.Configuration;

namespace Strategos.Ontology.Npgsql.Tests.Provider;

/// <summary>
/// DR-13 (R5, #130): the provider's database entry point is an INJECTED
/// <see cref="NpgsqlDataSource"/> (never an internally-constructed connection /
/// connection string), and the DI wiring documents a <c>Max Auto Prepare</c>
/// posture so the stable relate/unrelate/traversal statements are auto-prepared by
/// Npgsql across the pooled connections — server-side plan caching without the
/// provider having to hold a connection to call <c>PrepareAsync</c> explicitly.
/// </summary>
/// <remarks>
/// These are reflection / DI-configuration assertions — no live database (INV-2).
/// </remarks>
public class DataSourcePostureTests
{
    [Test]
    public async Task Provider_EntryPoint_IsInjectedNpgsqlDataSource()
    {
        // The provider's single public constructor must take an NpgsqlDataSource as
        // its DB entry point — injected, not built from a connection string inside
        // the provider. This is what lets the DI layer own pooling, vector mapping,
        // and the Max Auto Prepare posture.
        var ctors = typeof(PgVectorObjectSetProvider)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        await Assert.That(ctors).HasSingleItem();

        var firstParam = ctors[0].GetParameters()[0];
        await Assert.That(firstParam.ParameterType).IsEqualTo(typeof(NpgsqlDataSource));

        // No public/instance constructor accepts a raw connection string — the
        // provider never owns connection-string-to-data-source construction.
        var takesConnectionString = ctors.Any(c =>
            c.GetParameters().Any(p => p.ParameterType == typeof(string)));
        await Assert.That(takesConnectionString).IsFalse();
    }

    [Test]
    public async Task UsePgVector_ConfiguresMaxAutoPrepare()
    {
        // The DI shorthand builds the NpgsqlDataSource; it must enable Max Auto
        // Prepare so the stable, repeated relate/traversal statements are prepared
        // (server-side plan cache) transparently across pooled connections. We pin
        // the documented posture by inspecting the data source the DI path builds.
        var options = new OntologyOptions();
        options.UsePgVector("Host=localhost;Database=strategos;Username=u;Password=p");

        var services = new global::Microsoft.Extensions.DependencyInjection.ServiceCollection();
        foreach (var registration in options.ServiceRegistrations)
        {
            registration(services);
        }

        var dataSource = services
            .Where(d => d.ServiceType == typeof(NpgsqlDataSource))
            .Select(d => d.ImplementationInstance)
            .OfType<NpgsqlDataSource>()
            .Single();

        // The built data source's connection string carries a non-zero
        // Max Auto Prepare (auto-prepare enabled).
        var csb = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);
        await Assert.That(csb.MaxAutoPrepare).IsGreaterThan(0);
    }
}

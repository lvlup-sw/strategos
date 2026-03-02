using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests;

public class PgVectorServiceCollectionExtensionsTests
{
    private static NpgsqlDataSource CreateDataSource()
    {
        // NpgsqlDataSource requires a builder to construct — cannot be mocked directly.
        // Use a dummy connection string; the data source won't actually connect in these tests.
        var builder = new NpgsqlDataSourceBuilder("Host=localhost;Database=test_dummy");
        return builder.Build();
    }

    [Test]
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "Test code.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code.")]
    public async Task AddPgVectorObjectSets_RegistersIObjectSetProvider()
    {
        var services = new ServiceCollection();

        // Register required dependencies
        using var ds = CreateDataSource();
        services.AddSingleton(ds);
        services.AddSingleton(Substitute.For<IEmbeddingProvider>());

        services.AddPgVectorObjectSets(opts =>
        {
            opts.ConnectionString = "Host=localhost;Database=test";
        });

        var provider = services.BuildServiceProvider();
        var objectSetProvider = provider.GetService<IObjectSetProvider>();

        await Assert.That(objectSetProvider).IsNotNull();
        await Assert.That(objectSetProvider).IsTypeOf<PgVectorObjectSetProvider>();
    }

    [Test]
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "Test code.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code.")]
    public async Task AddPgVectorObjectSets_RegistersIObjectSetWriter()
    {
        var services = new ServiceCollection();

        using var ds = CreateDataSource();
        services.AddSingleton(ds);
        services.AddSingleton(Substitute.For<IEmbeddingProvider>());

        services.AddPgVectorObjectSets(opts =>
        {
            opts.ConnectionString = "Host=localhost;Database=test";
        });

        var provider = services.BuildServiceProvider();
        var writer = provider.GetService<IObjectSetWriter>();

        await Assert.That(writer).IsNotNull();
        await Assert.That(writer).IsTypeOf<PgVectorObjectSetProvider>();
    }

    [Test]
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "Test code.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code.")]
    public async Task AddPgVectorObjectSets_ProviderAndWriter_AreSameInstance()
    {
        var services = new ServiceCollection();

        using var ds = CreateDataSource();
        services.AddSingleton(ds);
        services.AddSingleton(Substitute.For<IEmbeddingProvider>());

        services.AddPgVectorObjectSets(opts =>
        {
            opts.ConnectionString = "Host=localhost;Database=test";
        });

        var provider = services.BuildServiceProvider();
        var objectSetProvider = provider.GetService<IObjectSetProvider>();
        var writer = provider.GetService<IObjectSetWriter>();

        await Assert.That(objectSetProvider).IsSameReferenceAs(writer);
    }

    [Test]
    public async Task AddPgVectorObjectSets_NullServices_Throws()
    {
        IServiceCollection services = null!;

        await Assert.That(() => services.AddPgVectorObjectSets(opts =>
        {
            opts.ConnectionString = "Host=localhost;Database=test";
        }))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentNullException));
    }

    [Test]
    public async Task AddPgVectorObjectSets_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        await Assert.That(() => services.AddPgVectorObjectSets(null!))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentNullException));
    }
}

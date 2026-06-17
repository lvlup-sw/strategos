using System.Reflection;

using Strategos.Ontology.MCP.Hosting;

namespace Strategos.Ontology.MCP.Hosting.Tests;

/// <summary>
/// Structural regression guard for the Hosting bridge. INV-6 (sealed-by-default): every concrete
/// class the Hosting assembly defines — public or not — must be sealed unless it is explicitly
/// designed for inheritance. Static classes (the factory and the builder extensions) and interfaces
/// are exempt; sealing is inapplicable to them. The DR-14 provider-bound dispatch introduced the
/// private NoEventStreamProvider fallback, and this guard fails the build the moment a future edit
/// adds an unsealed concrete type to the bridge.
/// </summary>
public sealed class HostingInvariantGuardTests
{
    private static Assembly HostingAssembly => typeof(OntologyServerToolFactory).Assembly;

    [Test]
    public async Task HostingConcreteTypes_AreSealed()
    {
        var unsealed = HostingAssembly.GetTypes()
            .Where(t => t.IsClass)
            // Static classes surface as abstract+sealed; sealing is inapplicable and the C# compiler
            // forbids the `sealed` modifier on them, so they are not candidates for this guard.
            .Where(t => !(t.IsAbstract && t.IsSealed))
            // Compiler-generated closures/iterators (display classes, the async/iterator state
            // machines behind the handler lambdas and NoEventStreamProvider) are not authored types.
            .Where(t => t.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is null)
            .Where(t => !t.IsSealed)
            .Select(t => t.FullName)
            .ToList();

        await Assert.That(unsealed).IsEmpty();
    }
}

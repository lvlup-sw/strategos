using System.Reflection;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// INV-2: the core <c>Strategos.Ontology.MCP</c> assembly must stay free of any
/// ModelContextProtocol SDK dependency. All SDK-typed code lives in the companion
/// <c>Strategos.Ontology.MCP.Hosting</c> package, preserving the core as an
/// SDK-agnostic, AOT-friendly surface.
/// </summary>
public sealed class AssemblyDependencyTests
{
    [Test]
    public async Task CoreMcpAssembly_HasNoModelContextProtocolDependency()
    {
        // typeof a core type forces the assembly to load; GetReferencedAssemblies
        // reports the SDK only if a ModelContextProtocol type leaked into core.
        var coreAssembly = typeof(OntologyToolDiscovery).Assembly;

        var referenced = coreAssembly.GetReferencedAssemblies();

        foreach (AssemblyName refName in referenced)
        {
            var leaks = refName.Name?.Contains("ModelContextProtocol", StringComparison.OrdinalIgnoreCase) ?? false;
            await Assert.That(leaks).IsFalse();
        }
    }
}

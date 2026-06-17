using System.Reflection;

using Strategos.Ontology.MCP.Hosting;

namespace Strategos.Ontology.MCP.Hosting.Tests;

/// <summary>
/// The .Hosting package is the SDK-bound companion to the SDK-free core. It must ship
/// the descriptor->tool adapter and the builder extension, and its assembly must
/// reference BOTH the ModelContextProtocol SDK AND the core Strategos.Ontology.MCP
/// package (the bridge depends on both ends).
/// </summary>
public sealed class PackagingTests
{
    [Test]
    public async Task Packaging_HostingPackage_ShipsAdapterAndExtension()
    {
        var hostingAssembly = typeof(OntologyServerToolFactory).Assembly;

        // Ships the public adapter factory.
        var factory = hostingAssembly.GetType("Strategos.Ontology.MCP.Hosting.OntologyServerToolFactory");
        await Assert.That(factory).IsNotNull();
        await Assert.That(factory!.IsPublic).IsTrue();
        await Assert.That(factory.GetMethod("CreateServerTools", BindingFlags.Public | BindingFlags.Static)).IsNotNull();

        // Ships the public IMcpServerBuilder extension.
        var extensions = hostingAssembly.GetType(
            "Microsoft.Extensions.DependencyInjection.OntologyMcpServerBuilderExtensions");
        await Assert.That(extensions).IsNotNull();
        await Assert.That(extensions!.IsPublic).IsTrue();

        // Two AddOntologyTools overloads ship: the explicit-graph form and the DI-resolved form
        // (DR-14). GetMethod(name) would throw AmbiguousMatchException across overloads, so assert
        // by name over the method set. Both expected parameter shapes MUST be present so that a
        // regression dropping either overload is caught (the single-count guard passed even when
        // one overload was missing).
        var addOntologyTools = extensions
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "AddOntologyTools")
            .ToList();
        await Assert.That(addOntologyTools.Count).IsEqualTo(2);

        // Each overload's parameter types (first is the IMcpServerBuilder this-arg on both).
        var signatures = addOntologyTools
            .Select(m => m.GetParameters().Select(p => p.ParameterType.Name).ToArray())
            .ToList();

        // The explicit-graph form: (IMcpServerBuilder, OntologyGraph).
        var hasExplicitGraphForm = signatures.Any(s =>
            s is ["IMcpServerBuilder", "OntologyGraph"]);
        await Assert.That(hasExplicitGraphForm).IsTrue();

        // The DI-resolved form: (IMcpServerBuilder) only.
        var hasDiResolvedForm = signatures.Any(s => s is ["IMcpServerBuilder"]);
        await Assert.That(hasDiResolvedForm).IsTrue();

        // The bridge references both ends: the SDK and the core MCP package.
        var referencedNames = hostingAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        var referencesSdk = referencedNames.Any(
            n => n.Contains("ModelContextProtocol", StringComparison.OrdinalIgnoreCase));
        await Assert.That(referencesSdk).IsTrue();

        var referencesCore = referencedNames.Any(
            n => string.Equals(n, "Strategos.Ontology.MCP", StringComparison.Ordinal));
        await Assert.That(referencesCore).IsTrue();
    }
}

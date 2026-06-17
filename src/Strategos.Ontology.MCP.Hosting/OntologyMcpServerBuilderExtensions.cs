using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

using Strategos.Ontology;
using Strategos.Ontology.MCP.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions that bridge the Strategos ontology onto an MCP server builder.
/// </summary>
public static class OntologyMcpServerBuilderExtensions
{
    /// <summary>
    /// Discovers the four ontology tools from <paramref name="graph"/> and registers them
    /// as callable MCP server tools on <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The MCP server builder (from <c>services.AddMcpServer()</c>).</param>
    /// <param name="graph">The ontology graph to derive tools from.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <remarks>
    /// Tool discovery reflects over result-record types to build output schemas, so this
    /// method inherits the same trim/AOT constraints as <see cref="OntologyServerToolFactory.CreateServerTools"/>.
    /// </remarks>
    [RequiresUnreferencedCode("Ontology tool discovery reflects over result-record types; not safe under trimming.")]
    [RequiresDynamicCode("Ontology tool discovery may require runtime code generation.")]
    public static IMcpServerBuilder AddOntologyTools(this IMcpServerBuilder builder, OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(graph);

        return builder.WithTools(OntologyServerToolFactory.CreateServerTools(graph));
    }

    /// <summary>
    /// Discovers the four ontology tools from the <see cref="OntologyGraph"/> registered in
    /// <paramref name="builder"/>'s service collection and registers them as provider-bound MCP
    /// server tools. Each tool dispatches against the host's DI-resolved
    /// <see cref="Strategos.Ontology.ObjectSets.IObjectSetProvider"/> at call time (DR-14, #113),
    /// so the host configures the backing provider once via DI rather than re-binding tools.
    /// </summary>
    /// <param name="builder">The MCP server builder (from <c>services.AddMcpServer()</c>).</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="OntologyGraph"/> is registered in the service collection.
    /// </exception>
    /// <remarks>
    /// Tool discovery reflects over result-record types to build output schemas, so this
    /// method inherits the same trim/AOT constraints as <see cref="OntologyServerToolFactory.CreateServerTools"/>.
    /// </remarks>
    [RequiresUnreferencedCode("Ontology tool discovery reflects over result-record types; not safe under trimming.")]
    [RequiresDynamicCode("Ontology tool discovery may require runtime code generation.")]
    public static IMcpServerBuilder AddOntologyTools(this IMcpServerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Resolve the graph from the same service collection the host registered it in (e.g. via
        // AddOntology). The graph is immutable, so capturing it once at registration is sound; the
        // mutable backing providers are resolved per-call from the request's IServiceProvider.
        var graphDescriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(OntologyGraph))
            ?? throw new InvalidOperationException(
                "AddOntologyTools() requires an OntologyGraph to be registered in the service "
                + "collection (e.g. via services.AddOntology(...)). Use the AddOntologyTools(graph) "
                + "overload to supply one explicitly.");

        if (graphDescriptor.ImplementationInstance is not OntologyGraph graph)
        {
            throw new InvalidOperationException(
                "The registered OntologyGraph must be a singleton instance (as produced by "
                + "services.AddOntology(...)). Use the AddOntologyTools(graph) overload to supply "
                + "a graph explicitly when it is not registered as an instance.");
        }

        return builder.WithTools(OntologyServerToolFactory.CreateServerTools(graph));
    }
}

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
}

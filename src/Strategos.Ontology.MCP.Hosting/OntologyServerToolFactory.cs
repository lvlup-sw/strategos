using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

using ModelContextProtocol.Server;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.MCP.Hosting;

/// <summary>
/// Adapts ontology <see cref="OntologyToolDescriptor"/>s into ModelContextProtocol
/// <see cref="McpServerTool"/>s. This is the SDK-bound bridge: all ModelContextProtocol
/// types live here, never in the core <c>Strategos.Ontology.MCP</c> assembly (INV-2).
/// </summary>
public static class OntologyServerToolFactory
{
    /// <summary>
    /// JSON property under the tool's <c>_meta</c> that carries the action constraint
    /// summaries. The MCP <c>Tool</c> shape has no native descriptor slot for them, so
    /// they ride along in <c>_meta</c> where MCP clients are permitted to surface
    /// implementation-defined metadata.
    /// </summary>
    internal const string ConstraintSummariesMetaKey = "constraintSummaries";

    /// <summary>
    /// Discovers the four ontology tools from <paramref name="graph"/> and adapts each
    /// into an <see cref="McpServerTool"/>, preserving the descriptor's output schema,
    /// annotations, title, and (for the action tool) its constraint summaries.
    /// </summary>
    /// <remarks>
    /// <see cref="OntologyToolDiscovery.Discover"/> reflects over result-record types to
    /// build output schemas, so this method inherits the same trim/AOT constraints.
    /// </remarks>
    [RequiresUnreferencedCode("OntologyToolDiscovery.Discover() reflects over result-record types; not safe under trimming.")]
    [RequiresDynamicCode("OntologyToolDiscovery.Discover() may require runtime code generation.")]
    public static IEnumerable<McpServerTool> CreateServerTools(OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var descriptors = new OntologyToolDiscovery(graph).Discover();
        var tools = new List<McpServerTool>(descriptors.Count);

        foreach (var descriptor in descriptors)
        {
            tools.Add(CreateServerTool(descriptor));
        }

        return tools;
    }

    private static McpServerTool CreateServerTool(OntologyToolDescriptor descriptor)
    {
        var toolName = descriptor.Name;

        // Minimal handler: the ontology tools are discovered declaratively here; the
        // concrete dispatch is wired by the host. The handler keeps the tool callable so
        // tools/call routes deterministically by name.
        var handler = (string? input) => $"{{\"tool\":\"{toolName}\"}}";

        var options = new McpServerToolCreateOptions
        {
            Name = descriptor.Name,
            Description = descriptor.Description,
            Title = descriptor.Title,
            OutputSchema = descriptor.OutputSchema,
            // The SDK only surfaces an explicitly provided OutputSchema on the protocol
            // Tool when structured content is opted in; otherwise it is silently dropped.
            UseStructuredContent = descriptor.OutputSchema.HasValue,
            Meta = BuildMeta(descriptor),
        };

        ApplyAnnotations(options, descriptor.Annotations);

        return McpServerTool.Create(handler, options);
    }

    private static void ApplyAnnotations(McpServerToolCreateOptions options, ToolAnnotations annotations)
    {
        options.ReadOnly = annotations.ReadOnlyHint;
        options.Destructive = annotations.DestructiveHint;
        options.Idempotent = annotations.IdempotentHint;
        options.OpenWorld = annotations.OpenWorldHint;
    }

    private static JsonObject? BuildMeta(OntologyToolDescriptor descriptor)
    {
        if (descriptor.ConstraintSummaries.Count == 0)
        {
            return null;
        }

        var summariesJson = JsonSerializer.SerializeToNode(descriptor.ConstraintSummaries);
        return new JsonObject
        {
            [ConstraintSummariesMetaKey] = summariesJson,
        };
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Strategos.Ontology.Actions;
using Strategos.Ontology.Events;
using Strategos.Ontology.MCP;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Query;
using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.MCP.Hosting;

/// <summary>
/// Adapts ontology <see cref="OntologyToolDescriptor"/>s into ModelContextProtocol
/// <see cref="McpServerTool"/>s. This is the SDK-bound bridge: all ModelContextProtocol
/// types live here, never in the core <c>Strategos.Ontology.MCP</c> assembly (INV-2).
/// </summary>
/// <remarks>
/// <para>
/// DR-14 (#113): the adapted tools are <em>provider-bound</em>. Each tool's handler resolves
/// the backing <see cref="IObjectSetProvider"/> (and, where applicable, the
/// <see cref="IActionDispatcher"/> and <see cref="IEventStreamProvider"/>) from the per-call
/// <see cref="RequestContext{CallToolRequestParams}.Services"/> at invocation time, so a
/// <c>tools/call</c> executes against the configured provider and returns real rows. The earlier
/// echo stub is gone.
/// </para>
/// <para>
/// The descriptor's <c>OutputSchema</c> and <c>_meta</c> (constraint summaries) ride through
/// unchanged, and every tool result record carries its own <c>_meta</c> envelope (INV-3).
/// </para>
/// </remarks>
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
    /// Discovers the four ontology tools from <paramref name="graph"/> and adapts each into a
    /// provider-bound <see cref="McpServerTool"/>, preserving the descriptor's output schema,
    /// annotations, title, and (for the action tool) its constraint summaries.
    /// </summary>
    /// <param name="graph">The ontology graph to derive tools from.</param>
    /// <returns>One provider-bound <see cref="McpServerTool"/> per discovered ontology tool.</returns>
    /// <remarks>
    /// Each tool's handler resolves its backing provider(s) from the per-call request's
    /// <see cref="IServiceProvider"/> (DR-14). <see cref="OntologyToolDiscovery.Discover"/>
    /// reflects over result-record types to build output schemas, so this method inherits the
    /// same trim/AOT constraints.
    /// </remarks>
    [RequiresUnreferencedCode("OntologyToolDiscovery.Discover() reflects over result-record types; not safe under trimming.")]
    [RequiresDynamicCode("OntologyToolDiscovery.Discover() may require runtime code generation.")]
    public static IEnumerable<McpServerTool> CreateServerTools(OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var descriptors = new OntologyToolDiscovery(graph)
            .Discover()
            .ToDictionary(d => d.Name);

        var tools = new List<McpServerTool>(descriptors.Count);
        foreach (var descriptor in descriptors.Values)
        {
            tools.Add(CreateServerTool(graph, descriptor));
        }

        return tools;
    }

    private static McpServerTool CreateServerTool(OntologyGraph graph, OntologyToolDescriptor descriptor)
    {
        var handler = BuildHandler(graph, descriptor.Name);

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

    // Maps each ontology tool name to a provider-bound handler. The backing providers are
    // resolved from the per-call RequestContext.Services so a single registration serves any
    // configured IObjectSetProvider (in-memory, Npgsql, ...) without re-binding tools (DR-14).
    private static Delegate BuildHandler(OntologyGraph graph, string toolName) => toolName switch
    {
        "ontology_explore" => ExploreHandler(graph),
        "ontology_query" => QueryHandler(graph),
        "ontology_action" => ActionHandler(graph),
        "ontology_validate" => ValidateHandler(graph),

        // OntologyToolDiscovery only ever yields the four names above; an unrecognized name is a
        // discovery/factory drift bug, surfaced loudly rather than silently echoed.
        _ => throw new InvalidOperationException(
            $"No provider-bound handler is registered for ontology tool '{toolName}'."),
    };

    // Every parameter that the underlying tool treats as optional carries a default here so the
    // SDK's AIFunctionFactory marks it optional in the input schema. Without a default the factory
    // makes the parameter REQUIRED even when its type is nullable, and a tools/call that omits it
    // fails to bind (DR-14: arguments dictionary is missing a value for the required parameter).
    private static Delegate ExploreHandler(OntologyGraph graph) =>
        (string scope, string? domain = null, string? objectType = null, string? traverseFrom = null, int maxDepth = 2) =>
            new OntologyExploreTool(graph).Explore(scope, domain, objectType, traverseFrom, maxDepth);

    private static Delegate QueryHandler(OntologyGraph graph) =>
        async (
            RequestContext<CallToolRequestParams> context,
            string objectType,
            string? domain = null,
            string? filter = null,
            string? traverseLink = null,
            string? interfaceName = null,
            string? include = null,
            string? semanticQuery = null,
            CancellationToken ct = default) =>
        {
            var tool = new OntologyQueryTool(
                graph,
                ResolveObjectSetProvider(context),
                ResolveEventStreamProvider(context),
                NullLogger<OntologyQueryTool>.Instance,
                context.Services?.GetService<IKeywordSearchProvider>());

            return await tool.QueryAsync(
                objectType,
                domain,
                filter,
                traverseLink,
                interfaceName,
                include,
                semanticQuery,
                ct: ct).ConfigureAwait(false);
        };

    private static Delegate ActionHandler(OntologyGraph graph) =>
        async (
            RequestContext<CallToolRequestParams> context,
            string objectType,
            string action,
            JsonElement request,
            string? domain = null,
            string? objectId = null,
            string? filter = null,
            CancellationToken ct = default) =>
        {
            var dispatcher = context.Services?.GetService<IActionDispatcher>()
                ?? throw new InvalidOperationException(
                    "ontology_action requires an IActionDispatcher to be registered in the host's service provider.");

            var tool = new OntologyActionTool(graph, dispatcher, ResolveObjectSetProvider(context));

            return await tool.ExecuteAsync(
                objectType,
                action,
                request,
                domain,
                objectId,
                filter,
                ct).ConfigureAwait(false);
        };

    private static Delegate ValidateHandler(OntologyGraph graph) =>
        (RequestContext<CallToolRequestParams> context, DesignIntent intent) =>
        {
            var query = context.Services?.GetService<IOntologyQuery>()
                ?? throw new InvalidOperationException(
                    "ontology_validate requires an IOntologyQuery to be registered in the host's service provider.");

            var tool = new OntologyValidateTool(query, context.Services?.GetService<IOntologyCoverageProvider>());
            return tool.Validate(intent);
        };

    private static IObjectSetProvider ResolveObjectSetProvider(RequestContext<CallToolRequestParams> context) =>
        context.Services?.GetService<IObjectSetProvider>()
            ?? throw new InvalidOperationException(
                "The ontology MCP tools require an IObjectSetProvider to be registered in the host's "
                + "service provider. Register one (e.g. services.AddSingleton<IObjectSetProvider>(...)) "
                + "before AddOntologyTools.");

    // The structural query path never reads the event stream; resolve a real provider when the host
    // registered one, otherwise fall back to a no-op so QueryAsync's non-null ctor contract holds
    // without forcing every host to register an event store for read-only queries.
    private static IEventStreamProvider ResolveEventStreamProvider(RequestContext<CallToolRequestParams> context) =>
        context.Services?.GetService<IEventStreamProvider>() ?? NoEventStreamProvider.Instance;

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

    /// <summary>
    /// No-op <see cref="IEventStreamProvider"/> used as the fallback for read-only structural
    /// queries when the host has not registered an event store. It yields no events; the
    /// query tool's structural path never invokes it.
    /// </summary>
    private sealed class NoEventStreamProvider : IEventStreamProvider
    {
        internal static readonly NoEventStreamProvider Instance = new();

        public async IAsyncEnumerable<OntologyEvent> QueryEventsAsync(
            EventQuery query,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }
    }
}

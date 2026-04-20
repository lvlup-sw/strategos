using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Schema;

namespace Strategos.Ontology.MCP.Internal;

/// <summary>
/// Generates JSON Schema for tool result types via .NET 10's
/// <see cref="JsonSchemaExporter"/>. Produces a JsonElement suitable for
/// MCP tool descriptors' <c>OutputSchema</c> field.
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.6
/// </summary>
internal static class JsonSchemaHelper
{
    /// <summary>
    /// Returns a JSON Schema element describing the serialization shape of
    /// <typeparamref name="T"/>. Uses the default serializer options;
    /// callers needing custom resolvers should add an overload.
    /// </summary>
    /// <remarks>
    /// Reflection-based — not safe under aggressive trimming or pure AOT.
    /// The MCP package marks itself AOT-compatible because the runtime tools
    /// themselves are AOT-clean, but tool-discovery via this helper requires
    /// reflection metadata for the result types. Callers running under
    /// trimming should statically reference the result types from a
    /// DynamicallyAccessedMembers-aware site.
    /// </remarks>
    [RequiresUnreferencedCode("Uses System.Text.Json reflection-based serialization to derive a schema for T.")]
    [RequiresDynamicCode("System.Text.Json schema export may require runtime code generation for T.")]
    public static JsonElement JsonSchemaFor<T>()
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(JsonSerializerOptions.Default, typeof(T));
        return JsonSerializer.SerializeToElement(node);
    }
}

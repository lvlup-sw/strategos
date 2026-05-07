using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Schema;

namespace Strategos.Ontology.MCP.Internal;

/// <summary>
/// Generates JSON Schema documents (as <see cref="JsonElement"/>) for arbitrary
/// CLR types using <see cref="JsonSchemaExporter"/> from .NET 10. Used to
/// populate <c>OntologyToolDescriptor.OutputSchema</c> for each MCP tool.
/// </summary>
internal static class JsonSchemaHelper
{
    /// <summary>
    /// Returns a JSON Schema describing the wire shape of <typeparamref name="T"/>.
    /// </summary>
    [RequiresUnreferencedCode("Schema generation reflects over T; not safe under trimming.")]
    [RequiresDynamicCode("Schema generation may require runtime code generation.")]
    public static JsonElement JsonSchemaFor<T>()
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(
            JsonSerializerOptions.Default,
            typeof(T));
        return JsonSerializer.SerializeToElement(node);
    }
}

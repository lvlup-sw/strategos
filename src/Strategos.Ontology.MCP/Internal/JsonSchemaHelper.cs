using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

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
    /// <remarks>
    /// For types decorated with <see cref="JsonPolymorphicAttribute"/> +
    /// <see cref="JsonDerivedTypeAttribute"/>, .NET 10's <see cref="JsonSchemaExporter"/>
    /// emits an <c>anyOf</c> schema. The MCP-spec recommendation (and what most
    /// clients dispatch on) is <c>oneOf</c> with a discriminator. When polymorphism
    /// is detected we rewrite the root <c>anyOf</c> to <c>oneOf</c> so the union
    /// shape is unambiguous to the consumer.
    /// </remarks>
    [RequiresUnreferencedCode("Schema generation reflects over T; not safe under trimming.")]
    [RequiresDynamicCode("Schema generation may require runtime code generation.")]
    public static JsonElement JsonSchemaFor<T>()
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(
            JsonSerializerOptions.Default,
            typeof(T));

        if (typeof(T).GetCustomAttributes(typeof(JsonPolymorphicAttribute), inherit: false).Length > 0
            && node is JsonObject obj
            && obj["anyOf"] is JsonArray anyOf)
        {
            // Replace anyOf with oneOf in-place. The branch sub-schemas already
            // carry the discriminator constants (e.g. resultKind="filter") that
            // make a oneOf legal — at runtime exactly one branch matches.
            obj.Remove("anyOf");
            obj["oneOf"] = anyOf.DeepClone();
        }

        return JsonSerializer.SerializeToElement(node);
    }
}

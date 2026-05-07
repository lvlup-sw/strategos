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
    /// Pure passthrough over <see cref="JsonSchemaExporter"/> — does NOT rewrite
    /// any keyword (in particular, <c>anyOf</c> is preserved as-is).
    /// </summary>
    /// <remarks>
    /// For polymorphic types where you want the MCP-spec <c>oneOf</c> dispatch
    /// shape, use <see cref="JsonSchemaForUnion{T}"/>; that overload validates
    /// the type carries <see cref="JsonPolymorphicAttribute"/> and rewrites
    /// the root <c>anyOf</c> to <c>oneOf</c> with a positive root-shape assertion.
    /// </remarks>
    [RequiresUnreferencedCode("Schema generation reflects over T; not safe under trimming.")]
    [RequiresDynamicCode("Schema generation may require runtime code generation.")]
    public static JsonElement JsonSchemaFor<T>()
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(
            JsonSerializerOptions.Default,
            typeof(T));

        return JsonSerializer.SerializeToElement(node);
    }

    /// <summary>
    /// Returns a JSON Schema for a polymorphic discriminated union type
    /// <typeparamref name="T"/>, rewriting the root <c>anyOf</c> emitted by
    /// <see cref="JsonSchemaExporter"/> to <c>oneOf</c> so MCP clients can
    /// dispatch on the discriminator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Throws <see cref="InvalidOperationException"/> if <typeparamref name="T"/>
    /// is not decorated with <see cref="JsonPolymorphicAttribute"/> — call
    /// <see cref="JsonSchemaFor{T}"/> for non-polymorphic types instead.
    /// </para>
    /// <para>
    /// Also throws <see cref="InvalidOperationException"/> if the rewrite did
    /// not produce a top-level <c>oneOf</c>, which signals that
    /// <see cref="JsonSchemaExporter"/>'s output shape has changed
    /// (e.g. wrapping in <c>$defs</c> / <c>$ref</c>) and the rewrite is
    /// silently no-oping — a regression we want to fail loudly on.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("Schema generation reflects over T; not safe under trimming.")]
    [RequiresDynamicCode("Schema generation may require runtime code generation.")]
    public static JsonElement JsonSchemaForUnion<T>() where T : class
    {
        if (typeof(T).GetCustomAttributes(typeof(JsonPolymorphicAttribute), inherit: false).Length == 0)
        {
            throw new InvalidOperationException(
                $"{typeof(T).Name} is not polymorphic; use JsonSchemaFor<T>() instead. " +
                "JsonSchemaForUnion requires [JsonPolymorphic].");
        }

        var node = JsonSchemaExporter.GetJsonSchemaAsNode(
            JsonSerializerOptions.Default,
            typeof(T));

        if (node is JsonObject obj && obj["anyOf"] is JsonArray anyOf)
        {
            // Replace anyOf with oneOf in-place. The branch sub-schemas already
            // carry the discriminator constants (e.g. resultKind="filter") that
            // make a oneOf legal — at runtime exactly one branch matches.
            obj.Remove("anyOf");
            obj["oneOf"] = anyOf.DeepClone();
        }

        // Positive root-shape assertion: a polymorphic T must yield a top-level
        // 'oneOf' (after the rewrite). If it doesn't, JsonSchemaExporter's
        // output shape has changed and our rewrite has silently no-op'd —
        // fail loudly so the descriptor never ships a malformed schema.
        if (node is not JsonObject after || after["oneOf"] is null)
        {
            throw new InvalidOperationException(
                $"Expected JsonSchemaExporter output for polymorphic type {typeof(T).Name} " +
                $"to contain a top-level 'oneOf' (after 'anyOf'->'oneOf' rewrite); got: {node?.ToJsonString() ?? "<null>"}. " +
                "JsonSchemaExporter output shape may have changed.");
        }

        return JsonSerializer.SerializeToElement(node);
    }
}

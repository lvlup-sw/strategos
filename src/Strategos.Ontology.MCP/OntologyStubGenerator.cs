using System.Text;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Generates enriched Python-style (.pyi) stubs from the OntologyGraph.
/// Each object type produces a class with a docstring describing its
/// properties, links, actions, events, and implemented interfaces.
/// </summary>
public sealed class OntologyStubGenerator
{
    private readonly OntologyGraph _graph;

    public OntologyStubGenerator(OntologyGraph graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Generates a Python stub string for each object type in the graph.
    /// </summary>
    public IReadOnlyList<string> Generate()
    {
        return _graph.ObjectTypes.Select(GenerateStub).ToList();
    }

    private static string GenerateStub(ObjectTypeDescriptor objectType)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"class {objectType.Name}:");
        sb.AppendLine("    \"\"\"");
        sb.AppendLine($"    {objectType.DomainName}.{objectType.Name}");

        AppendProperties(sb, objectType);
        AppendLinks(sb, objectType);
        AppendActions(sb, objectType);
        AppendEvents(sb, objectType);
        AppendInterfaces(sb, objectType);

        sb.AppendLine("    \"\"\"");
        sb.Append("    ...");
        return sb.ToString();
    }

    private static void AppendProperties(StringBuilder sb, ObjectTypeDescriptor objectType)
    {
        if (objectType.Properties.Count == 0)
        {
            return;
        }

        sb.AppendLine("    Properties:");
        foreach (var prop in objectType.Properties)
        {
            var typeName = MapClrTypeToPython(prop.PropertyType);
            var required = prop.IsRequired ? " (required)" : "";
            var computed = prop.IsComputed ? " (computed)" : "";
            sb.AppendLine($"        {prop.Name}: {typeName}{required}{computed}");
        }
    }

    private static void AppendLinks(StringBuilder sb, ObjectTypeDescriptor objectType)
    {
        if (objectType.Links.Count == 0)
        {
            return;
        }

        sb.AppendLine("    Links:");
        foreach (var link in objectType.Links)
        {
            var cardinalitySuffix = link.Cardinality switch
            {
                LinkCardinality.OneToMany => "[]",
                LinkCardinality.ManyToMany => "[]",
                _ => "",
            };
            var cardinalityLabel = link.Cardinality switch
            {
                LinkCardinality.OneToOne => "one-to-one",
                LinkCardinality.OneToMany => "one-to-many",
                LinkCardinality.ManyToMany => "many-to-many",
                _ => link.Cardinality.ToString(),
            };
            sb.AppendLine($"        {link.Name} -> {link.TargetTypeName}{cardinalitySuffix} ({cardinalityLabel})");
        }
    }

    private static void AppendActions(StringBuilder sb, ObjectTypeDescriptor objectType)
    {
        if (objectType.Actions.Count == 0)
        {
            return;
        }

        sb.AppendLine("    Actions:");
        foreach (var action in objectType.Actions)
        {
            var accepts = action.AcceptsType is not null ? $"request: {action.AcceptsType.Name}" : "";
            var returns = action.ReturnsType is not null ? $" -> {action.ReturnsType.Name}" : "";
            sb.AppendLine($"        {action.Name}({accepts}){returns}");
        }
    }

    private static void AppendEvents(StringBuilder sb, ObjectTypeDescriptor objectType)
    {
        if (objectType.Events.Count == 0)
        {
            return;
        }

        sb.AppendLine("    Events:");
        foreach (var evt in objectType.Events)
        {
            var materializes = evt.MaterializedLinks.Count > 0
                ? $" -> materializes {string.Join(", ", evt.MaterializedLinks)} link"
                : "";
            sb.AppendLine($"        {evt.EventType.Name}{materializes}");
        }
    }

    private static void AppendInterfaces(StringBuilder sb, ObjectTypeDescriptor objectType)
    {
        if (objectType.ImplementedInterfaces.Count == 0)
        {
            return;
        }

        var interfaceNames = string.Join(", ", objectType.ImplementedInterfaces.Select(i => i.Name));
        sb.AppendLine($"    Interfaces: {interfaceNames}");
    }

    private static string MapClrTypeToPython(Type clrType)
    {
        if (clrType == typeof(string))
        {
            return "str";
        }

        if (clrType == typeof(int) || clrType == typeof(long))
        {
            return "int";
        }

        if (clrType == typeof(float) || clrType == typeof(double) || clrType == typeof(decimal))
        {
            return "float";
        }

        if (clrType == typeof(bool))
        {
            return "bool";
        }

        if (clrType == typeof(DateTime) || clrType == typeof(DateTimeOffset))
        {
            return "datetime";
        }

        return clrType.Name;
    }
}

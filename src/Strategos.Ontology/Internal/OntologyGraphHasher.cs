using System.Security.Cryptography;
using System.Text;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Internal;

/// <summary>
/// Computes a content-stable SHA-256 hash over the structural fields of an
/// <see cref="OntologyGraph"/>. The hash powers <see cref="OntologyGraph.Version"/>
/// which in turn drives MCP `_meta.ontologyVersion` cache invalidation
/// downstream.
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.1
///
/// IMPORTANT: <c>Description</c> text and <see cref="OntologyGraph.Warnings"/>
/// are deliberately EXCLUDED from the hash. Documentation churn must not bust
/// caches that exist for structural invalidation. See design §4.1.
/// </summary>
internal static class OntologyGraphHasher
{
    /// <summary>
    /// Returns the lowercase hex SHA-256 over a stable serialization of the
    /// graph's structural fields. The serialization is order-independent
    /// (collections are sorted) so two graphs built from identical DSL produce
    /// identical hashes across processes.
    /// </summary>
    public static string ComputeVersion(OntologyGraph graph)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteStableHeader(writer, graph);
            WriteObjectTypes(writer, graph);
            WriteInterfaces(writer, graph);
            WriteCrossDomainLinks(writer, graph);
            WriteWorkflowChains(writer, graph);
        }

        var bytes = SHA256.HashData(stream.ToArray());
        return Convert.ToHexStringLower(bytes);
    }

    private static void WriteStableHeader(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("Domains:");
        foreach (var domainName in graph.Domains.Select(d => d.DomainName).OrderBy(n => n, StringComparer.Ordinal))
        {
            writer.Write(domainName);
        }
    }

    private static void WriteObjectTypes(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("ObjectTypes:");
        var sorted = graph.ObjectTypes
            .OrderBy(o => o.DomainName, StringComparer.Ordinal)
            .ThenBy(o => o.Name, StringComparer.Ordinal);

        foreach (var ot in sorted)
        {
            WriteObjectType(writer, ot);
        }
    }

    private static void WriteObjectType(BinaryWriter writer, ObjectTypeDescriptor ot)
    {
        writer.Write("ObjectType:");
        writer.Write(ot.DomainName);
        writer.Write(ot.Name);
        writer.Write(ot.ParentTypeName ?? string.Empty);

        WriteProperties(writer, ot.Properties);
        WriteActions(writer, ot.Actions);
        WriteLinks(writer, ot.Links);
        WriteEvents(writer, ot.Events);
        WriteLifecycle(writer, ot.Lifecycle);
        WriteImplementedInterfaces(writer, ot.ImplementedInterfaces);
    }

    private static void WriteProperties(BinaryWriter writer, IReadOnlyList<PropertyDescriptor> properties)
    {
        writer.Write("Properties:");
        foreach (var p in properties.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            WriteProperty(writer, p);
        }
    }

    private static void WriteProperty(BinaryWriter writer, PropertyDescriptor p)
    {
        writer.Write(p.Name);
        writer.Write((int)p.Kind);
        writer.Write(p.PropertyType.FullName ?? string.Empty);
        writer.Write(p.IsRequired);
        writer.Write(p.VectorDimensions ?? 0);
    }

    private static void WriteActions(BinaryWriter writer, IReadOnlyList<ActionDescriptor> actions)
    {
        writer.Write("Actions:");
        foreach (var a in actions.OrderBy(a => a.Name, StringComparer.Ordinal))
        {
            WriteAction(writer, a);
        }
    }

    private static void WriteAction(BinaryWriter writer, ActionDescriptor a)
    {
        // NOTE: Description is intentionally NOT written. See design §4.1 —
        // documentation churn must not bust structural caches.
        writer.Write(a.Name);
        writer.Write(a.AcceptsType?.FullName ?? string.Empty);
        writer.Write(a.ReturnsType?.FullName ?? string.Empty);
        writer.Write((int)a.BindingType);

        writer.Write("Pre:");
        foreach (var pre in a.Preconditions.OrderBy(p => p.Description, StringComparer.Ordinal))
        {
            writer.Write(pre.Description);
            writer.Write((int)pre.Kind);
            writer.Write((int)pre.Strength);
            writer.Write(pre.LinkName ?? string.Empty);
        }

        writer.Write("Post:");
        foreach (var post in a.Postconditions
            .OrderBy(p => (int)p.Kind)
            .ThenBy(p => p.PropertyName, StringComparer.Ordinal)
            .ThenBy(p => p.LinkName, StringComparer.Ordinal)
            .ThenBy(p => p.EventTypeName, StringComparer.Ordinal))
        {
            writer.Write((int)post.Kind);
            writer.Write(post.PropertyName ?? string.Empty);
            writer.Write(post.LinkName ?? string.Empty);
            writer.Write(post.EventTypeName ?? string.Empty);
        }
    }

    private static void WriteLinks(BinaryWriter writer, IReadOnlyList<LinkDescriptor> links)
    {
        writer.Write("Links:");
        foreach (var l in links.OrderBy(l => l.Name, StringComparer.Ordinal))
        {
            // NOTE: Description is intentionally NOT written. See design §4.1.
            writer.Write(l.Name);
            writer.Write(l.TargetTypeName);
            writer.Write((int)l.Cardinality);
            writer.Write("EdgeProperties:");
            foreach (var edgeProp in l.EdgeProperties.OrderBy(ep => ep.Name, StringComparer.Ordinal))
            {
                writer.Write(edgeProp.Name);
                writer.Write((int)edgeProp.Kind);
            }
        }
    }

    private static void WriteEvents(BinaryWriter writer, IReadOnlyList<EventDescriptor> events)
    {
        writer.Write("Events:");
        foreach (var e in events.OrderBy(e => e.EventType.FullName, StringComparer.Ordinal))
        {
            // NOTE: Description is intentionally NOT written. See design §4.1.
            writer.Write(e.EventType.FullName ?? string.Empty);
            writer.Write((int)e.Severity);
            writer.Write("MaterializedLinks:");
            foreach (var ml in e.MaterializedLinks.OrderBy(s => s, StringComparer.Ordinal))
            {
                writer.Write(ml);
            }

            writer.Write("UpdatedProperties:");
            foreach (var up in e.UpdatedProperties.OrderBy(s => s, StringComparer.Ordinal))
            {
                writer.Write(up);
            }
        }
    }

    private static void WriteLifecycle(BinaryWriter writer, LifecycleDescriptor? lifecycle)
    {
        writer.Write("Lifecycle:");
        if (lifecycle is null)
        {
            return;
        }

        writer.Write(lifecycle.PropertyName);
        writer.Write(lifecycle.StateEnumTypeName);

        writer.Write("States:");
        foreach (var state in lifecycle.States.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            writer.Write(state.Name);
            writer.Write(state.IsInitial);
            writer.Write(state.IsTerminal);
        }

        writer.Write("Transitions:");
        foreach (var t in lifecycle.Transitions
            .OrderBy(t => t.FromState, StringComparer.Ordinal)
            .ThenBy(t => t.ToState, StringComparer.Ordinal)
            .ThenBy(t => t.TriggerActionName ?? t.TriggerEventTypeName ?? string.Empty, StringComparer.Ordinal))
        {
            writer.Write(t.FromState);
            writer.Write(t.ToState);
            writer.Write(t.TriggerActionName ?? string.Empty);
            writer.Write(t.TriggerEventTypeName ?? string.Empty);
        }
    }

    private static void WriteImplementedInterfaces(BinaryWriter writer, IReadOnlyList<InterfaceDescriptor> interfaces)
    {
        writer.Write("ImplementedInterfaces:");
        foreach (var i in interfaces.OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            writer.Write(i.Name);
        }
    }

    private static void WriteInterfaces(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("Interfaces:");
        foreach (var i in graph.Interfaces.OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            writer.Write(i.Name);
            writer.Write("Properties:");
            foreach (var p in i.Properties.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.Write(p.Name);
                writer.Write((int)p.Kind);
                writer.Write(p.PropertyType.FullName ?? string.Empty);
            }
        }
    }

    private static void WriteCrossDomainLinks(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("CrossDomainLinks:");
        var sorted = graph.CrossDomainLinks
            .OrderBy(l => l.SourceDomain, StringComparer.Ordinal)
            .ThenBy(l => l.SourceObjectType.Name, StringComparer.Ordinal)
            .ThenBy(l => l.Name, StringComparer.Ordinal);

        foreach (var l in sorted)
        {
            // NOTE: Description is intentionally NOT written. See design §4.1.
            writer.Write(l.SourceDomain);
            writer.Write(l.SourceObjectType.Name);
            writer.Write(l.Name);
            writer.Write(l.TargetDomain);
            writer.Write(l.TargetObjectType.Name);
            writer.Write((int)l.Cardinality);
            writer.Write("EdgeProperties:");
            foreach (var ep in l.EdgeProperties.OrderBy(ep => ep.Name, StringComparer.Ordinal))
            {
                writer.Write(ep.Name);
                writer.Write((int)ep.Kind);
            }
        }
    }

    private static void WriteWorkflowChains(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("WorkflowChains:");
        foreach (var w in graph.WorkflowChains.OrderBy(w => w.WorkflowName, StringComparer.Ordinal))
        {
            writer.Write(w.WorkflowName);
            writer.Write(w.ConsumedType.ClrType.FullName ?? string.Empty);
            writer.Write(w.ProducedType.ClrType.FullName ?? string.Empty);
        }
    }
}

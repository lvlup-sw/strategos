using System.Security.Cryptography;
using System.Text;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Internal;

/// <summary>
/// Computes a deterministic SHA-256 hash over the structural fields of an
/// <see cref="OntologyGraph"/>. The hash is exposed via <see cref="OntologyGraph.Version"/>
/// and surfaced (with a "sha256:" prefix added at the wire-emission boundary)
/// as the <c>_meta.ontologyVersion</c> field on MCP tool responses so consumers
/// can invalidate cached schema views on mismatch.
/// </summary>
internal static class OntologyGraphHasher
{
    public static string ComputeVersion(OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        // What is hashed (per design 2026-04-19-mcp-surface-conformance.md §4.1):
        //   - Domain names (sorted).
        //   - For each ObjectType (sorted by domain, name): Name, DomainName,
        //     ParentTypeName, Kind (ObjectKind enum), KeyProperty name,
        //     Properties (Name/Kind/PropertyType/IsRequired/VectorDimensions),
        //     Actions (Name/AcceptsType/ReturnsType/BindingType/BoundWorkflowName/
        //     BoundToolName/BoundToolMethod/Preconditions/Postconditions),
        //     Links (Name/TargetTypeName/Cardinality/EdgeProperties),
        //     Events (EventType/Severity/MaterializedLinks/UpdatedProperties),
        //     Lifecycle (PropertyName/StateEnumTypeName/States/Transitions),
        //     ImplementedInterfaces (Name only — full interface definition lives
        //     in the graph-level Interfaces section), InterfacePropertyMappings
        //     (Source/Target/InterfaceName), InterfaceActionMappings
        //     (InterfaceActionName/ConcreteActionName).
        //   - For each Interface (sorted by name): Name, Properties, Actions
        //     (Name/AcceptsTypeName/ReturnsTypeName).
        //   - CrossDomainLinks (Source/Target/Cardinality/EdgeProperties).
        //   - WorkflowChains (Name/Consumed/Produced).
        //
        // What is deliberately NOT hashed (rationale):
        //   - Free-form Description text on actions, links, properties, lifecycle
        //     states/transitions, events, cross-domain links, and interface actions —
        //     documentation churn must NOT bust structural caches that exist to
        //     invalidate when the schema agents reason about actually changes shape.
        //   - OntologyGraph.Warnings — advisory, non-structural.
        //   - OntologyGraph.ObjectTypeNamesByType — derived index from ObjectTypes;
        //     mutating it without mutating the underlying ObjectTypes is impossible.
        //   - PropertyDescriptor.IsComputed / DerivedFrom / TransitiveDerivedFrom —
        //     captured implicitly via PropertyKind (Computed) and the derivation
        //     chain is reconstructable from Properties.
        //   - ExternalLinkExtensionPoints.MatchedLinkNames — derived during build.
        // ActionPrecondition.Description IS included because it is the precondition's
        // identity / sort key, distinct from per-action free-form documentation prose.
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            WriteStableHeader(writer, graph);
            WriteObjectTypes(writer, graph);
            WriteInterfaces(writer, graph);
            WriteCrossDomainLinks(writer, graph);
            WriteWorkflowChains(writer, graph);
        }

        var hash = SHA256.HashData(ms.ToArray());
        return Convert.ToHexStringLower(hash);
    }

    private static void WriteStableHeader(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("DOMAINS|");
        var domainNames = graph.Domains.Select(d => d.DomainName).OrderBy(n => n, StringComparer.Ordinal);
        foreach (var name in domainNames)
        {
            WriteString(writer, name);
        }

        writer.Write("|END_DOMAINS");
    }

    private static void WriteObjectTypes(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("OBJECT_TYPES|");
        var sorted = graph.ObjectTypes
            .OrderBy(ot => ot.DomainName, StringComparer.Ordinal)
            .ThenBy(ot => ot.Name, StringComparer.Ordinal);
        foreach (var ot in sorted)
        {
            WriteObjectType(writer, ot);
        }

        writer.Write("|END_OBJECT_TYPES");
    }

    private static void WriteObjectType(BinaryWriter writer, ObjectTypeDescriptor ot)
    {
        writer.Write("OT|");
        WriteString(writer, ot.DomainName);
        WriteString(writer, ot.Name);
        WriteString(writer, ot.ParentTypeName ?? string.Empty);

        // Kind is structural: an Entity-vs-Process designation changes how
        // consumers reason about the type, even with identical shape.
        writer.Write((byte)ot.Kind);

        // Key property name (or empty if absent). Renaming the key property
        // is a dispatch-routing change, not a documentation change.
        WriteString(writer, ot.KeyProperty?.Name ?? string.Empty);

        writer.Write("|PROPS|");
        foreach (var p in ot.Properties.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            WriteProperty(writer, p);
        }

        writer.Write("|ACTIONS|");
        foreach (var a in ot.Actions.OrderBy(a => a.Name, StringComparer.Ordinal))
        {
            WriteAction(writer, a);
        }

        writer.Write("|LINKS|");
        foreach (var l in ot.Links.OrderBy(l => l.Name, StringComparer.Ordinal))
        {
            WriteLink(writer, l);
        }

        writer.Write("|EVENTS|");
        foreach (var e in ot.Events.OrderBy(e => e.EventType.FullName ?? string.Empty, StringComparer.Ordinal))
        {
            WriteEvent(writer, e);
        }

        writer.Write("|LIFECYCLE|");
        if (ot.Lifecycle is not null)
        {
            WriteLifecycle(writer, ot.Lifecycle);
        }

        writer.Write("|IFACES|");
        foreach (var i in ot.ImplementedInterfaces.OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            WriteString(writer, i.Name);
        }

        // InterfacePropertyMappings record how a type's local properties satisfy
        // each declared interface (Via() bindings). They are user-authored, not
        // derived from other fields — rebinding interface property X from local
        // property A to local property B is a structural change to dispatch.
        writer.Write("|IPM|");
        foreach (var m in ot.InterfacePropertyMappings
                              .OrderBy(m => m.InterfaceName, StringComparer.Ordinal)
                              .ThenBy(m => m.TargetPropertyName, StringComparer.Ordinal)
                              .ThenBy(m => m.SourcePropertyName, StringComparer.Ordinal))
        {
            WriteString(writer, m.InterfaceName);
            WriteString(writer, m.TargetPropertyName);
            WriteString(writer, m.SourcePropertyName);
        }

        // InterfaceActionMappings record how a type's concrete actions satisfy
        // each declared interface action. Source-of-truth user input; rebinding
        // changes which concrete action the interface call routes to.
        writer.Write("|IAM|");
        foreach (var m in ot.InterfaceActionMappings
                              .OrderBy(m => m.InterfaceActionName, StringComparer.Ordinal)
                              .ThenBy(m => m.ConcreteActionName, StringComparer.Ordinal))
        {
            WriteString(writer, m.InterfaceActionName);
            WriteString(writer, m.ConcreteActionName);
        }

        writer.Write("|END_OT");
    }

    private static void WriteProperty(BinaryWriter writer, PropertyDescriptor p)
    {
        writer.Write("P|");
        WriteString(writer, p.Name);
        WriteString(writer, p.Kind.ToString());
        WriteString(writer, p.PropertyType.FullName ?? string.Empty);

        // PropertyDescriptor exposes IsRequired (not IsNullable); record IsRequired
        // for structural sensitivity per design §4.1.
        writer.Write(p.IsRequired);
        writer.Write(p.VectorDimensions ?? 0);
    }

    private static void WriteAction(BinaryWriter writer, ActionDescriptor a)
    {
        writer.Write("A|");
        WriteString(writer, a.Name);
        WriteString(writer, a.AcceptsType?.FullName ?? string.Empty);
        WriteString(writer, a.ReturnsType?.FullName ?? string.Empty);
        WriteString(writer, a.BindingType.ToString());

        // Action dispatch routing: rebinding from one workflow/tool to another
        // is a structural behavior change for cache invalidation, even if the
        // surface shape (Name/Accepts/Returns) is unchanged.
        WriteString(writer, a.BoundWorkflowName ?? string.Empty);
        WriteString(writer, a.BoundToolName ?? string.Empty);
        WriteString(writer, a.BoundToolMethod ?? string.Empty);

        writer.Write("|PRE|");
        foreach (var pc in a.Preconditions.OrderBy(x => x.Description, StringComparer.Ordinal))
        {
            WriteString(writer, pc.Description);
            WriteString(writer, pc.Expression);
            WriteString(writer, pc.Kind.ToString());
            WriteString(writer, pc.LinkName ?? string.Empty);
            WriteString(writer, pc.Strength.ToString());
        }

        writer.Write("|POST|");
        foreach (var pc in a.Postconditions
                              .OrderBy(x => x.Kind.ToString(), StringComparer.Ordinal)
                              .ThenBy(x => x.PropertyName ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(x => x.LinkName ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(x => x.EventTypeName ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(x => x.TargetTypeName ?? string.Empty, StringComparer.Ordinal))
        {
            WriteString(writer, pc.Kind.ToString());
            WriteString(writer, pc.PropertyName ?? string.Empty);
            WriteString(writer, pc.LinkName ?? string.Empty);
            WriteString(writer, pc.EventTypeName ?? string.Empty);
            WriteString(writer, pc.TargetTypeName ?? string.Empty);
        }
    }

    private static void WriteLink(BinaryWriter writer, LinkDescriptor l)
    {
        writer.Write("L|");
        WriteString(writer, l.Name);
        WriteString(writer, l.TargetTypeName);
        WriteString(writer, l.Cardinality.ToString());
        writer.Write("|EDGE|");
        foreach (var ep in l.EdgeProperties.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            WriteString(writer, ep.Name);
            WriteString(writer, ep.Kind.ToString());
        }
    }

    private static void WriteEvent(BinaryWriter writer, EventDescriptor e)
    {
        writer.Write("E|");
        WriteString(writer, e.EventType.FullName ?? string.Empty);
        WriteString(writer, e.Severity.ToString());
        writer.Write("|ML|");
        foreach (var ml in e.MaterializedLinks.OrderBy(x => x, StringComparer.Ordinal))
        {
            WriteString(writer, ml);
        }

        writer.Write("|UP|");
        foreach (var up in e.UpdatedProperties.OrderBy(x => x, StringComparer.Ordinal))
        {
            WriteString(writer, up);
        }
    }

    private static void WriteLifecycle(BinaryWriter writer, LifecycleDescriptor lc)
    {
        writer.Write("LC|");
        WriteString(writer, lc.PropertyName);
        WriteString(writer, lc.StateEnumTypeName);

        writer.Write("|STATES|");
        foreach (var s in lc.States.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            WriteString(writer, s.Name);
            writer.Write(s.IsInitial);
            writer.Write(s.IsTerminal);
        }

        writer.Write("|TRANS|");
        foreach (var t in lc.Transitions
                              .OrderBy(t => t.FromState, StringComparer.Ordinal)
                              .ThenBy(t => t.ToState, StringComparer.Ordinal)
                              .ThenBy(t => t.TriggerActionName ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(t => t.TriggerEventTypeName ?? string.Empty, StringComparer.Ordinal))
        {
            WriteString(writer, t.FromState);
            WriteString(writer, t.ToState);
            WriteString(writer, t.TriggerActionName ?? string.Empty);
            WriteString(writer, t.TriggerEventTypeName ?? string.Empty);
        }
    }

    private static void WriteInterfaces(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("INTERFACES|");
        foreach (var i in graph.Interfaces.OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            writer.Write("I|");
            WriteString(writer, i.Name);
            writer.Write("|PROPS|");
            foreach (var p in i.Properties.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                WriteString(writer, p.Name);
                WriteString(writer, p.Kind.ToString());
                WriteString(writer, p.PropertyType.FullName ?? string.Empty);
            }

            // Interfaces can declare actions (InterfaceActionDescriptor); these
            // are part of the schema agents reason about and must influence the
            // hash. Free-form Description on each action is excluded.
            writer.Write("|IACTIONS|");
            foreach (var a in i.Actions.OrderBy(a => a.Name, StringComparer.Ordinal))
            {
                WriteString(writer, a.Name);
                WriteString(writer, a.AcceptsTypeName ?? string.Empty);
                WriteString(writer, a.ReturnsTypeName ?? string.Empty);
            }
        }

        writer.Write("|END_INTERFACES");
    }

    private static void WriteCrossDomainLinks(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("XDL|");
        // Tie-breaker chain covers every structurally-significant field of
        // ResolvedCrossDomainLink so that two collections with the same logical
        // content always sort identically regardless of registration order.
        // CodeRabbit PR #49 Major: an incomplete sort key falls back to input
        // order for ties, which is not a stable canonicalization across builders.
        foreach (var x in graph.CrossDomainLinks
                              .OrderBy(x => x.SourceDomain, StringComparer.Ordinal)
                              .ThenBy(x => x.SourceObjectType.Name, StringComparer.Ordinal)
                              .ThenBy(x => x.Name, StringComparer.Ordinal)
                              .ThenBy(x => x.TargetDomain, StringComparer.Ordinal)
                              .ThenBy(x => x.TargetObjectType.Name, StringComparer.Ordinal)
                              .ThenBy(x => x.Cardinality.ToString(), StringComparer.Ordinal))
        {
            writer.Write("X|");
            WriteString(writer, x.SourceDomain);
            WriteString(writer, x.SourceObjectType.Name);
            WriteString(writer, x.Name);
            WriteString(writer, x.TargetDomain);
            WriteString(writer, x.TargetObjectType.Name);
            WriteString(writer, x.Cardinality.ToString());
            writer.Write("|EDGE|");
            foreach (var ep in x.EdgeProperties.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                WriteString(writer, ep.Name);
                WriteString(writer, ep.Kind.ToString());
            }
        }

        writer.Write("|END_XDL");
    }

    private static void WriteWorkflowChains(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("WF|");
        // Tie-breaker chain covers every structurally-significant field of
        // WorkflowChain (WorkflowName / ConsumedType / ProducedType) so chains
        // sharing a workflow name still sort deterministically. CodeRabbit
        // PR #49 Major: WorkflowName uniqueness is not enforced by the
        // descriptor, so the tie-breaker is required for canonicalization.
        foreach (var w in graph.WorkflowChains
                              .OrderBy(w => w.WorkflowName, StringComparer.Ordinal)
                              .ThenBy(w => w.ConsumedType.ClrType?.FullName ?? w.ConsumedType.SymbolKey ?? w.ConsumedType.Name, StringComparer.Ordinal)
                              .ThenBy(w => w.ProducedType.ClrType?.FullName ?? w.ProducedType.SymbolKey ?? w.ProducedType.Name, StringComparer.Ordinal))
        {
            writer.Write("W|");
            WriteString(writer, w.WorkflowName);
            WriteString(writer, w.ConsumedType.ClrType?.FullName ?? w.ConsumedType.SymbolKey ?? w.ConsumedType.Name);
            WriteString(writer, w.ProducedType.ClrType?.FullName ?? w.ProducedType.SymbolKey ?? w.ProducedType.Name);
        }

        writer.Write("|END_WF");
    }

    private static void WriteString(BinaryWriter writer, string s)
    {
        // Length-prefixed UTF-8 framing prevents accidental cross-field
        // collisions where adjacent fields could otherwise concatenate to the
        // same byte sequence under different splits.
        var bytes = Encoding.UTF8.GetBytes(s);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}

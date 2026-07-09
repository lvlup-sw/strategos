namespace Strategos.Ontology.MCP;

/// <summary>
/// The default no-op <see cref="IOntologyAuditSink"/>: it drops every emitted
/// <see cref="OntologyAbstainedRecord"/>. This is what
/// <see cref="OntologyAnswerComposer"/> uses when no sink is supplied, keeping existing
/// consumers of the parameterless composer source- and behavior-compatible (an
/// abstention is still produced; nothing is audited).
/// </summary>
public sealed class NoOpOntologyAuditSink : IOntologyAuditSink
{
    private NoOpOntologyAuditSink()
    {
    }

    /// <summary>The shared no-op instance used as the composer's default sink.</summary>
    public static NoOpOntologyAuditSink Instance { get; } = new();

    /// <inheritdoc />
    public void RecordAbstention(OntologyAbstainedRecord record)
    {
        // Intentionally no-op: the default sink audits nothing.
    }
}

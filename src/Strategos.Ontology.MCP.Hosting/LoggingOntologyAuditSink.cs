using Microsoft.Extensions.Logging;

using Strategos.Ontology.MCP;

namespace Strategos.Ontology.MCP.Hosting;

/// <summary>
/// The concrete <see cref="IOntologyAuditSink"/> a host wires (DR-17, emission half): it
/// writes each recorded abstention to an <see cref="ILogger"/> as a structured audit
/// entry. Only the nearest-records COUNT and the <c>ontology.abstained</c> event type are
/// logged — never the abstention's <see cref="RecordRef"/> monikers — so no record identity
/// is exfiltrated through the audit log.
/// </summary>
/// <remarks>
/// Lives in the Hosting bridge (not the SDK-agnostic core) and is wired via
/// <see cref="OntologyServerToolFactory.CreateAnswerComposer"/>. The core's default stays
/// the no-op <see cref="NoOpOntologyAuditSink"/>, so existing consumers are unaffected.
/// </remarks>
internal sealed class LoggingOntologyAuditSink : IOntologyAuditSink
{
    private readonly ILogger _logger;

    internal LoggingOntologyAuditSink(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public void RecordAbstention(OntologyAbstainedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _logger.LogInformation(
            "Ontology answering surface abstained ({EventType}); nearest records searched: {NearestRecordsCount}.",
            record.Type,
            record.NearestRecordsCount);
    }
}

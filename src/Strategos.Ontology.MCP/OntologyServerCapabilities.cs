namespace Strategos.Ontology.MCP;

/// <summary>
/// Initialize-time capability descriptor surfaced by an MCP server hosting the
/// ontology tools. Carries the graph version so downstream clients (Basileus
/// AgentHost, Exarchos schema cache) can populate <c>capabilities._meta.ontologyVersion</c>
/// in their MCP <c>initialize</c> response without poking at the graph directly.
/// </summary>
/// <param name="OntologyVersion">
/// Wire-format version identifier (sha256:<hex>) produced by
/// <see cref="ResponseMeta.WireFormat"/>.
/// </param>
public sealed record OntologyServerCapabilities(string OntologyVersion);

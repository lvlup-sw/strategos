namespace Strategos.Ontology.Provenance;

/// <summary>
/// A W3C PROV-DM <b>Entity</b> (DR-16, T23, #126): a thing whose provenance is
/// being described. A reified association IS modeled as a PROV Entity — the
/// qualified-influence node attaches to it.
/// </summary>
/// <remarks>
/// INV-6 (sealed) / INV-7 (immutable) / INV-8 (identity by id, never a CLR type):
/// a sealed positional record addressed by its opaque string id.
/// </remarks>
/// <param name="Id">The entity's identifier (e.g. the association's business id).</param>
public sealed record ProvEntity(string Id);

/// <summary>
/// A W3C PROV-DM <b>Activity</b> (DR-16, T23, #126): something that occurs over a
/// period and acts upon or with entities — here, the workflow step that generated
/// an association assertion.
/// </summary>
/// <remarks>INV-6 / INV-7 / INV-8 — sealed, immutable, id-addressed.</remarks>
/// <param name="Id">The activity's identifier (e.g. a workflow step id).</param>
public sealed record ProvActivity(string Id);

/// <summary>
/// A W3C PROV-DM <b>Agent</b> (DR-16, T23, #126): something bearing responsibility
/// for an activity or entity. Sourced from the G1 <c>CurrentAgentIdentity</c>
/// seam (its opaque header-safe value), never invented.
/// </summary>
/// <remarks>INV-6 / INV-7 / INV-8 — sealed, immutable, id-addressed.</remarks>
/// <param name="Id">The agent's identifier (the <c>CurrentAgentIdentity</c> value).</param>
public sealed record ProvAgent(string Id);

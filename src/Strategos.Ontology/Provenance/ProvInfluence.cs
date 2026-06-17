namespace Strategos.Ontology.Provenance;

/// <summary>
/// One W3C PROV-DM <b>qualified influence</b> edge (DR-16, T23, #126): a
/// directed, typed relation (<see cref="Relation"/>) from a subject id to an
/// object id. The reified association is the qualified-influence NODE; these are
/// the relations radiating from it (the entity ↔ activity ↔ agent triangle of
/// PROV-DM core).
/// </summary>
/// <remarks>
/// INV-6 (sealed) / INV-7 (immutable) / INV-8 (id-addressed): a sealed positional
/// record. Endpoints are PROV node ids (strings), never CLR types.
/// </remarks>
/// <param name="Subject">The id of the relation's subject (the influenced).</param>
/// <param name="Relation">The PROV-DM core relation type.</param>
/// <param name="Object">The id of the relation's object (the influencer).</param>
public sealed record ProvInfluence(string Subject, ProvRelation Relation, string Object);

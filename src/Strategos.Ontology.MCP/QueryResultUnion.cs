using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Discriminated base for the shapes <c>OntologyQueryTool</c> can return.
/// MCP clients dispatch on <c>resultKind</c> to pick the right schema branch
/// (<c>"filter"</c> for <see cref="QueryResult"/>, <c>"semantic"</c> for
/// <see cref="SemanticQueryResult"/>, <c>"association"</c> for the DR-15
/// edge/association view <see cref="AssociationQueryResult"/>).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "resultKind")]
[JsonDerivedType(typeof(QueryResult), "filter")]
[JsonDerivedType(typeof(SemanticQueryResult), "semantic")]
[JsonDerivedType(typeof(AssociationQueryResult), "association")]
public abstract record QueryResultUnion;

using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Discriminated base for the two shapes <c>OntologyQueryTool</c> can return.
/// MCP clients dispatch on <c>resultKind</c> to pick the right schema branch
/// (<c>"filter"</c> for <see cref="QueryResult"/>, <c>"semantic"</c> for
/// <see cref="SemanticQueryResult"/>).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "resultKind")]
[JsonDerivedType(typeof(QueryResult), "filter")]
[JsonDerivedType(typeof(SemanticQueryResult), "semantic")]
public abstract record QueryResultUnion;

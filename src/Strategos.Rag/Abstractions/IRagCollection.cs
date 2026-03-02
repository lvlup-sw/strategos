// =============================================================================
// <copyright file="IRagCollection.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Rag;

/// <summary>
/// Marker interface for RAG collection types.
/// Implement this interface on a class to identify it as a RAG collection
/// that can be used with <see cref="IVectorSearchAdapter{TCollection}"/>.
/// </summary>
[Obsolete("Use ontology Object Types via AddOntology(). See Strategos.Ontology package.", false)]
public interface IRagCollection
{
}

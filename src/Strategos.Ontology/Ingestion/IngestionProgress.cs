namespace Strategos.Ontology.Ingestion;

/// <summary>
/// Reports progress during ingestion pipeline execution.
/// </summary>
/// <param name="ChunksProcessed">Number of chunks processed so far.</param>
/// <param name="TotalChunks">Total number of chunks to process.</param>
/// <param name="Phase">Descriptive label for the current phase (e.g., "Chunking", "Embedding", "Storing").</param>
public sealed record IngestionProgress(int ChunksProcessed, int TotalChunks, string Phase);

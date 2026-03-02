namespace Strategos.Ontology.Ingestion;

/// <summary>
/// The result of executing an ingestion pipeline.
/// </summary>
/// <param name="ChunksProcessed">The total number of chunks processed.</param>
/// <param name="ItemsStored">The total number of items stored via the writer.</param>
/// <param name="Duration">The elapsed time for the pipeline execution.</param>
public sealed record IngestionResult(int ChunksProcessed, int ItemsStored, TimeSpan Duration);

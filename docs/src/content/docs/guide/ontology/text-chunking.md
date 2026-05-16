---
title: Text Chunking
description: Split documents into embedding-sized chunks before ingestion.
sidebar:
  order: 3
---

Embedding models accept inputs up to a fixed token limit, and retrieval quality drops when chunks are too large or split mid-thought. The chunking primitives in `Strategos.Ontology.Chunking` give you three built-in strategies plus an `ITextChunker` seam to plug in your own. Pair a chunker with `IngestionPipeline<T>` and the chunks flow straight into embeddings and storage.

## The contract

`ITextChunker` exposes a single method:

```csharp
public interface ITextChunker
{
    IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions? options = null);
}
```

A `TextChunk` is a `readonly record struct` carrying `Content`, the zero-based `Index`, and the `StartOffset` / `EndOffset` byte positions into the original text. The offsets survive every transformation, so downstream code can render highlights, store citations, or stitch chunks back into the source document without re-tokenizing.

`ChunkOptions` controls two knobs:

```csharp
public sealed record ChunkOptions
{
    public int MaxTokens { get; init; } = 512;
    public int OverlapTokens { get; init; } = 64;
}
```

`MaxTokens` caps the size of each chunk; the chunker uses a heuristic ratio of `1 token ≈ 0.75 words` to translate that into a word budget. `OverlapTokens` is how much trailing content from one chunk reappears at the start of the next — a small overlap preserves context across boundaries so a query that hits the seam still matches.

## Built-in strategies

`SentenceBoundaryChunker` is the default. It splits at sentence terminators (`.`, `!`, `?` followed by whitespace), packs sentences into chunks until adding the next would exceed `MaxTokens`, then applies overlap by replaying trailing sentences. When a single sentence exceeds the cap, it falls back to word-boundary splitting for that sentence only. Use it for prose, transcripts, and any text where sentence integrity matters.

`ParagraphChunker` splits on double newlines first, then delegates to `SentenceBoundaryChunker` for any paragraph that exceeds the cap. Use it for structured documents — markdown, code documentation, knowledge-base articles — where paragraph boundaries already signal topic shifts.

`FixedSizeChunker` splits strictly by word count with overlap. It ignores grammatical boundaries, so it is fast and predictable but can leave a chunk mid-sentence. Use it for content where structure is unreliable or doesn't exist — log dumps, OCR output, machine-generated transcripts.

All three return chunks with stable offsets. Empty input returns an empty list.

## Plugging into the ingestion pipeline

`IngestionPipeline<T>.Create()` chains a chunker, an embedding provider, a mapper, and a writer:

```csharp
using Strategos.Ontology.Chunking;
using Strategos.Ontology.Ingestion;
using Strategos.Ontology.ObjectSets;

public sealed record DocumentChunk : ISearchable
{
    public Guid Id { get; init; }
    public string Content { get; init; } = "";
    public float[] Embedding { get; init; } = [];
    public int SourceOffset { get; init; }
}

var result = await IngestionPipeline<DocumentChunk>.Create()
    .Chunk(new ParagraphChunker())
    .Embed(embeddingProvider)
    .Map((chunk, embedding) => new DocumentChunk
    {
        Id = Guid.NewGuid(),
        Content = chunk.Content,
        Embedding = embedding,
        SourceOffset = chunk.StartOffset,
    })
    .WriteTo(objectSetWriter)
    .Build()
    .ExecuteAsync(inputTexts);
```

The builder also accepts a `ChunkOptions`-only overload — `Chunk(ChunkOptions options)` selects `SentenceBoundaryChunker` and applies the supplied caps in one call:

```csharp
.Chunk(new ChunkOptions { MaxTokens = 384, OverlapTokens = 48 })
```

The pipeline returns an `IngestionResult` with the number of chunks processed, items stored, and the total duration.

## Registering a custom chunker

`ITextChunker` is a plain interface — implement it on your own type and inject it wherever an `ITextChunker` is expected. For DI, register it as a singleton:

```csharp
public sealed class HeadingAwareChunker : ITextChunker
{
    public IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions? options = null)
    {
        // split on markdown headings, fall back to sentence chunker per section
        ...
    }
}

services.AddSingleton<ITextChunker, HeadingAwareChunker>();
```

`IngestionPipeline<T>.Create().Chunk(myCustomChunker)` accepts the instance directly — no factory or attribute wiring needed.

## Picking values for `MaxTokens` and `OverlapTokens`

The defaults (`512` / `64`) work for `text-embedding-3-small` at 1536 dimensions. Bring them down (256/32) when query latency matters more than recall. Bring them up (1024/128) when chunk boundaries cause you to miss relevant passages. Keep overlap at roughly 10–15% of `MaxTokens`.

## Where to go next

- [Similarity Search](/strategos/guide/ontology/similarity-search/) — query the chunks you just ingested.
- [Polyglot Descriptors](/strategos/guide/ontology/polyglot-descriptors/) — for ingestion from non-.NET sources.

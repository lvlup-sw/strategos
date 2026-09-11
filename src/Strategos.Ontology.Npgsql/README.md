# Strategos.Ontology.Npgsql

A PostgreSQL object-set provider for Strategos.Ontology, backed by pgvector. It runs ontology queries and traversals as SQL.

The in-memory provider and this provider answer the same queries. Move from one to the other without a change to the query code.

## Requirements

- PostgreSQL 13 or later.
- The `vector` extension, for similarity search.

## Installation

```bash
dotnet add package LevelUp.Strategos.Ontology.Npgsql
```

## Register the provider

```csharp
services.AddPgVectorObjectSets(options =>
{
    options.ConnectionString = configuration.GetConnectionString("Ontology")!;
    options.Schema = "ontology";
    options.AutoCreateSchema = true;
    options.IndexType = PgVectorIndexType.Hnsw;
});
```

The call registers three services: the provider, `IObjectSetProvider` and `IObjectSetWriter`.

## Options

| Option | Default | Purpose |
|---|---|---|
| `ConnectionString` | (none) | The PostgreSQL connection string. Required. |
| `Schema` | `public` | The schema that holds the ontology tables |
| `AutoCreateSchema` | `false` | Creates the schema and the tables at start-up |
| `IndexType` | `IvfFlat` | The vector index type. `Hnsw` is the other value. |
| `IterativeScan` | (none) | Iterative-scan settings for a filtered similarity query |

Set `AutoCreateSchema` in development only. In production, apply the schema through the migration process.

## Generated identifiers

PostgreSQL truncates an identifier at 63 bytes. A long descriptor name or role name can therefore collide with another one, or can name a table that does not exist.

This provider sends every table name, endpoint column and index name through one truncate-and-hash function. The data-definition statements and the data-manipulation statements therefore derive the same identifier from the same logical name. A name within the limit is unchanged.

## Related packages

- `LevelUp.Strategos.Ontology` — the descriptor model and the query surface.
- `LevelUp.Strategos.Ontology.Embeddings` — an embedding provider for similarity search.

# TypeSpec SDLC Contracts Pipeline

**Date:** 2026-04-18
**Status:** Spike validated, pending implementation
**Owner:** Strategos.Contracts companion package
**Tracking:** exarchos#1125, basileus#152

## Problem

SDLC event schemas are independently maintained in two languages across two repositories:
- **Exarchos:** 77 event types as Zod schemas in `servers/exarchos-mcp/src/event-store/schemas.ts`
- **Basileus:** 20+ C# records implementing `ISdlcEvent` in `shared/Basileus.Core/Events/Sdlc/`

This creates a DIM-3 (Contracts) divergent instances risk. The `ExarchosEventDto` in Basileus is a hand-written wire format DTO with `[JsonPropertyName]` annotations to match Exarchos's camelCase convention — a manual sync point that will drift.

## Solution

TypeSpec defines the canonical SDLC event schemas. A build pipeline emits language-specific types for both consumers.

```
TypeSpec (canonical)
    │
    ├──► JSON Schema (language-neutral intermediate)
    │       │
    │       ├──► json-schema-to-zod ──► Zod schemas (Exarchos)
    │       └──► (embedded in NuGet as content files)
    │
    └──► C# records (Strategos.Contracts NuGet, consumed by Basileus)
```

## Package Structure

```
strategos/
  src/
    LevelUp.Strategos/                    # existing core DSL
    LevelUp.Strategos.Contracts/          # NEW companion package
      Contracts.csproj
      typespec/
        main.tsp                          # canonical schema source
        tspconfig.yaml                    # emitter config
      generated/
        json-schema/                      # emitted JSON Schema files
        csharp/                           # emitted C# records
      build/
        generate.mjs                      # orchestrates tsp compile + post-processing
```

### NuGet Package Contents

```
LevelUp.Strategos.Contracts.nupkg
  ├── lib/net10.0/
  │     └── LevelUp.Strategos.Contracts.dll    # compiled C# records
  ├── contentFiles/any/any/schemas/
  │     ├── SdlcEventEnvelope.schema.json      # JSON Schema files
  │     ├── WorkflowStartedData.schema.json    # (available to consuming projects)
  │     └── ...
  └── LevelUp.Strategos.Contracts.nuspec
```

JSON Schema files ship as NuGet content files so Exarchos (or any other consumer) can extract them at build time without taking a runtime dependency on .NET.

## TypeSpec Model Design

### Base Envelope

```typespec
@jsonSchema
namespace LevelUp.Sdlc.Contracts;

model SdlcEventEnvelope {
  @minLength(1) @maxLength(100)
  streamId: string;
  @minValue(1)
  sequence: int32;             // int32, not int64 — safe for JSON
  timestamp: utcDateTime;
  @minLength(1)
  type: string;                // event type discriminator
  @maxLength(200)
  correlationId?: string;
  @maxLength(200)
  causationId?: string;
  @maxLength(200)
  agentId?: string;
  @maxLength(50)
  agentRole?: string;
  source?: EventSource;
  @maxLength(20)
  schemaVersion?: string;
  data?: Record<unknown>;
}

enum EventSource { exarchos, basileus }
```

### Event Data Models

Each event type's `data` payload is a separate model:

```typespec
model WorkflowStartedData {
  featureId: string;
  workflowType: WorkflowType;
  designPath?: string;
  synthesisPolicy?: "always" | "never" | "on-request";
}

model PhaseTransitionData {
  from: string;
  to: string;
  trigger: string;
  featureId: string;
}
```

### Naming Convention

- Envelope: `SdlcEventEnvelope`
- Data payloads: `{EventCategory}{Action}Data` (e.g., `TaskCompletedData`, `GateExecutedData`)
- Enums: PascalCase values matching the wire format
- TypeSpec `model` keyword conflict: use `@encodedName("application/json", "model")` for fields named `model`

## Zod Generation Pipeline (Exarchos)

### Step 1: Dereference $refs

The raw JSON Schema emitter produces `$ref` links between files. `json-schema-to-zod` doesn't resolve cross-file references. The pipeline must dereference first:

```javascript
import $RefParser from '@apidevtools/json-schema-ref-parser';

const dereferenced = await $RefParser.dereference(schema);
const zodCode = jsonSchemaToZod(dereferenced, { name, module: 'esm', type: true });
```

### Step 2: Generate Zod Schemas

One `.ts` file per model, plus a barrel `index.ts`. Generated files live in `servers/exarchos-mcp/src/event-store/generated/`.

### Step 3: Migration

Incremental migration from hand-written `schemas.ts`:
1. Generate Zod types alongside existing schemas
2. Add tests comparing generated vs hand-written (structural equality)
3. Switch imports one event type at a time
4. Remove hand-written schemas when fully migrated

### Step 4: CI Guard

```yaml
# .github/workflows/schema-sync.yml
- name: Regenerate Zod from JSON Schema
  run: npm run generate:contracts
- name: Verify no drift
  run: git diff --exit-code servers/exarchos-mcp/src/event-store/generated/
```

## C# Generation Pipeline (Strategos.Contracts)

### Option A: TypeSpec C# Emitter

Use `@typespec/http-server-csharp` to emit C# types directly. Requires post-processing to produce clean `record` types (the emitter generates controller scaffolding by default).

### Option B: JSON Schema → NJsonSchema

Use NJsonSchema to generate C# classes from the emitted JSON Schema. More control over output shape, but another tool in the chain.

### Option C: Hand-Written C# Validated Against Schema

Write C# records by hand (current approach in Basileus.Core), but validate them against the JSON Schema in CI tests. Lowest tooling overhead, highest manual sync risk.

**Recommendation:** Start with Option C (lowest friction, validates correctness), migrate to Option A when the TypeSpec C# emitter stabilizes for pure data models.

## Spike Results

Validated in `spikes/typespec-contracts/`:

| Metric | Result |
|--------|--------|
| TypeSpec compilation | 7ms for 26 models |
| JSON Schema quality | Clean `$ref`, constraints, required fields, descriptions |
| Zod generation | 26/26 models generated successfully |
| Enum handling | Correct (`z.enum([...])`) |
| Nested types | `$ref` chains work (need dereferencing for Zod) |

### Known Issues

| Issue | Severity | Mitigation |
|-------|----------|------------|
| `$ref` not resolved by json-schema-to-zod | Medium | Dereference with `@apidevtools/json-schema-ref-parser` before generation |
| `int64` → `string` in JSON Schema | Low | Use `int32` for sequence (safe for local event stores; Marten handles internally) |
| `model` reserved in TypeSpec | Low | `@encodedName` annotation |
| Type naming (`FooSchema` for both schema and type) | Low | Post-process naming in generator script |

## Migration Strategy

### Phase 1: Establish Canonical Source
- Create TypeSpec models covering all 77 Exarchos + 20 Basileus event types
- Validate generated JSON Schema matches existing schemas (structural comparison)
- Ship `Strategos.Contracts` v0.1.0

### Phase 2: Exarchos Migration
- Generate Zod types from Strategos.Contracts JSON Schema artifacts
- Run existing tests against generated types (compatibility gate)
- Incrementally switch imports from `schemas.ts` to `generated/`

### Phase 3: Basileus Migration
- Add `Strategos.Contracts` NuGet dependency to `Basileus.Core`
- Replace `ISdlcEvent` implementations with types from contracts package
- Deprecate `ExarchosEventDto` (replaced by shared envelope)

### Phase 4: CI Integration
- Schema version bumps in Strategos.Contracts trigger downstream CI in both repos
- Breaking change detection via JSON Schema compatibility checks
- Generated code must be committed (no runtime codegen)

## Related

- [Strategic Framing](2026-04-18-strategic-framing-exarchos-basileus.md) — product boundaries and contract ownership
- exarchos#1109 — Cross-cutting constraints
- basileus#120 — Remote Event Types & Schema Mapping
- Spike: `spikes/typespec-contracts/`

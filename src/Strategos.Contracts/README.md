# LevelUp.Strategos.Contracts

Cross-product schema substrate. **TypeSpec is the single canonical source.** The
build emits two artifacts from it:

- **JSON Schema** (`schemas/json-schema/*.json`) — language-neutral, embedded as
  NuGet content (`contentFiles/any/any/schemas/`). Exarchos derives Zod from it.
- **C# records** (`Generated/*.g.cs`) — compiled into this DLL. Basileus
  references the DLL.

```
main.tsp  (canonical)
   │ tsp compile  (@typespec/json-schema)
   ▼
schemas/json-schema/*.json
   │ Strategos.Contracts.Codegen  (NJsonSchema parse + INV-6/7 template)
   ▼
Generated/*.g.cs   →   compiled into LevelUp.Strategos.Contracts.dll
```

Regenerate with `scripts/contracts-codegen.sh`. The emitted `schemas/` and
`Generated/` are **emitter-owned**: CI's codegen-guard (`.github/workflows/
contracts-codegen-guard.yml`) regenerates and fails on any hand-edit (DIM-6).

## C# emitter decision (T3 — INV-6 / INV-7 gate)

The generated records are consumer-facing contracts and must satisfy:

- **INV-6** — `sealed record`
- **INV-7** — every property `{ get; init; }`; collections as `IReadOnlyList<T>`

**Chosen path: NJsonSchema-backed custom template (the documented fallback), not
the native TypeSpec C# emitter.**

### Why not the native TypeSpec C# emitter

The only first-party TypeSpec C# emitter is `@typespec/http-client-csharp` (and
the Azure `@azure-tools/typespec-csharp`). Both are **HTTP service-client
generators**:

- They have hard peer dependencies on `@typespec/http` and
  `@azure-tools/typespec-client-generator-core` and expect operation/route
  definitions — they do not target plain `@jsonSchema` data models.
- Their model output is **mutable classes** (`public partial class` with
  `{ get; set; }`) plus client plumbing (pipelines, serialization helpers). They
  do not emit `sealed record` + `{ get; init; }` + `IReadOnlyList<T>`, and there
  is no configuration switch that produces that shape.

Shipping their default output would silently violate INV-6/INV-7 — exactly the
failure the decision gate exists to prevent.

### Why NJsonSchema + a custom template

NJsonSchema's own `CSharpClassStyle.Record` was also evaluated and rejected: in
11.6.1 it emits a `partial class` (not the `record` keyword), uses get-only
constructor-assigned properties (not `init` accessors), and adds a **mutable**
`AdditionalProperties` dictionary with a public setter — which violates the
init-only requirement.

So NJsonSchema is used **only as the parser / `$ref` resolver** (its robust
`JsonSchema` model), and `Strategos.Contracts.Codegen/RecordEmitter.cs` owns the
emitted shape with a template tuned to exactly `sealed record` + `{ get; init; }`
+ `IReadOnlyList<T>`. The shape is pinned by
`EmitterShapeTests.GeneratedRecord_IsSealed_InitOnly_ReadOnlyCollections`
(reflection over a generated record); if a future change regresses the shape,
that test goes red before anything ships.

**Implication for family tasks (T6+):** records are emitted by the in-repo
`Strategos.Contracts.Codegen` tool from JSON Schema, not by a TypeSpec emitter
plugin. New `.tsp` models flow through `scripts/contracts-codegen.sh`
automatically; no per-family emitter wiring is needed, but any new wire-name
encoding (e.g. `@encodedName` kebab-case for #98) must round-trip through the
`JsonPropertyName` the template emits.

### Emitter capabilities (extended in Family 1 / #36, Family 2 / #50, Family 3 / #98)

The emitter reads the raw JSON Schema directly (NJsonSchema is no longer on the
emit path) and classifies each document as enum / record / open-object:

- **String enums → C# `enum`.** Each member carries
  `[JsonStringEnumMemberName("<wire>")]` when the C# name diverges from the wire
  value (kebab/snake/camel/reserved-word), and the enum type carries
  `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` so it serializes to its
  **string** wire value — never a numeric ordinal — for Basileus and Zod. This
  is the `@encodedName` round-trip path #98 (kebab-case wire names) inherits.
- **`$ref` resolution.** A property `$ref`-ing another document resolves to the
  generated enum/record type (was `object`/`string`); enum refs are value types,
  record refs are reference types (so required record refs get the `= default!`
  INV-7 null-forgiving default).
- **Open objects** (`Record<unknown>`) are not emitted as standalone records;
  they surface as `object`-typed payload properties on their referrers.
- **Discriminated unions** (a top-level `anyOf` of `$ref` arms, the TypeSpec
  `union` form) → an `abstract record` base carrying
  `[JsonPolymorphic(TypeDiscriminatorPropertyName = "<discriminator>")]` and one
  `[JsonDerivedType]` per arm; each arm is a `sealed record` deriving from the
  base. The discriminator property is resolved **generically** — the const-pinned
  member shared by all arms, preferring `kind` — so the workflow-IR `kind` unions
  (#50) and the invariant `Enforcement` `mode` union (#98) both round-trip
  without hard-coding a single discriminator name.
- **Recursive types** (Family 3 / #98 `CheckNode`). A self-referential model (an
  arm whose `children`/`child` `$ref`s the union it belongs to) needs no special
  handling: `$ref` resolution is **by document name**, not by recursive descent,
  so a cycle resolves to the generated type name (`IReadOnlyList<CheckNode>` /
  `CheckNode`) without infinite recursion. The combinator tree is declarative-only
  (LB-1 / INV-4) — no arm admits an executable member — a guarantee asserted
  structurally by `SandboxGuaranteeTests`.

`scripts/contracts-codegen.sh` now prunes stale `schemas/json-schema/*.json`
before `tsp compile`, so a removed/renamed TypeSpec model does not linger as an
orphan schema/record (the json-schema emitter does not prune its own output) —
keeping the codegen-guard diff honest across P2/P3 renames.

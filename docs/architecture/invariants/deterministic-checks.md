# Deterministic Checks

Mechanical grep / structural patterns that this skill can run against the diff or working tree to surface candidate findings. These are starting points for human or agent reasoning, not verdicts. A pattern match is a *signal*, not a conclusion — confirm by reading context.

Coverage is limited to invariants where mechanical detection adds value. The remaining invariants are reasoning-driven; their checks live in the corresponding `INV-N-*.md` reference files.

## INV-1: Workflows → Wolverine + Marten via Roslyn SG

### Check 1.1: Hand-authored sagas outside the emitter

A class declaring `: Saga` (Wolverine's saga base) anywhere except `Strategos.Generators/Emitters/SagaEmitter.cs`'s emitted output is a violation — sagas are exclusively generated.

```bash
grep -rnE ':\s*Saga\b' src/Strategos/ src/Strategos.Infrastructure/ src/Strategos.Agents/ \
  --include='*.cs' \
  | grep -v 'src/Strategos.Generators/'
```

Expected: empty.

### Check 1.2: Core authoring project leaks a runtime dependency

`Strategos.csproj` (core authoring) should not directly reference Wolverine or Marten. Consumers receive those via emitted DI extensions.

```bash
grep -nE '<PackageReference\s+Include="(Wolverine|Marten)' \
  src/Strategos/Strategos.csproj
```

Expected: empty.

### Check 1.3: Ad-hoc saga storage in non-generator code

```bash
grep -rnE 'class\s+\w*Saga(Store|Repository|Persistence)\b' \
  src/Strategos/ src/Strategos.Infrastructure/ \
  --include='*.cs'
```

Expected: empty.

## INV-2: Ontology = analyzers + self-contained

### Check 2.1: Wolverine / Marten coupling in ontology projects

```bash
grep -rn 'Wolverine\|Marten' src/Strategos.Ontology*/ \
  --include='*.cs' --include='*.csproj'
```

Expected: zero matches. (Currently zero — keep it that way.)

### Check 2.2: Source generator added to the ontology generators project

The ontology generators project must remain analyzer-only.

```bash
grep -rnE 'IIncrementalGenerator|ISourceGenerator|\[Generator\b' \
  src/Strategos.Ontology.Generators/ \
  --include='*.cs'
```

Expected: zero matches.

### Check 2.3: Project role drift

```bash
grep -nE '<IsRoslynComponent>|<OutputItemType>|<ReferenceOutputAssembly>' \
  src/Strategos.Ontology.Generators/Strategos.Ontology.Generators.csproj
```

Expected: `IsRoslynComponent=true` with analyzer-style metadata. The presence of generator-specific MSBuild items (`<OutputItemType>Analyzer</OutputItemType>` combined with `[Generator]` attribute classes) flags drift.

## INV-3: MCP first-class, latest spec (2026-07-28)

### Check 3.1: Response records missing `_meta` envelope

Every public response record under `Strategos.Ontology.MCP` should expose `[JsonPropertyName("_meta")] ResponseMeta Meta`.

```bash
# Find response/result records that don't mention _meta:
grep -L '_meta' $(grep -rlE 'public\s+sealed\s+record\s+\w*(Result|Response)\b' \
  src/Strategos.Ontology.MCP/ --include='*.cs')
```

Expected: empty (every result file mentions `_meta`).

### Check 3.2: Tool descriptors missing `OutputSchema`

```bash
grep -rL 'OutputSchema' \
  $(grep -rlE 'OntologyToolDescriptor|McpToolDescriptor' \
    src/Strategos.Ontology.MCP/ --include='*.cs')
```

Expected: empty.

### Check 3.3: Deprecated protocol revision strings

```bash
grep -rn '2024-11-05\|2025-03-26\|2025-06-18\|2025-11-25' \
  src/Strategos.Ontology.MCP/ src/Strategos.Ontology.MCP.Hosting/ \
  src/Strategos.Agents.Mcp/ \
  --include='*.cs' --include='*.md'
```

Expected: zero hits. (Modern code targets `2026-07-28` only.) The scope covers
`src/Strategos.Agents.Mcp/` and `*.md` because the package README carries a
protocol pin and ships to the registry. Hosting is included so a pre-2026-07-28
response shape cannot be reintroduced on the SDK-bound `CallToolResult` bridge.

### Check 3.4: `CallToolResult` constructions omit `resultType`

Absent `resultType` is the pre-2026-07-28 response shape. Every
`new CallToolResult` (or `new()` inferred as one) in Hosting must assign
`ResultType`.

```bash
# Files that construct CallToolResult must mention ResultType:
grep -L 'ResultType' $(grep -rlE 'new CallToolResult|CallToolResult' \
  src/Strategos.Ontology.MCP.Hosting/ --include='*.cs')
```

Expected: empty.

### Check 3.5: Tool descriptor missing optional `Icons`

```bash
grep -L 'Icons' src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs
```

Expected: empty. The property is optional and must stay null when unset —
do not flag a missing placeholder icon as a gap.

## INV-4: Concrete workflow DSL nomenclature

### Check 4.1: Graph-theory terms in workflow DSL surface

Workflow DSL public surface must not contain `Graph`/`Node`/`Edge`/`Vertex` in code (excluding comments). Scoped to workflow DSL only — ontology DSL legitimately uses `Edge`/`Link` (domain terms).

```bash
grep -rinE '\b(graph|node|edge|vertex)\w*' \
  src/Strategos/Builders/ src/Strategos/Abstractions/ src/Strategos/Definitions/ \
  --include='*.cs' \
  | grep -vE '^[^:]+:[0-9]+:\s*(//|\*)'
```

Expected: empty.

## INV-5: Three-tiered validation, stable diagnostic IDs

### Check 5.1: Duplicate diagnostic IDs in the authoritative catalog

A duplicate ID is a contract violation — consumers suppress by ID. AGWF identities live in the generated catalog; AONT identities are still authored constants.

```bash
grep -rhoE '"(AGWF|AONT)[0-9]{3,}"' \
  src/Strategos.Contracts/Generated/AgwfCodes.g.cs \
  src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnosticIds.cs \
  | sort | uniq -d
```

Expected: empty.

### Check 5.1b: Hand-authored AGWF literals in production diagnostics

Workflow descriptors must consume `AgwfCodes.*`, not quote `AGWF0xx`. This scan is the production-literal gate; it is not the catalog inventory.

```bash
grep -rhoE '"(AGWF)[0-9]{3,}"' \
  src/Strategos.Generators/Diagnostics/ \
  --include='*.cs' \
  | sort | uniq -d
```

Expected: empty.

### Check 5.2: ID gaps (informational)

```bash
grep -rhoE '"(AGWF|AONT)[0-9]{3,}"' \
  src/Strategos.Contracts/Generated/AgwfCodes.g.cs \
  src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnosticIds.cs \
  | sort -u
```

Inspect output for gaps. Gaps are not violations themselves, but reusing one is. Retired IDs stay retired.

### Check 5.3: Removed IDs across releases

```bash
git diff <last-release-tag>..HEAD -- \
  'src/Strategos.Generators/Diagnostics/' \
  'src/Strategos.Ontology.Generators/Diagnostics/' \
  | grep -E '^-.*"(AGWF|AONT)[0-9]{3,}"'
```

Each removed ID is a back-compat break — must be gated on a major version bump.

## INV-6: Sealed-by-default

### Check 6.1: Public non-sealed concrete classes in DSL/descriptor namespaces

```bash
grep -rnE '^\s*public class \w' \
  src/Strategos/Builders/ src/Strategos/Definitions/ src/Strategos/Steps/ \
  src/Strategos.Ontology/Descriptors/ \
  --include='*.cs' \
  | grep -vE '\b(sealed|static|abstract|partial)\b'
```

Expected: empty, or only audited exceptions. Note: `partial` is permitted because the SG may emit the second part — when seen, verify the emitted half is `sealed`.

### Check 6.2: Public `virtual` members in sealed-by-default surfaces

`virtual` on a class that ends up sealed is dead code; on a non-sealed class it advertises an extension point that may not be audited.

```bash
grep -rnE '^\s*public virtual ' \
  src/Strategos/Builders/ src/Strategos/Definitions/ src/Strategos/Steps/ \
  src/Strategos.Ontology/Descriptors/ \
  --include='*.cs'
```

Expected: empty unless explicitly justified.

## INV-7: Immutable record state

### Check 7.1: Mutable setters in state types

Any `IWorkflowState` implementation should have `init;` properties, not `set;`.

```bash
grep -rlE ':\s*IWorkflowState\b' src/Strategos/ samples/ --include='*.cs' \
  | xargs grep -nE '\{\s*get;\s*set;\s*\}'
```

Expected: empty.

### Check 7.2: Mutable collection types in state properties

```bash
grep -rlE ':\s*IWorkflowState\b' src/Strategos/ samples/ --include='*.cs' \
  | xargs grep -nE '\b(List|Dictionary|HashSet)<'
```

Hits should use `ImmutableList<T>`, `ImmutableDictionary<K,V>`, or another truly immutable type. `IReadOnlyList<T>` is a view, not a substitute — only accept it when the backing collection is frozen after construction.

## INV-8: Polyglot identity (`ClrType` OR `SymbolKey`)

### Check 8.1: Unconditional `ClrType` dereference

```bash
grep -rnE '\bClrType\s*!?\s*\.\w' src/Strategos.Ontology/ --include='*.cs' \
  | grep -vE '(is\s+not\s+null|is\s+\{|\?\.)'
```

Each hit should have a preceding null check, pattern match, or null-conditional operator. If not, it's a polyglot violation.

### Check 8.2: `typeof(...)` reflection in ontology builder paths

```bash
grep -rnE '\btypeof\(' src/Strategos.Ontology/Builder/ --include='*.cs'
```

Each hit must be reachable only when `ClrType is not null` (or equivalently gated).

### Check 8.3: Test fixture polyglot coverage

```bash
grep -rn 'SymbolKey\s*=' src/Strategos.Ontology.Tests/ --include='*.cs'
```

Expected: at least one match. Zero means the polyglot path is unexercised by tests.

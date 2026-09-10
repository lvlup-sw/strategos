# INV-8: Polyglot identity — `ClrType` OR `SymbolKey`, both first-class

Ontology descriptors carry identity through one of two paths:

1. **`ClrType`** — for in-process CLR ingestion (most common today; reflection-based).
2. **`SymbolKey`** — a SCIP-style moniker for cross-language ingestion (Python, TypeScript, etc.), contributed via `IOntologySource`.

The schema requires at least one of the two to be non-null (enforced by `ObjectTypeDescriptor`). Both may be set; `SymbolKey` wins at merge. Builder and graph code must never *unconditionally* assume `ClrType` is present. `descriptor.ClrType.GetMethods()`, `clrType!.Name`, or `typeof(...)` reflection on a descriptor's CLR side silently break the polyglot path the moment an `IOntologySource` supplies a `SymbolKey`-only descriptor.

## Acceptance questions

- Does the proposal touch `ObjectTypeDescriptor`, `PropertyDescriptor`, `LinkDescriptor`, or anything that consumes them?
- Does it unconditionally dereference `descriptor.ClrType.*` or call `typeof(...)` without a `SymbolKey` fallback?
- If new ontology behavior depends on reflection, is there a parallel path that operates on `SymbolKey` — or, at minimum, an explicit "CLR-only" gate with a diagnostic surfaced to the caller?
- Do new tests include at least one descriptor with `ClrType = null, SymbolKey = <value>` to exercise the polyglot path?
- Are new public APIs over descriptors typed in terms that work for both — e.g., accepting an identity discriminator, not a `Type`?

## Repo-grounded checks

- `src/Strategos.Ontology/Descriptors/ObjectTypeDescriptor.cs:10-14, 69-80` — "DR-1 (polyglot descriptor schema): identity is no longer CLR-only"; enforces at least one of `ClrType` or `SymbolKey` (both may be set; `SymbolKey` wins at merge)
- `src/Strategos.Ontology/Sources/IOntologySource.cs:1-40` — extension point for non-CLR sources contributing `OntologyDelta`
- `src/Strategos.Ontology/Builder/IOntologyBuilder.cs:41,54` — `ObjectTypeFromDescriptor`, `ApplyDelta` are the polyglot entry points
- `src/Strategos.Ontology.MCP/OntologyStubGenerator.cs:6-25` — runtime emits Python `.pyi` stubs from the same graph

## Cross-cutting overlap

**axiom_overlap:** `DIM-3` (primary, Contracts — descriptor identity contract) · `DIM-1` (Topology — which sources feed the graph) · `DIM-6` (Architecture — extension surface for non-CLR sources)

## External grounding

- [SCIP — Source Code Intelligence Protocol](https://github.com/sourcegraph/scip) — `symbol` strings used to identify language constructs across languages; the model behind `SymbolKey`
- The future MCP serve path depends on polyglot identity to expose non-.NET ontologies to MCP clients; CLR-only assumptions today create migration debt later

## Severity guide

- **HIGH:** Code unconditionally dereferences `ClrType` (or uses `!` to suppress nullability) and is reachable from non-test consumers; new ontology test corpus lacks any `SymbolKey`-only descriptor.
- **MEDIUM:** A new feature is `ClrType`-only "for now" without a tracked plan for `SymbolKey` parity, or without a clear diagnostic when invoked with a non-CLR descriptor.
- **LOW:** Documentation describes ontology as ".NET descriptors" without mentioning polyglot sources. Reword.

## Worked example

**Violation:**

```csharp
public string GenerateLabel(ObjectTypeDescriptor descriptor)
{
    // Crashes when descriptor.ClrType is null (SymbolKey-only descriptor from IOntologySource)
    return descriptor.ClrType.GetCustomAttribute<DisplayAttribute>()?.Name
        ?? descriptor.ClrType.Name;
}
```

Unit tests using CLR types pass. The first time an `IOntologySource` produces a Python-originated descriptor, this throws `NullReferenceException` deep in the graph builder.

**Fix:**

```csharp
public string GenerateLabel(ObjectTypeDescriptor descriptor) =>
    descriptor switch
    {
        { ClrType: { } clr } =>
            clr.GetCustomAttribute<DisplayAttribute>()?.Name ?? clr.Name,

        { SymbolKey: { } key } =>
            LabelFromSymbolKey(key),

        _ => throw new InvalidOperationException(
            "Descriptor has neither ClrType nor SymbolKey — schema invariant violated upstream")
    };
```

Where reflection is genuinely required and a `SymbolKey` fallback is impractical, gate on `descriptor.ClrType is { } clrType` and surface a diagnostic (AONT*-tier ID) when invoked with a non-CLR descriptor — never silently no-op.

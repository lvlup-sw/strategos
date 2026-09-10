# INV-2: Ontology uses Roslyn analyzers (not generators) and is self-contained

The ontology subsystem (`Strategos.Ontology*`) is two things, and exactly two:

1. A runtime descriptor model — records like `ObjectTypeDescriptor`, `PropertyDescriptor`, `LinkDescriptor`.
2. Compile-time validation via a Roslyn **analyzer** (`OntologyDefinitionAnalyzer` reporting AONT001..AONT035), *not* a source generator.

It does not lower into Wolverine, Marten, or any other downstream library. It does not emit code via `IIncrementalGenerator` / `ISourceGenerator`. Adding either capability changes the contract consumers depend on (descriptor changes trigger source regeneration; a Wolverine/Marten dependency follows users who only want ontology validation).

> Note: `OntologyStubGenerator.cs` in `Strategos.Ontology.MCP` is a *runtime* stub emitter that writes `.pyi` files — not a Roslyn SG. The name "Generator" alone is not a violation; what matters is the role.

## Acceptance questions

- Does the proposal add a `PackageReference` to `Wolverine.*` or `Marten.*` in any `Strategos.Ontology*` project?
- Does it introduce a class implementing `IIncrementalGenerator` or `ISourceGenerator` under `Strategos.Ontology.Generators`?
- Does any `using` directive in `Strategos.Ontology*` import a Wolverine or Marten namespace?
- If the ontology needs to participate in a workflow, is the integration expressed at the workflow surface (one-way edge), not via ontology→workflow reverse coupling?

## Repo-grounded checks

- `src/Strategos.Ontology.Generators/Strategos.Ontology.Generators.csproj:8,13` — `IsRoslynComponent` with analyzer-only role; description: "Roslyn diagnostic analyzers... Validates... at compile time"
- `src/Strategos.Ontology.Generators/Analyzers/OntologyDefinitionAnalyzer.cs` — single analyzer driving all AONT reporting
- `src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnosticIds.cs:5-49` — stable, monotonic IDs
- `grep -rn 'Wolverine\|Marten' src/Strategos.Ontology*/` returns zero matches (current state)
- `src/Strategos.Ontology.MCP/OntologyStubGenerator.cs:11-25` — runtime stub emitter, not a Roslyn component (allowed)

## Cross-cutting overlap

**axiom_overlap:** `DIM-6` (primary, Architecture — dependency direction) · `DIM-3` (Contracts — analyzer diagnostics are a compile-time contract)

## External grounding

- [Roslyn analyzers vs source generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) — analyzers report diagnostics; generators emit code. Distinct roles, distinct consumer contracts.
- The polyglot identity story (see [INV-8](INV-8-polyglot-identity.md)) explicitly avoids any path that would require Wolverine or Marten in the ontology stack.

## Severity guide

- **HIGH:** A Wolverine/Marten reference is introduced in any `Strategos.Ontology*` project; an `IIncrementalGenerator` or `ISourceGenerator` is added under `Strategos.Ontology.Generators`.
- **MEDIUM:** A new ontology project takes a transitive Wolverine/Marten dependency via a third package; an analyzer is renamed or its ID range shifts in a way that breaks consumer suppression configs.
- **LOW:** Documentation calls the analyzer a "generator" colloquially. Reword.

## Worked example

**Violation A — coupling:**

```xml
<!-- src/Strategos.Ontology/Strategos.Ontology.csproj -->
<ItemGroup>
  <PackageReference Include="Marten" Version="*" />  <!-- couples ontology to event store -->
</ItemGroup>
```

**Violation B — role drift:**

```csharp
// src/Strategos.Ontology.Generators/OntologyClientGenerator.cs
[Generator(LanguageNames.CSharp)]
public sealed class OntologyClientGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) { /* emits .cs */ }
}
```

This converts the analyzer project into a generator project — consumers' builds now produce new generated source whenever a descriptor changes, which they did not opt into.

**Fix:**

If polyglot client emission is needed, do it at runtime via `OntologyStubGenerator`-style emitters that consumers invoke explicitly. If event-sourced ontology storage is needed, the descriptor model produces *inputs to the workflow surface* — the workflow surface is the only place Wolverine and Marten live.

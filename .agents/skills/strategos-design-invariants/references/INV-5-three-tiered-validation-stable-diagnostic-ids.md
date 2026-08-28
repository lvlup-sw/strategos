# INV-5: Three-tiered validation with stable diagnostic IDs

Validation happens at three tiers, and each error gets a stable, monotonically-assigned diagnostic ID:

1. **Builder-runtime** — fluent calls throw `InvalidOperationException` / `ArgumentException` for misuse caught at build time (`WorkflowBuilder.cs:59,91,140,245,310,388`).
2. **Roslyn analyzer** — compile-time diagnostics via `WorkflowDiagnostics.cs` (AGWF catalog through AGWF036, identities in `AgwfCodes.g.cs`) and `OntologyDefinitionAnalyzer.cs` (AONT001..035).
3. **Emitter-time** — source generator guards before emission.

Diagnostic IDs are part of the *public contract*. Consumers suppress specific IDs via `<NoWarn>`, `.editorconfig`, or `#pragma warning disable`. IDs must never be reused, renumbered, or silently removed within a non-major release.

Validation prefers the earliest tier that can catch the error: analyzer over emitter, emitter over runtime throw.

## Acceptance questions

- Does the proposal add a new validation case? If yes, is a new `AGWF*` or `AONT*` ID assigned (next unused number, not reusing a retired ID)?
- Does it remove or renumber an existing diagnostic ID? If yes, is the change gated on a major version bump?
- If the validation runs at a *fourth* tier (e.g., a startup-time check, a separate validator service), is that justified? Could it move into one of the three existing tiers?
- For builder-runtime throws: is the exception type and message stable enough that downstream tests can rely on it?
- Could a runtime-tier check be promoted to analyzer-tier (caught at compile, not at build call)?

## Repo-grounded checks

- `src/Strategos/Builders/WorkflowBuilder.cs:59,91,140,245,310,388` — fluent throws on misuse
- `src/Strategos.Contracts/Generated/AgwfCodes.g.cs` — AGWF identities through AGWF036 (TypeSpec-canonical; descriptors in `WorkflowDiagnostics.cs` consume these constants)
- `src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs` — AGWF descriptors with severity; IDs are not hand-authored literals
- `src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnosticIds.cs:5-49` — AONT001..035 covering core, preconditions, lifecycle, derivation, interface actions, extension points
- `src/Strategos.Ontology.Generators/Analyzers/OntologyDefinitionAnalyzer.cs` — single analyzer driving all AONT reporting

## Cross-cutting overlap

**axiom_overlap:** `DIM-7` (primary, Resilience — graceful failure paths) · `DIM-3` (Contracts — diagnostic IDs are consumer-facing) · `DIM-2` (Observability — diagnostics are observable signals at build time)

## External grounding

- [Roslyn diagnostic IDs convention](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) — prefixed, numeric, stable across versions
- StyleCop, FxCop, and the broader analyzer ecosystem set the consumer expectation that diagnostic IDs are durable across non-major releases

## Severity guide

- **HIGH:** A validation case ships without a diagnostic ID; an existing ID is reused for a different semantic; an ID is removed in a non-major release.
- **MEDIUM:** A new validation tier is added without justification for why it doesn't live in one of the three existing tiers; a runtime throw lands where an analyzer diagnostic could have caught the same case.
- **LOW:** A diagnostic message string is reworded without justification (consumers may match on substrings in CI logs).

## Worked example

**Violation:**

```csharp
// src/Strategos/Builders/WorkflowBuilder.cs
public IWorkflowBuilder<TState> Then(string name, Func<TState, Task<TState>> step)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new InvalidOperationException("Step name must be non-empty.");
    // No AGWF*/AONT* ID — consumers can't suppress this, tooling can't enumerate validation cases,
    // and this could have been caught at compile time by the analyzer.
}
```

**Fix:**

1. Assign the next unused diagnostic ID (e.g., `AGWF011`) in `WorkflowDiagnostics.cs`:

   ```csharp
   public static readonly DiagnosticDescriptor EmptyStepName = new(
       id: "AGWF011",
       title: "Workflow step name must be non-empty",
       messageFormat: "The 'Then' step name is empty or whitespace",
       category: "Strategos.Workflow",
       defaultSeverity: DiagnosticSeverity.Error,
       isEnabledByDefault: true);
   ```

2. Report it from the analyzer (compile-time) — earlier tier wins. If the value is dynamic (computed at build call), keep the throw *and* mirror it as `AGWF011` so consumers have a stable handle.

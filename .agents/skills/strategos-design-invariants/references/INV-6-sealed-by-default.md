# INV-6: Sealed-by-default for DSL and descriptor types

DSL types (`Strategos/Builders/`, `Strategos/Definitions/`, `Strategos/Steps/`) and descriptor types (`Strategos.Ontology/Descriptors/`) are `sealed record` or `sealed class` by default. The source generator targets *concrete* types when emitting Wolverine sagas and Marten event stores; a subclass with overridden behavior would be bypassed at the generated-code boundary, causing silent runtime divergence from author intent.

Extension is expressed through composition — interfaces, fluent extension methods, `IOntologySource` — not subclassing.

## Acceptance questions

- Does the proposal add a `public class` (non-`sealed`, non-`static`, non-`abstract`-by-design) under `Strategos/Builders/`, `Strategos/Definitions/`, `Strategos/Steps/`, or `Strategos.Ontology/Descriptors/`?
- If so, is there a documented reason it must be subclassable, *and* has the SG path that consumes that type been audited to make sure overrides won't be silently bypassed?
- Could the extension point be exposed as an interface or a fluent extension method instead?
- For ontology extensibility: does the new surface use `IOntologySource` (the documented extension point) rather than inheritance?
- Are any `public virtual` members being introduced? On a sealed class they are dead; on a non-sealed class they advertise an extension point that demands an audit.

## Repo-grounded checks

- `~40` `public sealed record` / `internal sealed class` / `public sealed class` declarations in `src/Strategos/{Builders,Definitions,Steps}/` and `src/Strategos.Ontology/Descriptors/`
- Exactly `1` `public abstract record` in scope (audited and intentional)
- `src/Strategos/Builders/Workflow.cs:25` — entry point is `static class Workflow<TState>`, not extendable
- `src/Strategos/Steps/StepResult.cs:26` — `public sealed record StepResult<TState>`
- `src/Strategos.Ontology/Descriptors/` — descriptor types in the sealed-by-default scan (not `OntologyStubGenerator`, which is MCP hosting)

## Cross-cutting overlap

**axiom_overlap:** `DIM-6` (primary, Architecture — limits extension surface) · `DIM-3` (Contracts — sealing IS the contract that says "don't subclass")

## External grounding

- [.NET Framework Design Guidelines — Unsealed Classes](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/unsealed-classes) — unsealing is a design decision that requires explicit justification
- Composition-over-inheritance is particularly load-bearing here because the SG generates code targeting concrete types; an override the SG doesn't know about cannot run

## Severity guide

- **HIGH:** A `public class` (non-`sealed`) lands in `Strategos/Builders/` or `Strategos.Ontology/Descriptors/`, and the SG path that targets that type has not been audited for override-safety.
- **MEDIUM:** A new public type is added but accidentally non-sealed; trivial to fix before ship, but a contract change once shipped.
- **LOW:** An `internal class` is left non-sealed (no consumer impact, but tighter is better).

## Worked example

**Violation:**

```csharp
// src/Strategos/Builders/WorkflowBuilder.cs
public class WorkflowBuilder<TState> : IWorkflowBuilder<TState>   // not sealed
{
    public virtual IWorkflowBuilder<TState> Then(...) { /* ... */ }
}

// A consumer subclasses:
public sealed class MyAuditingBuilder<TState> : WorkflowBuilder<TState>
{
    public override IWorkflowBuilder<TState> Then(...) { Log(...); return base.Then(...); }
}
```

The SG, which targets `WorkflowBuilder<TState>`, emits code that calls the base implementation. Runtime diverges from author intent — the consumer's `Log(...)` never fires inside the generated saga.

**Fix:**

```csharp
public sealed class WorkflowBuilder<TState> : IWorkflowBuilder<TState> { /* ... */ }
```

If genuine extensibility is needed, expose it through `IWorkflowBuilder<TState>` extension methods or a documented extension point (decorator interface) the SG knows to honor.

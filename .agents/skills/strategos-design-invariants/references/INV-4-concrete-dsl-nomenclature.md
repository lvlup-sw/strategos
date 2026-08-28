# INV-4: Fluent workflow DSL uses concrete domain nomenclature

The public workflow DSL surface — `IWorkflowBuilder`, `Workflow<TState>`, step types, fluent extension methods — uses words that workflow *authors* think in: `StartWith`, `Then`, `Branch`, `Fork`, `Join`, `RepeatUntil`, `AwaitApproval`, `OnFailure`. It does **not** use graph-theory terms (`Node`, `Edge`, `Vertex`, `Graph`) on the public authoring surface.

The ontology DSL is a *different* surface. `Object`, `Property`, `Link`, `HasOne`, `HasMany`, `Edge` there are Foundry-style domain modeling terms, not graph-theory leaks. The scope of this invariant is the workflow DSL only — `Strategos/Builders/`, `Strategos/Abstractions/`, `Strategos/Definitions/`.

## Acceptance questions

- Does the proposal introduce a public type, method, or member under `Strategos/Builders/`, `Strategos/Abstractions/`, or `Strategos/Definitions/` whose name contains `Node`, `Edge`, `Vertex`, or `Graph`?
- If the proposed name describes a structural concept (compiler-internal), is there a domain-aligned alternative — something a workflow author would naturally reach for?
- Is the name borrowed from how the SG happens to model the workflow internally, rather than from what the author is doing?
- Does a diagnostic, exception message, or trace name expose graph-theory terms to consumers?

## Repo-grounded checks

- `src/Strategos/Abstractions/IWorkflowBuilder.cs:30-320` — full DSL surface (`StartWith`, `Then`, `Finally`, `Branch`, `Fork`, `Join`, `RepeatUntil`, `AwaitApproval`, `OnFailure`)
- `src/Strategos/Builders/Workflow.cs:25-42` — static entry `Workflow<TState>.Create(name)`
- `grep -rin '\b(graph|node|edge|vertex)\b' src/Strategos/{Builders,Abstractions,Definitions}/` returns zero non-comment hits
- `src/Strategos.Generators/Emitters/MermaidEmitter.cs` exists for internal diagram output — internal, not DSL

## Cross-cutting overlap

**axiom_overlap:** `DIM-3` (primary, Contracts — public API names are a contract surface) · `DIM-8` (Prose — naming shapes how authors think)

## External grounding

- API design guidance favors domain names over structural names — public surfaces should read like the problem, not the implementation.
- The internal SG can model the workflow as a graph if it wants; that's an implementation detail and must not leak through the public surface.

## Severity guide

- **HIGH:** A `public` member named `AddNode`, `Vertex`, `Graph`, or `Edge` lands in the workflow DSL — once shipped, it's stuck for compatibility.
- **MEDIUM:** An `internal` workflow type leaks a graph term into a public-facing diagnostic, exception message, trace name, or generated code visible to consumers.
- **LOW:** A code comment or doc page describes a workflow as "a graph of steps." Reword.

## Worked example

**Violation:**

```csharp
public interface IWorkflowBuilder<TState>
{
    IWorkflowBuilder<TState> AddNode(string name, Func<TState, Task<TState>> action);
    IWorkflowBuilder<TState> AddEdge(string fromNode, string toNode);
    IWorkflowBuilder<TState> AddVertex<T>(T payload);
}
```

This forces authors to think in graphs, not workflows. They have to translate "step 1 runs, then step 2" into "add node 1, add node 2, add edge 1→2." The DSL stops being domain-aligned.

**Fix:**

```csharp
public interface IWorkflowBuilder<TState>
{
    IWorkflowBuilder<TState> Then(string name, Func<TState, Task<TState>> step);
    IWorkflowBuilder<TState> Branch(
        Func<TState, bool> predicate,
        Action<IWorkflowBuilder<TState>> ifTrue,
        Action<IWorkflowBuilder<TState>> ifFalse);
}
```

The author writes the workflow in workflow words. The internal SG remains free to model the result as a graph for emission purposes.

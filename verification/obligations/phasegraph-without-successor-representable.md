# phasegraph-without-successor-representable

Lens: **3. Representable Invalid States**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Claim

A `PhaseGraph` consumed by `TerminalReachabilityGuard.Report` must be a `Build` product of the same `WorkflowModel`. The type must not represent a successor map that `Build` would not emit for that model.

## What led here

`WithoutSuccessor` is a new public-to-the-assembly mutator. `Report` gained an optional `PhaseGraph?` parameter. Together they make a counterfactual graph a first-class value of the same type the production guard accepts. Survey backbone item 3; wildcard W1 (tests inject this graph; production rebuilds `Build(model)`).

## Code at this revision

- `src/Strategos.Generators/Models/PhaseGraph.cs:48-54` — private constructor over a raw `Dictionary<string, List<string>>`. No invariant that every step has the Failed edge `Build` always adds (`:328-334`), or that routed rejoin edges are present.
- `src/Strategos.Generators/Models/PhaseGraph.cs:120-137` — `WithoutSuccessor` copies the map, `RemoveAll`s one target, returns `new PhaseGraph(copy, EntryPhaseName)`. Removing a name that is not present is a silent no-op (`:131-134`). The result is still a `PhaseGraph`.
- `src/Strategos.Generators/Models/PhaseGraph.cs:102-103` — `SuccessorsOf` returns `[]` for an unknown step **and** for a known step whose list was emptied. Missing vs empty collapse.
- `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs:67, 127` — `PhaseGraph? phaseGraph = null`; under-reach uses `phaseGraph ?? PhaseGraph.Build(model)`. Any `PhaseGraph` is accepted, including one that is not a `Build` of `model`.
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:1038-1043` — production omits `phaseGraph`, so the guard rebuilds. The widened signature still permits a caller to pass a mutated graph.
- Tests (`src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs`) inject `WithoutSuccessor` as the positive under-reach fixture.

`Build` itself is not loosened. The invalid state is the **type** after the mutator and the **parameter** that accepts it.

## Failure scenario

A future production caller (or a test that is mistaken for a production proof) passes `WithoutSuccessor` into `Report`. The guard fires on an edge `Build` would have kept. The diagnostic then describes a graph no emitter will emit. Conversely, a real dropped saga dispatch whose IR still has the edge never produces this graph, so under-reach stays silent (the #184 class). The representable counterfactual is what the tests kill; the production `Build` product is not that state.

## Why not cheaper

Rung 1: share one `Build` instance between the emitter and the guard so a mutator cannot be the diagnostic subject. Situational: the generator does not pass a graph today (two builds). Instance-share would close drift and would not, by itself, remove `WithoutSuccessor` from the type.

Rung 2: `PhaseGraph` as a closed `Build`-only type (mutator `internal` to tests via a test-only subtype, or `WithoutSuccessor` not returning `PhaseGraph`). The language can make the counterfactual unrepresentable as the production type. That is the cheapest sound close of this class.

Rung 4: more tests that call `Build` only. Those tests do not stop the next caller from passing a mutated graph.

## Failure signal

Nothing in production. The optional parameter is unused on the generator path. A counterfactual graph in a test is a green under-reach. “Nothing.”

## Rollback

Delete `WithoutSuccessor` and the optional parameter. Tests that inject the mutator stop compiling; they are not a shipped artifact.

## Open questions

- Is `WithoutSuccessor` intended to stay as the only under-reach kill fixture? If yes, the obligation is “the production signature must not accept that type,” not “delete the mutator.” A test-only factory that does not share a type with `Report` would satisfy the claim.
- Can `SuccessorsOf` returning `[]` for unknown names hide a misspelled last-step in `EnumerateRejoinDispatchersOf`? A typo would look like “no edge to terminal” and fire under-reach, or look like “no successors at all” and still fire. Stakes: the empty-list sentinel is a second invalid state on the same type.

## What is expensive to find again

CHANGELOG and `PhaseGraph` remarks say the diagnostic and the table “cannot drift” because they share `Build`. They share the type and the algorithm. They do not share an instance, and the type is not closed over `Build` products.

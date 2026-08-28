# agwf035-error-and-model-both-set

Lens: **3. Representable Invalid States**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Claim

`WorkflowGeneratorResult` must not represent an AGWF035 Error together with a non-null `Model`. An unreachable-termination Error is a failed transform, not a warning attached to a generated saga.

## What led here

The common lens-3 shape is `{ passed: true, error: present }`. The generator result is that shape: `hasErrors` is computed **before** the model exists; `TerminalReachabilityGuard.Report` runs **after** and still returns `(model, diagnostics)`. `RegisterSourceOutput` emits whenever `Model is not null`. Survey backbone item 4 / wildcard W3.

AGWF037 in the same wave joins `hasErrors` and returns `(null, diagnostics)`. AGWF035 does not.

## Code at this revision

- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:929-942` — `hasErrors` includes `hasDuplicatePermittedForkTrigger` (AGWF037) and several older gates. AGWF035 is not on the list. A true `hasErrors` returns `new WorkflowGeneratorResult(null, diagnostics)`.
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:1033-1045` — after the model is constructed, `TerminalReachabilityGuard.Report` appends AGWF035 into the same `diagnostics` list, then `return new WorkflowGeneratorResult(model, diagnostics)` with `model` non-null.
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:75-87` — output: report every diagnostic, then `if (result.Model is not null) EmitWorkflowSources(...)`.
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:1257-1259` — `WorkflowGeneratorResult(WorkflowModel? Model, IReadOnlyList<Diagnostic> Diagnostics)` has no constructor invariant. Any combination is representable.
- `src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs:556-568` — AGWF035 is `DiagnosticSeverity.Error`, “an error, not a warning.”

JSON import never calls the guard (`WireToModelBridge` → `EmitWorkflowSources`). That is a reachability gap (other lenses). This obligation is the C# result shape: Error plus emitted saga.

## Failure scenario

A workflow trips under-reach or over-reach. The build shows AGWF035 Error. The saga, `ValidTransitions`, and worker handlers are still written. Suppress AGWF035 (or treat Errors as non-fatal in a host) and the broken saga is the shipped composition. Authors who “fixed” the diagnostic by editing the message polarity (see `agwf035-inverted-arg-polarity`) can still ship the saga that the Error described.

## Why not cheaper

Rung 1: generate the no-generation gate from catalog severity so every `severity: error` code is in `hasErrors`. Situational: no such generator exists; the list is hand-maintained (`:933-938`). That situational gap is why AGWF037 was OR-ed in by name and AGWF035 was not.

Rung 2: a result type that cannot hold `Model` when `Diagnostics` contains an Error of this class. The language can express it (null model when errors, or a discriminated union). The current record does not.

Rung 4: a test that AGWF035 implies no emitted source. That is a case list. The next Error code added after `hasErrors` repeats the class.

## Failure signal

The Error is visible. Emission of the saga is also visible (generated files appear). Nothing treats the pair as indeterminate. The two channels disagree and both look like a completed transform.

## Rollback

Add AGWF035 to `hasErrors` (or move the guard before the gate). Does not reverse already-generated consumer sagas until those consumers rebuild.

## Open questions

- Is AGWF035-without-gating intentional (advisory Error that still emits, like the resilience diagnostics at `:920-927`)? The descriptor says Error and “a workflow that cannot reach its termination does not run.” If maintainers intended emit-anyway, the obligation moves from “unrepresentable pair” to “severity is a lie.” The pair is still the defect class.
- Does any consumer treat generator Errors as fatal independent of emitted sources (CI `/warnaserror`, IDE fail-on-error)? If yes, the saga files may still be on disk beside a red build. Stakes: the obligation’s observer is the compiler host, not only the author.

## What is expensive to find again

`hasErrors` and the guard are eighty lines apart. The new under-reach arm inherited the placement. AGWF037’s new `hasDuplicatePermittedForkTrigger` branch makes the asymmetry look deliberate.

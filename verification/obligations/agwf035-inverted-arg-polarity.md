# agwf035-inverted-arg-polarity

Lens: **3. Representable Invalid States**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Claim

An AGWF035 diagnostic must not be representable with argument polarity that contradicts the sentence it emits. `{0}` must be the step that chains; `{2}` must be the successor it chained to.

## What led here

The catalog and `UnreachableTermination` descriptor keep one three-slot over-reach sentence. The new under-reach arm fills those slots with the opposite edge. Survey backbone item 2 and wildcard W1 named this as a string-typed structure with inverted roles. This lens owns the shape: one ID, one format, two mutually exclusive meanings.

## Code at this revision

- `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp:338-346` — `AgwfEntryUnreachableTermination.remediation` is the over-reach sentence: `Step '{0}' … chains to '{2}'`.
- `src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs:561-568` — same `messageFormat`. Remarks at `:550-553` document that argument 0 is *either* the declared terminal (under-reach) *or* the step whose successor is wrong (over-reach), and argument 2 is the other member of the pair.
- `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs:116-127` — over-reach reports `(stepName, workflow, successor)` at `:116`.
- `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs:157-163` — under-reach reports `(declaredTerminalStepName, workflow, lastStep)`. Remarks at `:139-140` state that inversion explicitly.
- `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs:412-430` — shared `Report` helper. One `Diagnostic.Create(UnreachableTermination, …)` for both arms. Dedup key is `$"{stepName}\u001f{successorStepName}"` (`:420`), so the slots are just strings.

A reader of an under-reach diagnostic is told that the terminal chains to the last step and that the saga runs *past* termination. The defect is the last step *not* dispatching the terminal.

## Failure scenario

Author writes a rejoin construct whose last step does not dispatch `Finally`. The generator emits AGWF035. The IDE / `dotnet build` text says `Step 'Finally' in workflow 'W' chains to 'HandlerLast'`. The author “fixes” the terminal’s successor (the sentence’s polarity) and leaves the missing rejoin edge. The diagnostic can clear or persist for the wrong reason. Exarchos / catalog consumers reprint the same lie.

## Why not cheaper

Rung 1 is available and is the cheapest sound proof: the TypeSpec catalog already owns the sentence. A second catalog member (or a polarity-specific `messageFormat` generated from one source) makes inverted fills unrepresentable. Contracts 0.7.0 already landed in this wave, so the “do not widen the catalog” constraint that justified reuse is gone.

Rung 2 (a typed args record / two descriptors in C# only) would also close the shape, but would desynchronize from the catalog unless generated.

Rung 4 cannot close it. Existing tests assert `Contains("CloseClaim")` / `Contains("PayClaim")`. Substring presence of both names passes when the sentence names the right steps and describes the wrong fault.

## Failure signal

The compiler reports Error. The channel does **not** separate “over-reach” from “under-reach”. Nothing downstream pages on polarity. “Nothing” for polarity; the diagnostic fire itself is visible.

## Rollback

Revert the under-reach `Report(…)` argument order, or add a catalog member. A published 0.7.0 catalog that already shipped the over-reach sentence does not reverse for consumers who already rendered it.

## Open questions

- Is a second AGWF code (new catalog member) acceptable in this wave, or must polarity stay on AGWF035? A new code is a Contracts bump and an Exarchos converter event. Keeping one code with a rewritten sentence is a breaking message change for over-reach readers. Either choice changes who sees what; the obligation (no inverted fill) holds under both.
- Do any out-of-repo consumers parse AGWF035 arguments by position and assume over-reach polarity? If yes, under-reach already breaks them; a catalog widen is then a compatibility event, not just a wording fix.

## What is expensive to find again

The inversion is documented in the remarks as if it were a feature. A later reader can treat the comments as authority and keep the lie. The plan’s “widen if the sentence becomes a lie” rule was the cheaper control and was not applied after the sibling 0.7.0 bump.

# wild-agwf-error-emit-policy

The wave treats AGWF035 and AGWF037 as one "class cannot silently reopen" family. They do not share an emit-or-gate policy. Nobody states which policy the family uses.

## What led here

CHANGELOG 2.11.0 lede (`CHANGELOG.md:16-17`) says the build-time diagnostic exists so the termination class cannot silently reopen. Residue extends that same AGWF035 id with the under-reach arm (`CHANGELOG.md:172-177`) and adds AGWF037 as "fail closed" (`CHANGELOG.md:179-181`).

AGWF037 is in the `hasErrors` list that returns a null model (`WorkflowIncrementalGenerator.cs:930-942`). AGWF036 already gated that way via `pathEndTypeCollisions`. The descriptor remarks for AGWF037 say "Reject the whole workflow (no saga)" (`WorkflowDiagnostics.cs:606`).

AGWF035 is reported **after** that gate, on a live model (`WorkflowIncrementalGenerator.cs:1033-1045`). The C# output path then emits if `result.Model is not null` (`:82-86`). An AGWF035 **error** still generates the saga. Resilience diagnostics in the same transform explicitly do not gate (`:920-926`); AGWF035 sits in that "report and emit" region without a matching comment.

Same wave, two error-severity codes sold as closing a silent-reopen class, opposite generation consequences.

## Failure scenario

A consumer suppresses AGWF035 (per-id, or a broader analyzer suppress). The generator still emits the saga. The broken composition is what ships. Suppress AGWF037 and there is no saga.

The inverse failure: a contributor copies the AGWF037 "fail closed" pattern onto a new termination code and assumes AGWF035 already works that way. Review reads "error" and "cannot silently reopen" and does not notice the gate list.

Import never calls AGWF035, so the C# path is the only place the split is visible. JSON import can still emit a table-plus-saga for the under-reach shape with no diagnostic at all.

## Code paths read (rev `324768f`)

- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:82-86`, `:102-112`, `:920-942`, `:1033-1045`
- `src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs:556-568`, `:606-618`
- `CHANGELOG.md:16-17`, `:172-181`

## Why not cheaper

- **Rung 1.** The `hasErrors` disjunction is hand-written. Nothing derives "Error ⇒ null model" from the catalog `severity` field. Situational: a generated gate list from `AgwfCatalog.tsp` would move this to rung 1.
- **Rung 2.** `DiagnosticSeverity.Error` does not type-bind to `WorkflowGeneratorResult.Model == null`. Both codes compile as Error.
- **Rung 3 is the cheapest sound rung.** A structural check that every AGWF error sold as "fail closed" / "cannot silently reopen" is a member of `hasErrors` — or a machine-readable split that names which errors emit — is graph membership. A component test of one code does not lock the family policy.

## What is expensive to find again

Roslyn will still report AGWF035 as an error on the compilation, so a default `TreatWarningsAsErrors` / un-suppressed Error fails the consumer build **and** leaves generated files in the IDE. The emit-vs-gate split is easy to read as "the build failed, so we are safe." Suppression is the path that ships the saga. The family language ("fail closed", "cannot silently reopen") does not distinguish those outcomes.

## Open questions

- Is AGWF035-without-gating intentional so a consumer can still inspect the generated saga while the error is visible? If yes, the obligation stands as a **stated-policy** claim: the family must document the split, and suppression must not be able to ship the saga unnoticed. If no, AGWF035 must join `hasErrors`.
- Do any in-repo or consumer `.editorconfig` / `NoWarn` entries already suppress AGWF035? If yes, the emit path is the shipped composition today, not a hypothetical.

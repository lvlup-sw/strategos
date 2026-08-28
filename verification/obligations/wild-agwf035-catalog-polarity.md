# wild-agwf035-catalog-polarity

Under-reach reuses the over-reach catalog sentence with inverted arguments. The constraint that justified that reuse was removed by a sibling track in the same wave. A consumer who actions the sentence remediates the wrong polarity.

## What led here

The plan's T1 widen-if-lying rule said to keep the existing AGWF035 template unless the sentence became a lie. Under-reach reports `{0}` = declared terminal, `{2}` = last step that should have dispatched it (`TerminalReachabilityGuard.cs:136-140`, `:157-163`; `WorkflowDiagnostics.cs:550-553`).

The catalog sentence still says `{0}` **chains to** `{2}` and "the saga runs **past** its declared termination" (`AgwfCatalog.tsp:344`; `WorkflowDiagnostics.cs:564`; `docs/diagnostics/agwf.md:43`). That is over-reach polarity. The descriptor remarks already document the argument swap (`WorkflowDiagnostics.cs:550-553`) and keep the sentence.

T2 in the same wave already paid `Strategos.Contracts` `0.6.0 → 0.7.0` for AGWF037 (`CHANGELOG.md:182`; `Strategos.Contracts.csproj:28-37`). The catalog was opened. The justification for shipping a lying template — avoid a catalog bump — is gone.

Tests assert only `Contains("CloseClaim")` / `Contains("PayClaim")` (`TerminalReachabilityDiagnosticTests.cs:470-473`). Substring presence cannot fail when the sentence names the right steps and describes the wrong fault.

Exarchos extracts `agwf-catalog.json` from the NuGet package. The remediation string is a published contract. The 0.5.0 consumer-upgrade note already treats AGWF035 as a deserializing consumer event (`docs/coordination/2026-08-23-contracts-0.5.0-consumer-upgrade.md`).

## Failure scenario

Under-reach fires correctly: `CloseClaim` should have been dispatched by `PayClaim`. The published sentence says `CloseClaim` chains to `PayClaim` and the saga runs past termination. A human or a catalog-driven tool "fixes" the over-reach (stop `CloseClaim` from chaining) instead of adding the missing dispatch. The real fault remains. The diagnostic still fires, or a new over-reach is introduced.

A consumer pinned to the 0.7.0 catalog that parses `{0}/{2}` as "step chains to successor" actions every under-reach report backwards.

## Code paths read (rev `324768f`)

- `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp:338-346`
- `src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs:550-568`
- `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs:136-140`, `:157-163`
- `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs:470-473`
- `docs/diagnostics/agwf.md:43`
- `src/Strategos.Contracts/Strategos.Contracts.csproj:28-37`
- `CHANGELOG.md:182`
- `docs/coordination/2026-08-23-contracts-0.5.0-consumer-upgrade.md` (lead: AGWF035 has a deserializing consumer)

## Why not cheaper

- **Rung 1.** The catalog, the Roslyn `messageFormat`, and `docs/diagnostics/agwf.md` are three copies of one English sentence. They could be generated from one polarity-tagged source. They are not. Situational.
- **Rung 2.** Argument slots are format strings. The compiler cannot see that `{0}` is "the step that chains" in the sentence and "the terminal" in the under-reach call.
- **Rung 3.** A checker can prove the three copies match each other (they do). It cannot prove the English polarity matches the call-site assignment without an allowlisted reading of the sentence.
- **Rung 4 is the cheapest sound rung.** A fixture that produces a real under-reach report and asserts the message describes a missing dispatch (not a chain past termination) fails today. Identity/catalog-roundtrip tests do not.

## What is expensive to find again

The remarks at `WorkflowDiagnostics.cs:550-553` look like they already solved the polarity by documenting the swap. The published sentence is what consumers read. The in-wave 0.7.0 bump is the fact that makes "do not widen" a stale constraint.

## Open questions

- Does Exarchos, or any consumer, parse AGWF035 `{0}/{2}` as "step chains to successor"? If yes, under-reach is actioned backwards on every fire, and this obligation is a present defect, not only a polarity risk. If no, the obligation is still the published sentence must describe the fault the arm reports once the catalog is already being bumped.
- Would rewriting the template in 0.7.0 be treated as a changed catalog string (the AGWF003 precedent in `src/Strategos.Contracts/CHANGELOG.md:43-46`) and therefore already in-scope for this bump?

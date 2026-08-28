# claim-agwf035-catalog-honest — Existing AGWF035 template is not a lie for under-reach

Lens: 6 Claim Derivation
Disposition: unsupported-claim finding
Inventory claims: 16, 101, 111
Confidence: high that the honesty claim is unsupported as a consistent pair

## Finding

The inventory asserts two things that cannot both be true of one message template:

1. Prefer the existing AGWF035 remediation; widen it only if that sentence becomes a lie (claim 16, plan T1).
2. Under-reach argument binding: `{0}` is the declared terminal; `{2}` is the last step that should have dispatched it (claim 111, `TerminalReachabilityGuard.cs:139–140`).

The unchanged template (claim 101, `AgwfCatalog.tsp:344`) says:

> Step '{0}' in workflow '{1}' chains to '{2}', which is not on the workflow's main flow. … the saga runs past its declared termination instead of completing.

That sentence is an over-reach description. If `{0}` is the terminal and `{2}` is the missing dispatcher, the sentence reads "the terminal chains to the last step" and "the saga runs past termination." That is the inverse of under-reach (nothing dispatches the terminal; the saga never reaches it).

Nothing in the survey or the catalog text exhibits a widened or dual-arm remediation. Survey backbone §2: under-reach reuses the over-reach catalog sentence with inverted args; `WorkflowDiagnostics.cs:564` still says `{0}` chains to `{2}` and the saga runs *past* termination. T2 already paid Contracts 0.7.0, so the "do not widen the catalog" constraint that justified keeping the sentence is gone.

This is not an obligation that the template "must stay unchanged." That process constraint produced a lying remediation. The finding is: the claim that the existing sentence can name the dropped-edge source as `{2}` without becoming a lie has no supporting exhibit.

## What a later proof would need

If someone promotes a repair: the catalog remediation (and `WorkflowDiagnostics` `MessageFormat`) must describe both arms without inversion. That is a rung-1 catalog change plus a rung-3 parity check (existing `AgwfCatalogParityTests` would then lock the new sentence, not the old lie).

## Ledger (for the honesty claim that failed promotion)

| | |
|---|---|
| **Claim** | The reused AGWF035 remediation remains a true description when `{2}` is a missing dispatcher rather than a bad successor. |
| **Scope** | `AgwfCatalog.tsp` AGWF035 remediation; `docs/diagnostics/agwf.md`; `WorkflowDiagnostics` message format; under-reach `Report` argument order. |
| **Consequence** | Authors and Exarchos catalog consumers are told the saga runs past termination when the fault is that nothing starts the terminal. |
| **Proof rung** | (none — unsupported) |
| **Proof artifact** | None. Identity/parity tests (existing-proof P16–P21) assert the catalog string *equals* the descriptor string. They cannot detect a semantic lie. |
| **Why not cheaper** | A cheaper rung cannot establish honesty of a sentence. |
| **Failure signal** | The diagnostic text is the signal, and it describes the wrong arm. |
| **Rollback** | Widen the catalog (Contracts already at 0.7.0). |
| **Lenses** | 6 Claim Derivation. Survey lenses 1, 7. |

**Open questions:**

- None that would restore the current sentence. A human could still choose to keep the lie; that would be a rung-6 acceptance of a known false remediation, not a validation of claim 16.

Line anchors at revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`: `AgwfCatalog.tsp:344`; `TerminalReachabilityGuard.cs:139–140`.

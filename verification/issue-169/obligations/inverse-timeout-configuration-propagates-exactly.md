# inverse-timeout-configuration-propagates-exactly

## Why this obligation exists

COV-06 found timeout fields at authoring, generator IR, journal metadata, scheduling, validation, and
terminal handling, but no active claim connecting custom/default values across those roots.

## Failure scenario and boundary

Source and imported workflows choose different defaults, custom ticks are lost or rounded, a
malformed/wrong-kind/non-positive imported value becomes silent omission, or a stale/forged timeout
mutates an active inverse. Consumers observe premature/late OutcomeUnknown.

## Assigned proof

- Rung: R4, source/import contract plus generated component tests.
- Existing artifact: default and custom source/import fixtures asserting exact extracted, persisted,
  scheduled, and validated ticks, with malformed string, non-string JSON kind, zero, negative,
  altered, stale, and forged negatives.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` passed these fixtures in the
  1,915-test generator suite locally; final-revision/protected subject binding is absent.

## Investigation Log

No unresolved question. The proof exercises both omitted/default and explicit/custom values through
source and imported representations. Independent review discovered two fail-open edges after Stage 3:
authored validation omitted the compensation timeout, and imported malformed/non-string values could
lower as absence. Final-subject construction/import diagnostics reject both classes before emission.

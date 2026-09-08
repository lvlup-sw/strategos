# rejected-typed-inverse-cannot-reach-emitted-runtime

## Why this obligation exists

Stage 3 PF-1 separated the core R1 result/plan invariant from generator integration. The workflow
proof analyzer and saga source emitter are registered through different incremental-generator output
paths. A private `ActionInverseAnalysis` constructor cannot make the emitter consume proof status.

## Failure scenario and boundary

A contradictory or mixed typed program reports AGWF044/045, yet generated inverse source remains
loadable or executable because emission/activation reads compensation configuration independently.
Enabled-by-default, `NotConfigurable` Error diagnostics are an important current barrier, but their
registration, severity, and custom tags are part of the seam that must remain closed.

## Assigned proof

- Rung: R3, deterministic structural analysis.
- Existing backstops: AGWF044/045 Error severity plus `NotConfigurable`, and invalid/mixed
  generated-component negatives.
- Proposed artifact: a proof-to-emission reachability check and a kill that removes/downgrades the
  blocking edge while inverse source would otherwise be emit-capable.
- Current disposition: Unproven. The independent-review P1 is fixed in the final source; same-tree
  pre-rebase revision `cdaa73a` covers AGWF041-045 fail-closed descriptor metadata in its generator
  suite, but no final-revision/protected closure guard is bound.

## Stage 3 disposition

The refutation lens found the failure reachable across independent outputs, so this is a new active
obligation rather than a wording change to the core R1 row.

## Investigation Log

No unresolved question. Packed analyzer activation remains independently owned at R5.

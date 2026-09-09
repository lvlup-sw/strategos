---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
cost_setting: high
scope_rule: independent wildcard refutation after deduplicating mechanism, intent, authority, production, proof, and history lenses
updated: 2026-09-07
skipped: speculative hypotheses without a concrete issue-169 evidence or claim boundary were not promoted
lens: wildcard
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: distinguishes authored executable compensation from derived contract and plan
---

# Stage 1 survey — wildcard

## Independent search boundary

After reading the six directed lenses, I excluded their primary findings: three-way inverse-proof
orchestration, topology completeness, durable claim authority, external transaction semantics,
schema/diagnostic drift, historical routing recurrence, and parallel-plan/serial-runtime refinement.
I then tried to falsify a different delivery claim: whether a successful isolated package consumer can
serve as evidence that the authored CLR inverse implements the mechanically derived ontology inverse.

## W-1 — the packed legal consumer deliberately demonstrates the contract/code trust boundary

**Not classified as a product defect; high-priority evidence-boundary finding.**

The legal source embedded in `scripts/verify-generator-consumer-build.sh` declares ontology contracts
over `Order.Stage`:

- `receive` changes stage 0 -> 1;
- `undo-receive` changes 1 -> 0;
- `complete` changes 1 -> 2; and
- `undo-complete` changes 2 -> 1.

The executable workflow state is a different type, `FlowState`, and contains only `WorkflowId`.
`ReceiveStep`, `CompleteStep`, `UndoReceiveStep`, and `UndoCompleteStep` all inherit one `ProbeStep`
whose `ExecuteAsync` returns the input `FlowState` unchanged. Nevertheless, the legal consumer is
intended to compile, while the `INVALID_COMPENSATION` variant changes the **declared ontology inverse
identity/contract** and is intended to receive AGWF044.

That is a concrete demonstration of the static boundary:

```text
AGWF044 proof subject: WorkflowActionReference -> ActionDescriptor contract
runtime worker subject: CLR IWorkflowStep<TState>.ExecuteAsync body/external effects
```

The analyzer verifies that the occurrence points at a closed action descriptor semantically equal to
the derived inverse. It does not and cannot prove that arbitrary method code realizes that descriptor,
that `TState` corresponds to the ontology object, or that an external refund/delete/etc. happened.
The CLR type and ontology identity remain separate by design.

This does not refute issue #169's implemented static calculus. The issue asks to compare a derived
inverse with an authored compensation and requires executable code for non-empty frames; it does not
define a program logic for arbitrary C#. The public docs generally use “authored inverse contract” and
separately assign external-effect idempotency to the implementation. The finding does refute an
over-broad interpretation of the package-script comment:

> This proves the packaged analyzer is loaded and enforcing both #167 and #169 rather than merely present.

The script can prove packaged analyzer loading, public API usability, and descriptor-level enforcement.
It cannot prove runtime state restoration or external compensation behavior. A green packed probe must
therefore be entered at the package/analyzer rung, not used as a replacement for the real
Wolverine/Marten behavioral fixture.

## Why the existing behavioral fixture is materially different

`DerivedCompensationWorkflow.cs` uses a workflow state with a real `Stage` property. Its forward A and
B steps check and advance that state; C checks stage 2 and throws; UndoB requires stage 2 and returns
stage 1; UndoA requires stage 1 and returns stage 0. The associated PostgreSQL behavioral test also
asserts invocation order and absence of UndoC. That fixture crosses the declared-contract/executable-
state boundary for one linear program in a way the packed probe intentionally does not.

It still does not prove arbitrary user inverse code. The durable public contract is necessarily:

1. Strategos statically proves the referenced **declarations**;
2. Strategos routes and correlates the executable inverse and folds its reported state;
3. the application is responsible for making the inverse implementation satisfy the declaration and
   for idempotently handling external effects under at-least-once delivery.

## False-green consequence

Without this distinction, a final evidence report could count both the packed consumer and static
analyzer suites as independent behavioral proofs even though they exercise the same declaration-level
assumption. It could then mark “authored inverse restores the forward requirement” Verified while the
only executable steps used by that evidence are no-ops. That would be an evidence-classification bug,
not necessarily a product-code bug.

## Stage 2 obligation seed

Split the broad inverse claim into three obligations:

1. **Static contract identity/equivalence:** the named inverse action descriptor is exactly the
   mechanically derived inverse. Cheapest sufficient evidence is R3 plus the packed consumer negative.
2. **Generated routing/state fold:** the exact inverse CLR type is invoked in derived order and its
   returned state is applied before the next inverse. Cheapest sufficient evidence is adversarial R4
   generated-saga execution plus the R5 supported-host linear fixture.
3. **Application implementation/external effect:** arbitrary inverse code actually satisfies its
   declared predicate and deduplicates at-least-once external effects. This is an explicit consumer
   assumption, not something Strategos can mark Verified from this diff. Evidence can show stable,
   injective rollback IDs and document the responsibility, but must not claim universal implementation
   correctness.

Mutation target: change the packed probe's legal ontology inverse guarantee while leaving its no-op CLR
body untouched; AGWF044 must react to the declaration. Separately change the real fixture's UndoB
returned stage or omit the reducer fold; the behavioral fixture must fail. The pair makes the proof
boundary observable and prevents either rung from impersonating the other.

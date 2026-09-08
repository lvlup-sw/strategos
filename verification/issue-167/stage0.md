---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, and documentation surface against merge-base 45c86a6
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative acceptance criteria for typed workflow binding
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: immediate downstream consumer of the workflow/action contract added here
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: merged typed action-calculus contract that this change consumes
  - path: https://github.com/lvlup-sw/basileus/issues/493
    why: downstream Basileus adoption coordination; implementation remains unproven
  - path: https://github.com/lvlup-sw/exarchos/issues/1893
    why: downstream Exarchos adoption coordination; implementation remains unproven
---

# Stage 0 — Issue #167 typed workflow binding

## Scope answer — recorded word for word

> Implement the next slice #167 and #169. Use [$verify-code](/home/reedsalus/.agents/skills/verify-code/SKILL.md) to review your changes. After applying any fixes, open PRs. You may do a single round of review with coderabbitai before pressing for my final authorization to merge.

This run verifies the #167 diff first. Issue #169 is the next stacked diff and receives its own
verification run. No merge is authorized by this instruction.

## Cost control

**High.** The diff changes published and generated contracts, compiler diagnostics, a source
generator that rejects consumer builds, graph serialization identity, and public runtime APIs.
Its reverse dependency closure reaches the separately maintained Exarchos and Basileus contract
consumers. All five stages and every survey, inventory, and evaluation lens are in scope, with
multiple independent refutation attempts for each surviving obligation.

## Reverse dependency closure

1. `ActionDescriptor` and both action builders: typed workflow binding, public API, graph hashing,
   ontology serialization, analyzers, and every ontology consumer.
2. `WorkflowActionReference`, `StepDefinition`, and builder overloads: fluent authoring, immutable
   runtime definitions, projections, generated contracts, and imported workflows.
3. Workflow extraction IR: every top-level, branch, loop, fork, approval, failure, and confidence
   occurrence; phase graph lowering; saga generation; JSON import.
4. Workflow binding proof: compilation-wide action catalog, closed predicate parser, shared finite
   solver, CFG entry/seam/exit/frame/authority/noninterference obligations, recursion detection, and
   AGWF039–AGWF043 diagnostics.
5. TypeSpec and generated `Strategos.Contracts` 0.11.0 artifacts: JSON Schema, generated C#,
   catalog, packaging, SymbolKey-only resolution, and external consumers.
6. Runtime refinement API and authority lattice: action/composite substitutability and public API
   baselines.
7. Documentation and migration guidance: authoring contract, diagnostic behavior, compatibility,
   and the promise that the legacy string binding still compiles with an unchanged graph hash.
8. Verification controls: generator/ontology/core/contracts suites, code-generation drift checks,
   builder public-API gate, docs build, and downstream adoption issues before landing.

## Boundary and reversal record

- Boundaries: compiler/source-generator, published NuGet contract, JSON wire schema, and external
  repository consumers. These force the high tier.
- Source reversal: revert the #167 commits and regenerate contracts. A published 0.11.0 package or
  consumer build rejected by the new Error diagnostics does not reverse until consumers rebuild or
  pin the earlier package.
- Data reversal: #167 preserves the existing ontology graph hash for the legacy string binding;
  the new workflow-step wire member is optional and omitted when absent.

## Initial run-wide questions

These are the Stage 0 questions as opened. Current dispositions are in `ledger.md`: the first two
have exact-current local support but remain `Indeterminate` pending protected execution and review;
#169 remains `Unproven`.

- Whether every legal fluent topology accepted by runtime lowering is either represented in the
  proof IR or rejected as AGWF042.
- Whether every externally visible schema/API projection is derived or guarded against drift.
- Whether #169 can consume the occurrence-level action identities without adding a second,
  incompatible workflow/action mapping.

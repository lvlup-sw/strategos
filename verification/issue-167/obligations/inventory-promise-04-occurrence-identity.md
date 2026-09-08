# Promise obligation 04 — occurrence-scoped action identity

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over `45c86a63a9437abd920240b4dc95b235c0f72d37`; final binding was pending.
- **Promise sources:** `IC-CHANGELOG-002`, `IC-MIGRATION-002`, `IC-WORKFLOW-API-001`–`004`, `IC-COMMENT-001`–`003`.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; see `../ledger.md` and `../final-evidence.md` for current posture.

## Obligation

Every class-based executable occurrence available through a public
`Action<IStepConfiguration<TState>>` callback can carry one immutable, names-only, exact ordinal
`(domain, object type, action)` identity. Runtime builder IR, C# extraction, wire projection, JSON
import, and proof must preserve it per occurrence. Missing identity must remain distinct from an
authored dynamic/invalid identity; two identities must not collapse merely because they reuse one
CLR step type. Missing, dynamic, unknown, or ambiguous occurrence identity is authoritative before
recursive-binding analysis: an ambiguous tuple that includes the bound action must produce AGWF040,
not be traversed and misreported as an AGWF042 cycle.

The cheapest sufficient proof is **R4, cross-layer component testing**. Reflection alone can list
the callbacks but cannot prove extraction, projection, import, or occurrence isolation.

## Delivery evidence

- `WorkflowActionReference.cs:17-55` is sealed and exposes get-only name fields; its constructor
  rejects blank components. `StepDefinition.cs:74-81` stores it per occurrence.
- `StepConfigurationBuilder.cs:20-55` accepts exactly one `.Performs`, rejects null/duplicates, and
  copies the reference onto the step.
- `StepExtractor.cs:1424-1493,1688-1745` scopes extraction to the occurrence's configure lambda,
  accepts exactly one direct constructor with constant nonblank arguments, and preserves the three
  states `Missing`, `Resolved`, and `DynamicOrInvalid`.
- `StepExtractorActionReferenceTests.cs:23-63` reflects the public builder assembly and pins all 12
  configured occurrence callbacks. The remainder covers linear, named, branch, loop, fork, join,
  failure, confidence, order-independent, duplicate, alias, and dynamic forms.
- `WorkflowActionReferenceTests.cs:35-232` proves runtime builder retention and two distinct action
  identities for two uses of the same CLR step type. Approval rejection/escalation configured paths
  have focused builder tests.
- `WorkflowDefinitionProjection.cs:145-191`, `WireToModelBridge.cs:711-748`, and
  `RoundTripIrFidelityTests.cs:136-299` cover runtime projection and imported occurrence identity.
- `WorkflowBindingProofAnalyzer` adds recursive dependencies only for occurrence identities that
  resolve to exactly one declaration; `AmbiguousSelfActionReference_ReportsAgwf040InsteadOfAgwf042`
  pins the diagnostic precedence.

## Boundary

The proof graph is keyed by effective phase name. Two occurrences that collapse to the same phase
name cannot claim different actions; the docs disclose the need for distinct step types or an
available instance-name overload. Delegate/lambda steps have no closed `.Performs` surface and are
rejected for a bound workflow.

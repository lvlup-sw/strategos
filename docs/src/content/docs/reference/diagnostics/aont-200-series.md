---
title: AONT200-series — Graph and Action Contract Diagnostics
sidebar:
  order: 4
---

The AONT200 range began with hand-authored-versus-ingested drift in Strategos
2.5 and remains the monotonic home for later graph and action-contract
diagnostics. A rule fires at the earliest tier that has enough information:
Roslyn reports statically visible authoring problems, while
`OntologyGraphBuilder` validates the exact merged graph. Error entries
aggregate into the `OntologyCompositionException` thrown by `Build()`;
warnings and information entries land on
`OntologyGraph.NonFatalDiagnostics` and are mirrored to the configured
`ILogger` with structured context.

For AONT201–208, reconcile the hand-authored declaration and ingester output
(or opt out of strictness where appropriate). AONT209–212 cover edge and link
shape. AONT213–221 cover action contracts, including exact typed sequential
composition.

## Diagnostic table

| Code | Severity | Title | Message | Fix | Since |
|------|----------|-------|---------|-----|-------|
| AONT201 | Error | Hand-declared property missing from ingested descriptor | hand-declared property `<property>` on `<domain>.<type>` is missing from the ingested descriptor. Pass-6b rename matcher may have missed this — verify the property name on the ingested side. | Confirm the property name on the ingested side matches the hand declaration. If the ingester intentionally omits the property, remove the hand declaration; otherwise the Pass-6b rename matcher may need a hint via a rename delta. | 2.5.0 |
| AONT202 | Warning | Hand-declared property type mismatches ingested | property `<property>` on `<domain>.<type>` has hand-declared type/kind (`<handType>/<handKind>`) that mismatches the ingested side (`<ingestedType>/<ingestedKind>`). | Align the two sides: change the hand declaration to match the ingested type and kind, fix the ingester, or remove one declaration so only one side owns the shape. | 2.5.0 |
| AONT203 | Warning | Ingested-only property missing from hand Define() under Strict | property `<property>` is present on the ingested descriptor of `<domain>.<type>` but not declared in hand Define(); type is marked `[DomainEntity(Strict = true)]`. | Either add the property to the hand-side `Define()` so both sides agree, drop `[DomainEntity(Strict = true)]` on the CLR type if strictness is no longer required, or remove the property from the ingester output. | 2.5.0 |
| AONT204 | Info | Ingested type not referenced by any hand-authored Define() | ingested-only descriptor `<domain>.<type>` is not referenced by any hand-authored type (no Links, ParentType, or KeyProperty references found). | Confirm the ingester's contribution is actually used. If unused, remove the contribution from the source; if used by an indirect path the hint missed, link to the type from hand-side via a `HasOne/HasMany/ManyToMany` or a `ParentType` reference. | 2.5.0 |
| AONT205 | Error | Mechanical ingester contributed to intent-only field | ingested descriptor `<domain>.<type>` contributes to intent-only field `<Actions\|Events\|Lifecycle>`. Mechanical ingesters must leave Actions, Events, and Lifecycle empty — those are hand-authored intent. | Strip Actions, Events, and Lifecycle from the ingester output. Those three fields are reserved for hand-authored `DomainOntology.Define()` declarations; mechanical ingesters contribute structure only. | 2.5.0 |
| AONT206 | Info | Hand-declared property is also ingested mechanically (opt-in hygiene hint) | property `<property>` on `<domain>.<type>` is declared in hand Define() and also contributed by the ingested side — consider removing the redundant hand declaration. | Remove the hand-side `Property(...)` so the ingester is the single source of truth, or leave both declarations if the hand-side is documentation. This diagnostic fires only when `OntologyOptions.EnableHygieneHints` (or MSBuild property `OntologyEnableHygieneHints`) is set. | 2.5.0 |
| AONT207 | Warning | Branch-hand vs main-hand property conflict (deferred) | Branch-hand and main-hand declarations conflict on `<domain>.<property>`; requires four-input fold support (deferred). | Reconcile the two hand-side declarations manually. Full branch-hand vs main-hand reconciliation requires four-input fold support and is deferred — registration-only with a Skip trigger landed in Task 29; the diagnostic currently surfaces as a warning. | 2.5.0 |
| AONT208 | Error | LanguageId disagreement between origins | descriptor `<domain>.<type>` has LanguageId disagreement between hand (`<handLanguage>`) and ingested (`<ingestedLanguage>`) contributions. | Align the `LanguageId` on both sides. The diagnostic only fires when the hand side opts into a non-default `LanguageId` (anything other than `dotnet`); the common dotnet/typescript polyglot composition is not flagged. | 2.5.0 |
| AONT209 | Error | Schema-only edge-property authoring was removed | A removed edge-property API is still used. | Model edge attributes as a reified `Association<T>` object instead of attaching properties directly to a link. | 2.9.0 |
| AONT210 | Error | Invalid reified-association endpoint cardinality | An association endpoint is not `ManyToOne`; the pair cannot form a valid reified relation. | Declare both association endpoints as `ManyToOne`, so many association rows can fold into one object at each end. | 2.9.0 |
| AONT211 | Error | Ambiguous traversal without descriptor override | `TraverseLink<T>(name)` targets a CLR type registered under multiple descriptor names. | Supply the descriptor-name overload, for example `TraverseLink<T>(name, "ExactDescriptor")`. | 2.9.0 |
| AONT212 | Error | Polymorphic target has no junction table | A link targets an interface with no registered implementor, producing an empty junction fan-out. | Register at least one object type that `Implements<TInterface>`, or retarget the link. | 2.9.0 |
| AONT213 | Error | Read-only action is not idempotent | A descriptor-first action sets `IsReadOnly` but not `Idempotent`. | Set both flags, or use fluent `ReadOnly()`, which implies `Idempotent()` by construction. | 3.0.0-rc.1 |
| AONT214 | Error | Invalid authority product lattice | An authority omits an axis, names an unknown level, is unused, or violates a declared implication/product order. | Put every authority on every axis, use declared levels, remove unused literals, and make explicit implications monotonic. | 3.0.0-rc.1 |
| AONT215 | Error | Action mutation falls outside its frame | A mutating postcondition names a resource absent from `TouchedResources`. | Add the resource to the frame if the implementation may write it, or remove/correct the effect declaration. | 3.0.0-rc.1 |
| AONT216 | Error | Compensation disagrees with derived inverse | The named compensator is missing, unprovable, or differs from the derived inverse in its requirement, guarantee, frame, or semantic authority. | Declare one closed same-subject compensator whose requirement is equivalent to the forward effective guarantee, whose effective guarantee re-enters the forward hard-requirement set, and whose frame and authority are equal. This does not prove concrete pre-state restoration. | 3.0.0-rc.1 |
| AONT217 | Error | Sequential action contracts are incompatible | The upstream effective guarantee does not imply the downstream hard requirement; the message includes both subjects/actions and a stable symbolic counterexample. | Strengthen the upstream `Ensures`, weaken/correct the downstream hard requirement, or insert an action whose real contract establishes it. Do not infer values from `ModifiesProperty`. | 3.0.0-rc.1 |
| AONT218 | Warning | Action is opaque to static composition proof | A hard requirement or guarantee contains `ActionPredicate.Custom`; the evaluator key is named. | Prefer the closed predicate vocabulary where possible. Otherwise register the runtime evaluator and treat the reported seams as only partially verified. | 3.0.0-rc.1 |
| AONT219 | Info | Action composability coverage | Reports concrete executable actions classified as composable, opaque, vacuous, invalid, or statically unresolved. Zero-action graphs suppress the percentage. | Add nontrivial, realizable guarantees and resolve opaque/invalid/unresolved contracts. Do not add `Ensures(True)` merely to change the metric. | 3.0.0-rc.1 |
| AONT220 | Info | Dynamic action sequence receives runtime-only verification | An explicit `ActionCalculus.Sequential` call uses a helper or mutable/dynamic collection the analyzer cannot resolve. | Use direct constructions, immutable single-assignment locals, statically initialized immutable collections, nested `Sequential` calls, `Identity`, or `ActionCompositionOperand.From` when build-time proof is desired. Runtime composition remains the backstop. | 3.0.0-rc.1 |
| AONT221 | Error | Invalid action predicate or contract | Covers unsupported expressions, duplicate action identities, subject mismatch, contradictory hard requirements/guarantees, and guarantees unrealizable within the frame. | Give every action a unique name within its subject; replace unsupported syntax with the typed fragment or explicit `Custom`; align subjects; make formulas satisfiable; and ensure untouched guarantees already follow from requirements. | 3.0.0-rc.1 |
| AONT222 | Error | Workflow binding is not exported for proof | The assembly declares a `BoundToWorkflow` binding and emits no portable proof catalog, so neither this compilation nor any referencing one can discharge the obligation. | Reference `LevelUp.Strategos.Generators` so the action contract is exported for the compilation that lowers the workflow, or remove the binding. Not configurable: an obligation that reaches nobody is not a thing to silence. | 3.0.0 |

## Action-composition interpretation

`AONT217` and `AONT221` are proof failures, not heuristics. The shared
runtime/analyzer kernel decides the closed predicate fragment exactly. A
`Custom` term produces `AONT218` instead of being assumed true or false.
`AONT220` means only that the analyzer cannot reconstruct that particular
sequence; `ActionCalculus.Sequential` still checks it at runtime.

`AONT219` uses concrete executable action descriptors as its denominator. An
action counts as composable only when its normalized contract is valid, closed,
nonopaque, and has a nontrivial effective guarantee. Read the detailed semantics
in [Typed action calculus](/reference/action-calculus/).

## Codes referenced in source comments but not yet emitted

| Code | Status |
|------|--------|
| AONT200 | Reserved — the umbrella label for this graph/action range. No descriptor emits it directly; AONT201–221 carry the individual diagnostics. |

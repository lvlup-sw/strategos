---
title: AONT200-series — Drift Diagnostics
sidebar:
  order: 4
---

The AONT200-series, added in Strategos 2.5.0 by [PR #43](https://github.com/lvlup-sw/strategos/pull/43), covers drift between hand-authored `DomainOntology.Define()` declarations and externally ingested `IOntologySource` contributions. "Drift" here means any case where the two sides disagree on the shape of a descriptor after `OntologyGraphBuilder` merges them — a hand-tagged property missing from the ingested side, a type/kind mismatch on a shared property, a strict-mode descriptor with ingested-only properties, an orphaned ingested type, or a language-id disagreement. Codes are emitted by the graph-freeze pass inside `OntologyGraphBuilder` (see `PerformGraphFreezeChecks`). Error-severity entries aggregate into the `OntologyCompositionException` thrown from `Build()`; warning and info entries land on `OntologyGraph.NonFatalDiagnostics` and are mirrored to the configured `ILogger` with structured properties `{DiagnosticId, DomainName, TypeName, PropertyName}`.

React to a drift diagnostic by reconciling the two contribution paths: fix the hand-side declaration, fix the ingester output, or — for Strict mode — opt out of strictness by setting `[DomainEntity(Strict = false)]`.

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

## Codes referenced in source comments but not yet emitted

| Code | Status |
|------|--------|
| AONT200 | Reserved — the umbrella label for the graph-freeze drift series. No descriptor emits it directly; AONT201–208 carry the per-case diagnostics. |

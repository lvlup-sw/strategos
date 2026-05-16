---
title: AONT100–199 — Link Composition
sidebar:
  order: 3
---

The AONT100–199 range covers runtime structural validation of link descriptors and per-domain registration invariants that surface during `OntologyGraphBuilder.Build()`. The diagnostics in this slice — currently AONT040 and AONT041 — fire before any cross-domain resolver runs, so the message names the original multi-registration or duplicate registration rather than a downstream "unresolvable" error. They throw `OntologyCompositionException`, whose `Diagnostics` property carries an `ImmutableArray<OntologyDiagnostic>` of every diagnostic that fired during the failed build.

## Diagnostic table

| Code | Severity | Title | Message | Fix | Since |
|------|----------|-------|---------|-----|-------|
| AONT040 | Error (runtime) | Duplicate ObjectType name within domain | Object type name `<name>` is registered twice in domain `<domain>`. First registration: CLR type `<typeA>`. Second registration: CLR type `<typeB>`. Either remove one registration, or specify distinct names via `Object<T>("name", ...)` | Remove one of the duplicate registrations, or give one of them a distinct name via `Object<T>("alternate-name", ...)`. The runtime check throws `OntologyCompositionException` before any cryptic dictionary error from downstream code. | 2.4.1 |
| AONT041 | Error (runtime) | Multi-registered descriptor identity in link | descriptor identity `<clrType-or-symbolKey>` has multiple registrations (`<domain>.<name>`, ...) but is also referenced as a link target / declares outgoing links / is declared as the source of a cross-domain link. Multi-registered types cannot participate in structural links. See #32 for a future relaxation path. Several sub-forms exist: CLR-simple-name fallback (`HasMany<TLinked>` against a multi-registered CLR type), descriptor-identity-by-registration, and an explicit-name divergence form (a link target registered with an explicit non-default name). | Drop the duplicate registration so the type has a single descriptor identity, or remove the link. Leaf types may remain multi-registered as long as they participate in no links anywhere. The explicit-name divergence form is fixed by registering the link target under the default name. | 2.4.1 |

:::caution[AONT041 is currently strict (leaf-only multi-registration)]
The diagnostic rejects every link that touches a multi-registered descriptor identity — as target, as source, or via cross-domain wiring. Issue [#32](https://github.com/lvlup-sw/strategos/issues/32) tracks a relaxation candidate; there is no concrete consumer for relaxed semantics yet, so the strict rule stands. Plan for single-registration when a type participates in structural links.
:::

## Codes referenced in source comments but not yet emitted

The codes below appear in source-code comments as forward-looking placeholders. No `DiagnosticDescriptor` or `OntologyDiagnostic` literal currently emits them — they are reserved for future link-composition diagnostics.

| Code | Status |
|------|--------|
| AONT042 | Reserved. Referenced in `OntologyGraphBuilder.cs` as part of the link-composition diagnostic family ("AONT040/AONT042-style messaging"). No descriptor yet. |

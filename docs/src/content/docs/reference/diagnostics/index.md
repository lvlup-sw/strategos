---
title: AONT Diagnostic Codes
sidebar:
  order: 1
---

`AONT`-prefixed diagnostics are the ontology layer's compile-time and runtime identifiers. Compile-time codes are emitted by the Roslyn analyzer in `Strategos.Ontology.Generators` against `DomainOntology` definitions; runtime codes are emitted by `OntologyGraphBuilder` during composition and graph-freeze. Every code listed below is sourced from a `DiagnosticDescriptor` or an `OntologyDiagnostic` literal in the codebase — see the linked pages for the canonical message, severity, and fix.

## Range overview

| Range | Surface | Page |
|---|---|---|
| AONT001–099 | Registration and composition (analyzers + runtime) | [AONT001–099](/reference/diagnostics/aont-001-aont-099/) |
| AONT100–199 | Link composition | [AONT100–199](/reference/diagnostics/aont-100-aont-199/) |
| AONT200+ | Drift between hand-authored and ingested ontology | [AONT200-series](/reference/diagnostics/aont-200-series/) |

:::tip[Search by code]
Pagefind indexes every code on these pages. Type `AONT001` (or any code) into the search bar to jump directly to its row.
:::

---
title: "Ontology-to-Tools Compilation — Reference Index"
source: "Zhou, X. et al. (2025). arXiv:2602.03439"
notice: "Reference copy for internal analysis"
---

# Ontology-to-Tools Compilation for Executable Semantic Constraint Enforcement in LLM Agents

**Xiaochi Zhou et al.** (2025). Cambridge Centre for Computational Chemical Engineering. arXiv:2602.03439.

An ontology-to-tools compilation framework that turns an OWL/RDF ontology (T-Box) into executable MCP tool interfaces with built-in constraint validation. LLM agents construct knowledge graph instances by invoking these compiled tools, which enforce semantic constraints at creation time rather than through post-hoc validation. Demonstrated on metal-organic polyhedra synthesis literature extraction.

## Files

| File | Section | Pages | Summary |
|------|---------|-------|---------|
| [main-paper](./main-paper.md) | Main paper | 1-27 | Introduction, framework design, performance evaluation (micro-F1 0.826), discussion, and methods |
| [supplementary](./supplementary.md) | Supplementary | 28-47 | Background/related work, MCP implementation details, meta-prompts, evaluation traces |
| [references](./references.md) | References | 48-54 | Complete bibliography |

## Relevance to Strategos Ontology Layer

This paper addresses the same core problem as our `Strategos.Ontology` layer: compiling domain ontologies into typed tool interfaces that constrain LLM agent action spaces. Key parallels:

- **T-Box → Tool compilation** maps directly to our `DomainOntology.Define()` → Roslyn source generator → `IOntologyQuery` pipeline
- **Hard constraints (axioms)** parallel our `Requires()` preconditions
- **Soft constraints (annotations)** parallel our `Description()` metadata
- **MCP tool exposure** parallels our `Strategos.Ontology.MCP` package
- **Constraint feedback loop** informs our postcondition/derivation chain staleness reasoning

See [ontology-to-tools-grounding.md](../ontology-to-tools-grounding.md) for the formal analysis.

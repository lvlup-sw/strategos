---
title: "Ontological Semantics — Reference Index"
source: "Nirenburg, S. & Raskin, V. (2004). Ontological Semantics. MIT Press."
notice: "Private reference copy -- not for distribution"
---

# Ontological Semantics

**Sergei Nirenburg and Victor Raskin** (2004). MIT Press.

A comprehensive approach to the treatment of text meaning by computer. Ontological semantics is an integrated complex of theories, methodologies, descriptions, and implementations centered on a world model (ontology) as the central resource for extracting and representing meaning.

## Chapters

### Part I: About Ontological Semantics

| File | Chapter | Pages | Summary |
|------|---------|-------|---------|
| [00-preface](/strategos/reference/ontological-semantics/00-preface/) | Preface | 2-5 | Overview of the theory, methodology, and descriptions that comprise ontological semantics. Introduces the "society of microtheories" architecture. |
| [01-introduction](/strategos/reference/ontological-semantics/01-introduction/) | Ch. 1: Introduction | 6-27 | Foundational concepts: the intelligent agent model, static knowledge sources (ontology, fact database, lexicon, onomasticon), dynamic knowledge sources (analyzer, generator), and the concept of microtheories. |
| [02-philosophy-of-linguistics](/strategos/reference/ontological-semantics/02-philosophy-of-linguistics/) | Ch. 2: Philosophy of Linguistics | 28-81 | Philosophy of science applied to computational linguistics. Theory components (purview, premises, body, justification), parameters of linguistic semantic theories. |
| [03-meaning-in-linguistics](/strategos/reference/ontological-semantics/03-meaning-in-linguistics/) | Ch. 3: Meaning in Linguistics | 82-99 | Positioning ontological semantics relative to formal semantics, cognitive linguistics, and computational linguistics traditions. |
| [04-lexical-semantics](/strategos/reference/ontological-semantics/04-lexical-semantics/) | Ch. 4: Lexical Semantics | 100-115 | Choices for lexical semantic representation: word senses, polysemy, synonymy, and the ontological semantic approach to lexicon construction. |
| [05-formal-ontology](/strategos/reference/ontological-semantics/05-formal-ontology/) | Ch. 5: Formal Ontology | 116-130 | Philosophical and formal approaches to ontology: metaphysics, ontological categories, inheritance hierarchies, properties, relations, and the distinction between ontology and natural language. |

### Part II: Ontological Semantics As Such

| File | Chapter | Pages | Summary |
|------|---------|-------|---------|
| [06-meaning-representation](/strategos/reference/ontological-semantics/06-meaning-representation/) | Ch. 6: Meaning Representation | 131-154 | Text Meaning Representation (TMR): format, structure, propositional content, attitudes, discourse relations, and the process of meaning construction. |
| [07-static-knowledge-sources](/strategos/reference/ontological-semantics/07-static-knowledge-sources/) | Ch. 7: Static Knowledge Sources | 155-203 | The ontology (concepts, properties, relations, inheritance), the fact database (instances), the lexicon (word-to-concept mappings), and the onomasticon (proper nouns). Core data structures of the framework. |
| [08-processing](/strategos/reference/ontological-semantics/08-processing/) | Ch. 8: Processing | 204-256 | Semantic text analysis: preprocessing, syntactic analysis, semantic analysis (basic and extended), and the control architecture for the analyzer. |
| [09-acquisition](/strategos/reference/ontological-semantics/09-acquisition/) | Ch. 9: Knowledge Acquisition | 257-295 | Methodologies for acquiring ontological knowledge, lexicon entries, and fact database content. Semi-automated acquisition tools and workflows. |
| [10-conclusion](/strategos/reference/ontological-semantics/10-conclusion/) | Ch. 10: Conclusion | 296-297 | Summary and future directions for ontological semantics. |
| [bibliography](/strategos/reference/ontological-semantics/bibliography/) | Bibliography | 298-328 | Complete bibliography of cited works. |

## Relevance to Strategos Ontology Layer

The following chapters are most directly relevant to the `Strategos.Ontology` layer (see `platform-architecture.md` section 4.14):

- **Ch. 5 (Formal Ontology)** — Ontological categories, inheritance hierarchies, property types, and relations. Maps to our Object Types, Links, and Properties.
- **Ch. 7 (Static Knowledge Sources)** — The ontology's internal structure (concepts as frames with property-value pairs), the fact database (instance patterns), and the lexicon (semantic mappings). Maps to our `ComposedOntology`, `IOntologyQuery`, cross-domain links, and interface system.
- **Ch. 1 (Introduction)** — Foundational concepts: the agent model, knowledge architecture, and the role of ontology in constraining agent action spaces.
- **Ch. 6 (Meaning Representation)** — TMR structure and the process/state representation model. Relevant to our Lifecycle and Action primitives.

See [ontology-theoretical-grounding.md](/strategos/reference/ontology-theoretical-grounding/) for the formal analysis mapping these concepts to our ontology layer.

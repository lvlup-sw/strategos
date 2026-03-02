# Design: Ontology Theoretical Grounding

## Problem Statement

The Strategos ontology layer (`Agentic.Ontology`) was designed with practical engineering goals: typed tool discovery, cross-domain linking, and compile-time validation. Its concept mapping draws from Palantir Foundry's ontology (a commercial product ontology) but lacks grounding in formal ontological semantics theory.

Nirenburg & Raskin's "Ontological Semantics" (2004) provides a comprehensive theoretical framework for building ontologies that serve as the backbone of intelligent agent systems -- precisely our use case. The textbook covers ontology structure, knowledge representation, static knowledge sources (ontology, lexicon, fact database), and processing methodologies. A systematic comparison could reveal architectural gaps, validate our design choices, and identify concepts from the literature that strengthen our ontology layer.

Two tasks are required:
1. **Convert** the 328-page PDF textbook to structured markdown (per-chapter files optimized for semantic search)
2. **Produce** a formal theoretical grounding analysis mapping our ontology layer against Nirenburg & Raskin's framework

## Chosen Approach

**Pipeline Conversion + Parallel Analysis** (Approach A from brainstorming).

Use `pymupdf4llm` (already installed) for bulk PDF-to-markdown extraction, then Claude post-processes each chapter for structural cleanup. The converted chapters live in `docs/reference/ontological-semantics/` as separate files. After conversion, produce a formal analysis document comparing our ontology primitives against the textbook's theoretical framework.

**Rationale:** `pymupdf4llm` is purpose-built for LLM/RAG markdown extraction, already installed, and handles FrameMaker-era PDFs well. Per-chapter files enable granular semantic search. Separating conversion from analysis allows parallel work and produces two independent, reusable artifacts.

## Requirements

### DR-1: PDF-to-Markdown Conversion

Convert the 328-page PDF (FrameMaker 5.5.6, PDF 1.4, untagged) to structured markdown using `pymupdf4llm`. Output as per-chapter files in `docs/reference/ontological-semantics/`.

**Chapter structure (from TOC):**
- `00-preface.md` -- Preface (p.2-5)
- `01-introduction.md` -- Ch. 1: Introduction to Ontological Semantics (p.6-27)
- `02-philosophy-of-linguistics.md` -- Ch. 2: Prolegomena to the Philosophy of Linguistics (p.28-81)
- `03-meaning-in-linguistics.md` -- Ch. 3: Ontological Semantics and the Study of Meaning (p.82-99)
- `04-lexical-semantics.md` -- Ch. 4: Choices for Lexical Semantics (p.100-115)
- `05-formal-ontology.md` -- Ch. 5: Formal Ontology and the Needs of Ontological Semantics (p.116-130)
- `06-meaning-representation.md` -- Ch. 6: Meaning Representation in Ontological Semantics (p.132-179)
- `07-static-knowledge-sources.md` -- Ch. 7: The Static Knowledge Sources: Ontology, Fact Database and Lexicons
- `08-processing.md` -- Ch. 8: Basic Processing in Ontological Semantic Text Analysis
- `09-acquisition.md` -- Ch. 9: Acquisition of Static Knowledge Sources
- `10-conclusion.md` -- Ch. 10: Conclusion
- `index.md` -- Master index with chapter summaries and cross-references

**Acceptance criteria:**
- All 10 chapters + preface converted to individual markdown files
- Section headings preserved at correct hierarchy levels (H1 for chapter, H2 for sections, H3 for subsections)
- Footnotes preserved (inline or endnotes per chapter)
- An `index.md` file provides chapter listing with one-paragraph summaries
- Files are readable and searchable as plain markdown

### DR-2: Conversion Quality Standards

Each chapter file must meet minimum quality standards for use as a reference corpus and semantic search source.

**Acceptance criteria:**
- No garbled text from encoding issues (FrameMaker PDFs sometimes produce ligature artifacts)
- Academic citations preserved (author-year format intact)
- Bullet lists and numbered lists properly formatted
- Block quotes and examples distinguished from body text
- Page numbers from the original PDF preserved as comments or metadata for cross-referencing
- Mathematical notation and special characters handled (fallback to plain text description if needed)

### DR-3: Theoretical Grounding Analysis Document

Produce a formal analysis document at `docs/reference/ontology-theoretical-grounding.md` that maps the Agentic.Ontology layer against Nirenburg & Raskin's ontological semantics framework.

**Document structure:**
1. **Executive Summary** -- Key findings and top recommendations
2. **Concept Mapping Table** -- Our primitives (Object Type, Property, Link, Action, Interface, Lifecycle, Derivation Chain, Precondition/Postcondition, Extension Point) mapped to Nirenburg & Raskin equivalents
3. **Alignment Analysis** -- Where our design aligns with the theory
4. **Gap Analysis** -- Concepts in the textbook we're missing or underrepresenting
5. **Terminology Review** -- Are we using terms correctly? Better names from the literature?
6. **Architectural Recommendations** -- Concrete suggestions for improving our ontology layer
7. **References** -- Page/section citations into the converted markdown chapters

**Acceptance criteria:**
- Every Agentic.Ontology primitive (from platform-architecture.md section 4.14.4) has a mapping entry
- Gap analysis identifies at least 3 specific concepts from the textbook that could strengthen our ontology
- Recommendations are concrete and actionable (not vague "consider X")
- All claims cite specific chapters/sections from the textbook
- Document is self-contained (readable without having read the full textbook)

### DR-4: Key Chapter Deep Analysis

Chapters 5 (Formal Ontology) and 7 (Static Knowledge Sources) are the most directly relevant to our ontology layer. These must receive deeper analysis with explicit comparisons to our `DomainOntology`, `IOntologyQuery`, and the builder DSL.

**Acceptance criteria:**
- Ch. 5 analysis covers: ontological categories, inheritance hierarchies, property types, relations between concepts, and how these map to our Object Types / Links / Properties
- Ch. 7 analysis covers: ontology structure (concepts, instances, properties, relations), fact database patterns, and the lexicon model -- mapped to our `ComposedOntology`, cross-domain links, and interface system
- Specific textbook definitions compared side-by-side with our descriptor types (`ObjectTypeDescriptor`, `PropertyDescriptor`, `LinkDescriptor`, `ActionDescriptor`, etc.)
- Identifies where Nirenburg & Raskin's concept hierarchy model differs from our flat Object Type registration model

### DR-5: Error Handling and Edge Cases

The PDF conversion must handle known issues with FrameMaker-era PDFs and the analysis must handle ambiguity in cross-domain concept mapping.

**Acceptance criteria:**
- Conversion gracefully handles: ligature encoding (fi, fl, ff), hyphenation artifacts at line breaks, header/footer stripping, footnote numbering across page breaks
- If `pymupdf4llm` produces garbled output for specific pages, those pages are flagged with `<!-- CONVERSION NOTE: manual review needed -->` comments
- The analysis document explicitly notes where textbook concepts have no clear mapping to our system (rather than forcing a mapping)
- Where terminology differs between the textbook and our implementation, both terms are noted with rationale for our choice

## Technical Design

### Conversion Pipeline

```text
PDF (328pp) ──→ pymupdf4llm ──→ Raw Markdown ──→ Chapter Splitter ──→ Per-Chapter Files
                                                         │
                                                         └──→ Claude Cleanup Pass ──→ Final Markdown
```

**Step 1: Bulk extraction** via `pymupdf4llm.to_markdown()` with page-level granularity.

**Step 2: Chapter splitting** using TOC metadata from `pymupdf.Document.get_toc()` to identify page ranges per chapter. The TOC has 189 entries with page numbers.

**Step 3: Post-processing** per chapter:
- Strip headers/footers (page numbers, "Page N" footers)
- Fix heading hierarchy
- Clean up ligature artifacts
- Preserve footnotes
- Add YAML frontmatter with chapter metadata

### Analysis Methodology

1. Convert all chapters to markdown (DR-1, DR-2)
2. Deep read Ch. 5 (Formal Ontology) and Ch. 7 (Static Knowledge Sources)
3. Read Ch. 1 (Introduction -- foundational concepts) and Ch. 6 (Meaning Representation)
4. Cross-reference against `platform-architecture.md` section 4.14 (all subsections)
5. Produce the grounding analysis document (DR-3)
6. Identify specific improvement recommendations

### Key Nirenburg & Raskin Concepts to Map

From the preface and Ch. 1, the textbook's knowledge architecture includes:
- **Ontology**: concepts (types of things), properties, relations, instances
- **Fact Database**: episodic memory of instances and their combinations
- **Lexicon**: mappings between language and ontology concepts
- **Onomasticon**: proper noun database (instances with names)
- **Text Meaning Representation (TMR)**: structured representation of text meaning

Our `Agentic.Ontology` maps most directly to the textbook's "Ontology" component, but may benefit from concepts in the Fact Database (instance patterns), Lexicon (semantic mappings), and TMR (structured intent representation) layers.

## Integration Points

- **Platform Architecture Doc**: `docs/reference/platform-architecture.md` section 4.14 is the authoritative spec for our ontology layer
- **Existing Plans**: `docs/plans/2026-02-24-ontology-layer.md` (63-task implementation plan) and `docs/plans/2026-03-01-ontology-spec-alignment.md` (18-task spec alignment)
- **Source Code**: The Strategos packages (`Strategos.Ontology`, `Strategos.Ontology.Generators`, `Strategos.Ontology.MCP`)
- **Design Doc**: `docs/designs/2026-02-24-ontology-layer.md` (original ontology design)

The theoretical grounding analysis may produce recommendations that feed into future refactoring plans. These should be captured as actionable items, not immediate code changes.

## Testing Strategy

This is a documentation/analysis task, not a code change. Verification is:
- **Conversion**: Spot-check 3 chapters (one early, one middle, one late) for structural accuracy against the PDF
- **Analysis**: Cross-reference every claim in the grounding document against the source chapter files
- **Completeness**: Verify all 12 Agentic.Ontology primitives from section 4.14.4 appear in the concept mapping table

## Open Questions

1. **Copyright considerations**: The textbook carries "Draft: Do Not Quote or Distribute" on the title page. The markdown conversion is for private reference use within this project. Should we add a notice to each file?
2. **Bibliography/References section**: The textbook likely has an extensive bibliography. Should we convert that to a separate `references.md` file?
3. **Figures and diagrams**: FrameMaker PDFs may contain diagrams. `pymupdf4llm` may extract these as images or skip them. Should we attempt image extraction or describe diagrams in text?

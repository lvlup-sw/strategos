# Implementation Plan: Ontology Theoretical Grounding

## Source Design
Link: `docs/designs/2026-03-01-ontology-theoretical-grounding.md`

## Scope
**Target:** Full design — PDF conversion (DR-1, DR-2, DR-5) + theoretical grounding analysis (DR-3, DR-4)
**Nature:** Documentation/analysis task — no production code changes. TDD does not apply; verification uses spot-checks and completeness audits.

## Summary
- Total tasks: 19
- Parallel groups: 3 phases (conversion, cleanup, analysis)
- Output: 13 markdown files (12 chapter files + 1 index) + 1 analysis document

## Spec Traceability

### Traceability Matrix

| Design Requirement | Key Requirements | Task ID(s) | Status |
|-------------------|-----------------|------------|--------|
| **DR-1: PDF-to-Markdown Conversion** | All chapters converted, headings preserved, footnotes kept, index.md created | 001-004 | Covered |
| **DR-2: Conversion Quality Standards** | No garbled text, citations preserved, lists formatted, page numbers preserved | 005-016 | Covered |
| **DR-3: Theoretical Grounding Analysis** | Concept mapping, alignment, gaps, terminology, recommendations, references | 017-018 | Covered |
| **DR-4: Key Chapter Deep Analysis** | Ch. 5 + Ch. 7 deep comparison against ontology primitives | 017 | Covered |
| **DR-5: Error Handling and Edge Cases** | Ligature handling, conversion notes, unmappable concepts noted | 005-016, 019 | Covered |

---

## Parallelization Groups

```text
Phase 1 ─── Tasks 001-004  (Conversion infrastructure + bulk extraction + splitting)
Phase 2 ─── Tasks 005-016  (Per-chapter cleanup — all 12 parallel)
Phase 3 ─── Tasks 017-018  (Analysis — sequential: deep read then write)
Phase 4 ─── Task 019       (Verification — after all above)

Phase 1 sequential ──────────────┐
Phase 2 parallel (12 chapters)  ─┤
Phase 3 sequential after Phase 2 ┤
Phase 4 sequential (last) ───────┘
```

---

## Tasks

### Task 001: Write PDF conversion script
**Implements:** DR-1, DR-5
**Phase:** Script creation (no TDD — utility script)

Write a Python script `scripts/convert-textbook.py` that:
1. Opens the PDF using `pymupdf`
2. Extracts markdown using `pymupdf4llm.to_markdown()` with page-level granularity
3. Splits output into per-chapter files using the page range map:
   - `00-preface.md`: PDF pages 2-5 (4 pp)
   - `01-introduction.md`: PDF pages 6-27 (22 pp)
   - `02-philosophy-of-linguistics.md`: PDF pages 28-81 (54 pp)
   - `03-meaning-in-linguistics.md`: PDF pages 82-99 (18 pp)
   - `04-lexical-semantics.md`: PDF pages 100-115 (16 pp)
   - `05-formal-ontology.md`: PDF pages 116-130 (15 pp)
   - `06-meaning-representation.md`: PDF pages 131-154 (24 pp)
   - `07-static-knowledge-sources.md`: PDF pages 155-203 (49 pp)
   - `08-processing.md`: PDF pages 204-256 (53 pp)
   - `09-acquisition.md`: PDF pages 257-295 (39 pp)
   - `10-conclusion.md`: PDF pages 296-297 (2 pp)
   - `bibliography.md`: PDF pages 298-328 (31 pp)
4. Adds YAML frontmatter to each file:
   ```yaml
   ---
   title: "Chapter N: Title"
   source: "Nirenburg & Raskin, Ontological Semantics (2004)"
   pages: "N-M"
   notice: "Private reference copy — not for distribution"
   ---
   ```
5. Strips "Page N" footer lines from each page

**Output:** `docs/reference/ontological-semantics/*.md` (12 files)
**Dependencies:** None

---

### Task 002: Create output directory structure
**Implements:** DR-1

Create `docs/reference/ontological-semantics/` directory and add a `.gitkeep` or the first converted file.

**Dependencies:** None
**Parallelizable with Task 001:** Yes

---

### Task 003: Run bulk conversion
**Implements:** DR-1

Execute the conversion script from Task 001. Verify all 12 files are generated with non-empty content.

**Verification:**
- All 12 `.md` files exist in `docs/reference/ontological-semantics/`
- Each file has >100 lines (sanity check)
- No file is empty or contains only frontmatter

**Dependencies:** 001, 002

---

### Task 004: Initial quality assessment
**Implements:** DR-2, DR-5

Read the first 50 lines of 3 sample chapters (01-introduction, 05-formal-ontology, 07-static-knowledge-sources) and assess:
- Are headings extracted correctly?
- Are ligatures handled? (look for "ﬁ", "ﬂ", "ﬀ" artifacts)
- Are footnotes present?
- Is body text readable?

Record any systematic issues that the cleanup tasks (005-016) need to address.

**Dependencies:** 003

---

### Task 005: Cleanup — 00-preface.md
**Implements:** DR-2, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Post-process the preface file:
1. Fix heading hierarchy (H1 for "Preface", H2 for subsections)
2. Fix ligature artifacts (fi→fi, fl→fl, ff→ff, ffi→ffi)
3. Remove "Page N" footer lines
4. Fix hyphenation artifacts at line breaks (rejoin split words)
5. Ensure academic citations are intact (author-year format)
6. Add `<!-- Page N -->` comments at page boundaries for reference
7. Flag any garbled sections with `<!-- CONVERSION NOTE: manual review needed -->`

**Dependencies:** 004

---

### Task 006: Cleanup — 01-introduction.md
**Implements:** DR-2, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Same cleanup process as Task 005 for Chapter 1 (22 pages). Additionally:
- Verify section numbering (1.1, 1.1.1, etc.) maps to correct heading levels
- Ensure the section on "Relevant Components of an Intelligent Agent's Model" (1.1.1) preserves the knowledge component list (ontology, fact database, lexicon, onomasticon)

**Dependencies:** 004

---

### Task 007: Cleanup — 02-philosophy-of-linguistics.md
**Implements:** DR-2, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Same cleanup process for Chapter 2 (54 pages — largest chapter). This chapter has deep subsection nesting (up to 4 levels: 2.4.2.7). Verify heading levels go to H5 where needed.

**Dependencies:** 004

---

### Task 008: Cleanup — 03-meaning-in-linguistics.md
**Implements:** DR-2, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Same cleanup process for Chapter 3 (18 pages).

**Dependencies:** 004

---

### Task 009: Cleanup — 04-lexical-semantics.md
**Implements:** DR-2, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Same cleanup process for Chapter 4 (16 pages).

**Dependencies:** 004

---

### Task 010: Cleanup — 05-formal-ontology.md (HIGH PRIORITY)
**Implements:** DR-2, DR-4, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Same cleanup process for Chapter 5 (15 pages). **High priority** — this is a key analysis chapter. Extra attention to:
- Ontological category definitions
- Hierarchy/inheritance terminology
- Property type classifications
- Relation type definitions

Ensure all definitions and formal structures are accurately preserved.

**Dependencies:** 004

---

### Task 011: Cleanup — 06-meaning-representation.md
**Implements:** DR-2, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Same cleanup process for Chapter 6 (24 pages). This chapter covers Text Meaning Representation (TMR) — preserve all formal notation and examples.

**Dependencies:** 004

---

### Task 012: Cleanup — 07-static-knowledge-sources.md (HIGH PRIORITY)
**Implements:** DR-2, DR-4, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Same cleanup process for Chapter 7 (49 pages). **High priority** — this is the other key analysis chapter. Extra attention to:
- Ontology structure (concepts, properties, relations, instances)
- Fact database patterns
- Lexicon entry format
- Onomasticon structure

This chapter likely contains the most directly comparable structures to our `ObjectTypeDescriptor`, `PropertyDescriptor`, `LinkDescriptor`, etc.

**Dependencies:** 004

---

### Task 013: Cleanup — 08-processing.md
**Implements:** DR-2, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Same cleanup process for Chapter 8 (53 pages).

**Dependencies:** 004

---

### Task 014: Cleanup — 09-acquisition.md
**Implements:** DR-2, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Same cleanup process for Chapter 9 (39 pages).

**Dependencies:** 004

---

### Task 015: Cleanup — 10-conclusion.md + bibliography.md
**Implements:** DR-2, DR-5
**Parallelizable:** Yes (Group: Phase 2)

Cleanup for the conclusion (2 pages) and bibliography (31 pages). The bibliography needs special handling:
- Preserve author-year citation format
- Each entry on its own line
- Alphabetical ordering preserved

**Dependencies:** 004

---

### Task 016: Create index.md
**Implements:** DR-1

Create `docs/reference/ontological-semantics/index.md` with:
1. Title and source attribution
2. Copyright/usage notice
3. Chapter listing with one-paragraph summaries
4. Cross-reference links to each chapter file
5. Note about which chapters are most relevant to the Strategos ontology layer

**Dependencies:** 005-015 (all cleanup tasks complete)

---

### Task 017: Write theoretical grounding analysis
**Implements:** DR-3, DR-4

This is the core analytical task. Read the cleaned chapters (prioritizing Ch. 5 and Ch. 7) alongside `platform-architecture.md` section 4.14, and produce:

**`docs/reference/ontology-theoretical-grounding.md`**

Document structure:

**1. Executive Summary** (200-300 words)
- Key findings from the comparison
- Top 3-5 recommendations

**2. Concept Mapping Table**
Map each Agentic.Ontology primitive to Nirenburg & Raskin equivalents:

| Our Primitive | N&R Equivalent | Chapter/Section | Notes |
|--------------|---------------|----------------|-------|
| Object Type | Concept | Ch. 7 | ... |
| Property | Property/Attribute | Ch. 7 | ... |
| Link | Relation | Ch. 7 | ... |
| Action | (no direct equivalent) | — | ... |
| Interface | (cross-type polymorphism) | Ch. 5 | ... |
| Cross-Domain Link | (inter-ontology relations) | Ch. 7 | ... |
| Precondition | (constraint) | Ch. 7 | ... |
| Postcondition | (effect) | Ch. 7 | ... |
| Lifecycle | (state/process) | Ch. 6 | ... |
| Derivation Chain | (derived property) | Ch. 7 | ... |
| Extension Point | (no direct equivalent) | — | ... |

**3. Alignment Analysis** — Where our design already aligns with N&R theory

**4. Gap Analysis** — At least 3 specific concepts we're missing. Candidate areas:
- N&R's concept hierarchy (IS-A inheritance) vs. our flat registration model
- N&R's "fact database" (instance patterns) vs. our lack of instance representation
- N&R's "lexicon" (natural language to ontology mapping) vs. our Action descriptions
- N&R's "onomasticon" (named entities) vs. no equivalent in our system
- N&R's "selectional restrictions" (property value constraints beyond preconditions)

**5. Terminology Review** — Correct/incorrect terminology usage

**6. Architectural Recommendations** — Concrete, actionable suggestions

**7. References** — Chapter/section citations

**Dependencies:** 005-016 (all chapters cleaned), plus reading `platform-architecture.md` section 4.14

---

### Task 018: Review and refine analysis
**Implements:** DR-3

Review the grounding analysis for:
1. Every ontology primitive from section 4.14.4 appears in the concept mapping table (12 primitives)
2. All claims cite specific textbook chapters/sections
3. Recommendations are concrete and actionable
4. Document is self-contained
5. Gap analysis has at least 3 specific items

**Dependencies:** 017

---

### Task 019: Final verification
**Implements:** DR-1, DR-2, DR-3, DR-5

Spot-check verification:
1. **Conversion quality**: Read 3 chapter files alongside the PDF pages to verify accuracy
   - Check Ch. 1 (early), Ch. 5 (middle, key chapter), Ch. 9 (late)
2. **Analysis completeness**: Verify all 12 ontology primitives from section 4.14.4 appear in the mapping table
3. **Citation integrity**: Verify 5 random citations in the analysis document trace back to the correct chapter/section

**Dependencies:** 016, 018

---

## Dependency Graph

```text
[001] Conversion script ──────┐
[002] Create directory ────────┼── [003] Run conversion ── [004] Quality assessment
                               │                                    │
                               │    ┌───────────────────────────────┘
                               │    │
                               │    ├── [005] Cleanup preface ──────┐
                               │    ├── [006] Cleanup ch.1 ─────────┤
                               │    ├── [007] Cleanup ch.2 ─────────┤
                               │    ├── [008] Cleanup ch.3 ─────────┤
                               │    ├── [009] Cleanup ch.4 ─────────┤
                               │    ├── [010] Cleanup ch.5 ★ ───────┤
                               │    ├── [011] Cleanup ch.6 ─────────┤  All parallel
                               │    ├── [012] Cleanup ch.7 ★ ───────┤
                               │    ├── [013] Cleanup ch.8 ─────────┤
                               │    ├── [014] Cleanup ch.9 ─────────┤
                               │    └── [015] Cleanup ch.10+bib ───┤
                               │                                    │
                               │                     [016] Index ◄──┘
                               │                         │
                               │         [017] Grounding analysis ◄─┘
                               │                   │
                               │         [018] Review analysis
                               │                   │
                               └──────── [019] Final verification
```

★ = high priority chapters for analysis (Tasks 010, 012)

## Task Summary

| ID | Title | Implements | Group | Priority |
|----|-------|-----------|-------|----------|
| 001 | Write conversion script | DR-1, DR-5 | Phase 1 | — |
| 002 | Create output directory | DR-1 | Phase 1 | — |
| 003 | Run bulk conversion | DR-1 | Phase 1 | — |
| 004 | Initial quality assessment | DR-2, DR-5 | Phase 1 | — |
| 005 | Cleanup preface | DR-2, DR-5 | Phase 2 | — |
| 006 | Cleanup ch.1 introduction | DR-2, DR-5 | Phase 2 | — |
| 007 | Cleanup ch.2 philosophy | DR-2, DR-5 | Phase 2 | — |
| 008 | Cleanup ch.3 meaning | DR-2, DR-5 | Phase 2 | — |
| 009 | Cleanup ch.4 lexical | DR-2, DR-5 | Phase 2 | — |
| 010 | Cleanup ch.5 formal ontology | DR-2, DR-4, DR-5 | Phase 2 | HIGH |
| 011 | Cleanup ch.6 representation | DR-2, DR-5 | Phase 2 | — |
| 012 | Cleanup ch.7 static knowledge | DR-2, DR-4, DR-5 | Phase 2 | HIGH |
| 013 | Cleanup ch.8 processing | DR-2, DR-5 | Phase 2 | — |
| 014 | Cleanup ch.9 acquisition | DR-2, DR-5 | Phase 2 | — |
| 015 | Cleanup ch.10 + bibliography | DR-2, DR-5 | Phase 2 | — |
| 016 | Create index.md | DR-1 | Phase 3 | — |
| 017 | Write grounding analysis | DR-3, DR-4 | Phase 3 | HIGH |
| 018 | Review analysis | DR-3 | Phase 4 | — |
| 019 | Final verification | DR-1-5 | Phase 4 | — |

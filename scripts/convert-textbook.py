#!/usr/bin/env python3
"""Convert Nirenburg & Raskin's Ontological Semantics PDF to per-chapter markdown.

Uses pymupdf4llm for LLM/RAG-optimized extraction, then splits by chapter boundaries.
"""

import re
import sys
from pathlib import Path

import pymupdf4llm
import pymupdf

PDF_PATH = Path.home() / "Documents/academic/textbooks/ontological-semantics.pdf"
OUTPUT_DIR = Path(__file__).resolve().parent.parent / "docs/reference/ontological-semantics"

# Chapter boundaries: (filename, title, start_page_0indexed, end_page_0indexed_inclusive)
CHAPTERS = [
    ("00-preface", "Preface", 1, 4),
    ("01-introduction", "Chapter 1: Introduction to Ontological Semantics", 5, 26),
    ("02-philosophy-of-linguistics", "Chapter 2: Prolegomena to the Philosophy of Linguistics", 27, 80),
    ("03-meaning-in-linguistics", "Chapter 3: Ontological Semantics and the Study of Meaning", 81, 98),
    ("04-lexical-semantics", "Chapter 4: Choices for Lexical Semantics", 99, 114),
    ("05-formal-ontology", "Chapter 5: Formal Ontology and the Needs of Ontological Semantics", 115, 129),
    ("06-meaning-representation", "Chapter 6: Meaning Representation in Ontological Semantics", 130, 153),
    ("07-static-knowledge-sources", "Chapter 7: The Static Knowledge Sources: Ontology, Fact Database and Lexicons", 154, 202),
    ("08-processing", "Chapter 8: Basic Processing in Ontological Semantic Text Analysis", 203, 255),
    ("09-acquisition", "Chapter 9: Acquisition of Static Knowledge Sources", 256, 294),
    ("10-conclusion", "Chapter 10: Conclusion", 295, 296),
    ("bibliography", "Bibliography", 297, 327),
]

FRONTMATTER_TEMPLATE = """---
title: "{title}"
source: "Nirenburg, S. & Raskin, V. (2004). Ontological Semantics. MIT Press."
pdf_pages: "{start}-{end}"
notice: "Private reference copy -- not for distribution"
---

"""

PAGE_FOOTER_PATTERN = re.compile(r"^Page \d+\s*$", re.MULTILINE)


def extract_chapter(pdf_path: Path, start: int, end: int) -> str:
    """Extract markdown for a page range using pymupdf4llm."""
    pages = list(range(start, end + 1))
    md = pymupdf4llm.to_markdown(str(pdf_path), pages=pages)
    return md


def clean_markdown(md: str) -> str:
    """Basic cleanup of extracted markdown."""
    # Remove "Page N" footer lines
    md = PAGE_FOOTER_PATTERN.sub("", md)
    # Remove excessive blank lines (3+ -> 2)
    md = re.sub(r"\n{4,}", "\n\n\n", md)
    # Fix common ligature artifacts from FrameMaker PDFs
    md = md.replace("\ufb01", "fi")
    md = md.replace("\ufb02", "fl")
    md = md.replace("\ufb00", "ff")
    md = md.replace("\ufb03", "ffi")
    md = md.replace("\ufb04", "ffl")
    return md.strip()


def main():
    if not PDF_PATH.exists():
        print(f"ERROR: PDF not found at {PDF_PATH}", file=sys.stderr)
        sys.exit(1)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Converting {PDF_PATH.name} -> {OUTPUT_DIR}/")
    print(f"  {len(CHAPTERS)} chapters to extract")

    for slug, title, start, end in CHAPTERS:
        outfile = OUTPUT_DIR / f"{slug}.md"
        page_count = end - start + 1
        print(f"  {slug}.md ({page_count} pages: {start+1}-{end+1})...", end=" ", flush=True)

        md = extract_chapter(PDF_PATH, start, end)
        md = clean_markdown(md)

        frontmatter = FRONTMATTER_TEMPLATE.format(
            title=title, start=start + 1, end=end + 1
        )
        outfile.write_text(frontmatter + md + "\n", encoding="utf-8")
        print(f"OK ({len(md)} chars)")

    print(f"\nDone. {len(CHAPTERS)} files written to {OUTPUT_DIR}/")


if __name__ == "__main__":
    main()

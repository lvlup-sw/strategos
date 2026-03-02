#!/usr/bin/env python3
"""Post-process converted textbook chapters: fix headings, join paragraphs, clean artifacts."""

import re
from pathlib import Path

OUTPUT_DIR = Path(__file__).resolve().parent.parent / "docs/reference/ontological-semantics"

# Pattern: **N.N.N Title** or **N. Title** at start of line -> markdown heading
HEADING_PATTERNS = [
    # Part headings: **I. About Ontological Semantics** -> # Part I: About Ontological Semantics
    (re.compile(r"^\*\*\s*(I{1,3}V?|VI{0,3})\.\s+(.+?)\s*\*\*\s*$", re.MULTILINE),
     lambda m: f"# Part {m.group(1)}: {m.group(2)}"),
    # Chapter headings: **1. Introduction...** -> # 1. Introduction...
    (re.compile(r"^\*\*\s*(\d{1,2})\.\s+(.+?)\s*\*\*\s*$", re.MULTILINE),
     lambda m: f"# {m.group(1)}. {m.group(2)}"),
    # Section headings: **1.1 Title** -> ## 1.1 Title
    (re.compile(r"^\*\*\s*(\d{1,2}\.\d{1,2})\s+(.+?)\s*\*\*\s*$", re.MULTILINE),
     lambda m: f"## {m.group(1)} {m.group(2)}"),
    # Subsection: **1.1.1 Title** -> ### 1.1.1 Title
    (re.compile(r"^\*\*\s*(\d{1,2}\.\d{1,2}\.\d{1,2})\s+(.+?)\s*\*\*\s*$", re.MULTILINE),
     lambda m: f"### {m.group(1)} {m.group(2)}"),
    # Sub-subsection: **1.1.1.1 Title** -> #### 1.1.1.1 Title
    (re.compile(r"^\*\*\s*(\d{1,2}\.\d{1,2}\.\d{1,2}\.\d{1,2})\s+(.+?)\s*\*\*\s*$", re.MULTILINE),
     lambda m: f"#### {m.group(1)} {m.group(2)}"),
    # Named sections without numbers: **Preface**, **Figure N.**, **Table N.**
    (re.compile(r"^\*\*(Preface|Bibliography|Conclusion|References|Index)\*\*\s*$", re.MULTILINE),
     lambda m: f"# {m.group(1)}"),
    # Figure/Table captions: **Figure N. ...** -> **Figure N.** ...  (keep as bold)
]

# Pattern for rejoining paragraphs split by PDF line breaks
# A line that ends mid-word (with a hyphen) should be joined
HYPHEN_BREAK = re.compile(r"(\w)-\n(\w)")
# Lines ending mid-sentence (lowercase letter, no punctuation) followed by next line starting lowercase
MID_SENTENCE_BREAK = re.compile(r"([a-z,;])\n([a-z])")


def fix_headings(text: str) -> str:
    """Convert bold-text headings to proper markdown headings."""
    for pattern, replacement in HEADING_PATTERNS:
        text = pattern.sub(replacement, text)
    return text


def join_broken_paragraphs(text: str) -> str:
    """Rejoin paragraphs that were split by PDF line breaks."""
    # Fix hyphenated line breaks: "ontol-\nogy" -> "ontology"
    text = HYPHEN_BREAK.sub(r"\1\2", text)
    # Join mid-sentence line breaks
    text = MID_SENTENCE_BREAK.sub(r"\1 \2", text)
    return text


def strip_page_markers(text: str) -> str:
    """Remove remaining page number artifacts."""
    # "Page N" on its own line
    text = re.sub(r"^\s*Page\s+\d+\s*$", "", text, flags=re.MULTILINE)
    # Bare numbers on their own line (page numbers that leaked through)
    text = re.sub(r"^\s*\d{1,3}\s*$", "", text, flags=re.MULTILINE)
    return text


def fix_spacing(text: str) -> str:
    """Normalize spacing."""
    # Collapse 3+ blank lines to 2
    text = re.sub(r"\n{4,}", "\n\n\n", text)
    # Remove trailing whitespace on lines
    text = re.sub(r"[ \t]+$", "", text, flags=re.MULTILINE)
    return text


def cleanup_file(filepath: Path) -> None:
    """Apply all cleanup transformations to a chapter file."""
    text = filepath.read_text(encoding="utf-8")

    # Split frontmatter from content
    parts = text.split("---", 2)
    if len(parts) >= 3:
        frontmatter = f"---{parts[1]}---\n\n"
        content = parts[2]
    else:
        frontmatter = ""
        content = text

    content = fix_headings(content)
    content = join_broken_paragraphs(content)
    content = strip_page_markers(content)
    content = fix_spacing(content)

    filepath.write_text(frontmatter + content.strip() + "\n", encoding="utf-8")


def main():
    files = sorted(OUTPUT_DIR.glob("*.md"))
    print(f"Cleaning up {len(files)} files in {OUTPUT_DIR}/")

    for filepath in files:
        print(f"  {filepath.name}...", end=" ", flush=True)
        cleanup_file(filepath)
        print("OK")

    print("Done.")


if __name__ == "__main__":
    main()

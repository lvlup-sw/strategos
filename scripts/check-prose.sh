#!/usr/bin/env bash
# -----------------------------------------------------------------------
# check-prose.sh
#
# DIM-8 grep gate. Scans project markdown (src/**/*.md, docs/**/*.md,
# root *.md) for AI-vocabulary cluster terms drawn from Wikipedia's
# "Signs of AI writing" guide (the catalog used by ~/.claude/skills/
# humanize/references/ai-writing-patterns.md, section 7 "Overused AI
# vocabulary words" — supplemented with a small set of high-signal tells
# from sections 4 and 8).
#
# The detection rule is per-paragraph (blank-line-separated text block):
# a paragraph with ≥3 hits across the term list trips the gate. This
# threshold was tuned against the current tree (2026-05-18) — no
# legitimate doc paragraph in src/**, docs/**, or root *.md trips it.
# The aim is to gate NEW slop, not retroactively reject existing prose.
#
# Decisions documented inline (per T-023 substep 5):
#   - Term list lives inline in this script (option A). No external
#     reference file — single point of edit, no path indirection.
#   - Detection is per-paragraph, not per-file. A density rule fires on
#     a single dense block, not a long doc with a sprinkle of hits.
#   - The catalog is a tight subset of high-signal AI tells. Words like
#     "key" and "important" are deliberately excluded — too common in
#     legitimate technical writing to gate on.
#
# Exit codes:
#   0  no violations
#   1  violations detected (file:paragraph#:hits on stderr)
# -----------------------------------------------------------------------
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# Inline term list. Each line is a regex alternative (already lowercased,
# matched case-insensitive). Word boundaries are added in the awk script.
TERMS=(
  # Section 7 — overused AI vocabulary
  delve delves delving
  tapestry tapestries
  testament
  underscore underscores underscoring underscored
  pivotal
  garner garners garnered garnering
  intricate intricacies
  interplay
  # Section 4 — promotional / advertisement-like
  seamlessly seamless
  leverage leverages leveraging leveraged
  vibrant
  groundbreaking
  breathtaking
  # Section 8 — copula-avoidance puff verbs (only when used figuratively;
  # we accept the over-trip risk because clustering with the others is
  # the real signal)
  boasts
)

TERM_RE="$(IFS='|'; echo "${TERMS[*]}")"

# Discover targets: root *.md, src/**/*.md, docs/**/*.md.
mapfile -t FILES < <(
  {
    find "$REPO_ROOT" -maxdepth 1 -type f -name '*.md'
    find "$REPO_ROOT/src" -type f -name '*.md' 2>/dev/null
    find "$REPO_ROOT/docs" -type f -name '*.md' 2>/dev/null
  } | sort -u
)

violations=""

for file in "${FILES[@]}"; do
  hits="$(
    awk -v term_re="($TERM_RE)" '
      BEGIN {
        IGNORECASE = 1;
        para_idx = 1;
        para = "";
      }
      function flush(    text, count, lc, padded) {
        text = para;
        count = 0;
        lc = tolower(text);
        # Pad with newlines so the [^a-z0-9_] boundary guard is satisfied even for
        # terms anchored at the very start/end of the paragraph. Scanning the padded
        # text unconditionally means edge terms are counted whether or not interior
        # matches already exist (a count==0 guard would have skipped them).
        padded = "\n" lc "\n";
        while (match(padded, "[^a-z0-9_]" term_re "[^a-z0-9_]")) {
          count++;
          padded = substr(padded, RSTART + RLENGTH);
        }
        if (count >= 3) {
          printf("paragraph#%d: %d hits\n", para_idx, count);
        }
        para = "";
        para_idx++;
      }
      {
        if ($0 ~ /^[[:space:]]*$/) {
          if (length(para) > 0) flush();
        } else {
          if (length(para) > 0) para = para "\n" $0;
          else                  para = $0;
        }
      }
      END {
        if (length(para) > 0) flush();
      }
    ' "$file"
  )"
  if [[ -n "$hits" ]]; then
    while IFS= read -r line; do
      violations+="$file: $line"$'\n'
    done <<< "$hits"
  fi
done

if [[ -n "$violations" ]]; then
  echo "check-prose: AI-vocabulary cluster detected (≥3 hits in a single paragraph)." >&2
  echo "Source list: ~/.claude/skills/humanize/references/ai-writing-patterns.md" >&2
  echo "$violations" >&2
  exit 1
fi

echo "check-prose: OK (no AI-vocabulary clusters)."
exit 0

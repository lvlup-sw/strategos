#!/usr/bin/env bash
# -----------------------------------------------------------------------
# check-agag-hygiene.sh
#
# DR-7 grep gate. Production code in src/Strategos.Agents/ must reference
# the AgentDiagnostics constants by name (e.g. AgentDiagnostics.AGAG002)
# rather than inlining the literal string "AGAG###". The only files
# allowed to carry the raw quoted literals are the constant definitions
# and the AgentException subclasses (which interpolate the const into
# their formatted messages).
#
# Allowed scopes:
#   - src/Strategos.Agents/Diagnostics/
#   - src/Strategos.Agents/Exceptions/
#
# Detection: matches "AGAG\d+" (quoted string literal). Comments that
# mention AGAG codes without quotes are NOT flagged — documentation is
# fine, the gate exists to prevent silently-typed code paths from drifting
# out of step with the const table.
#
# Exit codes:
#   0  no violations
#   1  violations detected (file:line citations on stderr)
# -----------------------------------------------------------------------
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TARGET_DIR="$REPO_ROOT/src/Strategos.Agents"

if [[ ! -d "$TARGET_DIR" ]]; then
  echo "check-agag-hygiene: target not found: $TARGET_DIR" >&2
  exit 1
fi

# grep -rn with -P to use Perl regex. Match a quoted AGAG literal: "AGAG###".
# Filter out the two allowed scopes by path. POSIX awk does the path filter
# so we don't depend on grep --exclude-dir semantics.
violations="$(
  grep -rnE --include='*.cs' '"AGAG[0-9]+"' "$TARGET_DIR" \
    | awk -F: '
        {
          path = $1;
          if (path ~ /\/Diagnostics\//) next;
          if (path ~ /\/Exceptions\//)  next;
          print $0;
        }
      ' \
    || true
)"

if [[ -n "$violations" ]]; then
  echo "check-agag-hygiene: AGAG string literals must live in Diagnostics/ or Exceptions/." >&2
  echo "Use AgentDiagnostics.AGAG### constants instead. Offending sites:" >&2
  echo "$violations" >&2
  exit 1
fi

echo "check-agag-hygiene: OK (no stray AGAG literals)."
exit 0

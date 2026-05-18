#!/usr/bin/env bash
# -----------------------------------------------------------------------
# check-catch-discipline.sh
#
# DR-10 grep gate. Production code in src/Strategos.Agents/ must not
# silently swallow exceptions:
#
#   - Parameterless catches (`catch {`) are banned outright.
#   - `catch (Exception)` / `catch (Exception ex)` is only allowed when
#     followed within 5 lines by a `throw` (rethrow OR translated throw
#     of a typed AgentException, e.g. `throw new AgentMcpException(...)`).
#
# This mirrors the post-T-010 catch ordering in AgentStepBase.cs and
# StrategosFunctionsChatClient.cs — both files catch Exception only at
# explicit translator boundaries and immediately rethrow, which is the
# DR-10 pattern.
#
# Exit codes:
#   0  no violations
#   1  violations detected (file:line citations on stderr)
# -----------------------------------------------------------------------
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TARGET_DIR="$REPO_ROOT/src/Strategos.Agents"

if [[ ! -d "$TARGET_DIR" ]]; then
  echo "check-catch-discipline: target not found: $TARGET_DIR" >&2
  exit 1
fi

bad=""

# Pass 1 — parameterless catches: `catch {` or `catch\n{` on the same line.
# `catch (...)` is not parameterless; `catch{` (no space) also caught.
pl_hits="$(grep -rnE --include='*.cs' '(^|[[:space:]])catch[[:space:]]*\{' "$TARGET_DIR" || true)"
if [[ -n "$pl_hits" ]]; then
  bad+="parameterless catch (forbidden by DR-10):"$'\n'"$pl_hits"$'\n'
fi

# Pass 2 — bare `catch (Exception)` / `catch (Exception ex)` without a
# trailing `throw` within the next 5 lines. We walk every .cs file under
# the target dir; awk does the look-ahead. The "next 5 lines" window is a
# heuristic — over-permissive (a throw 10 lines down counts as discipline);
# the goal is to surface the silent-swallow case where catch (Exception)
# does NOT throw at all.
while IFS= read -r -d '' file; do
  hits="$(
    awk -v rel="$file" '
      {
        line[NR] = $0;
      }
      END {
        for (i = 1; i <= NR; i++) {
          if (match(line[i], /catch[[:space:]]*\([[:space:]]*Exception([[:space:]][_a-zA-Z][_a-zA-Z0-9]*)?[[:space:]]*\)/)) {
            saw_throw = 0;
            limit = i + 5;
            if (limit > NR) limit = NR;
            for (j = i; j <= limit; j++) {
              if (line[j] ~ /(^|[^A-Za-z0-9_])throw([^A-Za-z0-9_]|$)/) {
                saw_throw = 1;
                break;
              }
            }
            if (!saw_throw) {
              printf("%s:%d:%s\n", rel, i, line[i]);
            }
          }
        }
      }
    ' "$file"
  )"
  if [[ -n "$hits" ]]; then
    bad+="bare catch (Exception) without throw (DR-10 swallow):"$'\n'"$hits"$'\n'
  fi
done < <(find "$TARGET_DIR" -type f -name '*.cs' -print0)

if [[ -n "$bad" ]]; then
  echo "check-catch-discipline: DR-10 violations detected." >&2
  echo "$bad" >&2
  exit 1
fi

echo "check-catch-discipline: OK (no parameterless or silent catches)."
exit 0

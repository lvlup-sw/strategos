#!/usr/bin/env bash
# -----------------------------------------------------------------------
# build-drift-issue.sh
#
# #51 builder API-stability gate (PR-B), task T13/T14 — cross-repo auto-issue
# payload builder.
#
# When src/Strategos/PublicAPI/PublicAPI.Shipped.txt diverges from the previous
# release tag, this script files a tracking issue on lvlup-sw/exarchos so the
# downstream strategos-api-mirror.test.ts mirror can re-baseline deliberately.
#
# It is the testable unit underneath .github/workflows/public-api-drift.yml:
#   * --dry-run         : print the `gh issue create` invocation, do not run it
#                         (no token needed) — used by DriftPayloadTests.
#   * (mocked gh)       : if a `gh` shim is on PATH, the real branch runs it and
#                         the shim asserts --repo/--label/body — used by the
#                         T14 mocked-gh dry-run job (DriftDryRunTests).
#
# Fail-soft on the secret (design §6.3): if EXARCHOS_ISSUES_PAT is absent, warn
# and exit 0 — NEVER fail the `main` push.
#
# Usage:
#   build-drift-issue.sh --diff-url <url> [--dry-run]
# -----------------------------------------------------------------------
set -uo pipefail

DIFF_URL=""
DRY_RUN=0

while [ $# -gt 0 ]; do
  case "$1" in
    --diff-url)
      DIFF_URL="${2:-}"
      shift 2
      ;;
    --dry-run)
      DRY_RUN=1
      shift
      ;;
    *)
      echo "build-drift-issue.sh: unknown argument '$1'" >&2
      exit 2
      ;;
  esac
done

if [ -z "${DIFF_URL}" ]; then
  echo "build-drift-issue.sh: --diff-url is required" >&2
  exit 2
fi

# Cross-product issue coordinates (verbatim — exarchos triage filters on these).
REPO="lvlup-sw/exarchos"
LABEL="cross-product:strategos"
TITLE="Strategos builder public API drifted — re-baseline strategos-api-mirror"
BODY="The Strategos builder public surface (the 7 Strategos.Builders interfaces) changed on a push to main.

Baseline diff: ${DIFF_URL}

The PublicAPI.Shipped.txt baseline that strategos-api-mirror.test.ts mirrors has changed. Re-baseline the mirror against the new signatures and confirm no unintended cross-product break.

Filed automatically by lvlup-sw/strategos .github/workflows/public-api-drift.yml (#51)."

# The fully-formed gh invocation. Kept on a single logical line so the dry-run
# output and the executed command are byte-identical in their flags.
GH_ARGS=(issue create --repo "${REPO}" --label "${LABEL}" --title "${TITLE}" --body "${BODY}")

if [ "${DRY_RUN}" -eq 1 ]; then
  # Print a human-readable, assertion-friendly rendering of the command.
  echo "DRY RUN — would execute:"
  echo "gh issue create --repo ${REPO} --label ${LABEL} --title \"${TITLE}\" --body \"<diff link: ${DIFF_URL}>\""
  exit 0
fi

# Fail-soft secret gate (design §6.3).
if [ -z "${EXARCHOS_ISSUES_PAT:-}" ]; then
  echo "EXARCHOS_ISSUES_PAT secret absent — skipping cross-repo notify (warn, not fail)." >&2
  echo "PAT absent: cross-repo issue was NOT filed; this is non-fatal on main."
  exit 0
fi

# Real path: invoke gh (the live workflow, or a mocked gh shim on PATH in the
# T14 dry-run job). GH_TOKEN authenticates gh against the exarchos repo.
GH_TOKEN="${EXARCHOS_ISSUES_PAT}" gh "${GH_ARGS[@]}"

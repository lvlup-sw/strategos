#!/usr/bin/env bash
# -----------------------------------------------------------------------
# check-unshipped-against-tag.sh
#
# PublicAPI baseline placement gate for src/Strategos (PR #206 follow-up).
#
# Convention (documented in src/Strategos/.editorconfig and
# BuilderApiBaselineTests.cs): PublicAPI.Shipped.txt lists members that exist
# in the last NuGet release (the newest v* tag); PublicAPI.Unshipped.txt lists
# members added since. The PublicApiAnalyzers accept a member declared in
# EITHER file, so nothing in the build stops a member that shipped in v2.10.0
# from being listed as "unshipped" (which then rolls into the next release's
# API notes as new), or a new member from being written straight into Shipped.
#
# This script fails when a line in PublicAPI.Unshipped.txt already shipped:
#   1. the same line is present in the tag's PublicAPI.Shipped.txt, or
#   2. the member is declared in the tag's source for one of the files the
#      .editorconfig re-enable block brought into analyzer scope after that
#      release (StepContext.cs, CompensationConfiguration.cs, StepDefinition.cs),
#      where the tag's Shipped.txt could not have listed it yet.
# For (2) a property line (`.get` / `.init` / `.set`) matches on the member
# name; a method line matches on the name plus the parameter type list, so an
# overload added after the tag is not mistaken for the one that shipped.
#
# Usage: check-unshipped-against-tag.sh [tag]
#   tag defaults to the newest v* tag reachable from HEAD (git describe).
# Exit 0 = every Unshipped line is genuinely unshipped; 1 = misplaced line(s);
# 2 = indeterminate (no tag, or the baseline files are unreadable).
# -----------------------------------------------------------------------
set -uo pipefail

UNSHIPPED='src/Strategos/PublicAPI/PublicAPI.Unshipped.txt'
SHIPPED='src/Strategos/PublicAPI/PublicAPI.Shipped.txt'
SCOPED_SOURCES=(
  'src/Strategos/Steps/StepContext.cs'
  'src/Strategos/Definitions/CompensationConfiguration.cs'
  'src/Strategos/Definitions/StepDefinition.cs'
)

tag="${1:-$(git describe --tags --abbrev=0 --match 'v*' 2>/dev/null || true)}"
if [ -z "${tag}" ]; then
  echo "::error::check-unshipped-against-tag: no v* tag reachable from HEAD; result indeterminate." >&2
  exit 2
fi
if [ ! -f "${UNSHIPPED}" ] || [ ! -f "${SHIPPED}" ]; then
  echo "::error::check-unshipped-against-tag: baseline files missing; result indeterminate." >&2
  exit 2
fi

tag_shipped="$(git show "${tag}:${SHIPPED}" 2>/dev/null || true)"
if [ -z "${tag_shipped}" ]; then
  echo "::error::check-unshipped-against-tag: ${SHIPPED} not found at ${tag}; result indeterminate." >&2
  exit 2
fi

# Concatenate the tag's re-enabled sources (a file absent at the tag is simply empty).
tag_sources=""
for src in "${SCOPED_SOURCES[@]}"; do
  tag_sources+="$(git show "${tag}:${src}" 2>/dev/null || true)"$'\n'
done

# Map a baseline type name to the source file it lives in at the tag; only the
# re-enabled files are inspected, so unrelated types are skipped.
type_in_scope() {
  case "$1" in
    Strategos.Steps.StepContext|Strategos.Definitions.CompensationConfiguration|Strategos.Definitions.StepDefinition) return 0 ;;
    *) return 1 ;;
  esac
}

# Reduce a fully-qualified parameter type to the simple name a C# declaration
# would use: strip namespaces, nullability markers and `!`/`?` annotations.
simple_type() {
  local t="$1"
  t="${t%%\!}"; t="${t%%\?}"
  t="${t##*.}"
  case "${t}" in
    String) t='string' ;;
    Int32) t='int' ;;
    Int64) t='long' ;;
    Boolean) t='bool' ;;
  esac
  printf '%s' "${t}"
}

status=0
while IFS= read -r line; do
  [ -z "${line}" ] && continue
  case "${line}" in \#*) continue ;; esac

  if grep -qxF -- "${line}" <<<"${tag_shipped}"; then
    echo "::error file=${UNSHIPPED}::already shipped at ${tag} (present in that tag's PublicAPI.Shipped.txt): ${line}"
    status=1
    continue
  fi

  decl="${line#static }"
  decl="${decl%% -> *}"
  if [[ "${decl}" == *'('* ]]; then
    # Method: Type.Name<...>(params)
    head="${decl%%(*}"
    params="${decl#*(}"; params="${params%)}"
    member="${head##*.}"; member="${member%%<*}"
    type="${head%.*}"
    type="${type%%<*}"
    type_in_scope "${type}" || continue
    # Build a regex: Name( <type1> ident , <type2> ident ... )
    regex="[[:space:]]${member}[[:space:]]*(<[^>]*>)?[[:space:]]*\\("
    if [ -n "${params}" ]; then
      first=1
      IFS=',' read -ra parts <<<"${params}"
      for part in "${parts[@]}"; do
        part="${part# }"
        ptype="${part%% *}"
        [ "${ptype}" = "out" ] && { part="${part#out }"; ptype="${part%% *}"; }
        stype="$(simple_type "${ptype}")"
        if [ "${first}" -eq 1 ]; then first=0; else regex+="[[:space:]]*,"; fi
        regex+="[[:space:]]*(out[[:space:]]+)?(this[[:space:]]+)?([A-Za-z_.]*\\.)?${stype}[^,)]*"
      done
    else
      regex+="[[:space:]]*"
    fi
    regex+="\\)"
  else
    # Property accessor or type line: Type.Member.get / Type.Member.init / Type
    accessor="${decl##*.}"
    case "${accessor}" in
      get|set|init) ;;
      *) continue ;;   # bare type line: shipped-ness of a type is judged by its members
    esac
    memberpath="${decl%.*}"
    member="${memberpath##*.}"
    type="${memberpath%.*}"
    type="${type%%<*}"
    type_in_scope "${type}" || continue
    regex="[[:space:]]${member}[[:space:]]*\\{"
  fi

  if grep -qE -- "${regex}" <<<"${tag_sources}"; then
    echo "::error file=${UNSHIPPED}::already shipped at ${tag} (declared in that tag's source): ${line}"
    status=1
  fi
done < "${UNSHIPPED}"

if [ "${status}" -ne 0 ]; then
  echo "------------------------------------------------------------------"
  echo "PublicAPI.Unshipped.txt lists members that already shipped in ${tag}."
  echo "Move each flagged line to PublicAPI.Shipped.txt (Shipped = present in the last release)."
  echo "------------------------------------------------------------------"
  exit 1
fi

echo "==> PublicAPI.Unshipped.txt holds only members added since ${tag}."

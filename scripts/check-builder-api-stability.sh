#!/usr/bin/env bash
# -----------------------------------------------------------------------
# check-builder-api-stability.sh
#
# #51 builder API-stability gate (PR-B), task T11 — fail-closed CI step.
#
# Builds src/Strategos with Microsoft.CodeAnalysis.PublicApiAnalyzers. The
# historical 7 Strategos.Builders entrypoints remain the downstream mirror's
# named subset. The local allowlist also covers the 3 continuation interfaces
# changed by #167, WorkflowActionReference, and its StepDefinition carrier. Any
# change to that public surface without a matching PublicAPI.Unshipped.txt entry
# raises RS0016/RS0017 and the build fails.
#
# On such a failure this script prints the cross-product remediation protocol
# VERBATIM (the exarchos strategos-api-mirror.test.ts consumer depends on this
# message being stable). Exit non-zero so CI fails closed.
# -----------------------------------------------------------------------
set -uo pipefail

PROJECT="${1:-src/Strategos/Strategos.csproj}"

# The verbatim remediation protocol. Keep this string byte-for-byte stable:
# it is the named protocol referenced by CONTRIBUTING.md, the
# IWorkflowBuilder<TState> doc-comment, and the CHANGELOG "Cross-product
# breaking changes" section.
REMEDIATION='Update PublicAPI.Unshipped.txt and add a CHANGELOG entry under Cross-product breaking changes.'

echo "==> Building ${PROJECT} with PublicApiAnalyzers (builder API-stability gate)"
build_log="$(mktemp)"
# MSBuild's parallel restore graph intermittently exits 1 without diagnostics in this
# repository. Keep the fail-closed gate deterministic so an actual PublicApiAnalyzer
# diagnostic, rather than restore scheduling, controls the result.
dotnet build "${PROJECT}" --configuration Release /warnaserror -m:1 2>&1 | tee "${build_log}"
status="${PIPESTATUS[0]}"

if [ "${status}" -ne 0 ]; then
  if grep -qE 'RS001[67]|RS0036|RS0037|RS0041' "${build_log}"; then
    echo ""
    echo "::error title=Builder public API drift::${REMEDIATION}"
    echo "------------------------------------------------------------------"
    echo "Builder public API drift detected (RS0016/RS0017)."
    echo "${REMEDIATION}"
    echo "------------------------------------------------------------------"
    echo "The allowlisted Strategos API is a cross-product contract. Its"
    echo "historical 7-entrypoint subset is mirrored by exarchos's"
    echo "strategos-api-mirror.test.ts. A breaking change must be declared"
    echo "in the baseline and the CHANGELOG so consumers can re-baseline"
    echo "deliberately."
  fi
  rm -f "${build_log}"
  exit "${status}"
fi

rm -f "${build_log}"
echo "==> Builder public API stable against baseline."

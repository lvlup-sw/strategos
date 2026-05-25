#!/usr/bin/env bash
# -----------------------------------------------------------------------
# check-builder-api-stability.sh
#
# #51 builder API-stability gate (PR-B), task T11 — fail-closed CI step.
#
# Builds src/Strategos with Microsoft.CodeAnalysis.PublicApiAnalyzers. The 7
# Strategos.Builders interfaces are baselined in
# src/Strategos/PublicAPI/PublicAPI.Shipped.txt (INV-1: builder surface only,
# scoped via PublicApi.globalconfig + .editorconfig). Any change to a builder
# signature with no matching PublicAPI.Unshipped.txt entry raises RS0016/RS0017
# and the build fails.
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
dotnet build "${PROJECT}" --configuration Release /warnaserror 2>&1 | tee "${build_log}"
status="${PIPESTATUS[0]}"

if [ "${status}" -ne 0 ]; then
  if grep -qE 'RS001[67]|RS0036|RS0037|RS0041' "${build_log}"; then
    echo ""
    echo "::error title=Builder public API drift::${REMEDIATION}"
    echo "------------------------------------------------------------------"
    echo "Builder public API drift detected (RS0016/RS0017)."
    echo "${REMEDIATION}"
    echo "------------------------------------------------------------------"
    echo "The 7 Strategos.Builders interfaces are a cross-product contract"
    echo "mirrored by exarchos's strategos-api-mirror.test.ts. A breaking"
    echo "change must be declared in the baseline and the CHANGELOG so the"
    echo "downstream mirror can re-baseline deliberately."
  fi
  rm -f "${build_log}"
  exit "${status}"
fi

rm -f "${build_log}"
echo "==> Builder public API stable against baseline."

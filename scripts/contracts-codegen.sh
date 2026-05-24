#!/usr/bin/env bash
# =============================================================================
# Strategos.Contracts code generation — the single regeneration entry point.
#
# 1. tsp compile  : TypeSpec (canonical) -> JSON Schema in schemas/json-schema/
# 2. ContractsCodegen : JSON Schema -> sealed records in Generated/*.g.cs
#
# Reused by the local build and by the codegen-guard CI workflow, which runs
# this then `git diff --exit-code` over schemas/ + Generated/ to reject any
# hand-edit of emitter-owned output (DIM-6).
# =============================================================================
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTRACTS_DIR="$REPO_ROOT/src/Strategos.Contracts"
CODEGEN_PROJ="$REPO_ROOT/src/Strategos.Contracts.Codegen/Strategos.Contracts.Codegen.csproj"

cd "$CONTRACTS_DIR"

if [ ! -d node_modules/@typespec ]; then
  echo "[contracts-codegen] restoring node toolchain..."
  npm install --no-audit --no-fund
fi

echo "[contracts-codegen] tsp compile ..."
# Clean stale emitted schemas so a removed/renamed TypeSpec model does not
# linger as an orphan JSON Schema (the json-schema emitter does not prune its
# own output). This keeps the codegen-guard diff honest: a deletion in TypeSpec
# propagates to schemas/ + Generated/ in one regeneration.
rm -f "$CONTRACTS_DIR/schemas/json-schema/"*.json
npx tsp compile .

echo "[contracts-codegen] bundling workflow IR schema ..."
# Inline the per-model workflow schemas reachable from WorkflowDefinitionV1 into
# a single self-contained workflow-definition-v1.schema.json (stable $id) — the
# equivalence-gate target every #53 fixture validates against (T17/T23).
node scripts/bundle-workflow-schema.mjs

echo "[contracts-codegen] emitting C# records ..."
dotnet run --project "$CODEGEN_PROJ" -- \
  "$CONTRACTS_DIR/schemas/json-schema" \
  "$CONTRACTS_DIR/Generated"

echo "[contracts-codegen] done."

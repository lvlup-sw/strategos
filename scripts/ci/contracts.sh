#!/usr/bin/env bash
# Regenerate the TypeSpec outputs, reject any drift from the committed files,
# and run the Contracts tests with the Node toolchain they drive.
source "$(dirname "$0")/lib.sh"

full_history
(cd src/Strategos.Contracts && npm ci)
bash scripts/contracts-codegen.sh
drift=$(git status --porcelain -- src/Strategos.Contracts/Generated src/Strategos.Contracts/schemas \
  src/Strategos.Ontology/Contracts/Generated docs/diagnostics)
if [[ -n "$drift" ]]; then
  echo "$drift" >&2
  git --no-pager diff --stat >&2
  die 'committed TypeSpec generated outputs differ; run bash scripts/contracts-codegen.sh and commit the result'
fi
dotnet run -c Release --project tests/Strategos.Contracts.Tests/Strategos.Contracts.Tests.csproj

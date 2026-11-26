#!/usr/bin/env bash
# Gate the published builder API surface and the Unshipped baseline placement,
# which diffs against the last v* tag.
source "$(dirname "$0")/lib.sh"

full_history
bash scripts/check-builder-api-stability.sh src/Strategos/Strategos.csproj \
  src/Strategos.Ontology/Strategos.Ontology.csproj
bash scripts/check-unshipped-against-tag.sh

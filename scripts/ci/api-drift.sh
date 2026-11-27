#!/usr/bin/env bash
# Record public API lines added or removed against the base in
# ci-out/api-drift.txt. This step reports drift; it never fails on it.
source "$(dirname "$0")/lib.sh"

base=$(base_sha)
mkdir -p "$OUT"
drift=$(git diff --no-color "$base" HEAD -- \
  src/Strategos/PublicAPI/PublicAPI.Shipped.txt \
  src/Strategos/PublicAPI/PublicAPI.Unshipped.txt \
  src/Strategos.Ontology/PublicAPI.Shipped.txt \
  src/Strategos.Ontology/PublicAPI.Unshipped.txt \
  | grep -E '^[-+](static )?Strategos\.' || true)
if [[ -n "$drift" ]]; then
  printf '%s\n' "${drift:0:48000}" >"$OUT/api-drift.txt"
  echo "Public API drift written to ci-out/api-drift.txt ($(wc -l <<<"$drift") lines)"
else
  echo 'No public API drift'
fi

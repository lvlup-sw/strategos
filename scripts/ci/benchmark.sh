#!/usr/bin/env bash
# Run the benchmarks that cover the changed areas, or all of them when no
# changed path names a benchmarked area. The pipeline marks this step soft_fail.
source "$(dirname "$0")/lib.sh"

full_history
changed=$(changed_files)
filters=()
while IFS= read -r path; do
  IFS=/ read -r -a parts <<<"$path"
  for part in "${parts[@]}"; do
    case "$part" in
      Selection) filters+=('*ThompsonSampling*' '*Selection*') ;;
      LoopDetection) filters+=('*LoopDetection*' '*Loop*') ;;
      Ledgers) filters+=('*Ledger*') ;;
      Budget) filters+=('*Budget*' '*Workflow*') ;;
      Rag) filters+=('*VectorSearch*' '*Search*') ;;
    esac
  done
done <<<"$changed"
if [[ ${#filters[@]} -eq 0 ]]; then
  filters=('*')
else
  mapfile -t filters < <(printf '%s\n' "${filters[@]}" | LC_ALL=C sort -u)
fi

mkdir -p "$OUT"
dotnet run -c Release --project src/Strategos.Benchmarks -- \
  --filter "${filters[@]}" --exporters json markdown --artifacts "$OUT/benchmark-results"

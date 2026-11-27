#!/usr/bin/env bash
# Pack the solution and prove consumers build against the packages, not src/.
source "$(dirname "$0")/lib.sh"

full_history
packages=$(mktemp -d)
feed=$(mktemp -d)
dotnet pack strategos.slnx -c Release -o "$packages"
bash scripts/verify-generator-consumer-build.sh "$packages"
bash scripts/pack-to-local-feed.sh "$feed"
bash scripts/verify-basileus-smoke.sh "$feed"

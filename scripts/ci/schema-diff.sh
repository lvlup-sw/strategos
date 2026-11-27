#!/usr/bin/env bash
# Classify the candidate Contracts schemas against the latest stable package on
# nuget.org and against the base. Breaking changes need a ContractsVersion bump
# and an entry in the breaking-changes allowlist.
source "$(dirname "$0")/lib.sh"

PROJECT=src/Strategos.Contracts/Strategos.Contracts.csproj
SCHEMAS=src/Strategos.Contracts/schemas
ALLOWLIST=src/Strategos.Contracts/schemas/breaking-changes.allowlist.json
PACKAGE=levelup.strategos.contracts
FEED=https://api.nuget.org/v3-flatcontainer/$PACKAGE
STABLE='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'

contracts_version() {
  local version
  version=$(sed -n 's:.*<ContractsVersion>\(.*\)</ContractsVersion>.*:\1:p')
  version=${version%%$'\n'*}
  [[ "$version" =~ $STABLE ]] || die "cannot classify stable ContractsVersion '$version'"
  echo "$version"
}

base=$(base_sha)
candidate=$(contracts_version <"$PROJECT")
baseline=$(git show "$base:$PROJECT" | contracts_version)

work=$(mktemp -d)
trap 'rm -rf -- "$work"' EXIT
latest=$(curl -fsSL --max-time 30 "$FEED/index.json" \
  | jq -r '.versions[]' | grep -E "$STABLE" | sort -V | tail -n 1)
[[ -n "$latest" ]] || die "no stable $PACKAGE version is published"
curl -fsSL --max-time 120 --max-filesize $((64 * 1024 * 1024)) \
  -o "$work/package.nupkg" "$FEED/$latest/$PACKAGE.$latest.nupkg"
echo "Published Contracts $latest, SHA-256 $(sha256sum "$work/package.nupkg" | cut -d' ' -f1)"

unzip -q "$work/package.nupkg" 'contentFiles/any/any/schemas/*' -d "$work/package" \
  || die 'the published package has no schema payload'
mkdir "$work/base"
git archive "$base" "$SCHEMAS" | tar -x -C "$work/base"

node scripts/contracts-schema-diff.mjs "$work/package/contentFiles/any/any/schemas" "$SCHEMAS" \
  "$latest" "$candidate" --allowlist "$ALLOWLIST"
node scripts/contracts-schema-diff.mjs "$work/base/$SCHEMAS" "$SCHEMAS" \
  "$baseline" "$candidate" --allowlist "$ALLOWLIST" --allow-equal-versions

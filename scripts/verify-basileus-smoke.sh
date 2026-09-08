#!/usr/bin/env bash
# -----------------------------------------------------------------------
# verify-basileus-smoke.sh
#
# Restores and runs the Basileus package-consumer smoke test from the local
# feed produced by pack-to-local-feed.sh. The restore uses a new, empty NuGet
# global-packages directory and isolated build outputs on every invocation. It
# then compares every restored LevelUp.Strategos nupkg byte-for-byte with the
# freshly packed local feed artifact, so a runner-wide cache, stale project
# assets, or a public package cannot satisfy the release-readiness gate.
#
# Usage:
#   scripts/verify-basileus-smoke.sh [feed-dir]
# -----------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
FEED_DIR="${1:-$REPO_ROOT/local-feed}"
SMOKE_PROJECT="$REPO_ROOT/tests/basileus-smoke/Basileus.Smoke.Tests/Basileus.Smoke.Tests.csproj"

if [[ ! -d "$FEED_DIR" ]]; then
  echo "ERROR: local package feed not found at $FEED_DIR" >&2
  exit 1
fi
if [[ ! -f "$SMOKE_PROJECT" ]]; then
  echo "ERROR: Basileus smoke project not found at $SMOKE_PROJECT" >&2
  exit 1
fi

FEED_DIR_ABS="$(cd "$FEED_DIR" && pwd)"
SMOKE_WORK_DIR="$(mktemp -d -t strategos-basileus-smoke.XXXXXX)"

cleanup() {
  # SMOKE_WORK_DIR is an exact path returned by mktemp, never a caller input.
  if [[ -n "${SMOKE_WORK_DIR:-}" && -d "$SMOKE_WORK_DIR" ]]; then
    rm -rf -- "$SMOKE_WORK_DIR"
  fi
}
trap cleanup EXIT HUP INT TERM

SMOKE_GLOBAL_PACKAGES="$SMOKE_WORK_DIR/global-packages"
SMOKE_NUGET_CONFIG="$SMOKE_WORK_DIR/NuGet.Config"
SMOKE_INTERMEDIATE_OUTPUT="$SMOKE_WORK_DIR/obj/"
SMOKE_BINARY_OUTPUT="$SMOKE_WORK_DIR/bin/"
mkdir -p "$SMOKE_GLOBAL_PACKAGES" "$SMOKE_INTERMEDIATE_OUTPUT" "$SMOKE_BINARY_OUTPUT"

if [[ -n "$(find "$SMOKE_GLOBAL_PACKAGES" -mindepth 1 -print -quit)" ]]; then
  echo "ERROR: fresh smoke global-packages directory is unexpectedly non-empty." >&2
  exit 1
fi

cat > "$SMOKE_NUGET_CONFIG" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="strategos-local" value="$FEED_DIR_ABS" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="strategos-local">
      <package pattern="LevelUp.Strategos" />
      <package pattern="LevelUp.Strategos.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

package_metadata_value() {
  local package_path="$1"
  local element_name="$2"

  unzip -p "$package_path" '*.nuspec' \
    | tr -d '\r' \
    | sed -n "s:.*<$element_name>\([^<]*\)</$element_name>.*:\1:p" \
    | head -1
}

find_exact_feed_package() {
  local expected_id="$1"
  local candidates=()
  local package_path
  local package_id

  while IFS= read -r package_path; do
    [[ "$package_path" == *.snupkg ]] && continue
    package_id="$(package_metadata_value "$package_path" id)"
    if [[ "$package_id" == "$expected_id" ]]; then
      candidates+=("$package_path")
    fi
  done < <(find "$FEED_DIR_ABS" -maxdepth 1 -type f -name '*.nupkg' -print | sort)

  if [[ ${#candidates[@]} -ne 1 ]]; then
    echo "FAIL: expected exactly one $expected_id nupkg in $FEED_DIR_ABS; found ${#candidates[@]}." >&2
    return 1
  fi

  printf '%s\n' "${candidates[0]}"
}

verify_restored_artifact() {
  local package_id="$1"
  local source_package="$2"
  local package_version
  local lowercase_id
  local lowercase_version
  local restored_dir
  local restored_package
  local metadata_file

  package_version="$(package_metadata_value "$source_package" version)"
  lowercase_id="$(printf '%s' "$package_id" | tr '[:upper:]' '[:lower:]')"
  lowercase_version="$(printf '%s' "$package_version" | tr '[:upper:]' '[:lower:]')"
  restored_dir="$SMOKE_GLOBAL_PACKAGES/$lowercase_id/$lowercase_version"
  restored_package="$restored_dir/$lowercase_id.$lowercase_version.nupkg"
  metadata_file="$restored_dir/.nupkg.metadata"

  if [[ -z "$package_version" || ! -f "$restored_package" ]]; then
    echo "FAIL: restored $package_id $package_version nupkg is missing from the isolated package directory." >&2
    exit 2
  fi
  if ! cmp -s "$source_package" "$restored_package"; then
    echo "FAIL: restored $package_id $package_version bytes do not match $source_package." >&2
    exit 2
  fi
  if [[ ! -f "$metadata_file" ]] || ! grep -Fq "$FEED_DIR_ABS" "$metadata_file"; then
    echo "FAIL: restored $package_id $package_version does not record $FEED_DIR_ABS as its source." >&2
    exit 2
  fi

  printf '  %s %s  %s\n' "$package_id" "$package_version" "$(sha256sum "$restored_package" | cut -d' ' -f1)"
}

declare -A FEED_PACKAGES
for package_id in \
  'LevelUp.Strategos.Contracts' \
  'LevelUp.Strategos.Identity.Abstractions' \
  'LevelUp.Strategos' \
  'LevelUp.Strategos.Agents'; do
  if ! FEED_PACKAGES["$package_id"]="$(find_exact_feed_package "$package_id")"; then
    exit 2
  fi
done

echo "Restore: $SMOKE_PROJECT"
echo "Isolated global packages: $SMOKE_GLOBAL_PACKAGES"
NUGET_PACKAGES="$SMOKE_GLOBAL_PACKAGES" \
  dotnet restore "$SMOKE_PROJECT" \
    --configfile "$SMOKE_NUGET_CONFIG" \
    --force-evaluate \
    --no-http-cache \
    --nologo \
    -v:m \
    /p:RestorePackagesPath="$SMOKE_GLOBAL_PACKAGES" \
    /p:NuGetPackageRoot="$SMOKE_GLOBAL_PACKAGES" \
    /p:BaseIntermediateOutputPath="$SMOKE_INTERMEDIATE_OUTPUT" \
    /p:BaseOutputPath="$SMOKE_BINARY_OUTPUT"

echo "Verified restored local artifacts (SHA-256):"
for package_id in \
  'LevelUp.Strategos.Contracts' \
  'LevelUp.Strategos.Identity.Abstractions' \
  'LevelUp.Strategos' \
  'LevelUp.Strategos.Agents'; do
  verify_restored_artifact "$package_id" "${FEED_PACKAGES[$package_id]}"
done

NUGET_PACKAGES="$SMOKE_GLOBAL_PACKAGES" \
  dotnet run \
    --project "$SMOKE_PROJECT" \
    --configuration Release \
    --no-restore \
    /p:RestorePackagesPath="$SMOKE_GLOBAL_PACKAGES" \
    /p:NuGetPackageRoot="$SMOKE_GLOBAL_PACKAGES" \
    /p:BaseIntermediateOutputPath="$SMOKE_INTERMEDIATE_OUTPUT" \
    /p:BaseOutputPath="$SMOKE_BINARY_OUTPUT"

echo "OK: Basileus smoke restored the freshly packed Strategos closure and passed from an isolated package directory."

#!/usr/bin/env bash
# -----------------------------------------------------------------------
# pack-to-local-feed.sh
#
# DR-11 / T-022 — release-readiness packaging step.
#
# Packs the complete in-repository dependency closure needed by the
# LevelUp.Strategos.Agents smoke package into a local directory-based NuGet
# feed. The smoke project at tests/basileus-smoke/Basileus.Smoke.Tests can then
# restore from a genuinely fresh feed instead of depending on a developer or
# runner's global package cache.
#
# Usage:
#   scripts/pack-to-local-feed.sh [feed-dir]
#
# Default feed-dir: ./local-feed (relative to the repo root, which is
# expected to be the script invoker's cwd — mirroring how
# scripts/verify-generator-consumer-build.sh is invoked from CI).
#
# Idempotent:
#   * Re-running first removes stale Contracts, Identity.Abstractions,
#     Strategos, and Agents artifacts from feed-dir, then repacks and verifies
#     exactly one artifact at every required identity/version.
#   * The smoke project consumes the feed via its own local
#     tests/basileus-smoke/Basileus.Smoke.Tests/nuget.config — this
#     script intentionally does NOT mutate the user's global
#     NuGet.Config. (Earlier drafts used `dotnet nuget add source`; we
#     dropped it because it leaves persistent state on the host and
#     because the smoke project's nuget.config makes it unnecessary.)
#
# Exit codes:
#   0  the complete closure was packed and its exact metadata was verified
#   1  invalid arguments / setup error
#   2  dependency-closure pack failed
#   3  pack output or dependency metadata did not match the expected closure
#
# Versioning note: Strategos.Agents.csproj is normally MinVer-derived (no
# explicit <Version> pin), using the release tag when present and a prerelease
# version on tagless CI, uniform with its product-line siblings. The smoke gate
# does not care about the real shipping version — it validates the
# basileus-consumed surface against a packed artifact — so Agents is packed at
# a deterministic SYNTHETIC prerelease version here (overriding MinVer for
# Agents only). Contracts keeps its
# independently pinned version; Identity.Abstractions and core Strategos keep
# their MinVer-derived version. All three are packed before Agents so every
# internal dependency declared by the synthetic package is available in a
# brand-new feed.
# -----------------------------------------------------------------------
set -euo pipefail

FEED_DIR="${1:-./local-feed}"
SMOKE_VERSION="2.7.0-smoke"
EXPECTED_NUPKG_NAME="LevelUp.Strategos.Agents.${SMOKE_VERSION}.nupkg"

# Resolve to absolute path so downstream messages are unambiguous, even
# if the script is invoked from a deeper cwd.
mkdir -p "$FEED_DIR"
FEED_DIR_ABS="$(cd "$FEED_DIR" && pwd)"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CONTRACTS_CSPROJ="$REPO_ROOT/src/Strategos.Contracts/Strategos.Contracts.csproj"
IDENTITY_CSPROJ="$REPO_ROOT/src/Strategos.Identity.Abstractions/Strategos.Identity.Abstractions.csproj"
CORE_CSPROJ="$REPO_ROOT/src/Strategos/Strategos.csproj"
AGENTS_CSPROJ="$REPO_ROOT/src/Strategos.Agents/Strategos.Agents.csproj"

for project_path in "$CONTRACTS_CSPROJ" "$IDENTITY_CSPROJ" "$CORE_CSPROJ" "$AGENTS_CSPROJ"; do
  if [[ ! -f "$project_path" ]]; then
    echo "ERROR: required pack project not found at $project_path" >&2
    exit 1
  fi
done

echo "Feed: $FEED_DIR_ABS"

package_metadata_value() {
  local package_path="$1"
  local element_name="$2"

  unzip -p "$package_path" '*.nuspec' \
    | tr -d '\r' \
    | sed -n "s:.*<$element_name>\([^<]*\)</$element_name>.*:\1:p" \
    | head -1
}

package_dependency_version() {
  local package_path="$1"
  local dependency_id="$2"

  unzip -p "$package_path" '*.nuspec' \
    | tr -d '\r' \
    | sed -n "s:.*<dependency id=\"$dependency_id\" version=\"\([^\"]*\)\".*:\1:p" \
    | head -1
}

find_exact_package() {
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
    echo "expected exactly one $expected_id nupkg in $FEED_DIR_ABS; found ${#candidates[@]}" >&2
    return 1
  fi

  printf '%s\n' "${candidates[0]}"
}

remove_stale_package_artifacts() {
  local package_id="$1"
  local artifact_path
  local artifact_name

  while IFS= read -r artifact_path; do
    artifact_name="$(basename "$artifact_path")"
    case "$artifact_name" in
      "$package_id".[0-9]*.nupkg|"$package_id".[0-9]*.snupkg)
        rm -f "$artifact_path"
        ;;
    esac
  done < <(find "$FEED_DIR_ABS" -maxdepth 1 -type f \
    \( -name "$package_id.*.nupkg" -o -name "$package_id.*.snupkg" \) -print)
}

pack_project() {
  local project_path="$1"
  local package_id="$2"
  shift 2

  echo "Pack: $project_path"
  if ! dotnet pack "$project_path" \
         -c Release \
         -o "$FEED_DIR_ABS" \
         --nologo \
         --disable-build-servers \
         -m:1 \
         -v:m \
         "$@"; then
    echo "FAIL: dotnet pack of $package_id failed." >&2
    exit 2
  fi
}

assert_exact_artifact() {
  local package_id="$1"
  local package_version="$2"
  local package_path="$3"
  local expected_path="$FEED_DIR_ABS/$package_id.$package_version.nupkg"

  if [[ "$package_path" != "$expected_path" || ! -f "$expected_path" ]]; then
    echo "FAIL: expected exact artifact $expected_path; resolved $package_path." >&2
    exit 3
  fi
}

assert_dependency_version() {
  local package_path="$1"
  local dependency_id="$2"
  local expected_version="$3"
  local actual_version

  actual_version="$(package_dependency_version "$package_path" "$dependency_id")"
  if [[ "$actual_version" != "$expected_version" ]]; then
    echo "FAIL: $package_path declares $dependency_id '$actual_version'; expected '$expected_version'." >&2
    exit 3
  fi
}

# Delete only the four closure identities. The version-leading filename guard
# keeps LevelUp.Strategos cleanup from matching sibling packages such as
# LevelUp.Strategos.Agents.
for package_id in \
  'LevelUp.Strategos.Contracts' \
  'LevelUp.Strategos.Identity.Abstractions' \
  'LevelUp.Strategos' \
  'LevelUp.Strategos.Agents'; do
  remove_stale_package_artifacts "$package_id"
done

# Serialize MSBuild and disable shared build servers for every closure member;
# the gate must be controlled by artifact/API correctness, not restore
# scheduling. AgentsSmokeVersion is consumed only by Strategos.Agents.csproj.
pack_project "$CONTRACTS_CSPROJ" 'LevelUp.Strategos.Contracts'
pack_project "$IDENTITY_CSPROJ" 'LevelUp.Strategos.Identity.Abstractions'
pack_project "$CORE_CSPROJ" 'LevelUp.Strategos'
pack_project "$AGENTS_CSPROJ" 'LevelUp.Strategos.Agents' \
  -p:AgentsSmokeVersion="$SMOKE_VERSION"

if ! CONTRACTS_NUPKG="$(find_exact_package 'LevelUp.Strategos.Contracts')" \
  || ! IDENTITY_NUPKG="$(find_exact_package 'LevelUp.Strategos.Identity.Abstractions')" \
  || ! CORE_NUPKG="$(find_exact_package 'LevelUp.Strategos')" \
  || ! AGENTS_NUPKG="$(find_exact_package 'LevelUp.Strategos.Agents')"; then
  echo "FAIL: packed dependency closure is incomplete or ambiguous." >&2
  exit 3
fi

CONTRACTS_VERSION="$(package_metadata_value "$CONTRACTS_NUPKG" version)"
IDENTITY_VERSION="$(package_metadata_value "$IDENTITY_NUPKG" version)"
CORE_VERSION="$(package_metadata_value "$CORE_NUPKG" version)"
AGENTS_VERSION="$(package_metadata_value "$AGENTS_NUPKG" version)"
EXPECTED_CONTRACTS_VERSION="$(sed -n 's:.*<ContractsVersion>\([^<]*\)</ContractsVersion>.*:\1:p' "$CONTRACTS_CSPROJ" | head -1)"

if [[ -z "$CORE_VERSION" || -z "$EXPECTED_CONTRACTS_VERSION" ]]; then
  echo "FAIL: could not resolve expected closure versions." >&2
  exit 3
fi
if [[ "$CONTRACTS_VERSION" != "$EXPECTED_CONTRACTS_VERSION" ]]; then
  echo "FAIL: Contracts version '$CONTRACTS_VERSION' does not match '$EXPECTED_CONTRACTS_VERSION'." >&2
  exit 3
fi
if [[ "$IDENTITY_VERSION" != "$CORE_VERSION" ]]; then
  echo "FAIL: Identity.Abstractions version '$IDENTITY_VERSION' does not match core '$CORE_VERSION'." >&2
  exit 3
fi
if [[ "$AGENTS_VERSION" != "$SMOKE_VERSION" ]]; then
  echo "FAIL: Agents version '$AGENTS_VERSION' does not match smoke version '$SMOKE_VERSION'." >&2
  exit 3
fi

assert_exact_artifact 'LevelUp.Strategos.Contracts' "$CONTRACTS_VERSION" "$CONTRACTS_NUPKG"
assert_exact_artifact 'LevelUp.Strategos.Identity.Abstractions' "$IDENTITY_VERSION" "$IDENTITY_NUPKG"
assert_exact_artifact 'LevelUp.Strategos' "$CORE_VERSION" "$CORE_NUPKG"
assert_exact_artifact 'LevelUp.Strategos.Agents' "$AGENTS_VERSION" "$AGENTS_NUPKG"

assert_dependency_version "$CORE_NUPKG" 'LevelUp.Strategos.Contracts' "$CONTRACTS_VERSION"
assert_dependency_version "$CORE_NUPKG" 'LevelUp.Strategos.Identity.Abstractions' "$IDENTITY_VERSION"
assert_dependency_version "$AGENTS_NUPKG" 'LevelUp.Strategos' "$CORE_VERSION"

if [[ "$(basename "$AGENTS_NUPKG")" != "$EXPECTED_NUPKG_NAME" ]]; then
  echo "FAIL: expected $EXPECTED_NUPKG_NAME; produced $(basename "$AGENTS_NUPKG")." >&2
  exit 3
fi

echo "Packed closure:"
printf '  %s %s\n' \
  'LevelUp.Strategos.Contracts' "$CONTRACTS_VERSION" \
  'LevelUp.Strategos.Identity.Abstractions' "$IDENTITY_VERSION" \
  'LevelUp.Strategos' "$CORE_VERSION" \
  'LevelUp.Strategos.Agents' "$AGENTS_VERSION"
echo "OK: $AGENTS_NUPKG and its complete in-repository dependency closure are ready for a fresh smoke-project restore."

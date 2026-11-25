# Shared helpers for the scripts in scripts/ci. Source this file; do not run it.
#
# Buildkite runs each step on a checkout of the tested merge (refs/pull/N/merge),
# so the merge's first parent is the PR base. Set BASE_SHA to run a script
# against some other commit, for example on a local branch.

set -euo pipefail
shopt -s inherit_errexit

cd "$(git rev-parse --show-toplevel)"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# Step outputs go here; .buildkite/pipeline.yml uploads them as artifacts.
OUT="$PWD/ci-out"

die() {
  echo "error: $*" >&2
  exit 1
}

base_sha() {
  if [[ -n "${BASE_SHA:-}" ]]; then
    git rev-parse --verify "${BASE_SHA}^{commit}"
    return
  fi
  git rev-parse --verify --quiet 'HEAD^2' >/dev/null \
    || die 'HEAD is not the tested merge commit; set BASE_SHA to the commit to compare against'
  git rev-parse 'HEAD^1'
}

changed_files() {
  local base
  base=$(base_sha)
  git diff --name-only "$base" HEAD
}

# MinVer reads version tags, and several checks diff against the base or the
# last v* tag, so they need complete history and every public tag.
full_history() {
  if [[ "$(git rev-parse --is-shallow-repository)" == true ]]; then
    git fetch --quiet --unshallow origin
  fi
  git fetch --quiet --tags origin
}

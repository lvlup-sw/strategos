#!/usr/bin/env bash
# Build once, run every coverage test project, and gate merged line coverage.
#
# The gate counts exact Cobertura line counters over the Strategos shipping
# assemblies (test, JasperFx, Wolverine, Marten, Weasel and Npgsql assemblies
# are filtered out) and fails below THRESHOLD percent. Every project must run
# and pass at least one test.
#
# PostgreSQL: Strategos.Generators.Behavioral.Tests needs a database named
# strategos_behavioral, passed as an Npgsql connection string in
# STRATEGOS_BEHAVIORAL_TEST_CONN. When that variable is unset, this script
# starts a throwaway PostgreSQL server on 127.0.0.1 for the duration of the run.
# That needs either PostgreSQL server binaries in the image (initdb, pg_ctl,
# createdb), or root with apt-get and package network access, in which case it
# installs the distribution's postgresql package first. As root the server runs
# as the postgres user, because PostgreSQL refuses to run as root.
source "$(dirname "$0")/lib.sh"

THRESHOLD=80
SOLUTION=strategos.slnx
ASSEMBLY_FILTERS='-*Tests;-JasperFx*;-Wolverine*;-Marten*;-Weasel*;-Npgsql*'
PG_PORT=55432

# Adding or removing a test project must change this list, so the gate cannot
# shrink without a reviewed scripts/ci change. Benchmarks.Tests is out of scope
# and Contracts.Tests runs in the contracts step.
PROJECTS=(
  tests/Strategos.Agents.Mcp.Tests/Strategos.Agents.Mcp.Tests.csproj
  tests/Strategos.Agents.Tests/Strategos.Agents.Tests.csproj
  tests/Strategos.Architecture.Tests/Strategos.Architecture.Tests.csproj
  tests/Strategos.EnumJson.SourceGeneration.Tests/Strategos.EnumJson.SourceGeneration.Tests.csproj
  tests/Strategos.Generators.Behavioral.Tests/Strategos.Generators.Behavioral.Tests.csproj
  tests/Strategos.Generators.Tests/Strategos.Generators.Tests.csproj
  tests/Strategos.Identity.Abstractions.Tests/Strategos.Identity.Abstractions.Tests.csproj
  tests/Strategos.Infrastructure.Tests/Strategos.Infrastructure.Tests.csproj
  tests/Strategos.Ontology.Embeddings.Tests/Strategos.Ontology.Embeddings.Tests.csproj
  tests/Strategos.Ontology.Generators.Tests/Strategos.Ontology.Generators.Tests.csproj
  tests/Strategos.Ontology.MCP.Hosting.Tests/Strategos.Ontology.MCP.Hosting.Tests.csproj
  tests/Strategos.Ontology.MCP.Tests/Strategos.Ontology.MCP.Tests.csproj
  tests/Strategos.Ontology.Npgsql.Tests/Strategos.Ontology.Npgsql.Tests.csproj
  tests/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj
  tests/Strategos.Rag.Tests/Strategos.Rag.Tests.csproj
  tests/Strategos.Tests/Strategos.Tests.csproj
)

check_roster() {
  local discovered expected
  discovered=$(find tests -mindepth 2 -maxdepth 2 -path 'tests/*Tests/*.csproj' \
    ! -path '*Benchmarks.Tests*' ! -path '*Contracts.Tests*' | LC_ALL=C sort)
  expected=$(printf '%s\n' "${PROJECTS[@]}" | LC_ALL=C sort)
  if [[ "$discovered" != "$expected" ]]; then
    diff <(echo "$expected") <(echo "$discovered") >&2 || true
    die 'test projects differ from the coverage roster in scripts/ci/coverage.sh'
  fi
}

# Shared build inputs, samples and benchmarks need the whole solution. Other
# changes build only the coverage projects and their references.
complete_solution() {
  local path
  while IFS= read -r path; do
    case "$path" in
      samples/* | src/Strategos.Benchmarks/* | *.sln | *.slnx | *.props | *.targets | global.json)
        return 0
        ;;
    esac
  done <<<"$1"
  return 1
}

pg_bindir() {
  local initdb
  initdb=$(find /usr/lib/postgresql -path '*/bin/initdb' 2>/dev/null | sort -V | tail -n 1)
  [[ -n "$initdb" ]] || initdb=$(command -v initdb) || return 1
  dirname "$initdb"
}

start_postgres() {
  [[ -n "${STRATEGOS_BEHAVIORAL_TEST_CONN:-}" ]] && return

  local bin
  bin=$(pg_bindir || true)
  if [[ -z "$bin" && "$(id -u)" == 0 ]] && command -v apt-get >/dev/null; then
    echo '--- Installing PostgreSQL'
    apt-get update -qq
    DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends postgresql >/dev/null
    bin=$(pg_bindir || true)
  fi
  [[ -n "$bin" ]] || die 'PostgreSQL server binaries are unavailable; set STRATEGOS_BEHAVIORAL_TEST_CONN or run as root with apt-get'

  local as_pg=()
  if [[ "$(id -u)" == 0 ]]; then
    id postgres >/dev/null 2>&1 || die 'running as root requires a postgres user to own the server'
    as_pg=(runuser -u postgres --)
  fi

  echo '--- Starting PostgreSQL'
  PG_DIR=$(mktemp -d)
  local password
  password=$(od -An -N24 -tx1 /dev/urandom | tr -d ' \n')
  printf '%s\n' "$password" >"$PG_DIR/password"
  [[ ${#as_pg[@]} -eq 0 ]] || chown -R postgres "$PG_DIR"
  "${as_pg[@]}" "$bin/initdb" -D "$PG_DIR/data" -U postgres --pwfile="$PG_DIR/password" \
    --auth=scram-sha-256 -E UTF8 >/dev/null
  "${as_pg[@]}" "$bin/pg_ctl" -D "$PG_DIR/data" -l "$PG_DIR/server.log" -w \
    -o "-c listen_addresses=127.0.0.1 -p $PG_PORT -k $PG_DIR" start >/dev/null
  PG_STOP=("${as_pg[@]}" "$bin/pg_ctl" -D "$PG_DIR/data" -m fast stop)
  trap '"${PG_STOP[@]}" >/dev/null || true' EXIT
  PGPASSWORD="$password" "$bin/createdb" -h 127.0.0.1 -p "$PG_PORT" -U postgres strategos_behavioral
  export STRATEGOS_BEHAVIORAL_TEST_CONN="Host=127.0.0.1;Port=$PG_PORT;Username=postgres;Password=$password;Maximum Pool Size=4;Database=strategos_behavioral"
}

reportgenerator() {
  if [[ -x /tools/reportgenerator ]]; then
    /tools/reportgenerator "$@"
  else
    command reportgenerator "$@"
  fi
}

main() {
  full_history
  check_roster

  local changed results="$OUT/coverage" scope="$SOLUTION"
  changed=$(changed_files)
  mkdir -p "$OUT"
  mkdir "$results"
  if ! complete_solution "$changed"; then
    scope="$OUT/coverage.slnf"
    jq -n --arg path "$PWD/$SOLUTION" '{solution: {path: $path, projects: $ARGS.positional}}' \
      --args "${PROJECTS[@]}" >"$scope"
  fi

  echo "--- Building $(basename "$scope")"
  dotnet restore "$scope"
  dotnet build "$scope" -c Release --no-restore --disable-build-servers

  start_postgres

  local index project output allow_empty
  for index in "${!PROJECTS[@]}"; do
    project=${PROJECTS[$index]}
    output=$(printf '%s/project-%02d' "$results" "$index")
    mkdir "$output"
    echo "--- Testing $project"
    dotnet run --project "$project" -c Release --no-build --no-restore -- \
      --coverage --coverage-settings "$PWD/scripts/ci/coverage-settings.xml" \
      --coverage-output-format cobertura --coverage-output "$output/coverage.cobertura.xml" \
      --report-trx --report-trx-filename tests.trx --results-directory "$output"
    # Architecture tests contain no shipping code, so their report may be empty.
    allow_empty=()
    [[ "$project" == */Strategos.Architecture.Tests.csproj ]] && allow_empty=(--allow-empty)
    python3 scripts/ci/coverage_gate.py project "$output/tests.trx" "$output/coverage.cobertura.xml" "${allow_empty[@]}"
  done

  echo '--- Merging coverage'
  reportgenerator "-reports:$results/project-*/coverage.cobertura.xml" "-targetdir:$results" \
    -reporttypes:Cobertura -verbosity:Warning "-assemblyfilters:$ASSEMBLY_FILTERS"
  python3 scripts/ci/coverage_gate.py merged "$results/Cobertura.xml" --threshold "$THRESHOLD"
}

main "$@"

#!/usr/bin/env bash
# -----------------------------------------------------------------------
# verify-generator-consumer-build.sh
#
# G1 / F1 regression net (v2.7.0-preview.1).
#
# Builds a throwaway consumer project that PackageReferences only the packed
# LevelUp.Strategos + LevelUp.Strategos.Generators nupkgs from the given
# package source directory, then writes a trivial type that derives from
# IPhaseAwareSaga. If the abstractions package does NOT flow transitively
# from the metapackage, this build fails with CS0246, which is exactly the
# bug that motivated this script.
#
# Usage:
#   scripts/verify-generator-consumer-build.sh <path-to-packages-dir>
#
# Exit codes:
#   0  consumer build succeeded
#   1  invalid arguments / setup error
#   2  consumer build FAILED (regression detected)
# -----------------------------------------------------------------------
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "usage: $0 <packages-dir>" >&2
  exit 1
fi

PACKAGES_DIR="$(realpath "$1")"

if [[ ! -d "$PACKAGES_DIR" ]]; then
  echo "packages dir does not exist: $PACKAGES_DIR" >&2
  exit 1
fi

# Discover the versions that were packed (so we don't hard-code a number).
GEN_NUPKG="$(ls -1 "$PACKAGES_DIR"/LevelUp.Strategos.Generators.*.nupkg 2>/dev/null | head -1 || true)"
CORE_NUPKG="$(ls -1 "$PACKAGES_DIR"/LevelUp.Strategos.*.nupkg 2>/dev/null \
  | grep -v 'Generators' | grep -v 'Identity' | grep -v 'Ontology' | grep -v 'Agents' \
  | grep -v 'Infrastructure' | grep -v 'Benchmarks' | grep -v 'Rag' | grep -v 'snupkg' \
  | head -1 || true)"

if [[ -z "$GEN_NUPKG" || -z "$CORE_NUPKG" ]]; then
  echo "could not find LevelUp.Strategos.Generators.*.nupkg or LevelUp.Strategos.*.nupkg in $PACKAGES_DIR" >&2
  exit 1
fi

# Extract version from the generator nupkg filename.
GEN_FILENAME="$(basename "$GEN_NUPKG")"
VERSION="$(echo "$GEN_FILENAME" | sed -E 's/^LevelUp\.Strategos\.Generators\.(.+)\.nupkg$/\1/')"

echo "Consumer probe: Strategos $VERSION from $PACKAGES_DIR"

PROBE_DIR="$(mktemp -d)"
trap 'rm -rf "$PROBE_DIR"' EXIT

cat > "$PROBE_DIR/ConsumerProbe.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RestoreAdditionalProjectSources>$PACKAGES_DIR</RestoreAdditionalProjectSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LevelUp.Strategos" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Generators" Version="$VERSION" />
  </ItemGroup>
</Project>
EOF

cat > "$PROBE_DIR/Probe.cs" <<'EOF'
// Mirrors what the Strategos source generator emits: a saga deriving from
// IPhaseAwareSaga. If LevelUp.Strategos.Identity.Abstractions does NOT flow
// transitively to consumers (the F1 bug), this fails with CS0246.
using Strategos.Identity.Abstractions;

namespace ConsumerProbe;

public partial class ProbeSaga : IPhaseAwareSaga
{
    public string CurrentPhaseName { get; private set; } = "init";
}
EOF

# Use a project-local global-packages dir so this probe is hermetic and not
# influenced by stale entries in the developer's user-wide ~/.nuget/packages.
# Critical: without this isolation, a stale cached copy of a previously-
# published nupkg (with different metadata) can mask a regression.
PROBE_GLOBAL_PACKAGES="$PROBE_DIR/.nuget-packages"
mkdir -p "$PROBE_GLOBAL_PACKAGES"

if dotnet build "$PROBE_DIR/ConsumerProbe.csproj" \
     --nologo \
     -v:m \
     --no-cache \
     /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
     /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES"; then
  echo "OK: consumer build succeeded; IPhaseAwareSaga is reachable transitively."
  exit 0
else
  echo "FAIL: consumer build failed. The generator + abstractions dep flow is broken (F1 regression)." >&2
  exit 2
fi

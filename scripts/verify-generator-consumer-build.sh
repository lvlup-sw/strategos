#!/usr/bin/env bash
# -----------------------------------------------------------------------
# verify-generator-consumer-build.sh
#
# G1 / F1 regression net (v2.7.0-preview.1).
#
# Builds a throwaway consumer project that PackageReferences only packed
# Strategos artifacts from the given package source directory. The probe keeps
# the original IPhaseAwareSaga dependency-flow check and also compiles a typed
# workflow/action binding through the packaged source generator. A second build
# deliberately introduces an illegal seam and must fail with AGWF041, proving
# the packaged analyzer is loaded and enforcing #167 rather than merely present.
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
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
CENTRAL_PACKAGES_FILE="$SCRIPT_DIR/../src/Directory.Packages.props"

if [[ ! -d "$PACKAGES_DIR" ]]; then
  echo "packages dir does not exist: $PACKAGES_DIR" >&2
  exit 1
fi

package_metadata_value() {
  local package_path="$1"
  local element_name="$2"

  unzip -p "$package_path" '*.nuspec' \
    | tr -d '\r' \
    | sed -n "s:.*<$element_name>\([^<]*\)</$element_name>.*:\1:p" \
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
  done < <(find "$PACKAGES_DIR" -maxdepth 1 -type f -name '*.nupkg' -print | sort)

  if [[ ${#candidates[@]} -ne 1 ]]; then
    echo "expected exactly one $expected_id nupkg in $PACKAGES_DIR; found ${#candidates[@]}" >&2
    return 1
  fi

  printf '%s\n' "${candidates[0]}"
}

# Resolve exact package identities from nuspec metadata. Filename-prefix matching
# is deliberately insufficient because one feed can contain multiple package
# versions and similarly prefixed packages.
CORE_NUPKG="$(find_exact_package 'LevelUp.Strategos')"
AGENTS_NUPKG="$(find_exact_package 'LevelUp.Strategos.Agents')"
GEN_NUPKG="$(find_exact_package 'LevelUp.Strategos.Generators')"
CONTRACTS_NUPKG="$(find_exact_package 'LevelUp.Strategos.Contracts')"
IDENTITY_NUPKG="$(find_exact_package 'LevelUp.Strategos.Identity.Abstractions')"
ONTOLOGY_NUPKG="$(find_exact_package 'LevelUp.Strategos.Ontology')"

VERSION="$(package_metadata_value "$CORE_NUPKG" version)"
for package_path in "$AGENTS_NUPKG" "$GEN_NUPKG" "$ONTOLOGY_NUPKG"; do
  package_version="$(package_metadata_value "$package_path" version)"
  if [[ "$package_version" != "$VERSION" ]]; then
    echo "packed Strategos versions disagree: expected $VERSION, found $package_version in $package_path" >&2
    exit 1
  fi
done

if [[ -z "$VERSION" ]]; then
  echo "could not read packed Strategos version from $CORE_NUPKG" >&2
  exit 1
fi
CONTRACTS_VERSION="$(package_metadata_value "$CONTRACTS_NUPKG" version)"
IDENTITY_VERSION="$(package_metadata_value "$IDENTITY_NUPKG" version)"
if [[ -z "$CONTRACTS_VERSION" || -z "$IDENTITY_VERSION" ]]; then
  echo "could not read packed Contracts or Identity.Abstractions version" >&2
  exit 1
fi
WOLVERINE_VERSION="$(sed -n 's/.*PackageVersion Include="WolverineFx" Version="\([^"]*\)".*/\1/p' "$CENTRAL_PACKAGES_FILE")"
WOLVERINE_MARTEN_VERSION="$(sed -n 's/.*PackageVersion Include="WolverineFx.Marten" Version="\([^"]*\)".*/\1/p' "$CENTRAL_PACKAGES_FILE")"
MARTEN_VERSION="$(sed -n 's/.*PackageVersion Include="Marten" Version="\([^"]*\)".*/\1/p' "$CENTRAL_PACKAGES_FILE")"

if [[ -z "$WOLVERINE_VERSION" || -z "$WOLVERINE_MARTEN_VERSION" || -z "$MARTEN_VERSION" ]]; then
  echo "could not resolve packed-consumer runtime versions from $CENTRAL_PACKAGES_FILE" >&2
  exit 1
fi

echo "Consumer probe: Strategos $VERSION from $PACKAGES_DIR"
echo "Packed artifact SHA-256:"
sha256sum \
  "$CORE_NUPKG" \
  "$AGENTS_NUPKG" \
  "$CONTRACTS_NUPKG" \
  "$GEN_NUPKG" \
  "$IDENTITY_NUPKG" \
  "$ONTOLOGY_NUPKG"

PROBE_DIR="$(mktemp -d)"
trap 'rm -rf "$PROBE_DIR"' EXIT

cat > "$PROBE_DIR/NuGet.Config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="strategos-local" value="$PACKAGES_DIR" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="strategos-local">
      <package pattern="LevelUp.Strategos*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

cat > "$PROBE_DIR/ConsumerProbe.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LevelUp.Strategos" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Agents" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Generators" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Ontology" Version="$VERSION" />
    <!-- The generated saga surface targets the same Wolverine/Marten compile-time
         runtime as Strategos.Generators.Behavioral.Tests. These are explicit
         consumer dependencies, not dependencies of the development-only analyzer. -->
    <PackageReference Include="WolverineFx" Version="$WOLVERINE_VERSION" />
    <PackageReference Include="WolverineFx.Marten" Version="$WOLVERINE_MARTEN_VERSION" />
    <PackageReference Include="Marten" Version="$MARTEN_VERSION" />
  </ItemGroup>
</Project>
EOF

cat > "$PROBE_DIR/Probe.cs" <<'EOF'
using System;
using System.Threading;
using System.Threading.Tasks;

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Identity.Abstractions;
using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Steps;

namespace ConsumerProbe;

// Mirrors what the generator emits. If Identity.Abstractions does not flow
// transitively from the packed metapackage, this fails with CS0246.
public partial class ProbeSaga : IPhaseAwareSaga
{
    public string CurrentPhaseName { get; private set; } = "init";
}

public sealed class Order
{
    public int Stage { get; set; }
}

public sealed class OrdersOntology : DomainOntology
{
    public override string DomainName => "orders";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Order>("Order", obj =>
        {
            obj.Action("fulfill")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage)
                .BoundToWorkflow(new WorkflowBindingReference("consumer-probe"));

            obj.Action("receive")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);

#if ILLEGAL_CONTRACT
            obj.Action("complete")
                .Requires(order => order.Stage == 2)
#else
            obj.Action("complete")
                .Requires(order => order.Stage == 1)
#endif
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage);
        });
    }
}

[WorkflowState]
public sealed record FlowState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
}

public class ProbeStep : IWorkflowStep<FlowState>
{
    public Task<StepResult<FlowState>> ExecuteAsync(
        FlowState state,
        StepContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(StepResult<FlowState>.FromState(state));
}

public sealed class ReceiveStep : ProbeStep { }
public sealed class CompleteStep : ProbeStep { }

[Workflow("consumer-probe")]
public static partial class ConsumerProbeWorkflowDefinition
{
    public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
        .Create("consumer-probe")
        .StartWith<ReceiveStep>(step => step.Performs(
            new WorkflowActionReference("orders", "Order", "receive")))
        .Finally<CompleteStep>(step => step.Performs(
            new WorkflowActionReference("orders", "Order", "complete")));
}
EOF

# Use a project-local global-packages dir so this probe is hermetic and not
# influenced by stale entries in the developer's user-wide ~/.nuget/packages.
# Critical: without this isolation, a stale cached copy of a previously-
# published nupkg (with different metadata) can mask a regression.
PROBE_GLOBAL_PACKAGES="$PROBE_DIR/.nuget-packages"
mkdir -p "$PROBE_GLOBAL_PACKAGES"

if ! dotnet build "$PROBE_DIR/ConsumerProbe.csproj" \
       --nologo \
       -v:m \
       --no-cache \
       /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
       /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES"; then
  echo "FAIL: legal packed consumer build failed." >&2
  exit 2
fi

verify_restored_artifact() {
  local package_id="$1"
  local package_version="$2"
  local source_package="$3"
  local lowercase_id
  local restored_package
  local metadata_file

  lowercase_id="$(printf '%s' "$package_id" | tr '[:upper:]' '[:lower:]')"
  restored_package="$PROBE_GLOBAL_PACKAGES/$lowercase_id/$package_version/$lowercase_id.$package_version.nupkg"
  metadata_file="$PROBE_GLOBAL_PACKAGES/$lowercase_id/$package_version/.nupkg.metadata"

  if [[ ! -f "$restored_package" ]] || ! cmp -s "$source_package" "$restored_package"; then
    echo "FAIL: restored $package_id bytes do not match $source_package." >&2
    exit 2
  fi

  if [[ ! -f "$metadata_file" ]] || ! grep -Fq "$PACKAGES_DIR" "$metadata_file"; then
    echo "FAIL: restored $package_id does not record the requested local feed as its source." >&2
    exit 2
  fi
}

verify_restored_artifact 'LevelUp.Strategos' "$VERSION" "$CORE_NUPKG"
verify_restored_artifact 'LevelUp.Strategos.Agents' "$VERSION" "$AGENTS_NUPKG"
verify_restored_artifact 'LevelUp.Strategos.Contracts' "$CONTRACTS_VERSION" "$CONTRACTS_NUPKG"
verify_restored_artifact 'LevelUp.Strategos.Generators' "$VERSION" "$GEN_NUPKG"
verify_restored_artifact 'LevelUp.Strategos.Identity.Abstractions' "$IDENTITY_VERSION" "$IDENTITY_NUPKG"
verify_restored_artifact 'LevelUp.Strategos.Ontology' "$VERSION" "$ONTOLOGY_NUPKG"

echo "OK: legal packed binding compiled and IPhaseAwareSaga is reachable transitively."

ILLEGAL_LOG="$PROBE_DIR/illegal-build.log"
set +e
dotnet build "$PROBE_DIR/ConsumerProbe.csproj" \
  --nologo \
  -v:m \
  --no-restore \
  --no-incremental \
  /p:DefineConstants=ILLEGAL_CONTRACT \
  /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
  /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES" >"$ILLEGAL_LOG" 2>&1
illegal_status=$?
set -e

if [[ "$illegal_status" -eq 0 ]]; then
  echo "FAIL: illegal packed consumer seam compiled; AGWF041 enforcement did not run." >&2
  cat "$ILLEGAL_LOG" >&2
  exit 2
fi

mapfile -t illegal_error_codes < <(
  sed -nE 's/.*[[:space:]]error[[:space:]]([[:alpha:]]+[[:digit:]]+):.*/\1/p' "$ILLEGAL_LOG" \
    | sort -u
)
if [[ ${#illegal_error_codes[@]} -ne 1 || "${illegal_error_codes[0]}" != 'AGWF041' ]]; then
  echo "FAIL: illegal packed consumer did not fail exclusively with AGWF041." >&2
  cat "$ILLEGAL_LOG" >&2
  exit 2
fi

if ! grep -Fq "internal seam 'ReceiveStep' -> 'CompleteStep' is not composable" "$ILLEGAL_LOG"; then
  echo "FAIL: AGWF041 did not identify the deliberately illegal consumer seam." >&2
  cat "$ILLEGAL_LOG" >&2
  exit 2
fi

echo "OK: illegal packed binding failed closed with AGWF041."

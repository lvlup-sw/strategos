#!/usr/bin/env bash
# -----------------------------------------------------------------------
# verify-generator-consumer-build.sh
#
# G1 / F1 packed-artifact regression net.
#
# Builds a throwaway consumer project that PackageReferences only packed
# Strategos artifacts from the given package source directory. The probe keeps
# the original IPhaseAwareSaga dependency-flow check and compiles a typed
# workflow/action binding with mechanically proved compensation through the
# packaged source generator. Negative builds deliberately introduce an illegal
# seam, a contradictory workflow inverse, and a contradictory ontology-authored
# inverse; they must fail exclusively with AGWF041, AGWF044, and AONT216
# respectively. This proves both packaged analyzer assemblies are restored,
# loaded, and enforcing #167/#169 rather than merely present.
#
# Usage:
#   scripts/verify-generator-consumer-build.sh <path-to-packages-dir>
#
# Exit codes:
#   0  consumer build succeeded
#   1  invalid arguments / setup error
#   2  consumer build FAILED (regression detected)
#   3  consumer verification INDETERMINATE (restore/infrastructure failure)
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
ONTOLOGY_GEN_NUPKG="$(find_exact_package 'LevelUp.Strategos.Ontology.Generators')"

VERSION="$(package_metadata_value "$CORE_NUPKG" version)"
for package_path in "$AGENTS_NUPKG" "$GEN_NUPKG" "$ONTOLOGY_NUPKG" "$ONTOLOGY_GEN_NUPKG"; do
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
  "$ONTOLOGY_NUPKG" \
  "$ONTOLOGY_GEN_NUPKG"

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
    <WarningsAsErrors>nullable</WarningsAsErrors>
    <!-- The four leaf actions are occurrence contracts consumed by the workflow;
         only the aggregate fulfill action legitimately binds the whole workflow. -->
    <NoWarn>AONT004</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LevelUp.Strategos" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Agents" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Generators" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Ontology" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Ontology.Generators" Version="$VERSION" />
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
    public string Id { get; set; } = "";
    public int Stage { get; set; }
}

public sealed class OrdersOntology : DomainOntology
{
    public override string DomainName => "orders";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Order>(obj =>
        {
            obj.Key(order => order.Id);
            obj.Property(order => order.Stage);

            obj.Action("fulfill")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage)
                .BoundToWorkflow(new WorkflowBindingReference("consumer-probe"));

            obj.Action("receive")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);

            obj.Action("undo-receive")
                .Requires(order => order.Stage == 1)
                .Ensures(order => order.Stage == 0)
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

            obj.Action("undo-complete")
                .Requires(order => order.Stage == 2)
#if ILLEGAL_CONTRACT
                .Ensures(order => order.Stage == 2)
#else
                .Ensures(order => order.Stage == 1)
#endif
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
public sealed class UndoReceiveStep : ProbeStep { }
public sealed class UndoCompleteStep : ProbeStep { }

[Workflow("consumer-probe")]
public static partial class ConsumerProbeWorkflowDefinition
{
    public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
        .Create("consumer-probe")
        .StartWith<ReceiveStep>(step => step
            .Performs(new WorkflowActionReference("orders", "Order", "receive"))
#if INVALID_COMPENSATION
            .Compensate<UndoReceiveStep>(new WorkflowActionReference(
                "orders", "Order", "undo-complete")))
#else
            .Compensate<UndoReceiveStep>(new WorkflowActionReference(
                "orders", "Order", "undo-receive")))
#endif
        .Finally<CompleteStep>(step => step
            .Performs(new WorkflowActionReference("orders", "Order", "complete"))
            .Compensate<UndoCompleteStep>(new WorkflowActionReference(
                "orders", "Order", "undo-complete")));
}

#if INVALID_AUTHORED_INVERSE
public sealed class InvalidAuthoredInverseOntology : DomainOntology
{
    public override string DomainName => "aont216-probe";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Order>(obj =>
        {
            obj.Key(order => order.Id);
            obj.Property(order => order.Stage);

            obj.Action("publish")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage)
                .CompensatedBy("unpublish");

            obj.Action("unpublish")
                .Requires(order => order.Stage == 2)
                .Ensures(order => order.Stage == 0)
                .Modifies(order => order.Stage);
        });
    }
}
#endif
EOF

# Use a project-local global-packages dir so this probe is hermetic and not
# influenced by stale entries in the developer's user-wide ~/.nuget/packages.
# Critical: without this isolation, a stale cached copy of a previously-
# published nupkg (with different metadata) can mask a regression.
PROBE_GLOBAL_PACKAGES="$PROBE_DIR/.nuget-packages"
mkdir -p "$PROBE_GLOBAL_PACKAGES"

if ! dotnet restore "$PROBE_DIR/ConsumerProbe.csproj" \
       --nologo \
       -v:m \
       --no-cache \
       /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
       /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES"; then
  echo "INDETERMINATE: packed consumer dependencies could not be restored; no product verdict was reached." >&2
  exit 3
fi

if ! dotnet build "$PROBE_DIR/ConsumerProbe.csproj" \
       --nologo \
       -v:m \
       -warnaserror \
       --no-restore \
       /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
       /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES"; then
  echo "FAIL: legal packed consumer build failed or emitted a warning." >&2
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
verify_restored_artifact 'LevelUp.Strategos.Ontology.Generators' "$VERSION" "$ONTOLOGY_GEN_NUPKG"

echo "OK: legal packed binding and typed compensation compiled warning-free; IPhaseAwareSaga is reachable transitively."

INVALID_AUTHORED_INVERSE_LOG="$PROBE_DIR/invalid-authored-inverse-build.log"
set +e
dotnet build "$PROBE_DIR/ConsumerProbe.csproj" \
  --nologo \
  -v:m \
  --no-restore \
  --no-incremental \
  /p:DefineConstants=INVALID_AUTHORED_INVERSE \
  /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
  /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES" >"$INVALID_AUTHORED_INVERSE_LOG" 2>&1
invalid_authored_inverse_status=$?
set -e

if [[ "$invalid_authored_inverse_status" -eq 0 ]]; then
  echo "FAIL: contradictory ontology-authored inverse compiled; AONT216 enforcement did not run." >&2
  cat "$INVALID_AUTHORED_INVERSE_LOG" >&2
  exit 2
fi

mapfile -t invalid_authored_inverse_error_codes < <(
  sed -nE 's/.*[[:space:]]error[[:space:]]([[:alpha:]]+[[:digit:]]+):.*/\1/p' \
    "$INVALID_AUTHORED_INVERSE_LOG" \
    | sort -u
)
if [[ ${#invalid_authored_inverse_error_codes[@]} -ne 1 \
   || "${invalid_authored_inverse_error_codes[0]}" != 'AONT216' ]]; then
  echo "FAIL: contradictory ontology-authored inverse did not fail exclusively with AONT216." >&2
  cat "$INVALID_AUTHORED_INVERSE_LOG" >&2
  exit 2
fi

if ! grep -Fq "Action 'publish' names compensation 'unpublish'" \
     "$INVALID_AUTHORED_INVERSE_LOG"; then
  echo "FAIL: AONT216 did not identify the deliberately contradictory ontology-authored inverse." >&2
  cat "$INVALID_AUTHORED_INVERSE_LOG" >&2
  exit 2
fi

echo "OK: contradictory ontology-authored inverse failed closed with AONT216."

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

INVALID_COMPENSATION_LOG="$PROBE_DIR/invalid-compensation-build.log"
set +e
dotnet build "$PROBE_DIR/ConsumerProbe.csproj" \
  --nologo \
  -v:m \
  --no-restore \
  --no-incremental \
  /p:DefineConstants=INVALID_COMPENSATION \
  /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
  /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES" >"$INVALID_COMPENSATION_LOG" 2>&1
invalid_compensation_status=$?
set -e

if [[ "$invalid_compensation_status" -eq 0 ]]; then
  echo "FAIL: contradictory packed consumer inverse compiled; AGWF044 enforcement did not run." >&2
  cat "$INVALID_COMPENSATION_LOG" >&2
  exit 2
fi

mapfile -t invalid_compensation_error_codes < <(
  sed -nE 's/.*[[:space:]]error[[:space:]]([[:alpha:]]+[[:digit:]]+):.*/\1/p' "$INVALID_COMPENSATION_LOG" \
    | sort -u
)
if [[ ${#invalid_compensation_error_codes[@]} -ne 1 \
   || "${invalid_compensation_error_codes[0]}" != 'AGWF044' ]]; then
  echo "FAIL: contradictory packed consumer inverse did not fail exclusively with AGWF044." >&2
  cat "$INVALID_COMPENSATION_LOG" >&2
  exit 2
fi

if ! grep -Fq "declares inverse action 'orders/Order/undo-complete' for forward action 'orders/Order/receive'" \
     "$INVALID_COMPENSATION_LOG"; then
  echo "FAIL: AGWF044 did not identify the deliberately contradictory packed consumer inverse." >&2
  cat "$INVALID_COMPENSATION_LOG" >&2
  exit 2
fi

echo "OK: contradictory packed consumer inverse failed closed with AGWF044."

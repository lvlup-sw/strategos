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
# packaged source generator. This proves both packaged analyzer assemblies are
# restored, loaded, and enforcing #167/#169 rather than merely present.
#
# Arms, in the order they run:
#
#   1. legal            - restores, builds warning-free under -warnaserror, and
#                         byte-compares every restored nupkg against the feed.
#   2. INVALID_AUTHORED_INVERSE - an ontology CompensatedBy whose named action is
#                         not the derived inverse; must fail with AONT216 only.
#   3. ILLEGAL_CONTRACT - a workflow seam whose upstream guarantee does not imply
#                         the downstream requirement; must fail with AGWF041 only.
#   4. INVALID_COMPENSATION - a typed Compensate() naming a contradictory inverse
#                         action; must fail with AGWF044 only.
#   5. MISSING_INVERSE  - a rollback-reachable, state-changing occurrence with no
#                         inverse at all while a sibling occurrence has a typed
#                         one, so the compensation scope is not mechanically
#                         derivable; must fail with AGWF045 only.
#   6. suppression matrix - arms 2, 4 and 5 rebuilt under <NoWarn> and under an
#                         .editorconfig "dotnet_diagnostic.<id>.severity = none".
#                         All three descriptors carry NotConfigurable, so each
#                         refutation must still be the sole build error. Both
#                         channels are first shown to REMOVE a configurable
#                         diagnostic (AGWF010 for the generator path via the
#                         CONFIGURABLE_CONTROL variant, AONT004 for the analyzer
#                         path), so no arm can pass on an inert build flag.
#   7. cross-assembly   - a producer project declaring the ontology and a consumer
#                         project declaring the workflow, both built against the
#                         packed analyzers. Proves the packed generator exports a
#                         proof catalog, that the consumer reads it and proves the
#                         binding, that an illegal seam across the boundary is
#                         refuted with AGWF041, and that a producer built without
#                         the generator is refused with AONT222.
#
# Out of reach of any descriptor tag: -p:RunAnalyzers=false and
# -p:RunAnalyzersDuringBuild=false unload EVERY analyzer, so NotConfigurable
# cannot survive them and no compile-time arm can assert against them.
# (-p:SkipAnalyzers=true is a no-op here and does not disable them.) A consumer
# that builds that way is caught at host start instead, when OntologyGraphBuilder
# refuses to freeze a graph whose authored compensation disagrees with the
# derived inverse.
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
CENTRAL_PACKAGES_FILE="$SCRIPT_DIR/../Directory.Packages.props"

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
    <!-- NoWarn is driven entirely by the script so a suppression arm can point it at a
         different id. The default (passed by every arm below) is AONT004: the four leaf
         actions are occurrence contracts consumed by the workflow; only the aggregate
         fulfill action legitimately binds the whole workflow. When an arm redirects
         NoWarn, AONT004 reappears as a warning, and that reappearance is the control
         proving the NoWarn channel really reached the packaged analyzers. -->
    <NoWarn>\$(ProbeNoWarn)</NoWarn>
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
#if UNRESOLVED_BINDING
                // Since #204 this is the DEFERRAL arm, not a failure arm: the bound
                // workflow is not in this compilation, the packed generator exports
                // this action's contract instead, and the obligation travels to
                // whichever compilation lowers the workflow. The build stays clean.
                .BoundToWorkflow(new WorkflowBindingReference("consumer-probe-elsewhere"));
#else
                .BoundToWorkflow(new WorkflowBindingReference("consumer-probe"));
#endif

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
#if MISSING_INVERSE
        // 'receive' keeps its typed inverse, so the workflow claims rollback; the
        // rollback-reachable 'complete' occurrence has a non-empty frame and no
        // compensation at all, so the scope is not mechanically derivable (AGWF045).
        .Finally<CompleteStep>(step => step
            .Performs(new WorkflowActionReference("orders", "Order", "complete")));
#else
        .Finally<CompleteStep>(step => step
            .Performs(new WorkflowActionReference("orders", "Order", "complete"))
            .Compensate<UndoCompleteStep>(new WorkflowActionReference(
                "orders", "Order", "undo-complete")));
#endif
}

#if CONFIGURABLE_CONTROL
// The suppression matrix's channel control. A workflow with steps and no
// Finally<> produces AGWF010, a configurable Warning from the same generator the
// AGWF044/AGWF045 arms come from, promoted to an error by -warnaserror. If a
// suppression channel cannot remove AGWF010, that channel never reached the
// packaged generator and those arms would prove nothing.
[Workflow("consumer-probe-control")]
public static partial class ControlWorkflowDefinition
{
    public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
        .Create("consumer-probe-control")
        .StartWith<ReceiveStep>();
}
#endif

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
       /p:ProbeNoWarn=AONT004 \
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
  /p:ProbeNoWarn=AONT004 \
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
  /p:ProbeNoWarn=AONT004 \
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
  /p:ProbeNoWarn=AONT004 \
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

# ---------------------------------------------------------------------------
# MISSING_INVERSE: a rollback-reachable occurrence with no proven inverse.
#
# 'receive' keeps its typed compensation, so the workflow claims rollback, but
# the terminal 'complete' occurrence declares none. The compensation scope is
# therefore not mechanically derivable and must fail with AGWF045.
# ---------------------------------------------------------------------------
MISSING_INVERSE_LOG="$PROBE_DIR/missing-inverse-build.log"
set +e
dotnet build "$PROBE_DIR/ConsumerProbe.csproj" \
  --nologo \
  -v:m \
  --no-restore \
  --no-incremental \
  /p:ProbeNoWarn=AONT004 \
  /p:DefineConstants=MISSING_INVERSE \
  /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
  /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES" >"$MISSING_INVERSE_LOG" 2>&1
missing_inverse_status=$?
set -e

if [[ "$missing_inverse_status" -eq 0 ]]; then
  echo "FAIL: non-derivable packed consumer rollback scope compiled; AGWF045 enforcement did not run." >&2
  cat "$MISSING_INVERSE_LOG" >&2
  exit 2
fi

mapfile -t missing_inverse_error_codes < <(
  sed -nE 's/.*[[:space:]]error[[:space:]]([[:alpha:]]+[[:digit:]]+):.*/\1/p' "$MISSING_INVERSE_LOG" \
    | sort -u
)
if [[ ${#missing_inverse_error_codes[@]} -ne 1 \
   || "${missing_inverse_error_codes[0]}" != 'AGWF045' ]]; then
  echo "FAIL: non-derivable packed consumer rollback scope did not fail exclusively with AGWF045." >&2
  cat "$MISSING_INVERSE_LOG" >&2
  exit 2
fi

if ! grep -Fq "step 'CompleteStep' is not compensable (no compensation step or inverse action is declared)" "$MISSING_INVERSE_LOG"; then
  echo "FAIL: AGWF045 did not identify the deliberately uncompensated packed consumer occurrence." >&2
  cat "$MISSING_INVERSE_LOG" >&2
  exit 2
fi

echo "OK: non-derivable packed consumer rollback scope failed closed with AGWF045."


# ---------------------------------------------------------------------------
# Suppression matrix.
#
# AONT216, AGWF044 and AGWF045 all carry WellKnownDiagnosticTags.NotConfigurable,
# so a consumer cannot silence a refuted inverse. These arms rebuild the negative
# probes with the two suppression channels a packaged consumer actually owns and
# require each refutation to remain the sole build error:
#
#   * <NoWarn> / -p:NoWarn -> routed here through the ProbeNoWarn property
#   * .editorconfig        -> dotnet_diagnostic.<id>.severity = none
#
# Every arm is controlled, because "the build still failed" is worthless if the
# channel never applied. Two control subjects are needed because the two
# refutation families travel different compiler paths:
#
#   * AONT216 is an ANALYZER diagnostic, so analyzers run and the configurable
#     AONT004 warning from the same analyzer is the control.
#   * AGWF044/AGWF045 are SOURCE GENERATOR diagnostics. csc reports generator
#     errors and then skips analyzer execution entirely, so no analyzer warning
#     can serve as their control. AGWF010 -- a configurable Warning from the same
#     generator, promoted to an error here by -warnaserror -- is used instead, and
#     each channel must be shown to remove it before the AGWF044/AGWF045 arms
#     count.
#
#     AGWF039 held this role until #204. It no longer can: the cross-assembly
#     layout it existed to let a consumer silence now has a real exit -- the
#     declaring assembly exports its contract -- so AGWF039 carries
#     NotConfigurable and neither channel removes it. A control has to be
#     configurable AND generator-produced, and AGWF010 is both.
#
# Out of reach of any descriptor tag: -p:RunAnalyzers=false and
# -p:RunAnalyzersDuringBuild=false unload EVERY analyzer and generator, so no
# CustomTags value can survive them and no compile-time arm can assert against
# them. That case is caught at host start, when OntologyGraphBuilder refuses to
# freeze a graph whose authored compensation disagrees with the derived inverse.
# ---------------------------------------------------------------------------
PROBE_EDITORCONFIG="$PROBE_DIR/.editorconfig"

write_severity_none_editorconfig() {
  cat > "$PROBE_EDITORCONFIG" <<EDITORCONFIG_EOF
root = true

[*.cs]
dotnet_diagnostic.$1.severity = none
EDITORCONFIG_EOF
}

# Runs one probe build. Sets probe_arm_status; the caller decides what that means.
run_probe_arm() {
  # $1 log path, $2 DefineConstants symbol; remaining args are extra msbuild properties.
  local log="$1" symbol="$2"
  shift 2

  set +e
  dotnet build "$PROBE_DIR/ConsumerProbe.csproj" \
    --nologo \
    -v:m \
    --no-restore \
    --no-incremental \
    "$@" \
    "/p:DefineConstants=$symbol" \
    /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
    /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES" >"$log" 2>&1
  probe_arm_status=$?
  set -e
}

probe_arm_error_codes() {
  sed -nE 's/.*[[:space:]]error[[:space:]]([[:alpha:]]+[[:digit:]]+):.*/\1/p' "$1" | sort -u
}

require_sole_error() {
  # $1 log, $2 expected id, $3 message substring, $4 description of the arm.
  local log="$1" expected="$2" needle="$3" label="$4"
  local codes

  if [[ "$probe_arm_status" -eq 0 ]]; then
    echo "FAIL: $label compiled; $expected was silenced." >&2
    cat "$log" >&2
    exit 2
  fi

  mapfile -t codes < <(probe_arm_error_codes "$log")
  if [[ ${#codes[@]} -ne 1 || "${codes[0]}" != "$expected" ]]; then
    echo "FAIL: $label did not fail exclusively with $expected (saw: ${codes[*]:-none})." >&2
    cat "$log" >&2
    exit 2
  fi

  if ! grep -Fq "$needle" "$log"; then
    echo "FAIL: $label produced $expected but not for the deliberate defect." >&2
    cat "$log" >&2
    exit 2
  fi
}

require_error_removed() {
  # $1 log, $2 id that the channel had to remove, $3 description of the arm.
  if grep -Eq "error $2:" "$1"; then
    echo "FAIL: $3 did not remove $2, so that channel is inert and the arms relying on it prove nothing." >&2
    cat "$1" >&2
    exit 2
  fi
}

# --- Channel control: a configurable generator diagnostic must be silenceable ----
# The control variant carries other diagnostics too (its workflow declares no
# terminal step). That is harmless: the control asks only whether each channel can
# remove AGWF010, not whether the probe builds.
run_probe_arm "$PROBE_DIR/control-agwf010.log" CONFIGURABLE_CONTROL /p:ProbeNoWarn=AONT004
if ! grep -Fq "AGWF010" "$PROBE_DIR/control-agwf010.log"; then
  echo "FAIL: the control variant did not produce AGWF010; the suppression matrix has no control subject." >&2
  cat "$PROBE_DIR/control-agwf010.log" >&2
  exit 2
fi

run_probe_arm "$PROBE_DIR/control-agwf010-nowarn.log" CONFIGURABLE_CONTROL \
  '/p:ProbeNoWarn=AONT004%3BAGWF010'
require_error_removed \
  "$PROBE_DIR/control-agwf010-nowarn.log" \
  AGWF010 \
  "<NoWarn>AGWF010"

write_severity_none_editorconfig AGWF010
run_probe_arm "$PROBE_DIR/control-agwf010-editorconfig.log" CONFIGURABLE_CONTROL /p:ProbeNoWarn=AONT004
rm -f "$PROBE_EDITORCONFIG"
require_error_removed \
  "$PROBE_DIR/control-agwf010-editorconfig.log" \
  AGWF010 \
  ".editorconfig severity=none for AGWF010"

echo "OK: control -- both suppression channels do remove a configurable generator diagnostic (AGWF010)."

# --- AONT216: the ontology analyzer path ----------------------------------
# NoWarn is pointed at AONT216 instead of AONT004; AONT004 must therefore
# reappear, proving the redirected NoWarn value really reached the analyzer.
run_probe_arm "$PROBE_DIR/nowarn-AONT216.log" INVALID_AUTHORED_INVERSE /p:ProbeNoWarn=AONT216
require_sole_error \
  "$PROBE_DIR/nowarn-AONT216.log" \
  AONT216 \
  "Action 'publish' names compensation 'unpublish'" \
  "the contradictory ontology-authored inverse under <NoWarn>AONT216"
if ! grep -Fq 'warning AONT004' "$PROBE_DIR/nowarn-AONT216.log"; then
  echo "FAIL: NoWarn control is inert; redirecting NoWarn away from AONT004 did not surface it." >&2
  cat "$PROBE_DIR/nowarn-AONT216.log" >&2
  exit 2
fi
echo "OK: AONT216 survived <NoWarn>AONT216 (control AONT004 reappeared)."

# severity = none is applied to AONT216 and to AONT004 while NoWarn is empty;
# AONT004 must therefore be absent, proving the .editorconfig was read.
cat > "$PROBE_EDITORCONFIG" <<'EDITORCONFIG_EOF'
root = true

[*.cs]
dotnet_diagnostic.AONT216.severity = none
dotnet_diagnostic.AONT004.severity = none
EDITORCONFIG_EOF
run_probe_arm "$PROBE_DIR/editorconfig-AONT216.log" INVALID_AUTHORED_INVERSE /p:ProbeNoWarn=
rm -f "$PROBE_EDITORCONFIG"
require_sole_error \
  "$PROBE_DIR/editorconfig-AONT216.log" \
  AONT216 \
  "Action 'publish' names compensation 'unpublish'" \
  "the contradictory ontology-authored inverse under .editorconfig severity=none"
if grep -Fq 'warning AONT004' "$PROBE_DIR/editorconfig-AONT216.log"; then
  echo "FAIL: .editorconfig control is inert; AONT004 survived severity=none with an empty NoWarn." >&2
  cat "$PROBE_DIR/editorconfig-AONT216.log" >&2
  exit 2
fi
echo "OK: AONT216 survived .editorconfig severity=none (control AONT004 was removed)."

# --- AGWF044 / AGWF045: the source generator path -------------------------
GENERATOR_SUPPRESSION_CASES=(
  "INVALID_COMPENSATION:AGWF044:declares inverse action 'orders/Order/undo-complete' for forward action 'orders/Order/receive'"
  "MISSING_INVERSE:AGWF045:step 'CompleteStep' is not compensable (no compensation step or inverse action is declared)"
)

for suppression_case in "${GENERATOR_SUPPRESSION_CASES[@]}"; do
  suppression_symbol="${suppression_case%%:*}"
  suppression_rest="${suppression_case#*:}"
  suppression_id="${suppression_rest%%:*}"
  suppression_needle="${suppression_rest#*:}"

  run_probe_arm "$PROBE_DIR/nowarn-$suppression_id.log" "$suppression_symbol" \
    "/p:ProbeNoWarn=AONT004%3B$suppression_id"
  require_sole_error \
    "$PROBE_DIR/nowarn-$suppression_id.log" \
    "$suppression_id" \
    "$suppression_needle" \
    "the $suppression_symbol probe under <NoWarn>$suppression_id"
  echo "OK: $suppression_id survived <NoWarn>$suppression_id."

  write_severity_none_editorconfig "$suppression_id"
  run_probe_arm "$PROBE_DIR/editorconfig-$suppression_id.log" "$suppression_symbol" \
    /p:ProbeNoWarn=AONT004
  rm -f "$PROBE_EDITORCONFIG"
  require_sole_error \
    "$PROBE_DIR/editorconfig-$suppression_id.log" \
    "$suppression_id" \
    "$suppression_needle" \
    "the $suppression_symbol probe under .editorconfig severity=none"
  echo "OK: $suppression_id survived .editorconfig severity=none."
done

if [[ -e "$PROBE_EDITORCONFIG" ]]; then
  echo "FAIL: the suppression matrix left an .editorconfig behind in the probe directory." >&2
  exit 2
fi

echo "OK: AONT216, AGWF044 and AGWF045 all survived <NoWarn> and .editorconfig severity=none."

# --- Arm 7: the cross-assembly seam, at the packed tier (#204) -------------
#
# Every arm above lives in ONE compilation, which is the shape the #167 proof was
# always complete for. This one splits the binding across a real assembly
# boundary: a producer project declares the ontology and the binding, a consumer
# project declares the workflow and references the producer, and both build
# against the PACKED analyzers rather than the ones in this repository's bin.
#
# Three verdicts, because each fails differently:
#   legal      - the producer exports a proof catalog and defers; the consumer
#                reads it and proves the binding. Both build clean.
#   illegal    - the producer's leaf contract no longer refines; the consumer must
#                refute it with AGWF041, because no other compilation can.
#   unexported - the producer is built WITHOUT the generator package, so nothing
#                exports the contract. AONT222 must refuse it at the producer,
#                since a consumer only ever sees an absence.

XASM_DIR="$PROBE_DIR/xasm"
mkdir -p "$XASM_DIR/producer" "$XASM_DIR/consumer"
cp "$PROBE_DIR/NuGet.Config" "$XASM_DIR/NuGet.Config"

write_producer_project() {
  local with_generator="$1"
  local generator_reference=""
  if [[ "$with_generator" == "with-generator" ]]; then
    generator_reference="<PackageReference Include=\"LevelUp.Strategos.Generators\" Version=\"$VERSION\" />"
  fi

  cat > "$XASM_DIR/producer/ProducerProbe.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>ProducerProbe</AssemblyName>
    <NoWarn>\$(ProbeNoWarn)</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LevelUp.Strategos" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Ontology" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Ontology.Generators" Version="$VERSION" />
    $generator_reference
    <PackageReference Include="WolverineFx" Version="$WOLVERINE_VERSION" />
    <PackageReference Include="WolverineFx.Marten" Version="$WOLVERINE_MARTEN_VERSION" />
    <PackageReference Include="Marten" Version="$MARTEN_VERSION" />
  </ItemGroup>
</Project>
EOF
}

write_producer_source() {
  local receive_requires="$1"

  cat > "$XASM_DIR/producer/Producer.cs" <<EOF
using Strategos.Ontology;
using Strategos.Ontology.Builder;

namespace ProducerProbe;

public sealed class Order
{
    public string Id { get; set; } = string.Empty;
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
                .BoundToWorkflow("xasm-probe");

            obj.Action("receive")
                .Requires(order => order.Stage == $receive_requires)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);

            obj.Action("complete")
                .Requires(order => order.Stage == 1)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage);
        });
    }
}
EOF
}

cat > "$XASM_DIR/consumer/XasmConsumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NoWarn>\$(ProbeNoWarn)</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../producer/ProducerProbe.csproj" />
    <PackageReference Include="LevelUp.Strategos" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Agents" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Generators" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Ontology" Version="$VERSION" />
    <PackageReference Include="LevelUp.Strategos.Ontology.Generators" Version="$VERSION" />
    <PackageReference Include="WolverineFx" Version="$WOLVERINE_VERSION" />
    <PackageReference Include="WolverineFx.Marten" Version="$WOLVERINE_MARTEN_VERSION" />
    <PackageReference Include="Marten" Version="$MARTEN_VERSION" />
  </ItemGroup>
</Project>
EOF

cat > "$XASM_DIR/consumer/Consumer.cs" <<'EOF'
using System;
using System.Threading;
using System.Threading.Tasks;

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Steps;

namespace XasmConsumer;

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

// The bound action, its contract and the ontology that declares them are all in
// ProducerProbe. Nothing here mentions them except by ordinal name.
[Workflow("xasm-probe")]
public static partial class XasmProbeWorkflowDefinition
{
    public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
        .Create("xasm-probe")
        .StartWith<ReceiveStep>(step => step
            .Performs(new WorkflowActionReference("orders", "Order", "receive")))
        .Finally<CompleteStep>(step => step
            .Performs(new WorkflowActionReference("orders", "Order", "complete")));
}
EOF

build_xasm() {
  local log="$1"
  shift
  dotnet build "$XASM_DIR/consumer/XasmConsumer.csproj" \
    --nologo \
    -v:m \
    --no-incremental \
    /p:ProbeNoWarn=AONT004 \
    /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
    /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES" \
    "$@" > "$log" 2>&1 || true
}

# (7a) legal — the producer exports, the consumer proves.
write_producer_project with-generator
write_producer_source 0
if ! dotnet restore "$XASM_DIR/consumer/XasmConsumer.csproj" \
       --nologo -v:m --no-cache \
       /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
       /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES" > "$PROBE_DIR/xasm-restore.log" 2>&1; then
  echo "INDETERMINATE: the cross-assembly probe could not restore; no product verdict was reached." >&2
  cat "$PROBE_DIR/xasm-restore.log" >&2
  exit 3
fi

build_xasm "$PROBE_DIR/xasm-legal.log"
if grep -Eq "error (AGWF|AONT)[0-9]+" "$PROBE_DIR/xasm-legal.log"; then
  echo "FAIL: the legal cross-assembly binding did not build clean." >&2
  grep -E "error (AGWF|AONT)[0-9]+" "$PROBE_DIR/xasm-legal.log" >&2
  exit 2
fi
if ! grep -Fq "Build succeeded" "$PROBE_DIR/xasm-legal.log"; then
  echo "FAIL: the legal cross-assembly probe did not build at all." >&2
  cat "$PROBE_DIR/xasm-legal.log" >&2
  exit 2
fi
echo "OK: a workflow binding declared in a referenced assembly builds clean through the packed analyzers."

# (7b) illegal seam — the imported contract no longer refines.
write_producer_source 9
build_xasm "$PROBE_DIR/xasm-illegal.log"
if ! grep -Fq "AGWF041" "$PROBE_DIR/xasm-illegal.log"; then
  echo "FAIL: an illegal cross-assembly seam was not refuted (expected AGWF041)." >&2
  cat "$PROBE_DIR/xasm-illegal.log" >&2
  exit 2
fi
echo "OK: an illegal seam across an assembly boundary is refuted with AGWF041."

# (7c) unexported — the producer has no generator, so nothing carries the contract.
write_producer_project without-generator
write_producer_source 0
if ! dotnet restore "$XASM_DIR/producer/ProducerProbe.csproj" \
       --nologo -v:m --no-cache \
       /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
       /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES" > "$PROBE_DIR/xasm-producer-restore.log" 2>&1; then
  echo "INDETERMINATE: the unexported-producer probe could not restore; no product verdict was reached." >&2
  exit 3
fi
dotnet build "$XASM_DIR/producer/ProducerProbe.csproj" \
  --nologo -v:m --no-incremental \
  /p:ProbeNoWarn=AONT004 \
  /p:RestorePackagesPath="$PROBE_GLOBAL_PACKAGES" \
  /p:NuGetPackageRoot="$PROBE_GLOBAL_PACKAGES" > "$PROBE_DIR/xasm-unexported.log" 2>&1 || true
if ! grep -Fq "AONT222" "$PROBE_DIR/xasm-unexported.log"; then
  echo "FAIL: a binding no assembly exports was not refused (expected AONT222)." >&2
  cat "$PROBE_DIR/xasm-unexported.log" >&2
  exit 2
fi
echo "OK: a binding declared by an assembly that exports no proof catalog is refused with AONT222."

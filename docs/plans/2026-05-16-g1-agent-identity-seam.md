# Implementation Plan — G1-Strategos Agent-Identity Seam

**Date:** 2026-05-16
**Feature ID:** `g1-agent-identity-seam`
**Design:** [`docs/designs/2026-05-16-g1-agent-identity-seam.md`](../designs/2026-05-16-g1-agent-identity-seam.md)
**Release target:** `LevelUp.Strategos.Identity.Abstractions 2.7.0-preview.1` + `LevelUp.Strategos.Generators 2.7.0-preview.1` + `LevelUp.Strategos 2.7.0-preview.1`
**Test framework:** TUnit 1.2.11 + Verify.SourceGenerators 2.5.0 + NSubstitute 5.3.0
**TUnit invocation:** `dotnet test src/Strategos.Identity.Abstractions.Tests/ -- --treenode-filter "/*/*/*/{TestName}"` (per [feedback_tunit_test_invocation](../../.claude/memory/feedback_tunit_test_invocation.md))

## Iron Law

**NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST.** Every task starts with `[RED]` — a test that fails for a specific reason — before any production code is written.

## Topology overview

```text
                            ┌───────────────┐
                            │ T1 skeleton   │  (sln, csproj, packages, PublicAPI)
                            └───────┬───────┘
                                    │
            ┌───────────────────────┼───────────────────────────┐
            │           │           │           │           │
        ┌───▼──┐    ┌───▼──┐    ┌──▼──┐    ┌───▼──┐    ┌────▼───┐
        │ T2   │    │ T3   │    │ T4  │    │ T5   │    │  T6    │ ◄── PARALLEL
        │ Wkfl │    │ Agnt │    │ Hdrs│    │ IPhase│   │ Provid │     (5 tasks)
        │ Id   │    │ Id   │    │     │    │ Aware │   │ Port   │
        └──────┘    └───┬──┘    └─────┘    └──┬───┘    └────────┘
                        │                     │
                        │                     │
                    ┌───▼─────────────────┐   │
                    │ T7 Accessor port    │   │
                    │ + Fake impl         │   │
                    └─────────────────────┘   │
                                              │
                              ┌───────────────▼────────────┐
                              │ T8 Generator emit          │  ◄── BLOCKING
                              │   CurrentPhaseName +       │      (touches generators)
                              │   IPhaseAwareSaga          │
                              └───────────┬────────────────┘
                                          │
                  ┌───────────────┬───────────────┬───────────────┬───────────────┐
                  │               │               │               │               │
              ┌───▼──┐         ┌──▼──┐         ┌──▼──┐         ┌──▼──┐         ┌──▼──┐
              │ T9   │         │ T10 │         │ T11 │         │ T12 │         │ T13 │ ◄── PARALLEL
              │ DR-7 │         │ Snap│         │ E2E │         │ DR-8│         │coord│     (5 tasks
              │ neg. │         │ regn│         │ stub│         │sweep│         │basil│      after T8)
              └──────┘         └─────┘         └─────┘         └─────┘         └──┬──┘
                                          │                                       │
                                  ┌───────▼──────┐                                │
                                  │ T14 Release  │  ◄── FINAL                     │
                                  │   CHANGELOG  │ (T13 runs *after* T14 since    │
                                  │   versions   │  basileus needs preview.1)     │
                                  │   pack       │◄───────────────────────────────┘
                                  └──────────────┘
```

## Task list

---

### Task 1: Bootstrap `Strategos.Identity.Abstractions` project skeleton

**Phase:** RED → GREEN → REFACTOR
**DR coverage:** DR-1, DR-7 (foundation)
**Parallelizable:** No (blocks all subsequent tasks)
**Estimated:** 5 min

1. **[RED]** Write `tests/build/ProjectStructureTests.cs::Strategos_Identity_Abstractions_Csproj_Exists_AndTargetsNetStandard20`
   - File: `src/Strategos.Identity.Abstractions.Tests/ProjectStructureTests.cs`
   - Asserts `Strategos.Identity.Abstractions.csproj` exists, `TargetFramework=netstandard2.0`, `ManagePackageVersionsCentrally=true`
   - **Expected failure:** csproj does not yet exist

2. **[GREEN]** Create minimum skeleton
   - File: `src/Strategos.Identity.Abstractions/Strategos.Identity.Abstractions.csproj` (netstandard2.0 + Lvlup.Build + MinVer + PackageId `LevelUp.Strategos.Identity.Abstractions`)
   - File: `src/Strategos.Identity.Abstractions.Tests/Strategos.Identity.Abstractions.Tests.csproj` (net10.0 + TUnit + NSubstitute + Verify.SourceGenerators + ProjectReference to abstractions)
   - File: `src/Strategos.Identity.Abstractions/PublicAPI.Shipped.txt` (empty — preview, no shipped surface yet)
   - File: `src/Strategos.Identity.Abstractions/PublicAPI.Unshipped.txt` (empty; tasks T2..T7 will populate)
   - Update `src/strategos.sln` — add both projects
   - Update `src/Directory.Packages.props` — no new PackageVersion entries required (uses existing TUnit/NSubstitute/Verify versions)
   - Update `src/stylecop.json` — inherit existing
   - **Iron law check:** the structural test now passes

3. **[REFACTOR]** None — minimal skeleton

**Dependencies:** None
**Output artifact:** project skeleton committed; `dotnet build src/Strategos.Identity.Abstractions/` passes with no source

---

### Task 2: `WorkflowIdentity` sealed record

**Phase:** RED → GREEN → REFACTOR
**DR coverage:** DR-2, DR-8 (rows 3, 6)
**Parallelizable:** Yes (with T3, T4, T5, T6)
**Estimated:** 5 min

1. **[RED]** Write `WorkflowIdentityTests.cs` with TUnit tests:
   - `WorkflowIdentity_ConstructedWithValue_StoresValueExactly`
   - `WorkflowIdentity_NullValue_ThrowsArgumentNullException`
   - `WorkflowIdentity_EmptyValue_ThrowsArgumentException`
   - `WorkflowIdentity_WhitespaceValue_ThrowsArgumentException`
   - `WorkflowIdentity_NonAsciiValue_ThrowsArgumentException`
   - `WorkflowIdentity_IsSealedRecord_ViaReflection` (asserts `typeof(WorkflowIdentity).IsSealed`)
   - File: `src/Strategos.Identity.Abstractions.Tests/WorkflowIdentityTests.cs`
   - **Expected failure:** `WorkflowIdentity` type does not exist

2. **[GREEN]** Implement the record
   - File: `src/Strategos.Identity.Abstractions/WorkflowIdentity.cs`
   - `public sealed record WorkflowIdentity` with positional `string Value` parameter
   - Constructor body validates: non-null, non-empty after trim, ASCII-safe (each char `<= 0x7E && >= 0x20`)
   - Update `PublicAPI.Unshipped.txt` with the new public surface

3. **[REFACTOR]** Extract ASCII validation into `internal static class IdentityValueValidator` if shared with `AgentIdentity` (likely needed)

**Dependencies:** T1
**Test names:** TUnit `Method_Scenario_Outcome` convention

---

### Task 3: `AgentIdentity` sealed record

**Phase:** RED → GREEN → REFACTOR
**DR coverage:** DR-2, DR-8 (rows 3, 6)
**Parallelizable:** Yes (with T2, T4, T5, T6)
**Estimated:** 4 min

1. **[RED]** Write `AgentIdentityTests.cs` — mirrors T2 tests:
   - `AgentIdentity_ConstructedWithValue_StoresValueExactly`
   - `AgentIdentity_NullValue_ThrowsArgumentNullException`
   - `AgentIdentity_EmptyValue_ThrowsArgumentException`
   - `AgentIdentity_NonAsciiValue_ThrowsArgumentException`
   - `AgentIdentity_IsSealedRecord_ViaReflection`
   - File: `src/Strategos.Identity.Abstractions.Tests/AgentIdentityTests.cs`
   - **Expected failure:** `AgentIdentity` type does not exist

2. **[GREEN]** Implement
   - File: `src/Strategos.Identity.Abstractions/AgentIdentity.cs`
   - `public sealed record AgentIdentity(string Value)` with validation in constructor
   - Reuses `IdentityValueValidator` from T2's refactor
   - Update `PublicAPI.Unshipped.txt`

3. **[REFACTOR]** Confirm shared validator pattern

**Dependencies:** T1 (T2 if validator is shared — coordinate)

---

### Task 4: `StrategosHeaders` constants

**Phase:** RED → GREEN
**DR coverage:** DR-4
**Parallelizable:** Yes
**Estimated:** 3 min

1. **[RED]** Write `StrategosHeadersTests.cs`:
   - `StrategosHeaders_WorkflowIdentity_EqualsExpectedConstantValue` (`"x-strategos-workflow-identity"`)
   - `StrategosHeaders_AgentIdentity_EqualsExpectedConstantValue` (`"x-strategos-agent-identity"`)
   - `StrategosHeaders_AllKeys_FollowXStrategosPrefix` (reflection over public consts)
   - `StrategosHeaders_AllKeys_AreAsciiLowerKebabCase`
   - File: `src/Strategos.Identity.Abstractions.Tests/StrategosHeadersTests.cs`
   - **Expected failure:** type does not exist

2. **[GREEN]** Implement
   - File: `src/Strategos.Identity.Abstractions/StrategosHeaders.cs`
   - `public static class StrategosHeaders` with `public const string WorkflowIdentity` and `AgentIdentity`
   - Update `PublicAPI.Unshipped.txt`

**Dependencies:** T1

---

### Task 5: `IPhaseAwareSaga` interface

**Phase:** RED → GREEN
**DR coverage:** DR-6 (interface portion)
**Parallelizable:** Yes
**Estimated:** 3 min

1. **[RED]** Write `IPhaseAwareSagaTests.cs`:
   - `IPhaseAwareSaga_IsPublicInterface_ViaReflection`
   - `IPhaseAwareSaga_HasCurrentPhaseNameProperty_AsStringGetter`
   - `IPhaseAwareSaga_CurrentPhaseName_HasNoSetter`
   - File: `src/Strategos.Identity.Abstractions.Tests/IPhaseAwareSagaTests.cs`
   - **Expected failure:** type does not exist

2. **[GREEN]** Implement
   - File: `src/Strategos.Identity.Abstractions/IPhaseAwareSaga.cs`
   - `public interface IPhaseAwareSaga { string CurrentPhaseName { get; } }`
   - Update `PublicAPI.Unshipped.txt`

**Dependencies:** T1

---

### Task 6: `IAgentIdentityProvider` port + stub fake

**Phase:** RED → GREEN → REFACTOR
**DR coverage:** DR-3, DR-8 (row 4)
**Parallelizable:** Yes (depends on T2, T3)
**Estimated:** 6 min

1. **[RED]** Write contract tests using a `StubAgentIdentityProvider` fake:
   - `StubAgentIdentityProvider_DeriveStepIdentity_ReturnsAgentIdentity_ContainingWorkflowAndPhase`
   - `StubAgentIdentityProvider_DeriveStepIdentity_NullWorkflow_ThrowsArgumentNullException`
   - `StubAgentIdentityProvider_DeriveStepIdentity_NullPhaseName_ThrowsArgumentNullException`
   - `StubAgentIdentityProvider_DeriveStepIdentity_EmptyPhaseName_ThrowsArgumentException`
   - `StubAgentIdentityProvider_ParseWorkflowHeader_NullValue_ThrowsArgumentNullException`
   - `StubAgentIdentityProvider_ParseWorkflowHeader_ValidValue_RoundTrips`
   - File: `src/Strategos.Identity.Abstractions.Tests/IAgentIdentityProviderContractTests.cs`
   - File: `src/Strategos.Identity.Abstractions.Tests/Fakes/StubAgentIdentityProvider.cs`
   - **Expected failure:** `IAgentIdentityProvider` type does not exist

2. **[GREEN]** Implement the port
   - File: `src/Strategos.Identity.Abstractions/IAgentIdentityProvider.cs`
   - Two methods: `AgentIdentity DeriveStepIdentity(WorkflowIdentity, string)`, `WorkflowIdentity ParseWorkflowHeader(string)`
   - File: `src/Strategos.Identity.Abstractions.Tests/Fakes/StubAgentIdentityProvider.cs` — minimal impl that concatenates inputs (e.g., `$"{workflow.Value}#{phaseName}"`)
   - Update `PublicAPI.Unshipped.txt`

3. **[REFACTOR]** Stabilize XML docs; ensure both methods document their argument-validation contract

**Dependencies:** T2, T3

---

### Task 7: `IAgentIdentityAccessor` port + fake

**Phase:** RED → GREEN → REFACTOR
**DR coverage:** DR-5, DR-8 (row 1)
**Parallelizable:** Yes (depends on T2, T3; can run with T6)
**Estimated:** 6 min

1. **[RED]** Write contract tests using a `FakeAgentIdentityAccessor`:
   - `FakeAgentIdentityAccessor_NoEnvelopeContext_CurrentWorkflowReturnsNull`
   - `FakeAgentIdentityAccessor_NoEnvelopeContext_CurrentAgentReturnsNull`
   - `FakeAgentIdentityAccessor_BothHeadersPresent_ReturnsParsedRecords`
   - `FakeAgentIdentityAccessor_OnlyWorkflowHeader_CurrentAgentReturnsNull`
   - `FakeAgentIdentityAccessor_HeaderValueInvalid_ReturnsNullNoThrow`
   - File: `src/Strategos.Identity.Abstractions.Tests/IAgentIdentityAccessorContractTests.cs`
   - File: `src/Strategos.Identity.Abstractions.Tests/Fakes/FakeAgentIdentityAccessor.cs` (in-memory dictionary-backed)
   - **Expected failure:** `IAgentIdentityAccessor` type does not exist

2. **[GREEN]** Implement
   - File: `src/Strategos.Identity.Abstractions/IAgentIdentityAccessor.cs`
   - `public interface IAgentIdentityAccessor { WorkflowIdentity? CurrentWorkflow { get; } AgentIdentity? CurrentAgent { get; } }`
   - File: `Fakes/FakeAgentIdentityAccessor.cs` — constructed with `IDictionary<string,string>?` representing envelope headers; returns null when constructed with null
   - Update `PublicAPI.Unshipped.txt`

3. **[REFACTOR]** Ensure XML docs note that `null` is the contract for "no envelope context" (matches `IHttpContextAccessor` convention)

**Dependencies:** T2, T3

---

### Task 8: Generator emits `CurrentPhaseName` + `IPhaseAwareSaga` base

**Phase:** RED → GREEN → REFACTOR
**DR coverage:** DR-6 (generator portion)
**Parallelizable:** No (touches the generator; T9, T10, T11 depend on this)
**Estimated:** 10 min

1. **[RED]** Add tests to `Strategos.Generators.Tests`:
   - `SagaEmitter_GeneratesPartialClass_WithIPhaseAwareSagaInBaseList`
     - Uses existing `CSharpGeneratorDriver` pattern from `SagaEmitterIntegrationTests.cs`
     - Asserts generated source contains `: Saga, IPhaseAwareSaga`
   - `SagaEmitter_GeneratesCurrentPhaseNameProperty_AsComputedReadOnlyOverPhaseToString`
     - Asserts generated source contains `public string CurrentPhaseName => Phase.ToString();`
   - `SagaEmitter_GeneratesUsingForStrategosIdentityAbstractions`
     - Asserts usings list includes the abstractions namespace
   - File: `src/Strategos.Generators.Tests/SagaIdentityEmitterTests.cs` (new)
   - **Expected failure:** generated output does not contain the new lines

2. **[GREEN]** Modify generator:
   - File: `src/Strategos.Generators/Strategos.Generators.csproj` — add `<ProjectReference Include="..\Strategos.Identity.Abstractions\Strategos.Identity.Abstractions.csproj" PrivateAssets="all" />` AND add `<None Include="..\Strategos.Identity.Abstractions\bin\$(Configuration)\netstandard2.0\Strategos.Identity.Abstractions.dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />` so the analyzer assembly is published alongside the generator
   - File: `src/Strategos.Generators/Emitters/SagaEmitter.cs` lines 75-80 — add `"Strategos.Identity.Abstractions"` to the `usings` list
   - File: `src/Strategos.Generators/Emitters/SagaEmitter.cs` line 156 — change `$"public partial class {sagaClassName} : Saga"` to `$"public partial class {sagaClassName} : Saga, IPhaseAwareSaga"`
   - File: `src/Strategos.Generators/Emitters/Saga/SagaPropertiesEmitter.cs` — after the `Phase` property block (after line 65), append:
     ```csharp
     sb.AppendLine("    /// <summary>Gets the current saga phase as a stable string identifier (Phase.ToString()).</summary>");
     sb.AppendLine("    public string CurrentPhaseName => Phase.ToString();");
     sb.AppendLine();
     ```

3. **[REFACTOR]** Consider extracting to a dedicated `SagaIdentityComponentEmitter : ISagaComponentEmitter` if cohesion improves; otherwise leave inline in `SagaPropertiesEmitter` (the property logically belongs with other saga properties)

**Dependencies:** T1, T5
**Note:** the generator assembly ships analyzer-style; consumer packages reference `Strategos.Identity.Abstractions` at runtime separately

---

### Task 9: DR-7 negation tests (generator does NOT emit identity fields)

**Phase:** RED → GREEN (no-op; just adds the regression net)
**DR coverage:** DR-7
**Parallelizable:** Yes (with T10, T11; depends on T8)
**Estimated:** 4 min

1. **[RED]** Write negation tests in `Strategos.Generators.Tests`:
   - `SagaEmitter_DoesNotEmit_CurrentAgentIdentity_Property`
   - `SagaEmitter_DoesNotEmit_InitializeIdentity_Helper`
   - `SagaEmitter_DoesNotEmit_WorkflowIdentityField`
   - `SagaEmitter_DoesNotEmit_IdentityProviderField`
   - `SagaEmitter_DoesNotEmit_InternalsVisibleTo_Attribute`
   - Each test asserts the generated source string does NOT contain the named token
   - File: `src/Strategos.Generators.Tests/SagaIdentityEmitterTests.cs` (append; or separate file `SagaIdentityNegationTests.cs`)
   - **Expected outcome:** these tests pass on the first run (T8 didn't emit any of these) — this is the regression net for any future drift

**Dependencies:** T8
**Note:** because these tests pass immediately, they are a regression-guard. Document explicitly in the test file that they enforce DR-7.

---

### Task 10: Existing saga snapshots regenerate

**Phase:** REGRESSION
**DR coverage:** DR-6 (acceptance criteria — existing samples)
**Parallelizable:** Yes (with T9, T11; depends on T8)
**Estimated:** 5 min

1. **[RED]** Run `dotnet test src/Strategos.Generators.Tests/` — expect existing snapshot tests for Phronesis, review-loop, fork/join samples, and `samples/{AgenticCoder,ContentPipeline,MultiModelRouter}` to FAIL because their snapshots now contain the new `CurrentPhaseName` + `IPhaseAwareSaga` lines

2. **[GREEN]** Run Verify auto-accept (e.g., `dotnet verify accept` or copy `.received.txt` to `.verified.txt`) and inspect each snapshot diff to confirm the ONLY changes are:
   - `: Saga` → `: Saga, IPhaseAwareSaga`
   - one new property block `public string CurrentPhaseName => Phase.ToString();`
   - one new `using Strategos.Identity.Abstractions;`

3. **[REFACTOR]** None — snapshots are the regression spec

**Dependencies:** T8
**Manual verification step:** human eyeballs the diff on at least one sample snapshot to confirm scope

---

### Task 11: End-to-end seam verification (no Basileus reference)

**Phase:** RED → GREEN
**DR coverage:** DR-6 acceptance criteria (compile + runtime behavior with stubs)
**Parallelizable:** Yes (with T9, T10; depends on T6, T8)
**Estimated:** 8 min

1. **[RED]** Write `EndToEndStubIntegrationTests.cs`:
   - `GeneratedSaga_WithStubProvider_CompilesAndExposesPhaseName_WithoutBasileusReference`
     - Compiles a trivial workflow definition through `CSharpGeneratorDriver`
     - Builds the resulting assembly referencing ONLY `Strategos.*` (assert csproj of the test fixture, or check loaded assemblies don't include `Basileus.*`)
     - Loads + instantiates the saga via reflection
     - Asserts `saga is IPhaseAwareSaga`
     - Asserts `saga.CurrentPhaseName` returns `Phase.ToString()` (e.g., `"NotStarted"` before any step runs)
   - `IPhaseAwareSaga_FakeMiddlewareStampsEnvelopeHeaders_StubProviderDerivesAgent`
     - Constructs a `FakeAgentIdentityAccessor` populated from a fake envelope-header dictionary
     - Constructs a `StubAgentIdentityProvider`
     - Simulates middleware: read `saga.CurrentPhaseName`, call `provider.DeriveStepIdentity(workflow, phase)`, stamp header, read via accessor
     - Asserts the derived `AgentIdentity` is read back correctly
   - File: `src/Strategos.Generators.Tests/EndToEndStubIntegrationTests.cs`
   - **Expected failure:** generator hasn't been updated yet (T8 dependency)

2. **[GREEN]** Tests pass once T8 lands; no additional code beyond what T6/T8 produced

3. **[REFACTOR]** None — this is the acceptance gate

**Dependencies:** T6, T8

---

### Task 12: DR-8 error handling and edge cases integration sweep

**Phase:** RED → GREEN
**DR coverage:** DR-8 — error handling and edge cases (all six rows of the table — integration-level)
**Parallelizable:** Yes (with T9, T10, T11; depends on T6, T7, T8)
**Estimated:** 5 min

1. **[RED]** Write a single integration test class that asserts the DR-8 table holds end-to-end:
   - `DR8_AccessorReadOutsideHandler_ReturnsNull_NoThrow` (covers DR-8 row 1; instantiates `FakeAgentIdentityAccessor` with null envelope and reads both properties)
   - `DR8_NoIncomingWorkflowHeader_MiddlewareGeneratesNewIdentity_DocumentedAsBasileusContract` (covers DR-8 row 2; asserts a TODO/skip flag that the basileus middleware is responsible; this test is a *documentation* test — passes trivially but anchors the contract)
   - `DR8_IdentityRecordConstructedWithNullValue_ThrowsArgumentException` (covers DR-8 row 3; runs against both `WorkflowIdentity` and `AgentIdentity` parametrically)
   - `DR8_ProviderReturnsNull_DocumentedAsBasileusContract_NotEnforcedHere` (covers DR-8 row 4; documentation test; the stub provider's contract is enforced in T6)
   - `DR8_HandlerEmitsMessage_HeadersRideOnEnvelope_NativeWolverineMechanism_DocumentedNotTested` (covers DR-8 row 5; documentation test; Wolverine-native, covered by Wolverine's own test suite)
   - `DR8_HeaderValueWithNonAsciiCharacter_RecordConstructorRejects` (covers DR-8 row 6; parametric over both identity records)
   - File: `src/Strategos.Identity.Abstractions.Tests/DR8EdgeCasesIntegrationTests.cs`
   - **Expected failure:** at least one assertion path doesn't yet exist (likely the parametric coverage)

2. **[GREEN]** Tests pass once T2, T3, T7 land; no additional production code needed — this is the DR-8 acceptance gate, not new behavior

3. **[REFACTOR]** None — this is the explicit DR-8 traceability anchor

**Dependencies:** T2, T3, T6, T7
**Note:** Rows 2 and 4 and 5 of DR-8 are basileus-middleware contracts; the tests here document the boundary but enforce only the Strategos-side rows (1, 3, 6).

---

### Task 13: DR-9 basileus coordination handoff

**Phase:** Coordination (not RED-GREEN per se — this is a cross-repo task)
**DR coverage:** DR-9
**Parallelizable:** No (final coordination step, runs after T12 release)
**Estimated:** 8 min

1. **Open basileus tracking issue** on lvlup-sw/basileus titled `G1-strategos integration: re-cut PR #184 against Strategos.Identity.Abstractions 2.7.0-preview.1` with body including:
   - Link to this plan and the design doc
   - Required basileus changes:
     - Reference `LevelUp.Strategos.Identity.Abstractions 2.7.0-preview.1`
     - Move `Basileus.Core.Contracts.Identity.{SpiffeId, WorkflowId, WorkflowIdentity, AgentIdentity, IAgentIdentityProvider}` to `Basileus.Identity.Spiffe.*` as adapter implementations (or delete; consume Strategos types)
     - Implement `Basileus.Identity.Spiffe.SpiffeAgentIdentityProvider : Strategos.Identity.Abstractions.IAgentIdentityProvider`
     - Implement `Basileus.AgentHost.Middleware.StrategosHeaderMiddleware` (Wolverine middleware spec from the design §9)
     - Configure `opts.Policies.PropagateIncomingHeaderToOutgoing(StrategosHeaders.WorkflowIdentity)` in `UseWolverine` registration
     - Revise basileus design doc `2026-05-13-g1-implementation-phase-0.md` to reflect envelope-header storage (preserve §4 INV-8 derivation-from-saga-state rationale; update §6 Strategos-side deliverables to read "Strategos owns identity ports; this PR is the basileus adapter only")
   - Acceptance criteria mirrored from design DR-9:
     - basileus PR #184 builds against the published `2.7.0-preview.1`
     - Integration test on basileus side verifies outbound envelope carries both `x-strategos-workflow-identity` and `x-strategos-agent-identity` headers
     - basileus design doc revision committed in the same PR

2. **Comment on basileus PR #184** linking to the new tracking issue, this plan, and the Strategos design

3. **Update this Strategos issue (#71) and #70** with:
   - Note that A1/A2/A3 (issues #67/#68/#69) are descoped per this design
   - Link to the published `Strategos.Identity.Abstractions 2.7.0-preview.1` on nuget.org
   - Mark #67/#68/#69 closed-as-superseded with reference to this design

**Acceptance criteria:**
- basileus tracking issue exists with full task list
- PR #184 comment is posted
- Strategos issues #67/#68/#69 are closed-as-superseded with cross-links

**Dependencies:** T12 (preview release must be published first so basileus can pin to it)
**Not parallelizable:** runs last
**Note:** This is coordination work, not Strategos code. It belongs in the Strategos plan because it's required for the design to actually deliver value (basileus is the inaugural consumer).

---

### Task 14: Release: CHANGELOG + version bump + pack verification

**Phase:** RED → GREEN
**DR coverage:** DR-10
**Parallelizable:** No (final step, depends on T1..T11)
**Estimated:** 6 min

1. **[RED]** Write a release-shape verification test (TUnit `[Test]` in `Strategos.Identity.Abstractions.Tests`):
   - `Release_DotnetPack_ProducesThreePackages_AtPreviewVersion`
     - Shells `dotnet pack src/ -c Release` (or reads from a pre-packed staging directory)
     - Asserts existence of `LevelUp.Strategos.Identity.Abstractions.2.7.0-preview.1.nupkg`, `LevelUp.Strategos.Generators.2.7.0-preview.1.nupkg`, `LevelUp.Strategos.2.7.0-preview.1.nupkg`
   - **Expected failure:** version is still 2.6.0; CHANGELOG missing

2. **[GREEN]** Apply release deltas:
   - `CHANGELOG.md` — add `## [2.7.0-preview.1] - 2026-05-16` section under the existing `## Unreleased`. Body:
     - `### Added` — `Strategos.Identity.Abstractions` package debut (`WorkflowIdentity`, `AgentIdentity`, `IAgentIdentityProvider`, `IAgentIdentityAccessor`, `IPhaseAwareSaga`, `StrategosHeaders`)
     - `### Changed` — generator emits `CurrentPhaseName` computed property on every saga; saga base list adds `IPhaseAwareSaga` interface. Additive — no breaking changes.
     - `### Migration` — basileus consumers register `opts.Policies.PropagateIncomingHeaderToOutgoing(StrategosHeaders.WorkflowIdentity)` in their `UseWolverine` block
   - Bump `MinVer` minimum to `2.7.0-preview.1` (or pass via `dotnet pack /p:MinVerSkip=true /p:Version=2.7.0-preview.1` if MinVer is git-tag-driven)
   - Pre-tag verification: `dotnet pack src/Strategos.Identity.Abstractions/` produces a valid nupkg
   - `README.md` (Strategos root) — add a new `### Identity Seam (v2.7.0-preview.1)` subsection documenting the abstractions package, `StrategosHeaders`, and the `PropagateIncomingHeaderToOutgoing` consumer-side registration
   - Move `PublicAPI.Unshipped.txt` entries → `PublicAPI.Shipped.txt` (preview API is still pre-shipped per #51 protocol; defer this move to GA, but document the surface in CHANGELOG)

3. **[REFACTOR]** None — release artifacts speak for themselves

**Dependencies:** T1..T11

---

## Parallelization groups

| Wave | Tasks | Concurrency |
|---|---|---|
| 0 | T1 | 1 |
| 1 | T2, T3, T4, T5 | 4 |
| 2 | T6, T7 | 2 |
| 3 | T8 | 1 (touches generators — serial) |
| 4 | T9, T10, T11, T12 | 4 |
| 5 | T14 (release) | 1 |
| 6 | T13 (basileus coordination, after T14 publishes) | 1 |

**Total expected critical path:** ~45 min (T1 + T2 + T6 + T8 + T11 + T14 + T13 = 5 + 5 + 6 + 10 + 8 + 6 + 8 = 48 min).

**Worktree strategy:** Wave 1 can run in 4 parallel worktrees; Wave 2 in 2; Wave 4 in 4. All must rebase onto T8 once it lands. T13 is coordination (no code) and runs after T14 publishes to nuget.org.

## Design-to-Task Coverage Matrix

| DR | Title | Task(s) |
|---|---|---|
| DR-1 | `Strategos.Identity.Abstractions` package | T1 (skeleton), T12 (pack verification) |
| DR-2 | `WorkflowIdentity` + `AgentIdentity` sealed records | T2, T3 |
| DR-3 | `IAgentIdentityProvider` port | T6 |
| DR-4 | `StrategosHeaders` constants | T4 |
| DR-5 | `IAgentIdentityAccessor` for in-handler reads | T7 |
| DR-6 | Generator emits `CurrentPhaseName` + `IPhaseAwareSaga` | T5 (interface), T8 (emit), T10 (snapshot regen), T11 (e2e verification) |
| DR-7 | Generator emits NO other identity code | T9 (negation tests) |
| DR-8 | Error handling and edge cases | T2 (rows 3, 6), T3 (rows 3, 6), T6 (row 4), T7 (row 1), **T12 (consolidated sweep across all six rows)** |
| DR-9 | Cross-repo coordination (basileus PR #184 re-cut) | **T13 (basileus tracking issue + PR #184 comment + Strategos issue closeout)** |
| DR-10 | Release `2.7.0-preview.1` | T14 |

## Risks and mitigations

1. **Generator-package layout (ProjectReference vs analyzer pack):** the generator must ship `Strategos.Identity.Abstractions` as an analyzer-side reference so the generator can compile against it at SG-time. T8 step 2 covers this with `<None Include=... Pack="true" PackagePath="analyzers/dotnet/cs" />`. If the analyzer-pack approach fails, fallback is `<PackageReference Include="LevelUp.Strategos.Identity.Abstractions" Version="2.7.0-preview.1" PrivateAssets="all" GeneratePathProperty="true" />` and embed via `<TargetPath>analyzers/dotnet/cs/$(PackageStrategos_Identity_Abstractions)\lib\netstandard2.0\Strategos.Identity.Abstractions.dll</TargetPath>` — well-documented Roslyn SG pattern.
2. **MinVer tag-driven versioning:** the repo uses MinVer for versioning from git tags. T12 may need to pre-create a `v2.7.0-preview.1` git tag *before* pack, or use `/p:Version=2.7.0-preview.1` override.
3. **Existing snapshot test count:** T10 may regenerate 20+ snapshot files. Manual diff inspection is needed on at least one representative sample (AgenticCoder) to confirm the change is *only* the additive `CurrentPhaseName` + base-list update.
4. **TUnit filter syntax:** all `dotnet test` invocations use `-- --treenode-filter "/*/*/*/{TestName}"` per project convention (see memory).

## Out of scope for this plan

- Wolverine middleware implementation (lives in basileus repo)
- SPIFFE adapter (lives in basileus repo)
- basileus design doc revision (cross-repo task)
- `Strategos.Identity` default accessor implementation package (deferred to 2.7.0 GA)
- G3 ProvenanceEnvelope (#61), G5 SubagentSpawn (#62), G4 PartitionablePair (#60)

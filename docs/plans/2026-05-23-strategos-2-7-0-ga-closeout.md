# Implementation Plan — Strategos 2.7.0 GA closeout

**Design:** `docs/designs/2026-05-23-strategos-2-7-0-ga-closeout.md`
**Date:** 2026-05-23 · **Milestone:** Strategos 2.7.0 — Agent Capabilities (GA)
**Iron Law:** no production code without a failing test first.

## Test invocation

Per project convention, TUnit runs via tree-node filter, **not** `--filter`:

```bash
dotnet test src/Strategos.Agents.Tests -- --treenode-filter "/*/*/*/<TestName>"
```

## Behaviors / scope

| Source | Behavior | Code? | TDD shape |
|---|---|---|---|
| #85 | Foreign (non-`AgentException`) tool-source throw is wrapped in `AgentToolSourceException` (AGAG007) with URI user-info redacted; `AgentException` subtypes propagate unchanged | Yes | RED→GREEN→REFACTOR (T1–T4) |
| #84 | Remove dead `AgentStepConfiguration.ChatClientConfigurator` record property | Yes | Refactor under green suite (T5) |
| #92 | Settle `AgentToolLoopException` payload; XML-doc the OTel mapping | Docs | T6 (prose-gated) |
| #83/#86/#87 | Design-diagram + XML-doc reconciliation | Docs | T7 (prose-gated) |
| #67/#68/#69 | Close as superseded by PR #81 | None | T8 (issue housekeeping) |
| #70 | CHANGELOG + cut 2.7.0 GA | Release | T9 (prose-gated, MinVer discipline) |

## Key files

- Port: `src/Strategos.Agents.Abstractions/IToolSource.cs`
- Seam (impl): `src/Strategos.Agents/Configuration/StrategosFunctionsChatClient.cs` → `ResolveToolsAsync`
- Exception: `src/Strategos.Agents/Exceptions/AgentToolSourceException.cs` (AGAG007, reused)
- #84 record: `src/Strategos.Agents/Configuration/AgentStepConfiguration.cs`; builder pass-site `src/Strategos.Agents/AgentStepBuilder.cs:196`
- Redaction tests: `src/Strategos.Agents.Tests/Unit/Configuration/StrategosFunctionsChatClientToolSourceInjectionTests.cs`
- New fixtures: `src/Strategos.Agents.Tests/Fixtures/` (sibling to `InProcessTestToolSource.cs`, `ThrowingStreamingChatClient.cs`)
- Prose gate: `src/Strategos.Agents.Tests/Unit/DocumentationAndProseGateTests.cs` (scans `.cs`/`.md`/`.csproj` under Strategos.Agents + `src/Strategos.Agents/README.md` + `CHANGELOG.md`)

---

## PR α — agent closeout (T1–T7)

### Task 1: Foreign tool-source throw is wrapped and redacted
**Phase:** RED → GREEN → REFACTOR

1. [RED] Add fixture `ForeignThrowingToolSource` (a bare `IToolSource` whose `GetToolsAsync` throws a non-`AgentException` — e.g. `InvalidOperationException` — with a message containing `https://alice:s3cr3t@mcp.example/tools`).
   - File: `src/Strategos.Agents.Tests/Fixtures/ForeignThrowingToolSource.cs`
2. [RED] Write test: `ResolveTools_ForeignSourceThrows_WrapsInAgentToolSourceExceptionWithRedactedMessage`
   - File: `src/Strategos.Agents.Tests/Unit/Configuration/StrategosFunctionsChatClientToolSourceInjectionTests.cs`
   - Asserts: thrown type is `AgentToolSourceException`; `Diagnostic == AGAG007`; message contains `mcp.example` but **not** `s3cr3t` nor `alice:s3cr3t`; `InnerException` is the original `InvalidOperationException`.
   - Expected failure: today the seam propagates the raw `InvalidOperationException` unchanged.
3. [GREEN] In `ResolveToolsAsync`, wrap the `source.GetToolsAsync` call: rethrow `OperationCanceledException` and `AgentException` unchanged; wrap any other exception in `AgentToolSourceException(RedactUserInfo(ex.Message), source.GetType().FullName, ex)`.
   - File: `src/Strategos.Agents/Configuration/StrategosFunctionsChatClient.cs`
4. [REFACTOR] Extract `RedactUserInfo(string)` as an `internal static` helper (regex/Uri-span elision of `scheme://user:pass@` → `scheme://`). Keep it private/internal to `Strategos.Agents`.

**Dependencies:** None
**Parallelizable:** No (shares file with T2/T3)

### Task 2: AgentException subtypes propagate unchanged
**Phase:** RED → GREEN

1. [RED] Add fixture `AgentExceptionThrowingToolSource` whose `GetToolsAsync` throws `AgentToolSourceException("boom", "Custom")` (already-redacted, conforming).
   - File: `src/Strategos.Agents.Tests/Fixtures/AgentExceptionThrowingToolSource.cs`
2. [RED] Write test: `ResolveTools_AgentExceptionSource_PropagatesUnchanged`
   - File: same injection test file as T1
   - Asserts: the exact thrown instance is rethrown (reference-equal / not re-wrapped, no nested `AgentToolSourceException` of an `AgentToolSourceException`).
   - Expected failure: only after T1's catch exists, to pin that the `catch (AgentException) { throw; }` arm precedes the foreign-wrap arm.
3. [GREEN] Satisfied by T1's `catch (AgentException) throw;` ordering; add the arm if T1 was written wrap-first.

**Dependencies:** T1
**Parallelizable:** No

### Task 3: Cancellation propagates unwrapped
**Phase:** RED → GREEN

1. [RED] Write test: `ResolveTools_SourceThrowsOperationCanceled_PropagatesUnwrapped`
   - File: same injection test file
   - Asserts: a cancelled token surfacing `OperationCanceledException` from a source is **not** wrapped in `AgentToolSourceException`.
   - Expected failure: a naive `catch (Exception)` would swallow/wrap cancellation.
2. [GREEN] Ensure `catch (OperationCanceledException) throw;` precedes the foreign-wrap arm.
   - File: `src/Strategos.Agents/Configuration/StrategosFunctionsChatClient.cs`

**Dependencies:** T1
**Parallelizable:** No

### Task 4: IToolSource failure-contract doc
**Phase:** GREEN (docs) — prose-gated

1. [GREEN] Add a `### Failure contract` paragraph to the `IToolSource` XML doc: conforming adapters throw an `AgentException` subtype with redaction applied in-adapter (propagated unchanged); foreign exceptions are wrapped-and-redacted at the `ResolveToolsAsync` boundary; redaction covers URI user-info only (the DR-10 threat), not arbitrary secrets.
   - File: `src/Strategos.Agents.Abstractions/IToolSource.cs`
2. Verify `DocumentationAndProseGateTests` still passes (no AI-writing tells).

**Dependencies:** T1
**Parallelizable:** Yes (doc-only, separate file)

### Task 5: Remove dead `ChatClientConfigurator` property (#84)
**Phase:** RED → GREEN → REFACTOR

1. [RED] In `AgentStepConfigurationTests`, delete the `config.ChatClientConfigurator` `IsNull` assertion and the `ChatClientConfigurator:` named arg from its construction sites. Suite no longer compiles → the failing state.
   - File: `src/Strategos.Agents.Tests/Unit/Configuration/AgentStepConfigurationTests.cs`
2. [GREEN] Remove the property (decl + ctor param + assignment) from the record; drop the `ChatClientConfigurator: _chatClientConfigurator` pass-site in the builder (the configurator stays applied at compose-time in `ComposeChatClient`; only the unread record field is removed).
   - Files: `src/Strategos.Agents/Configuration/AgentStepConfiguration.cs`, `src/Strategos.Agents/AgentStepBuilder.cs`
3. [REFACTOR] Remove the now-orphaned `ChatClientConfigurator: null,` named args from all remaining construction sites:
   - `src/Strategos.Agents.Tests/Integration/AgentStepBaseExecuteTests.cs` (5×), `AgentStepBaseStreamingTests.cs` (3×), `AgentStepBaseToolLoopTests.cs` (1×)
   - Confirm `dotnet build` clean + full Agents.Tests suite green.

**Dependencies:** None
**Parallelizable:** No (touches builder + many test files)

### Task 6: Settle #92 — AgentToolLoopException payload doc
**Phase:** GREEN (docs) — prose-gated

1. [GREEN] Keep payload as `IReadOnlyList<ChatMessage>` (no type change). Add XML-doc note mapping it to OTel gen-ai span events (`gen_ai.tool.name`, `gen_ai.tool.call.id`) and stating the structured `ToolCallTrace` record is deferred (no consumer).
   - File: `src/Strategos.Agents/Exceptions/AgentToolLoopException.cs`
2. Verify prose gate passes.

**Dependencies:** None
**Parallelizable:** Yes

### Task 7: Docs reconciliation #83/#86/#87
**Phase:** GREEN (docs) — prose-gated

1. [GREEN] #83 — update the DR-1/DR-2 diagram in `docs/designs/2026-05-17-strategos-agents-meai-10-5.md` to show `AgentStepBuilder.Build(IChatClient)` (matches shipped code).
2. [GREEN] #86 — document the `AgentStepBuilder` tool-list bound expectation (XML doc on the relevant `WithTool`/`Build` member).
   - File: `src/Strategos.Agents/AgentStepBuilder.cs`
3. [GREEN] #87 — document direct `ChatClientBuilder` use in `AgentStepBuilder.ComposeChatClient`.
   - File: `src/Strategos.Agents/AgentStepBuilder.cs`
4. Verify prose gate passes.

**Dependencies:** None
**Parallelizable:** Partially (T7.1 is a different file from T7.2/T7.3)

---

## Issue housekeeping (no PR)

### Task 8: Close #67/#68/#69 as superseded
1. `gh issue close 67 68 69` each with a comment: superseded by PR #81 (identity seam emits `CurrentPhaseName` + `IPhaseAwareSaga` base-list addition; validated by shipped generator tests) — original A1–A3 subtask shape not implemented.

**Dependencies:** None · **Parallelizable:** Yes · **No test** (administrative)

---

## PR β — release (T9)

### Task 9: CHANGELOG + cut 2.7.0 GA (#70)
**Phase:** GREEN (release) — prose-gated

1. [GREEN] CHANGELOG.md: add 2.7.0 GA section. Include the #85 entry noting the tool-source seam now **wraps+redacts foreign exceptions** and narrows the prior "propagate unchanged" contract to `AgentException`-subtypes-only.
2. Version bump per MinVer discipline — explicit `<Version>` is silently overwritten unless paired with `<MinVerSkip>true</MinVerSkip>` + `<PackageVersion>` (see `project_minver_version_override`). Verify via `dotnet pack` / `nbgv`-equivalent that the produced nupkg version is `2.7.0`.
3. Verify prose gate passes on CHANGELOG.md.

**Dependencies:** T1–T7 merged (CHANGELOG reflects final surface)
**Parallelizable:** No · **Sequence:** after PR α merges

---

## Parallelization summary

- **Sequential chain (PR α core):** T1 → T2, T1 → T3 (same file, same seam).
- **Parallel-safe within PR α:** T4 (doc), T5 (#84, different files), T6 (#92 doc), T7 (docs) can proceed alongside the T1 chain; only T5 touches many files so run it on its own pass to avoid churn.
- **Independent:** T8 (housekeeping, no code).
- **Gated last:** T9 (release) after PR α merges.

## Risks

- **Prose gate (DIM-8):** T4/T6/T7/T9 all add prose under gated paths — write in domain voice, no AI tells, or `DocumentationAndProseGateTests` fails.
- **#84 blast radius:** 12 construction sites; a missed site breaks compilation (caught by build). Pure refactor — no behavior change.
- **#85 redaction scope:** `RedactUserInfo` covers URI user-info only; the XML doc must state this limit so no false assurance is implied (design §Fork 1).

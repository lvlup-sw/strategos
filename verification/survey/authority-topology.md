---
repo_path: /home/reedsalus/.cursor/worktrees/strategos/891j
target_kind: diff
revision: 324768f4d4f6d292e7d86045f711c6c50946b8c9
base_revision: 4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa
target_ref: cursor/c801a047
cost_setting: high
scope_rule: reverse dependency closure of changed surfaces vs merge-base 4d060f4
updated: 2026-08-27
skipped: none
lens: 3. Authority Topology
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/185
    why: residue tracker and “still open by design” list
  - path: /home/reedsalus/.cursor/plans/issue_185_remainder_125df8c7.plan.md
    why: the dispatch plan this follow-up is verifying
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/docs/specs/2026-08-22-correctness-core.md
    why: AGWF035 / termination-class design
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/CHANGELOG.md
    why: claims the diff makes about itself (Residue (#185) subsection)
---

# Survey lens 3 — Authority Topology

Revision `324768f`. Diff `4d060f4...HEAD`. Scope surfaces from `verification/stage0.md`.
External references treated as leads, not facts.

**Adopted single-source idiom in this repo (measured, not assumed):** TypeSpec →
`scripts/contracts-codegen.sh` → checked-in `Generated/` + `schemas/` +
`docs/diagnostics/agwf.md`, bound by `.github/workflows/contracts-codegen-guard.yml`
(regenerate + `git diff --exit-code`). Production C# may not hand-author `AGWF0xx`
literals (`AgwfSingleSourceTests`). Public shipping surface is bound by
RS0016/RS0017 against `PublicAPI.{Shipped,Unshipped}.txt`. Later code that bypasses
those idioms is stronger evidence than their absence.

---

## B1. AGWF diagnostic identity (the `AGWF0xx` closed set)

**Count: 11 representations.**

| # | Kind | Where |
|---|---|---|
| 1 | Authored TypeSpec enum | `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp:24-56` (`DuplicatePermittedForkTrigger: "AGWF037"` at :55) |
| 2 | Authored TypeSpec models | same file, one `AgwfEntry*` per code; AGWF037 at :358-366 (`id: "AGWF037"`) |
| 3 | Generated C# enum | `src/Strategos.Contracts/Generated/AgwfCode.g.cs:21-146` |
| 4 | Generated C# constants | `src/Strategos.Contracts/Generated/AgwfCodes.g.cs:16-110` |
| 5 | Generated catalog JSON | `src/Strategos.Contracts/Generated/agwf-catalog.json` |
| 6 | Generated enum schema | `src/Strategos.Contracts/schemas/json-schema/AgwfCode.json:5-37` |
| 7 | Generated entry schema | `src/Strategos.Contracts/schemas/json-schema/AgwfEntryDuplicatePermittedForkTrigger.json` (and 30 siblings) |
| 8 | Generated docs table | `docs/diagnostics/agwf.md:13-45` |
| 9 | Test constant | `AgwfCatalogSchemaTests.GroundTruthCodes` (`AgwfCatalogSchemaTests.cs:26-35`) |
| 10 | Test constant | `AgwfCatalogEmitterTests.GroundTruthCodes` (`AgwfCatalogEmitterTests.cs:23-32`) — **identical copy of #9** |
| 11 | Test constant | `AgwfMarkdownTests.GroundTruthCodes` (`AgwfMarkdownTests.cs:18-27`) — **identical copy of #9** |

Plus a twelfth authored map if counted separately: `AgwfCodeEnumTests.Expected`
name→wire pairs at `AgwfCodeEnumTests.cs:22-55`. Production reporting
(`WorkflowDiagnostics.cs:36` etc.) uses `AgwfCodes.*` and is **not** a twelfth
authority for the identity string.

**Claimed authority:** TypeSpec (`AgwfCatalog.tsp:7-12`).

**Effective authority for generated C# / catalog / markdown:** the `AgwfEntry*`
**models**, not the TypeSpec enum. `AgwfCatalogEmitter.RunAsync`
(`AgwfCatalogEmitter.cs:62-87`) reads only `AgwfEntry*.json`. It does not read
`AgwfCode.json`. The TypeSpec `enum AgwfCode` compiles to representation #6 only.

**Binders that exist:**

- `contracts-codegen-guard.yml:56-60` — generated #3–#8 must match a regenerate.
- `AgwfSingleSourceTests.cs:29-68` — no production `AGWF0xx` literal under
  `src/Strategos*` (excludes `Generated/`, `*.Tests`).
- `AgwfCatalogSchemaTests` — entry-schema ids == `GroundTruthCodes` (#9).
- `AgwfCatalogEmitterTests` — catalog JSON ids == `GroundTruthCodes` (#10).
- `AgwfMarkdownTests` — markdown rows == `GroundTruthCodes` (#11).
- `AgwfCodeEnumTests` — generated enum == `Expected` name/wire table.

**Binder that does not exist:** nothing requires TypeSpec `enum AgwfCode` (#1) and
TypeSpec `AgwfEntry*` models (#2) to enumerate the same set. Adding an enum member
without a model updates `AgwfCode.json` and leaves `AgwfCode.g.cs` /
`agwf-catalog.json` unchanged. Adding a model without an enum member does the
reverse. Codegen-guard still passes if both outputs are regenerated.

**Adherence:** the #52 / INV-5 identity pipeline **exists and is used** for AGWF037
(new member added in tsp, regenerated artifacts, `WorkflowDiagnostics` routes
through `AgwfCodes.DuplicatePermittedForkTrigger` at `WorkflowDiagnostics.cs:611-612`).
The single-source grep gate still holds for production C#. The idiom **decayed
inside the catalog itself**: two authored lists in one file, and three hand-copied
`GroundTruthCodes` arrays plus one `Expected` table that are not derived from
TypeSpec. Those tests bind copies to generated output; they do not derive the
copies from one source.

**Finding:** more than one authoritative representation of the code set
(TypeSpec enum **and** TypeSpec models). Three identical test-constant lists
(`SchemaTests` / `EmitterTests` / `MarkdownTests`) are a fourth authored surface
with no derivation binder between the copies.

---

## B2. AGWF diagnostic metadata (severity, title, message)

**Count: 5 representations.**

| # | Kind | Where |
|---|---|---|
| 1 | Authored TypeSpec model fields | e.g. AGWF037 `severity`/`summary`/`remediation` at `AgwfCatalog.tsp:361-364` |
| 2 | Generated catalog / schema / markdown | derived from #1 via emitter + codegen-guard |
| 3 | Authored Roslyn descriptors | `WorkflowDiagnostics.cs` `title` / `messageFormat` / `defaultSeverity` — AGWF037 at :611-618; AGWF035 at :561-568 |
| 4 | Parity test | `AgwfCatalogParityTests.cs:38-78` compares #2 JSON to #3 live descriptors |
| 5 | CHANGELOG / spec prose | `CHANGELOG.md:172-182`; `docs/specs/2026-08-22-correctness-core.md` |

`WorkflowDiagnostics.cs:15-20` states the split in comments: catalog is single
source for **identity**; “Severities and message formats remain authored here.”

**Authority:** two. TypeSpec models **and** `WorkflowDiagnostics` descriptors are
both hand-authored. They agree today (parity test would fail otherwise).

**Binder:** `AgwfCatalogParityTests` — **agreement**, not derivation. Nothing
generates `DiagnosticDescriptor` fields from TypeSpec.

**Adherence:** the parity gate **exists and is used** for the new AGWF037
descriptor (title/message match the tsp remediation). The adopted identity idiom
was **not** extended to metadata. This is the same dual-authoring the parity
test was written to paper over (`AgwfCatalogParityTests.cs:15-22`).

**Finding:** two authoritative representations of severity/title/message, even
though they agree at this revision.

---

## B3. Workflow transition graph (`PhaseGraph` / `ValidTransitions` / Mermaid)

**Count: 6 representations of “the real successor graph.”**

| # | Kind | Where |
|---|---|---|
| 1 | Shared algorithm | `src/Strategos.Generators/Models/PhaseGraph.cs` (`Build` at :67; remarks at :16-17 claim diagnostic + `ValidTransitions` cannot drift) |
| 2 | Emitted public table | `TransitionsEmitter.cs:56` calls `PhaseGraph.Build(model)`; emits `ValidTransitions` at :68-85 |
| 3 | Guard under-reach | `TerminalReachabilityGuard.cs:127` `phaseGraph ?? PhaseGraph.Build(model)` |
| 4 | Guard over-reach | `TerminalReachabilityGuard.cs:85-117` — **list-position** successor via `NextNotIn` + `constructOwned`, does **not** read `PhaseGraph` |
| 5 | Mermaid diagram | `MermaidEmitter.cs:43-109` — independent classification + per-construct edge walk; **does not call `PhaseGraph`** |
| 6 | Terminal phase name literals | `PhaseGraph.CompletedPhase`/`FailedPhase` at `PhaseGraph.cs:41-46` **and** `PhaseEnumEmitter.cs:110-113` hard-coded `Completed,` / `Failed,` |

**Authority claimed:** one `PhaseGraph` (`CHANGELOG.md:177`; `PhaseGraph.cs:16-17`).

**What production actually shares:** the **type**, not one instance.
`WorkflowIncrementalGenerator.cs:1038-1043` calls `TerminalReachabilityGuard.Report`
**without** a `phaseGraph`. `TransitionsEmitter.Emit` rebuilds independently at
`:56`. Same `Build` algorithm, two calls.

**Binders:**

- #2 and #3 under-reach are bound to #1 by calling `PhaseGraph.Build`.
- #4 is a second successor algorithm. No test or type requires it to equal
  `PhaseGraph.SuccessorsOf`.
- #5 has no binder to #1. The correctness-core spec
  (`docs/specs/2026-08-22-correctness-core.md:159`) named Mermaid as the same
  lying-public-API class as `ValidTransitions`. ValidTransitions was retargeted
  onto `PhaseGraph`; Mermaid was not.
- #6: `TransitionsEmitter.cs:84-85` uses `PhaseGraph.CompletedPhase`/`FailedPhase`.
  `PhaseEnumEmitter` does not. No binder requires the emitted enum member names
  to equal those constants.

**Adherence:** the lift of `PhaseGraph` out of `TransitionsEmitter` **exists** and
the under-reach arm **uses** it. The “share one graph so they cannot drift”
pattern **decayed / was never applied** to (a) Mermaid, (b) the over-reach arm,
(c) `PhaseEnumEmitter` terminal names, (d) instance sharing at the production
call site.

**Finding:** more than one authoritative successor graph. `PhaseGraph` is
authority for `ValidTransitions` + under-reach only. Mermaid and over-reach are
unbound copies of the same boundary. Terminal phase names have two authors.

---

## B4. PermitTrigger uniqueness (at most one closed trigger per diagnostic-fork edge)

**Count: 5 representations of the uniqueness rule.**

| # | Kind | Where |
|---|---|---|
| 1 | Runtime validator | `DiagnosticForkBuilder.PermitTrigger` HashSet add at `src/Strategos/Builders/DiagnosticForkBuilder.cs:160-165` (`InvalidOperationException`) |
| 2 | Generator model floor | `DiagnosticForkModel.Create` at `DiagnosticForkModel.cs:125-132` (throws); shared helper `FindDuplicateTriggerNames` at :149 |
| 3 | C# extract diagnostic | `DiagnosticForkExtractor.cs:119-151` own `HashSet` + `AGWF037`; skips `Create` at :174-180 |
| 4 | JSON import diagnostic | `WireToModelBridge.cs:459-491` calls `FindDuplicateTriggerNames` then reports `AGWF037` |
| 5 | Catalog / docs / CHANGELOG | `AgwfCatalog.tsp:358-366`; `docs/diagnostics/agwf.md:45`; `CHANGELOG.md:179-182` |

**Authority:** none single. Runtime #1 and generator #2/#3/#4 independently
implement “declare each trigger at most once.” #3 and #4 bind **reporting** to
`WorkflowDiagnostics.DuplicatePermittedForkTrigger` (identity from B1). The
**rule** is re-stated: extractor uses a private `HashSet` (`:119`), not
`FindDuplicateTriggerNames`. Runtime uses `HashSet<ForkTrigger>` on the CLR enum,
not the string helper.

**Binder:** none that requires runtime throw text, model throw text, and AGWF037
remediation to stay one sentence. `FindDuplicateTriggerNames` binds import (#4)
and model Create (#2) only.

**Adherence:** the “reject, do not first-wins-dedup” decision is implemented on
both C# extract and JSON import (claimed at `CHANGELOG.md:181-182`). The
extractor correctly avoids calling `Create` after a duplicate (`:174-180`) so the
model floor does not crash the generator. That is adherence to the fail-closed
pattern. The uniqueness **rule** itself has two independent algorithms
(runtime enum HashSet vs generator string HashSet).

**Finding:** two authoritative uniqueness implementations (runtime builder vs
generator). Import is bound to the generator helper; C# extract is a third
in-process copy of the same HashSet logic.

---

## B5. `DescriptorSource` member set and ordinals

**Count: 8 representations.**

| # | Kind | Where |
|---|---|---|
| 1 | Static type | `src/Strategos.Ontology/Descriptors/DescriptorSource.cs:40-64` (`HandAuthored=0`, `Ingested=1`, `HandAuthoredContract=2`) |
| 2 | PublicAPI | `src/Strategos.Ontology/PublicAPI.Unshipped.txt:326-329` |
| 3 | Numeric tests | `DescriptorSourceTests.cs:18-33` (asserts 0/1/2) |
| 4 | Runtime consumers | `IngestedIntentInvariant.cs:22`; `OntologyBuilder.cs:164-165` `IsHandSide`; `MergeTwo.cs` |
| 5 | Updated shipping docs | `CHANGELOG.md:192-194` |
| 6 | Stale shipping docs | `docs/src/content/docs/reference/ontology/api/source.md:63-66` lists **only** `HandAuthored` and `Ingested` |
| 7 | Stale shipping docs | `docs/src/content/docs/guide/ontology/ontology-sources.md:40-43` lists **only** two members; `:47` still says AONT205 gates “an ingested descriptor” without naming `HandAuthoredContract` |
| 8 | Stale design/reference copies | `docs/src/content/docs/guide/ontology/polyglot-descriptors.md:25,35` (snippet + “Source is HandAuthored … and Ingested”); `docs/src/content/docs/reference/2026-04-19-ingest-ontology-from-source.md:224`; `docs/designs/2026-05-10-ontology-2-5-0-polyglot-ingestion.md:114` |

**Authority:** `DescriptorSource.cs` enum. Numeric values are documented as
contract (`:8-10`).

**Binder:** RS0016/RS0017 binds #2 to the public enum. `DescriptorSourceTests`
binds #3 to #1. #4 consumes #1 (not a copy). **No binder** for docs #6–#8.

**Adherence / decay:** the additive-enum pattern (append `= 2`, do not move 0/1)
**exists and is followed** in the type, PublicAPI, and tests. This revision
**edited** `source.md` (CLR-free wording) and **left** the two-member provenance
list at `:63-66`. That is decay: the page was in the author’s hands and the
third member was not added. `polyglot-descriptors.md` was also edited in this
diff and still describes a two-value `Source`.

**Finding:** documentation is an unbound second (and third, and fourth)
representation of the member set. After this change, two shipping guide/reference
pages still omit `HandAuthoredContract = 2`.

---

## B6. MCP `CallToolResult.resultType`

**Count: 7 representations.**

| # | Kind | Where |
|---|---|---|
| 1 | External protocol | MCP 2026-07-28 `resultType` (`"complete"` \| `"input_required"` \| extensions) — not in repo |
| 2 | Hosting constant | `OntologyServerToolFactory.CompletedResultType = "complete"` at `:57` |
| 3 | Explicit assignments | `OntologyServerToolFactory.cs:386` (`MapTraversalResult`) and `:412` (`ErrorResult`) |
| 4 | SDK wrap of four discovered tools | handlers at `:155-225` return domain objects; MCP SDK 2.2.0 constructs `CallToolResult`. Hosting does not assign `ResultType` on that path |
| 5 | Package pin | `Strategos.Ontology.MCP.Hosting.csproj:18-20` `VersionOverride="2.2.0"` (comment: 1.3.0 has no `ResultType`) |
| 6 | Tests | `TraversalToolHostingTests.cs:98-112`; `ProviderBoundDispatchTests.cs:130-131` (four-tool path asserts `result.ResultType == CompletedResultType`) |
| 7 | INV-3 checklist | `.agents/skills/strategos-design-invariants/references/INV-3-mcp-first-class-latest-spec.md:11,23,36`; `deterministic-checks.md:112-124` (file-level `grep -L ResultType`) |

**Authority:** protocol spec (#1) for the field; `CompletedResultType` (#2) for
the value Strategos emits. SDK 2.2.0 is a second production writer for the
four-tool path (#4).

**Binder:**

- Tests #6 bind traversal + one query path to #2.
- Check 3.4 is a **documented grep**, not a CI job (no `.github` reference).
  `grep -L ResultType` on files that mention `CallToolResult` is file-level: one
  assignment anywhere in the file satisfies it. An unenforced convention is not
  an authority.
- Nothing requires every future `new CallToolResult` to go through one helper.

**Adherence:** the two **manual** constructions set `ResultType = CompletedResultType`.
INV-3’s “every `CallToolResult` construction” claim (`INV-3-mcp-first-class-latest-spec.md:23`)
holds for Hosting source as written today. The four-tool path relies on SDK
default/behavior, which `ProviderBoundDispatchTests` observes but Hosting does
not set. The adopted “always emit complete” idiom **exists** and is **partially**
applied.

**Finding:** two production writers for `resultType` (explicit Hosting vs SDK
wrap). INV-3 Check 3.4 is not a binder (unenforced, file-level).

---

## B7. MCP `Tool.icons` / `OntologyToolDescriptor.Icons`

**Count: 6 representations.**

| # | Kind | Where |
|---|---|---|
| 1 | External protocol | MCP 2026-07-28 `Tool.icons` / `Icon` |
| 2 | Core mirror type | `src/Strategos.Ontology.MCP/ToolIcon.cs:12-25` (`src`/`mimeType`/`sizes`/`theme`) |
| 3 | Descriptor slot | `OntologyToolDescriptor.cs:43` `IReadOnlyList<ToolIcon>? Icons` (default null) |
| 4 | Hosting map | `OntologyServerToolFactory.ApplyIcons` at `:248-262` maps onto SDK `Icon`; null → unset |
| 5 | PublicAPI | `src/Strategos.Ontology.MCP/PublicAPI.Unshipped.txt:126-127,194-203` |
| 6 | Tests + INV-3 | `OntologyToolDescriptorTests.cs:19-51`; `OntologyServerToolFactoryTests.cs:60-84`; Check 3.5 `deterministic-checks.md:126-133` (`grep -L Icons` on one file) |

**Authority:** protocol shape (#1). In-repo, `ToolIcon` + `Icons` nullable
property are the shipping types (INV-2 forbids an MCP dependency in core, so a
mirror type is the adopted pattern).

**Binder:** RS0016/RS0017 binds #5 to #2/#3. `ApplyIcons` is a **manual field
map**, not derived from a schema. Check 3.5 is an unenforced one-file grep
(not a binder). Tests bind null-when-unset and the four-field map.

**Adherence:** INV-2 mirror pattern **exists and is followed** (all four icon
fields mapped; null does not invent a placeholder). Check 3.5 cannot fail a
placeholder-icon regression; it only proves the identifier `Icons` appears in
one file.

**Finding:** none on dual authority for the in-repo type (one `ToolIcon`, one
`Icons` slot, PublicAPI bound). The protocol mirror is a known INV-2 split, not
a second in-repo author. Residual: Check 3.5 is not a binder.

---

## B8. Renovate `extends` preset path

**Count: 2 representations.**

| # | Kind | Where |
|---|---|---|
| 1 | Live config | `renovate.json:5` `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json` |
| 2 | CHANGELOG prose | `CHANGELOG.md:184-185` (describes `tools/` vs repo-root; does not quote the full token) |

**Authority:** `renovate.json` (the only value Renovate reads).

**Binder:** none. Nothing in this repo fetches or diffs the remote preset.
Nothing asserts the path token still resolves. The #181 class (control looks
present, 404s) has no structural gate.

**Adherence:** a single live path (good). The previous wrong path was itself a
single representation that was simply wrong — uniqueness did not prevent
inertness.

**Finding:** one live authority, **no binder** to the external object it names.
Count is 2 only because CHANGELOG restates the `tools/` fact without being
derived from `renovate.json`.

---

## B9. `IActionBuilder<T>.Requires` obsolescence

**Count: 7 representations of “how authors declare preconditions.”**

| # | Kind | Where |
|---|---|---|
| 1 | Interface `[Obsolete]` | `IActionBuilderOfT.cs:39-40` message: use `ActionDescriptor.Preconditions`; no fluent successor |
| 2 | Implementation `[Obsolete]` | `ActionBuilderOfT.cs:77-78` (same message) |
| 3 | Successor field | `ActionDescriptor.cs:28-33` `Preconditions` |
| 4 | PublicAPI | `PublicAPI.Unshipped.txt:109` lists `Requires(...)` — **does not record Obsolete** |
| 5 | Test | `IActionBuilderTests.cs:65-76` reflects `[Obsolete]` + message fragments |
| 6 | Guide (updated, still demos Requires) | `docs/src/content/docs/guide/ontology/index.md:45-46` example + caution at `:65-67` |
| 7 | Packaged README (unchanged) | `src/Strategos.Ontology/README.md:33-34` still shows `.Requires(...)` with **no** obsolete note |

Also: `Directory.Build.targets:4-5` suppresses `CS0618` for all test projects.
Older docs (`ontology-theoretical-grounding.md:84`,
`ontology-to-tools-grounding.md:143`, etc.) still teach `.Requires()` as the
pattern; those pages were not in this diff.

**Authority:** `[Obsolete]` on the interface (#1). #3 is the named successor.

**Binder:** `IActionBuilderTests` binds #5 to #1. Compiler requires #2 to exist
as a method; the duplicate `[Obsolete]` on #2 is convention. PublicAPI (#4)
tracks presence (RS0016/RS0017), **not** obsolescence. Docs #6/#7 have no binder
to #1.

**Adherence:** Obsolete + test **exist**. The guide was updated (caution box)
but the getting-started sample still calls `.Requires`. The package README —
the copy that ships — was **not** updated. That is decay of “docs follow the
attribute.”

**Finding:** two shipping document authorities for the happy path (guide with
caution vs README without). PublicAPI is not a binder for Obsolete.

---

## B10. Contracts package version `0.7.0`

**Count: 3 representations.**

| # | Kind | Where |
|---|---|---|
| 1 | MSBuild property | `Strategos.Contracts.csproj:37-40` `ContractsVersion` → `Version` + `PackageVersion` |
| 2 | Test constant | `PackagingTests.cs:105-119` asserts nupkg name and nuspec `<version>0.7.0</version>` |
| 3 | CHANGELOG | `CHANGELOG.md:182` |

**Authority:** `ContractsVersion` (#1). `Version`/`PackageVersion` are derived
from it in the same PropertyGroup.

**Binder:** `PackagingTests` requires the packed artifact to be `0.7.0`. Comment
at `Strategos.Contracts.csproj:32-35` claims a `contracts-v<version>` publish
workflow fails closed if the tag disagrees — that workflow was not re-read as
primary evidence in this lens (open question).

**Adherence:** the one-property pin **exists and is used** for the 0.6.0→0.7.0
bump.

**Finding:** none. Test constant is a copy bound by the pack test. CHANGELOG is
prose, not a second pin.

---

## Pattern existence vs decay (summary)

| Adopted idiom | Exists? | This diff’s adherence |
|---|---|---|
| TypeSpec → codegen-guard for AGWF **identity** | Yes | Used for AGWF037; **decayed** by dual tsp enum+models and 3× `GroundTruthCodes` |
| TypeSpec → descriptors for AGWF **metadata** | No (parity test instead) | Dual-authored; parity holds today |
| Shared `PhaseGraph` for diagnostic + `ValidTransitions` | Yes (type) | Under-reach uses it; Mermaid / over-reach / `PhaseEnumEmitter` names do not |
| `AgwfSingleSourceTests` grep | Yes | Held (production C# uses `AgwfCodes`) |
| PublicAPI RS0016/RS0017 | Yes | Used for `HandAuthoredContract`, `Icons`, `ToolIcon`; does not track Obsolete |
| INV-3 deterministic greps | Documented | **Not CI**; Check 3.4 file-level; Check 3.5 one-file |
| Additive enum (append, never move) | Yes | Followed in `DescriptorSource`; **docs decayed** on the same pages this diff edited |
| INV-2 MCP mirror types | Yes | `ToolIcon` ↔ SDK `Icon` mapped; null-when-unset held |

---

## What else was read

- `verification/stage0.md` (complete)
- `references/survey-lenses.md` §3
- `AgwfSingleSourceTests`, `AgwfCatalogParityTests`, `contracts-codegen-guard.yml`,
  `AgwfCatalogEmitter.cs`
- `PhaseGraph.cs`, `TransitionsEmitter.cs`, `TerminalReachabilityGuard.cs`,
  `WorkflowIncrementalGenerator.cs` (guard registration + `EmitWorkflowSources`),
  `MermaidEmitter.cs`, `PhaseEnumEmitter.cs`
- `OntologyServerToolFactory.cs`, `OntologyToolDescriptor.cs`, `ToolIcon.cs`,
  Hosting csproj pin, INV-3 spec + `deterministic-checks.md` §3.4–3.5
- `DescriptorSource.cs`, `IngestedIntentInvariant.cs`, PublicAPI.Unshipped,
  `source.md` diff, `ontology-sources.md`, `polyglot-descriptors.md`
- `IActionBuilderOfT.cs`, `ActionBuilderOfT.cs`, `ActionDescriptor.cs`,
  `IActionBuilderTests.cs`, `Directory.Build.targets`, ontology README + guide
- `DiagnosticForkBuilder.cs`, `DiagnosticForkModel.cs`, `DiagnosticForkExtractor.cs`,
  `WireToModelBridge.cs` (duplicate-trigger paths)
- `renovate.json`, `CHANGELOG.md` Residue (#185), catalog/schema/markdown tests

Issue 185 and the plan file were treated as leads only; no claim from them was
promoted to a stated behavior.

## Assumptions

- Nested `.worktrees/` copies are not scope; only the worktree root at `891j` is.
- Untracked `docs/2026-06-16-edge-*` files are out of scope (stage0).
- MCP SDK 2.2.0 is assumed to populate `CallToolResult.ResultType` on the
  four-tool wrap path because `ProviderBoundDispatchTests` observes `"complete"`.
  The SDK source was not opened.
- `contracts-v0.7.0` publish-tag gate mentioned in the csproj comment was not
  verified as a live workflow in this lens.

## Open questions

1. Does any test compare `schemas/json-schema/AgwfCode.json` (TypeSpec enum) to
   the `AgwfEntry*` id consts (TypeSpec models)? None was found; confirm absence
   is complete.
2. Does MCP SDK 2.2.0 **require** Hosting to set `ResultType`, or does it default
   to `"complete"` when wrapping a domain return value? Discriminates whether B6
   #4 is a second writer or SDK-owned.
3. Is `contracts-v<version>` tag/workflow a real binder for B10, or only a
   comment?
4. Should Mermaid be in the `PhaseGraph` closure (spec lead at
   `docs/specs/2026-08-22-correctness-core.md:159`) or is diagram drift accepted?
5. Is the AGWF035 over-reach arm a different boundary (list-position safety)
   or the same “real graph” as `PhaseGraph`? The CHANGELOG treats them as one
   shared graph.

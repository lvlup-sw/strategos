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

# Stage 0 — Target, scope set, cost control

## Target

- Kind: **diff**
- Branch: `cursor/c801a047`
- HEAD: `324768f4d4f6d292e7d86045f711c6c50946b8c9`
- Compare: `main` / merge-base `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
- Working tree: three untracked `docs/{designs,plans,research}/2026-06-16-edge-*` files are **out of scope** (plan T6: do not reopen junction-table / traversal leftover docs).

## Scope question — recorded answer (word for word)

> Knock out remaining #185 residue that is implementable without maintainer portal work or Option B: AGWF035 route-analysis, AGWF037 duplicate PermitTrigger, renovate path (#181), MCP resultType+Icons (#176/#177), DescriptorSource.HandAuthoredContract (#163), and Requires obsolete + rationale docs (#115). Must read: GitHub issue 185 (lvlup-sw/strategos), the plan at /home/reedsalus/.cursor/plans/issue_185_remainder_125df8c7.plan.md, docs/specs/2026-08-22-correctness-core.md, and the six track branches already merged into cursor/c801a047 (HEAD 324768f) vs main/4d060f4.

## Cost control (stated before any lens)

**Setting: high.** Signal that produced it:

1. The change **alters a published and generated contract** (`Strategos.Contracts` **0.6.0 → 0.7.0**, TypeSpec catalog, generated `AgwfCodes`, JSON Schema, NuGet content consumed by Exarchos).
2. The reverse-dependency closure **crosses process / network / trust boundaries**: MCP `CallToolResult` (protocol), Renovate GitHub App (external process), Contracts package consumers outside this repository.
3. The change **touches controls others rely on**: AGWF035/037 compile-time diagnostics, AONT205 retarget, INV-3 deny-list, PublicAPI shipping surface.
4. Author instruction: run **full survey + inventory + evaluation**, not cheapest-inline-only.

**Machinery this setting requires:** all five stages; every survey lens (1–7); every inventory lens (1–8); every evaluation lens including wildcard; **several independent refutation attempts per obligation** (not one). No stages or lenses skipped. `skipped: none`.

**Do not lower the setting during the run.** A raise is allowed if a still-higher signal appears; record the trigger.

## Out of wave (do not invent obligations that demand these)

- Option B (carry construct identity through exclusive paths) — deferred; AGWF036 stays the ship
- #147 Trusted Publishing (nuget.org portal)
- #133 parity-gate confirm and #174 live-issue proof
- #156.1 `sampleSize` semantic call and #156.3 positional `DiagnosticForkCount_{i}` re-key
- Maintainer portal work

#156.2 (duplicate PermitTrigger / AGWF037) **is** in this wave.

## Changed surfaces (from the diff, not file-name guess)

1. **`PhaseGraph` + `EdgeBuilder`** — lifted from private nested type in `TransitionsEmitter` to shared internal `src/Strategos.Generators/Models/PhaseGraph.cs`.
2. **`TerminalReachabilityGuard`** — AGWF035 under-reach / route-analysis arm added beside existing over-reach.
3. **`WorkflowIncrementalGenerator`** — still the production registration of the guard; now passes shared `PhaseGraph` / `MainFlowClassification`.
4. **`TransitionsEmitter`** — consumes shared `PhaseGraph` for emitted `ValidTransitions` (generated public API).
5. **AGWF catalog / Contracts 0.7.0** — `AgwfCatalog.tsp` adds AGWF037; generated C#, JSON catalog, JSON Schema, `docs/diagnostics/agwf.md`, packaging tests, `Directory.Build.targets`.
6. **`DiagnosticForkExtractor` / `DiagnosticForkModel` / `FluentDslParser` / `WireToModelBridge` / `WorkflowDiagnostics`** — AGWF037 on C# extract and JSON import; reject, do not first-wins-dedup.
7. **`renovate.json`** — second `extends` path now `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json`.
8. **`OntologyServerToolFactory`** — `CallToolResult` constructions set `resultType`; Hosting package pin 2.2.0.
9. **`OntologyToolDescriptor.Icons` + `ToolIcon`** — optional icons; null when unset; PublicAPI.Unshipped.
10. **INV-3 catalog** — deny-list / checklist updates for resultType vs icon gap.
11. **`DescriptorSource.HandAuthoredContract = 2`** — additive enum; `Ingested` stays 1; AONT205 retargeted via `IngestedIntentInvariant`.
12. **`OntologyBuilder` / `IOntologyBuilder` / `OntologyGraphBuilder` / `MergeTwo` / `OntologyDelta` / `ActionDescriptor`** — HandAuthoredContract authorship + merge provenance.
13. **`IActionBuilder<T>.Requires` `[Obsolete]`** — points at `ActionDescriptor.Preconditions`; no fluent successor.
14. **Ontology / platform docs** — CLR-free XOR polymorphic limit; first-class descriptor path.
15. **CHANGELOG Residue (#185)** — claims about all of the above.

## Reverse dependency closure (ranked by consequence × exposure)

### S1. AGWF035 route arm + shared PhaseGraph — **highest**

- **Changed:** `TerminalReachabilityGuard`, `PhaseGraph`, `TransitionsEmitter`, `WorkflowIncrementalGenerator`.
- **Consumers:** every `[Workflow]` compilation; generated `ValidTransitions` / `IsValidTransition` (emitted public API); generator tests; import path sharing the same IR.
- **Transitive:** consumer apps’ generated sagas; any tool that reads `ValidTransitions`.
- **Boundary:** compile-time diagnostic (INV-5). Shared graph with emitted public transition table — a drift is a published-API lie.
- **Reversal:** revert the generator commits. Does not reverse already-emitted consumer code until those consumers rebuild.

### S2. Contracts 0.7.0 + AGWF037 — **highest**

- **Changed:** TypeSpec catalog, generated codes/schemas, extractor, import bridge, packaging tests.
- **Consumers in-repo:** `Strategos.Generators` (`using Strategos.Contracts.Generated`), contracts-codegen-guard, AGWF single-source grep gate.
- **Consumers out-of-repo:** Exarchos extracts `agwf-catalog.json` and JSON Schema from the NuGet package. Emitted converters throw on unknown members, so consumer upgrade must precede producer emission.
- **Boundary:** published NuGet contract; process/network to Exarchos.
- **Reversal:** source revert. A published 0.7.0 tag does not reverse for already-upgraded consumers.

### S3. MCP `resultType` + `Icons` — **high**

- **Changed:** Hosting factory, `OntologyToolDescriptor`, `ToolIcon`, Hosting package pin, INV-3.
- **Consumers:** MCP clients speaking 2026-07-28; INV-3 audits; hosting tests.
- **Boundary:** network / protocol.
- **Reversal:** revert + downgrade Hosting pin. Protocol clients that already expect `resultType` would then see the old omission.

### S4. `DescriptorSource.HandAuthoredContract` + AONT205 retarget — **high**

- **Changed:** enum, builder, graph builder, merge, invariant, PublicAPI.
- **Consumers:** fluent ontology authors, TypeSpec/JSON contract ingest, merge of hand-authored + ingested graphs, AONT205 diagnostics.
- **Boundary:** published public API (additive enum). Diagnostic retarget changes who fails the build.
- **Reversal:** revert. Enum value `2` if published is a compatibility event.

### S5. `IActionBuilder<T>.Requires` obsolete — **medium-high**

- **Changed:** interface + implementation + PublicAPI + docs.
- **Consumers:** existing `Object<T>` fluent authors (must still compile; CS0618 if warnings-as-errors).
- **Boundary:** published public API (RS0016/RS0017).
- **Reversal:** revert Obsolete attribute. No runtime behavior change claimed.

### S6. Renovate preset path — **medium** (boundary crossing, low code)

- **Changed:** `renovate.json` one path token.
- **Consumers:** Renovate GitHub App (outside repo).
- **Boundary:** external process. If the path is still wrong, the bot never applies the org preset (the #181 class: a control that looks present and is inert).
- **Reversal:** revert the one-line path.

### S7. Docs + CHANGELOG claims — **medium** (claim inventory seed)

- **Changed:** CHANGELOG Residue subsection, ontology guides/reference, INV-3 spec, deterministic-checks, `docs/diagnostics/agwf.md`.
- **Consumers:** humans, design-invariant audits, later verification runs.
- **Boundary:** none runtime. These are the **claim inventory** corpus for Stage 2 derivation.

## Closure members that cross a boundary

| Surface | Boundary | Why it dominates cost |
|---|---|---|
| Contracts 0.7.0 / AGWF catalog | published package + Exarchos | generated contract, converter throw-on-unknown |
| MCP `CallToolResult.resultType` | network / protocol | composition must emit 2026-07-28 shape |
| `OntologyToolDescriptor.Icons` | published API + protocol | optional; null-when-unset is the invariant |
| Renovate `extends` | external process | inert-if-404 class |
| Generated `ValidTransitions` | emitted public API | shared `PhaseGraph` with AGWF035 |
| `DescriptorSource` / `Requires` | published PublicAPI | additive / obsolete, still a shipping surface |

## Reversal

A `git revert` of `4d060f4..324768f` reverses the source. It does **not** reverse:

- A published `contracts-v0.7.0` tag (not created by this branch; claim to check).
- Already-generated consumer saga source until rebuild.
- A Renovate run that already applied the corrected preset (benign).

## Existing verification workspace

None. This is the first run. Create `verification/` and extend it; do not overwrite later.

## Instructions for every later lens

- Treat external references as **leads, not facts** (`references/validating-claims.md`).
- Anchor findings to file and line at revision `324768f`.
- Do not invent obligations that require Option B, #147, #133/#174 maintainer proof, or #156.1/#156.3.
- Do not edit the plan file.
- Untracked `docs/2026-06-16-edge-*` files are out of scope.
- An empty lens is a useful result. Invented findings are worse than none.

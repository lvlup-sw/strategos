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

# Obligation ledger — `cursor/c801a047` @ 324768f vs 4d060f4

Cost control **high** was stated in `verification/stage0.md` before any lens ran. Survey: `verification/survey.md`. Evidence files: `verification/obligations/{slug}.md`. Claim-derivation index: `verification/obligations/_claim-derivation-index.md`. Guard draft: `verification/guards-draft.md`.

Canonical slugs below. Duplicate inventory files remain on disk as contributing evidence; the **Lenses** row lists them.

## Scope answer (word for word)

> Knock out remaining #185 residue that is implementable without maintainer portal work or Option B: AGWF035 route-analysis, AGWF037 duplicate PermitTrigger, renovate path (#181), MCP resultType+Icons (#176/#177), DescriptorSource.HandAuthoredContract (#163), and Requires obsolete + rationale docs (#115). Must read: GitHub issue 185 (lvlup-sw/strategos), the plan at /home/reedsalus/.cursor/plans/issue_185_remainder_125df8c7.plan.md, docs/specs/2026-08-22-correctness-core.md, and the six track branches already merged into cursor/c801a047 (HEAD 324768f) vs main/4d060f4.

## Open questions (run-wide)

- Is AGWF035-without-gating (Error still emits saga) intentional house style?
- Does any out-of-repo producer assign `DescriptorSource.HandAuthoredContract`?
- Does Renovate resolve `lvlup-claude` after the `exarchos` rename?
- Is `contracts-v0.7.0` published? Is `contracts-test` a required check?
- Is `ErrorResult` `{resultType: complete, isError: true}` protocol-legal? (INV-3 asserts yes.)

---

## Active

### [agwf035-underreach-ir-not-emission] — Under-reach locks IR, not saga dispatch

| | |
|---|---|
| **Claim** | AGWF035 under-reach fires when a rejoin last step does not dispatch the declared terminal in the **IR `PhaseGraph`**, not only when a test-injected graph lacks that edge. The shipped-saga walk is a third representation and is out of this wave. |
| **Scope** | S1. `TerminalReachabilityGuard.ReportUnderReach`; generator call omits `phaseGraph`; `PhaseGraph.Build` from the same model the #184 handler once ignored. |
| **Consequence** | The #184 class (IR has the rejoin; emitter forgets `Start{Finally}`) compiles. Postgres is required again. `ValidTransitions` still advertises the edge. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Closure of `PhaseGraph` rejoin edges ↔ emitted `Start{target}`. Current positives inject `WithoutSuccessor`. |
| **Why not cheaper** | Two `Build` calls agree today. Types cannot express “this IR edge is in the saga.” A generator test of `Build(model)` cannot fail for a forgotten handler. |
| **Failure signal** | Nothing at compile for the emitter-miss class. Hung saga at runtime. |
| **Rollback** | Revert the under-reach arm. Does not create a third-walk lock. |
| **Lenses** | False-Green; Promise Against Delivery; Representable Invalid States; Claim Derivation; Recurrence; Wildcard inventory |

**Open questions:**

- Does under-reach cover a #182/#186 emitter drop if the IR still has the construct? `(partial: mechanism says no)`
- Is a saga-emission lock in-scope for this wave, or is IR-vs-graph the accepted T1 deliverable? `(needs human input)`

Evidence: `agwf035-underreach-injected-graph.md`, `pad-agwf035-underreach-is-ir-not-emission.md`, `phasegraph-without-successor-representable.md`, `wild-three-walk-rejoin-agreement.md`, `claim-agwf035-emitter-dropped-edge.md`, `recur-termination-route-complete.md`.

### [phasegraph-type-not-instance] — Guard and table do not share one graph instance

| | |
|---|---|
| **Claim** | Guard and `ValidTransitions` resolve successors from one `PhaseGraph` instance so they cannot drift. |
| **Scope** | `TransitionsEmitter.cs:56`; `TerminalReachabilityGuard.cs:127`; generator `Report` does not pass a graph. |
| **Consequence** | Two later-divergent `Build`s publish a table that disagrees with AGWF035. CHANGELOG overclaims “share one PhaseGraph.” |
| **Proof rung** | Construction and generation |
| **Proof artifact** | Pass one `Build` result into both `Report` and `Emit`, or an edge-equality lock. |
| **Why not cheaper** | Type-share is already present. The claim is instance-share. Call-site scan ignores argument 6. |
| **Failure signal** | Nothing. |
| **Rollback** | Revert the lift. Consumer tables do not reverse until rebuild. |
| **Lenses** | Promise Against Delivery; False-Green; Claim Derivation; Authority Topology |

**Open questions:** None on instance vs type.

Evidence: `pad-phasegraph-type-not-instance.md`, `agwf035-call-site-scan-ignores-graph.md`, `claim-phasegraph-no-drift.md`.

### [agwf035-catalog-polarity-lie] — Under-reach ships the over-reach sentence

| | |
|---|---|
| **Claim** | An AGWF035 under-reach report describes a missing dispatch, or the catalog sentence is rewritten once the catalog is already bumped. |
| **Scope** | `AgwfCatalog.tsp`; `WorkflowDiagnostics.cs:564`; `ReportUnderReach` arg order `{0}`=terminal, `{2}`=dispatcher. Contracts 0.7.0 already paid. |
| **Consequence** | Author or Exarchos remediates the wrong polarity. Tests `Contains` both names and stay green. |
| **Proof rung** | Construction and generation |
| **Proof artifact** | Widen `messageFormat` (or a second catalog member). T1’s “do not widen unless a lie” is already met. |
| **Why not cheaper** | Three string copies can match and still invert polarity. Substring tests cannot fail the lie. |
| **Failure signal** | The diagnostic text. |
| **Rollback** | Widen the catalog in this 0.7.0 bump. A published tag does not reverse. |
| **Lenses** | Promise Against Delivery; Representable Invalid States; Claim Derivation; Wildcard inventory |

**Open questions:** None on the contradiction. Does Exarchos parse `{0}/{2}` as chain-to-successor?

Evidence: `pad-agwf035-message-lie.md`, `agwf035-inverted-arg-polarity.md`, `wild-agwf035-catalog-polarity.md`, `claim-agwf035-catalog-honest.md`.

### [agwf035-error-still-emits] — AGWF035 Error does not join hasErrors

| | |
|---|---|
| **Claim** | AGWF errors sold as fail-closed share one emit-or-gate policy. AGWF035 Error must not pair with a generated saga, or the split is machine-readable. |
| **Scope** | `WorkflowIncrementalGenerator` `hasErrors` (`:933-941`) vs `Report` at `:1038`. AGWF037 joins the list; AGWF035 does not. |
| **Consequence** | Suppress AGWF035 and the broken saga is the shipped composition. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Add AGWF035 to `hasErrors`, or an explicit split table. |
| **Why not cheaper** | `DiagnosticSeverity.Error` does not null the model. Gate list is hand-maintained. |
| **Failure signal** | Error plus generated files. |
| **Rollback** | Add AGWF035 to `hasErrors`. Does not reverse already-emitted consumer sagas. |
| **Lenses** | Promise Against Delivery; Representable Invalid States; Wildcard inventory |

**Open questions:**

- Is emit-anyway intentional so authors can inspect the saga? `(needs human input)`

Evidence: `pad-agwf035-error-still-emits.md`, `agwf035-error-and-model-both-set.md`, `wild-agwf-error-emit-policy.md`.

### [agwf035-json-import-unreached] — Guard never runs on JSON import

| | |
|---|---|
| **Claim** | Every authoring front that emits `ValidTransitions` also runs `TerminalReachabilityGuard.Report`. |
| **Scope** | `BridgeImportFile` → `EmitWorkflowSources`. One `Report` site, C# only. |
| **Consequence** | Imported under-reach lowers with no AGWF035. AGWF037 *does* run on import. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Call-graph: every `EmitWorkflowSources` path calls `Report`. |
| **Why not cheaper** | Missing call is representable. C# `RunGenerator` does not close import. |
| **Failure signal** | Nothing on import. |
| **Rollback** | Call `Report` from the import path. |
| **Lenses** | Promise Against Delivery; Integration Completeness |

**Open questions:** Do production consumers declare `*.workflow.json` AdditionalFiles? Import maps `Loops`/`Branches` as null; forks are the live import subject.

Evidence: `pad-agwf035-json-import-unreached.md`, `int-agwf035-json-import-unreached.md`.

### [agwf035-all-complete-silent] — All-Complete + Finally stays silent

| | |
|---|---|
| **Claim** | A `Branch` whose cases all `.Complete()` plus `.Finally<T>()` does not fire AGWF035. |
| **Scope** | Under-reach fire rule; generator negative. |
| **Consequence** | Legal exclusive-complete workflows fail the build. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire` (direct `Report` + `RunGenerator`). |
| **Why not cheaper** | Silence is semantic for one authored shape. |
| **Failure signal** | False-positive AGWF035. |
| **Rollback** | Revert the under-reach arm. |
| **Lenses** | Promise Against Delivery; Claim Derivation |

**Open questions:** None for the C# fixture.

Evidence: `pad-all-complete-finally-silent.md`, `claim-agwf035-rejoin-only-silent-exclusive.md`.

### [agwf035-overreach-preserved] — Over-reach half still holds

| | |
|---|---|
| **Claim** | AGWF035 still fires for not-last terminal or construct-owned successor. Corpus stays silent. |
| **Scope** | Over-reach arm; spec DR-3. |
| **Consequence** | This wave regresses the already-shipped half. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Existing over-reach fixtures + corpus never-fires. |
| **Why not cheaper** | Position/successor rules are not a type invariant. |
| **Failure signal** | Compile-time AGWF035. |
| **Rollback** | Revert under-reach only. |
| **Lenses** | Claim Derivation |

**Open questions:** None.

Evidence: `claim-agwf035-overreach-preserved.md`.

### [agwf037-reject-not-dedup] — Duplicate PermitTrigger is rejected

| | |
|---|---|
| **Claim** | Two same-trigger `PermitTrigger` declarations fail AGWF037 on C# extract and JSON import. Distinct triggers stay clean. Generation is gated. |
| **Scope** | Extractor, `FindDuplicateTriggerNames`, import scan, `hasErrors`. |
| **Consequence** | First-wins drops one evidence schema. JSON has no CS0152. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Extractor / generator / import twins. |
| **Why not cheaper** | Reject-and-gate-emission is composition, not a type. CS0152 is a different C# mechanism. |
| **Failure signal** | AGWF037. Missing report + generated saga is the violation. |
| **Rollback** | Revert AGWF037. CS0152 returns on the emit path. |
| **Lenses** | Promise Against Delivery; Claim Derivation; Recurrence |

**Open questions:** Empty trigger names are skipped (`agwf037-empty-trigger-names-skipped`). Dual uniqueness authorities (runtime enum vs generator string).

Evidence: `pad-agwf037-reject-not-dedup.md`, `claim-agwf037-reject-not-dedup.md`, `agwf037-empty-trigger-names-skipped.md`, `recur-collision-reject.md`.

### [contracts-0-7-0-pack-incomplete] — 0.7.0 source pin is real; pack test does not lock AGWF037 artifacts

| | |
|---|---|
| **Claim** | A green 0.7.0 pack test means the nupkg is versioned 0.7.0 **and** contains `agwf-catalog.json` plus `AgwfEntryDuplicatePermittedForkTrigger.json`. |
| **Scope** | `ContractsVersion`; `PackagingTests`; NuGet Content items. |
| **Consequence** | A 0.7.0 nupkg that lost the catalog/entry schema still passes the test this wave updated. Exarchos extracts from the nupkg. |
| **Proof rung** | Production-path integration tests |
| **Proof artifact** | Named-entry asserts on those two pack paths. Version assert already exists. |
| **Why not cheaper** | Source files ≠ packed files. Compiler does not see NuGet content. |
| **Failure signal** | Nothing in this repo. |
| **Rollback** | Revert the 0.7.0 bump. Adds no pack requirement. Published tag does not reverse. |
| **Lenses** | False-Green; Promise Against Delivery; Integration Completeness; Claim Derivation; Exposure |

**Open questions:** Does a pack of this revision actually embed the files? Is `contracts-v0.7.0` published?

Evidence: `pad-contracts-0-7-0.md`, `contracts-pack-omits-agwf037.md`, `int-contracts-pack-agwf037-unasserted.md`, `claim-contracts-0-7-0.md`, `compat-agwf037-closed-enum-upgrade-order.md`.

### [contracts-changelog-contradicts-0-7-0] — Lede and package CHANGELOG omit the bump

| | |
|---|---|
| **Claim** | The 2.11.0 record states 0.6.0→0.7.0 and names AGWF037. |
| **Scope** | Product lede `CHANGELOG.md:17` vs Residue `:182`; packaged `Strategos.Contracts/CHANGELOG.md`. |
| **Consequence** | Readers conclude 0.6.0 / no AGWF037. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | The three texts. |
| **Why not cheaper** | Docs are not types. |
| **Failure signal** | Nothing. |
| **Rollback** | Edit the two CHANGELOG files. |
| **Lenses** | Promise Against Delivery |

**Open questions:** None on the contradiction.

Evidence: `pad-contracts-changelog-contradicts-0-7-0.md`.

### [schema-diff-skip-succeeds] — Schema-diff job succeeds when it did not compare

| | |
|---|---|
| **Claim** | `contracts-schema-diff` is non-success when it did not run the structural diff, and it compares against `contracts-v*` not product `v*`. |
| **Scope** | Surrounding gate. Workflow unchanged this wave; this wave adds a schema. |
| **Consequence** | Breaking or new schema is “checked” by a green job that printed “no diff to run.” AGWF037 NOTICE can go unseen. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | `have_prev=false` ⇒ non-success; match `contracts-v*`. |
| **Why not cheaper** | `JsonSchemaDiff` unit tests do not run when the `node` step is skipped. |
| **Failure signal** | Job name stays green. |
| **Rollback** | Revert a fail-closed workflow change. |
| **Lenses** | False-Green; Exposure |

**Open questions:** Is the job a required check? Baseline `contracts-v0.4.0` vs untagged 0.6.0?

Evidence: `schema-diff-skip-succeeds.md`, `compat-contracts-schema-diff-unbound.md`.

### [mcp-resulttype-and-pin] — Hosting pin and factory resultType hold; wrap and CI do not close the class

| | |
|---|---|
| **Claim** | Hosting pins MCP 2.2.0 so every constructed `CallToolResult` emits `resultType: complete`. INV-3 denies the pre-2026-07-28 shape on the protected path. |
| **Scope** | Factory `MapTraversalResult` / `ErrorResult`; Hosting `VersionOverride` 2.2.0; CPM still 1.3.0; INV-3 3.4/3.5 not in `ci.yml`. |
| **Consequence** | Four tools rely on SDK wrap. Grep-for-token can pass on a mention. Override can be removed while tests keep their own pin. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Production-csproj pin assert; per-construction `ResultType` assignment; INV-3 job that cannot skip-as-pass. |
| **Why not cheaper** | Types require the property, not assignment. File-level grep is comment-satisfiable. |
| **Failure signal** | Protocol clients. Nothing in CI. |
| **Rollback** | Revert pin + two assignments. Clients that already expect `resultType` then see the omission. |
| **Lenses** | Promise Against Delivery; False-Green; Integration Completeness; Exposure; Wildcard inventory; Recurrence |

**Open questions:** `ErrorResult` + `complete` protocol-legal? (INV-3 asserts yes.) Do four SDK-wrapped tools emit `resultType`?

Evidence: `pad-hosting-pin-and-resulttype.md`, `int-mcp-hosting-pin-vs-cpm.md`, `inv3-resulttype-icons-grep-substring.md`, `int-inv-3-checks-not-in-ci.md`, `wild-hosting-override-escapes-renovate.md`, `compat-mcp-resulttype-icons-wire.md`, `wild-resulttype-not-error-channel.md`, `recur-mcp-pin-bound.md`.

### [icons-null-when-unset] — No placeholder; non-null path unreached

| | |
|---|---|
| **Claim** | `Icons` stays null when unset. Non-null `Icons` → `Tool.icons` is reachable from `AddOntologyTools` if a consumer supplies icons. |
| **Scope** | Descriptor; `ApplyIcons`; `Discover` never sets. |
| **Consequence** | Null-when-unset holds because nothing assigns. Hosts cannot supply icons through the public factory. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Discovery asserts null. Non-null mapping is test-only via `CreateServerTool`. |
| **Why not cheaper** | Default-null does not prove discovery never assigns a placeholder. |
| **Failure signal** | Nothing. |
| **Rollback** | Revert the property. Creates no producer. |
| **Lenses** | Promise Against Delivery; Integration Completeness; Claim Derivation |

**Open questions:** Is consumer `Icons` a future factory overload?

Evidence: `pad-icons-null-when-unset.md`, `int-mcp-icons-non-null-unreached.md`, `claim-icons-null-when-unset.md`.

### [handauthoredcontract-unreached] — Member 2 has no producer; merge stamps 0

| | |
|---|---|
| **Claim** | `HandAuthoredContract = 2` is assigned by a shipped authoring surface, survives merge, and is treated as hand-side by AONT201/203/204. |
| **Scope** | Enum; `IngestedIntentInvariant` (skip-unless-Ingested **is** reached); `MergeTwo.cs:67`; GraphBuilder `== HandAuthored` at `:330/:409/:566`. |
| **Consequence** | Only tests stamp `2`. Merge restamps `HandAuthored`. Unwidened `==` skips value 2. AONT205 retarget is real; the three-way split is pre-merge only. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Production assignment-site scan; `MergeTwo` preserves 2; exhaustive `IsHandSide`. |
| **Why not cheaper** | Unused enum members compile. Merge test asserts the collapse. |
| **Failure signal** | Nothing. Callers see `0` or `1`. |
| **Rollback** | Revert enum + invariant. Value `2` if published is a compatibility event. |
| **Lenses** | Promise Against Delivery; Representable Invalid States; Integration Completeness; Exposure; Wildcard inventory; Claim Derivation |

**Open questions:**

- Any out-of-repo TypeSpec/JSON ingest stamps `2`? `(needs human input)`
- Is parent `Source = HandAuthored` the intended lattice identity? `(needs human input)`

Evidence: `pad-handauthoredcontract-unreached.md`, `int-hand-authored-contract-unassigned.md`, `wild-handauthoredcontract-lost-at-merge.md`, `compat-descriptorsource-handauthoredcontract-collapse.md`, `descriptorsource-eq-handauthored-excludes-contract.md`, `claim-handauthoredcontract-ingest-assignment.md`, `claim-aont205-ingested-only.md`, `claim-handauthoredcontract-additive.md`.

### [descriptor-source-docs-omit-member-2] — Edited docs still list two members

| | |
|---|---|
| **Claim** | Document which authoring surface maps to which `DescriptorSource` value. |
| **Scope** | `source.md:65-66`; `ontology-sources.md:42-43`. |
| **Consequence** | Authors stamp `Ingested` and still fail AONT205. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | The two lists. |
| **Why not cheaper** | Docs are not types. |
| **Failure signal** | Nothing. |
| **Rollback** | Add the third bullet. |
| **Lenses** | Promise Against Delivery; Claim Derivation |

**Open questions:** None.

Evidence: `pad-descriptor-source-docs-omit-member-2.md`, `claim-descriptorsource-docs-three-members.md`.

### [requires-obsolete-observable] — Requires stays; in-repo CS0618 is suppressed

| | |
|---|---|
| **Claim** | `Requires` is obsolete, still compiles and still writes Preconditions, and a clean in-repo test compile is not evidence that consumers see CS0618. |
| **Scope** | Interface + impl; `Directory.Build.targets` CS0618 `NoWarn` for all tests/benchmarks; packaged README still demos `.Requires`. |
| **Consequence** | Attribute removal + dropped reflection test stays green. Later obsoletes inherit the silence. Warnings-as-errors consumers fail. |
| **Proof rung** | Compiler and type system |
| **Proof artifact** | `[Obsolete]` + unchanged body. Compile of a `NoWarn`-free subject that fails CS0618. |
| **Why not cheaper** | Obsolete is a compiler feature. This wave removed it from in-repo callers. |
| **Failure signal** | CS0618 where not suppressed. This suite: nothing. |
| **Rollback** | Remove `CS0618` from `NoWarn` and/or the attributes. |
| **Lenses** | False-Green; Promise Against Delivery; Representable Invalid States; Exposure; Claim Derivation |

**Open questions:** Soft/Link left current on purpose? Do published consumers use TreatWarningsAsErrors?

Evidence: `pad-requires-obsolete-still-compiles.md`, `requires-cs0618-suppressed-in-suite.md`, `requires-obsolete-still-mutates-preconditions.md`, `compat-requires-obsolete-warning-break.md`, `compat-cs0618-nowarn-unscoped.md`, `claim-requires-still-compiles.md`.

### [renovate-resolve-unasserted] — Path token edited; resolve unobserved

| | |
|---|---|
| **Claim** | Renovate resolves the organisation’s dotnet preset. |
| **Scope** | `renovate.json` second `extends` token. Target file exists on `lvlup-claude` → `exarchos` rename. |
| **Consequence** | Inert-looking control (#181 class) if the slug 404s. |
| **Proof rung** | Production-path integration tests |
| **Proof artifact** | None in this repo. Path-token suffix is a weaker, cheaper claim that holds. |
| **Why not cheaper** | Types cannot resolve a GitHub `local>` preset. |
| **Failure signal** | Nothing in this repo. |
| **Rollback** | Revert the one-line path. |
| **Lenses** | Promise Against Delivery; Claim Derivation; Recurrence |

**Open questions:**

- Does `lvlup-claude` still resolve after the `exarchos` rename? `(needs human input)`

Evidence: `pad-renovate-resolve-unasserted.md`, `claim-renovate-path-token.md`, `claim-renovate-resolves-preset.md`, `recur-inert-control-resolves.md`.

### [aont205-analyzer-unreached] — Roslyn descriptor never reports

| | |
|---|---|
| **Claim** | A shipped analyzer `DiagnosticDescriptor` is reported, or it is not a compile-time control. |
| **Scope** | `OntologyDiagnostics.IngestedContributesToIntentOnly` vs `ReportDiagnostics`. Runtime AONT205 is a different root. |
| **Consequence** | `LevelUp.Strategos.Ontology.Generators` cannot fire AONT205. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Analyzer call-graph must `Diagnostic.Create` that descriptor. |
| **Why not cheaper** | An unused `static readonly` field compiles. |
| **Failure signal** | Nothing at compile time. |
| **Rollback** | Revert the descriptor. Runtime AONT205 stays. |
| **Lenses** | Integration Completeness |

**Open questions:** Is compile-time AONT205 deferred?

Evidence: `int-aont205-analyzer-unreached.md`.

### [compat-agwf035-breaking] — Existing error id gains failing C# shapes

| | |
|---|---|
| **Claim** | The under-reach arm is a breaking diagnostic for `[Workflow]` compilations that previously succeeded. |
| **Scope** | Generators → C# extract only. |
| **Consequence** | Generator upgrade can turn a clean consumer build into AGWF035. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Fire/silent fixtures. Current tests inject `WithoutSuccessor`. |
| **Why not cheaper** | A call-site scan cannot prove which shapes newly fail. |
| **Failure signal** | Consumer compile AGWF035. Silent on JSON import. |
| **Rollback** | Revert `5e94af4`. |
| **Lenses** | Exposure |

**Open questions:** Do any real workflows fail on production `Build`?

Evidence: `compat-agwf035-underreach-breaking-diagnostic.md`.

### [compat-validtransitions-nonreversing] — Emitted table does not roll back with the generator

| | |
|---|---|
| **Claim** | A generator revert is not a revert of already-emitted consumer `ValidTransitions` tables. |
| **Scope** | `{PascalName}Transitions.g.cs`. |
| **Consequence** | A non-rebuild keeps the old table after revert. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Emitter tests. No equality lock vs `4d060f4`. |
| **Why not cheaper** | Emitter derivation does not prove the lift preserved sets. |
| **Failure signal** | Nothing. |
| **Rollback** | Revert generator. Does not reverse consumer source until rebuild. |
| **Lenses** | Exposure |

**Open questions:** Do consumers check the table at runtime?

Evidence: `compat-validtransitions-nonreversing.md`.

### [compat-publicapi-omits-obsolete] — Unshipped records add/remove only

| | |
|---|---|
| **Claim** | RS0016/RS0017 cannot prove `Requires` is obsolete. |
| **Scope** | Ontology `PublicAPI.Unshipped.txt`. `Shipped.txt` empty. |
| **Consequence** | Dropping `[Obsolete]` does not fail RS0016. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Unshipped diffs. Analyzer has no Obsolete column. |
| **Why not cheaper** | PublicAPI files are not generated from attributes. |
| **Failure signal** | RS0016 on add/remove. Nothing on attribute changes. |
| **Rollback** | Revert Unshipped lines. |
| **Lenses** | Exposure |

**Open questions:** Is empty Shipped a convention?

Evidence: `compat-publicapi-unshipped-omits-obsolete.md`.

### [diagnostic-fork-ctor-open] — DiagnosticFork IR constructible outside Create

| | |
|---|---|
| **Claim** | Empty anchors, duplicate/empty triggers, empty seed, `MaxForks < 1` must not be constructible except via `Create`. |
| **Scope** | `DiagnosticForkModel` / `PermittedForkTriggerModel` records. |
| **Consequence** | `#151` lowering can switch on non-unique triggers with no AGWF037. |
| **Proof rung** | Compiler and type system |
| **Proof artifact** | Private primary constructor; `Create` only. |
| **Why not cheaper** | Throw in `Create` is one factory, not a type. |
| **Failure signal** | Nothing unless `Create` throws. |
| **Rollback** | Hide constructor. |
| **Lenses** | Representable Invalid States |

**Open questions:** Any production `new DiagnosticForkModel` today?

Evidence: `diagnostic-fork-primary-ctor-bypasses-create.md`.

### [traversal-result-flags-independent] — TraversalResult permits contradictory flags

| | |
|---|---|
| **Claim** | `IsError: false` + `Error` present (or the inverse) must not be representable. |
| **Scope** | `TraversalResult` → `MapTraversalResult` success arm. |
| **Consequence** | Host emits complete success whose structured content still has `"error"`. |
| **Proof rung** | Compiler and type system |
| **Proof artifact** | Discriminated `Success | Error`. |
| **Why not cheaper** | The named `{ passed, error }` shape. |
| **Failure signal** | Nothing. Clients key on `isError`. |
| **Rollback** | Close the type. |
| **Lenses** | Representable Invalid States |

**Open questions:** Any constructor besides TraverseTool?

Evidence: `traversal-result-iserror-error-independent.md`.

### [agwf037-catalog-identity] — Catalog tests can pass on a stale file or a mention

| | |
|---|---|
| **Claim** | Catalog tests extended to AGWF037 fail if the id is missing from a freshly compiled catalog, or if `agwf.md` has only a mention. |
| **Scope** | `GroundTruthCodes` / `Expected` appends; emitter reads committed JSON; markdown `Contains`. |
| **Consequence** | Unwiring the diagnostic leaves identity tests green. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Emitter: regenerate-then-compare. Markdown: parse table id cells. |
| **Why not cheaper** | Lists are hand-authored. |
| **Failure signal** | Nothing. These tests report pass. |
| **Rollback** | Revert the list appends. |
| **Lenses** | False-Green |

**Open questions:** Do `AgwfCatalog_HandEdit_FailsGuard` + `contracts-codegen-guard` already cover freshness?

Evidence: `agwf037-catalog-identity-stale-or-mention.md`.

### [claim-clr-free-xor-docs] — Docs name the XOR polymorphic limit

| | |
|---|---|
| **Claim** | Docs name `ObjectTypeFromDescriptor` / `ApplyDelta` as the CLR-free path; SymbolKey-only interface fan-out is not expressible. |
| **Scope** | Guide pages this wave edited. |
| **Consequence** | Authors invent a fluent twin the types cannot express. |
| **Proof rung** | Human judgment |
| **Proof artifact** | Review that the pages state the limit the types already enforce. |
| **Why not cheaper** | Guide pages are not generated. |
| **Failure signal** | Nothing. |
| **Rollback** | Revert the doc edits. |
| **Lenses** | Claim Derivation |

**Open questions:** Untracked edge-layer docs are out of scope.

Evidence: `claim-clr-free-xor-polymorphic.md`.

### [claim-issue-185-tracker] — Tracker still lists this wave as open

| | |
|---|---|
| **Claim** | Issue 185 comment 2 still lists under-reach / #181 / #163 / #115 / #156 / #176 / #177 as open-by-design. This branch claims to implement them. |
| **Scope** | GitHub issue 185 vs CHANGELOG Residue. |
| **Consequence** | Residue tracker and the change disagree. A “Close #185” title auto-closed it once. |
| **Proof rung** | Human judgment |
| **Proof artifact** | Issue comment vs Residue subsection. |
| **Why not cheaper** | Tracker state is not a type. |
| **Failure signal** | Nothing. |
| **Rollback** | Update the issue comment after merge. Do not title the PR “Close”. |
| **Lenses** | Claim Derivation |

**Open questions:** None on the disagreement.

Evidence: `claim-issue-185-tracker-close.md`.

---

## Supported claims (kept as obligations with existing proof)

These survived inventory as “the code supports the claim.” They stay in the active ledger so evaluation can refute them.

- `agwf035-all-complete-silent` — supported by generator negative.
- `agwf035-overreach-preserved` — supported by existing fixtures.
- `agwf037-reject-not-dedup` — supported on C# and JSON; gates emission.
- `icons-null-when-unset` — supported because Discover never assigns (also the unreached-producer finding).
- `claim-handauthoredcontract-additive` — ordinals 0/1/2 locked by test (absorbed into `handauthoredcontract-unreached`).
- `claim-renovate-path-token` — path suffix is correct (absorbed into `renovate-resolve-unasserted`).
- `claim-clr-free-xor-docs` — pages state the limit (human review).

---

## Recurrence classes (guard candidates)

Specified in `verification/guards-draft.md`. All seven recurring classes **stay open**. R6 (Obsolete without successor) appeared once; no guard owed.

| ID | Class | This wave | Stays open |
|---|---|---|---|
| G-R1 | Termination under/over-reach | extends-guard (IR only) | yes |
| G-R2 | First-wins / silent collision | adds AGWF037 | yes (per-id, not one policy) |
| G-R3 | Inert-looking control | instance-fix only | yes |
| G-R4 | MCP pin / deny-list | extends INV-3 greps | yes (not CI) |
| G-R5 | Enum ordinals only append | instance-fix `= 2` | yes |
| G-R7 | Diagnostic with no authorable trigger | no change | yes |
| G-R8 | Table / saga / diagnostic drift | type-share | yes |

---

## Refuted

Stage 3 majority (reachability / premise / named-proof). Discriminating evidence in `verification/evaluation/refutation-*.md`.

| Slug | Killer | Evidence that killed it |
|---|---|---|
| `phasegraph-type-not-instance` | reachability | `PhaseGraph.Build` is a pure function of the unchanged model; two calls cannot drift |
| `agwf035-json-import-unreached` | reachability + premise | Import sets `Loops`/`Branches` null and has no `Finally`; CHANGELOG scopes AGWF035 to C# |
| `agwf035-error-still-emits` | premise | “All Error AGWFs gate emission” was never claimed |
| `schema-diff-skip-succeeds` | reachability + premise | `fetch-depth: 0`; this repo has `v*` tags; workflow unchanged |
| `aont205-analyzer-unreached` | all three | This wave did not claim compile-time AONT205 |
| `compat-agwf035-breaking` | reachability + premise | Production `Build` still has rejoin edges |
| `compat-validtransitions-nonreversing` | reachability + premise | Standing generator contract, not a claim this target made |
| `compat-publicapi-omits-obsolete` | reachability + premise | Duplicate of `requires-obsolete-observable` |
| `diagnostic-fork-ctor-open` | majority | Both fronts reject or call `Create` |
| `traversal-result-flags-independent` | majority | Only TraverseTool constructs; flags set together |
| `agwf037-catalog-identity` | majority | `contracts-codegen-guard` already regen-and-diffs |
| `claim-issue-185-tracker` | all three | Tracker state is not a product path |
| `renovate-resolve-unasserted` (as “bot applies”) | reachability + premise | Path token matches the existing file |

## Resolved by fix-up (this run, after 324768f)

| Slug | Resolving change |
|---|---|
| `agwf035-catalog-polarity-lie` | Widened AGWF035 remediation to name both arms |
| `contracts-0-7-0-pack-incomplete` | `PackagingTests` asserts catalog + AGWF037 schema |
| `descriptor-source-docs-omit-member-2` | Third member on `source.md` and `ontology-sources.md` |
| `contracts-changelog-contradicts-0-7-0` | Product lede 0.4.0→0.7.0; Contracts CHANGELOG names AGWF037; PhaseGraph claim softened |

---

## Assumptions

- External references are leads. Obligations are grounded in cited code at `324768f`.
- `PhaseGraph.Build` is a pure function of the model, so two calls agree *today*.
- Issue 185 “still open by design” is tracker state at comment time, not proof this branch left the work undone.
- Spec DR-3 is over-reach only; under-reach is specified in the plan/issue.
- Out of wave: Option B, #147, #133/#174 maintainer proof, #156.1/#156.3.

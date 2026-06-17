# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

**Npgsql instance-anchored link traversal, depth-tiered (DR-12, #131).**
`PgVectorObjectSetProvider.ExecuteAsync<T>` / `StreamAsync<T>` now serve
`.Where(s => s.Key == id).TraverseLink<TLinked>("link")` expressions, closing the
DR-7..DR-11b gap where a `TraverseLinkExpression` threw `NotSupportedException`.
The lowering is **depth-tiered**: a chain of ≤ 3 monomorphic hops lowers to a
flat `vertex ⋈ junction ⋈ vertex ⋈ …` join chain (sized to the planner's
`join_collapse_limit` of 8 — 1 anchor + 2 relations/hop = 7 at 3 hops), while a
deeper chain — or any plan with a polymorphic hop (which counts as fan-out via a
`UNION ALL` over the per-`(link, target-descriptor)` junction tables) — lowers to
a `WITH RECURSIVE` walk. Hop targets resolve from the ontology graph (DR-10),
never `typeof` (INV-8); every join is inner so a zero-relation source yields an
empty result at any depth (#114 / DR-8 preserved). All new types
(`TraversalPlan`, `TraversalStep`, `TraversalLowering`) are `internal` —
`Strategos.Ontology.Npgsql` carries no PublicAPI baseline — so no `PublicAPI.*.txt`
re-baseline is required. See the provider reference's *Link traversal
(depth-tiered lowering)* section.

### Cross-product breaking changes

This section is **present on every release that touches the builder public
surface, even when empty** — it forces deliberate author intent. The 7
`Strategos.Builders` interfaces (`IWorkflowBuilder<TState>`,
`IBranchBuilder<TState>`, `ILoopBuilder<TState>`, `IForkJoinBuilder<TState>`,
`IApprovalBuilder<TState, TApprover>`, `IFailureBuilder<TState>`,
`IStepConfiguration<TState>`) are a cross-product contract mirrored by
exarchos's `strategos-api-mirror.test.ts`. They are baselined in
`src/Strategos/PublicAPI/PublicAPI.Shipped.txt` and enforced by
`Microsoft.CodeAnalysis.PublicApiAnalyzers` (RS0016/RS0017). When a builder
signature changes, the CI gate fails closed with the message:

> Update PublicAPI.Unshipped.txt and add a CHANGELOG entry under Cross-product breaking changes.

Follow it: move the new/changed lines into `PublicAPI.Unshipped.txt` and record
the change here so the downstream exarchos mirror can re-baseline deliberately.

**Ontology edge-properties surface removed (DR-5, #120, closes #114).** The
schema-only edge-properties footgun — attaching ad-hoc properties to a *link*
rather than modeling the relationship as a first-class object — has been removed
outright. The following public `Strategos.Ontology` members are gone:

- `IEdgeBuilder` and its internal `EdgeBuilder` implementation.
- The `IObjectTypeBuilder<T>.ManyToMany<TLinked>(string, Action<IEdgeBuilder>)`
  edge-config overload (the plain `ManyToMany<TLinked>(string)` is retained).
- `ICrossDomainLinkBuilder.WithEdge(Action<IEdgeBuilder>)`.
- `IExtensionPointBuilder.RequiresEdgeProperty<T>(string)` and the
  `ExternalLinkExtensionPoint.RequiredEdgeProperties` collection (and the
  backing `RequiredEdgeProperty` record).
- `LinkDescriptor.EdgeProperties`, `CrossDomainLinkDescriptor.EdgeProperties`,
  and the `ResolvedCrossDomainLink.EdgeProperties` constructor parameter.

**Migration:** model edge attributes on a reified `Association<T>` declared with
`builder.Association<T>(...)` (DR-4) — an association is a standalone
object-with-two-endpoints that carries its own key and its own edge-attribute
properties. A new analyzer diagnostic **`AONT209`** (error) fires on any residual
edge-property authoring attempt and points to `Association<T>`.

These types are not part of the `src/Strategos` builder PublicAPI baseline (which
is scoped to the 7 `Strategos.Builders` interfaces), so no `PublicAPI.*.txt`
re-baseline is required. The ontology graph version hash changed: the `|EDGE|`
framing was removed from `OntologyGraphHasher`, so cached graph versions will
differ. The two now-unreachable diagnostics that validated the deleted surface,
`AONT008` (EdgeTypeMissingProperty) and `AONT033` (ExtensionPointEdgeMissing),
remain registered but dormant (INV-5: ids are never reused).

### Added — Npgsql edge-layer hardening (DR-13, #130)

- **R1 — reverse junction index.** Every per-`(link, target-descriptor)` junction
  table now emits an explicit `CREATE INDEX ... (target_id, source_id)` alongside
  the forward `UNIQUE (source_id, target_id)` constraint, so reverse traversal and
  target-endpoint FK probes are index-backed instead of falling back to a
  sequential scan.
- **R2 — vertex key-property unique index.** `EnsureSchemaAsync` now emits a
  `CREATE UNIQUE INDEX ... ((data->>'key'))` expression index on the descriptor's
  business-id key property, making the relate/traversal endpoint-resolution
  subqueries (`data->>'key' = @id`) resolve a single deterministic row. Keyless
  (pgvector-only) tables keep their pre-DR-13 DDL byte-identical.
- **R4 — atomic, single-statement relate (TOCTOU closed, 3→1 round-trips).** The
  plain and attributed Npgsql relate is collapsed from three commands (two eager
  `SELECT EXISTS` probes + one `INSERT`, each its own round-trip on no shared
  snapshot) into ONE self-validating statement issued as a single `NpgsqlBatch`:
  endpoint resolution + idempotent insert (a data-modifying CTE, run exactly once)
  + the returned `src_exists`/`tgt_exists` flags, all under Postgres's single
  `WITH` snapshot. A missing endpoint — including one deleted concurrently —
  surfaces the typed `RelationEndpointNotFoundException` instead of a silent no-op,
  and no dangling row is written. The pre-DR-13 TOCTOU caveat on `RelateAsync` is
  removed. A disallowed self-loop takes a probe-only (no-insert) path so its
  endpoint-first ordering is preserved.
- **R5 — `NpgsqlDataSource` posture + auto-prepare.** The provider's only DB entry
  point is the INJECTED `NpgsqlDataSource` (no internal connection-string
  construction), pinned by a reflection test. The `UsePgVector` DI shorthand now
  enables Npgsql `Max Auto Prepare` (default 25 when the caller's connection string
  does not set one), so the small, fixed set of relate/unrelate/traversal statement
  shapes are server-side-prepared transparently across pooled connections — the
  "OR `Max Auto Prepare` documented" branch of the R5 contract. Auto-prepare is
  parameter-name-agnostic, so the existing `@named` parameters are retained (the
  established SQL-shape test suite asserts them); positional `$1` rewriting was
  judged gratuitous churn for no behavioural gain under auto-prepare. The `Npgsql`
  package is pinned `9.0.3` → `10.0.3`.
- **R6 — `RelateBatchAsync` reserved on `IObjectSetWriter`.** New
  `Task RelateBatchAsync(IReadOnlyList<RelateRequest>, CancellationToken)` member
  plus a new sealed `RelateRequest` record reserve a set-based relate surface for
  bulk edge ingestion (#115). The in-memory provider fulfils it with a per-request
  loop carrying the same eager-validation/self-loop/idempotency contract; the
  Npgsql provider throws `NotSupportedException` until #115 lands the batched DML.
  These `Strategos.Ontology` types are not part of the `src/Strategos` builder
  PublicAPI baseline (scoped to the 7 `Strategos.Builders` interfaces), so no
  `PublicAPI.*.txt` re-baseline is required.
- **R8 — pgvector iterative-scan knobs.** New `IterativeScanOptions` /
  `IterativeScanMode` on `Strategos.Ontology.Npgsql`, wired into
  `PgVectorOptions.IterativeScan`. When set, a similarity query is shaped as an
  index-ordered ANN CTE and run inside a transaction with transaction-scoped
  `SET LOCAL hnsw.iterative_scan` / `hnsw.max_scan_tuples` / `hnsw.ef_search`, so a
  filtered search keeps scanning the HNSW index until it has enough post-filter
  rows to satisfy `topK` (instead of post-filtering a fixed candidate set and
  under-returning). **Runtime requirement:** the pgvector SERVER extension must be
  `>= 0.8.0` (iterative scans are a 0.8.0 feature). The `Pgvector` .NET client pin
  is bumped `0.3.0` → `0.3.2`; that client's version line is independent of — and
  cannot express — the server-extension requirement.

## [2.8.0] - 2026-05-25

The **cross-product schema substrate** release. TypeSpec remains the single
canonical source; the build emits JSON Schema (exarchos derives Zod) and C#
records (basileus consumes the DLL).

### Cross-product breaking changes

_(none this release)_ — the 7 `Strategos.Builders` interfaces are now baselined
in `PublicAPI.Shipped.txt` and frozen by `PublicApiAnalyzers` (#51); this
release establishes the baseline without changing any signature.

### Added

- **Ontology MCP registration bridge** (#104) — new
  `LevelUp.Strategos.Ontology.MCP.Hosting` package adapts ontology tool
  descriptors into `ModelContextProtocol` server tools and registers them on an
  MCP server builder, preserving `OutputSchema` + `ToolAnnotations`.
- **Builder API-stability gate** (#51) — `Microsoft.CodeAnalysis.PublicApiAnalyzers`
  wired from zero with a populated baseline; a CI gate fails closed on builder
  signature drift and opens a re-baseline issue against exarchos's
  `strategos-api-mirror.test.ts`.
- **AGWF single-source catalog** (#52) — the 10 `AGWF*` diagnostic codes are
  now generated from a single TypeSpec-sourced catalog (`agwf-catalog.json` +
  `AgwfCode` enum), giving exarchos (#1256) a 1:1 mapping target. Drift
  hardening across the #51/#52 follow-ups (#105, #106, #107).

### Cross-product schemas (`LevelUp.Strategos.Contracts` 0.3.0, published separately)

- **Semantic-merge-queue surface** (#63–#66) — `MergeGateDecision`,
  `JourneyResult`, the `WorkflowRef` discriminated union, and `WorkflowCatalog`,
  all extending a shared response envelope carrying `_meta.degraded` +
  `DegradedReason` (fallback is always visible, never silent).
- Contracts is independently versioned (`contracts-v*` tag); 0.3.0 is an
  additive minor over 0.2.0 (events + workflow IR).

## [2.7.0] - 2026-05-24

### Changed (BREAKING) — Agent step contract

The single-arity `IAgentStep<TState>` / `AgentStepBase<TState>` types
have been removed (DR-11, see `meai-10-5/T-021`). LLM-powered steps now
implement `IAgentStep<TState, TResult>` and are built exclusively
through `AgentStepBuilder<TState, TResult>`. The builder enforces the
`SystemPrompt` / `UserPrompt` / `ApplyResult` hooks at `Build()` time
(AGAG001) and produces a sealed `AgentStepBase<TState, TResult>` —
subclassing is no longer the construction path.

Before (deleted):

```csharp
public class AnalyzeStep : IAgentStep<DocumentState>
{
    public string GetSystemPrompt() => "...";
    public Type? GetOutputSchemaType() => typeof(DocumentAnalysis);

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state, StepContext ctx, CancellationToken ct) => ...;
}
```

After:

```csharp
var step = new AgentStepBuilder<DocumentState, DocumentAnalysis>()
    .WithSystemPrompt(_ => "...")
    .WithUserPrompt(s => s.DocumentText)
    .WithApplyResult((state, result, _) =>
        Task.FromResult(new StepResult<DocumentState>(state with { Analysis = result })))
    .Build(chatClient);
```

See `src/Strategos.Agents/README.md` for the canonical example and
`docs/designs/2026-05-17-strategos-agents-meai-10-5.md` for the
design rationale.

### Added — Tool sources and streaming (DR-9, DR-1)

- **`IToolSource` port + `AgentToolSource` in-process adapter.** Agent
  steps can register lazily-resolved tool providers via
  `AgentStepBuilder.WithToolSource(IToolSource)`. `AgentToolSource`
  builds `AIFunction`s from ordinary CLR members — either by reflecting
  `[AgentTool]`-annotated methods (`AgentToolSource.FromObject`) or by
  wrapping explicit delegates (`AgentToolSource.FromDelegates`) — with
  no `ModelContextProtocol` dependency. The MCP tool-source port was
  generalized to `IToolSource` during 2.7.0 development; the
  MCP-specific `McpToolSource` (in `Strategos.Agents.Mcp`) now
  implements `IToolSource`. Sources merge after `WithTool` tools in
  registration order, each resolved at most once and cached for the
  middleware lifetime. Resolution failures surface as
  `AgentToolSourceException` (`AGAG007`) for the in-process adapter and
  `AgentMcpException` (`AGAG004`) for the MCP adapter.
- **`McpToolSource` pins `ModelContextProtocol` 1.3.0** (2025-11-25 MCP spec revision; see
  `src/Directory.Packages.props`). The MCP adapter is isolated in the
  `Strategos.Agents.Mcp` package so the core `Strategos.Agents` surface
  carries no MCP dependency.
- **Streaming observability via `WithStreaming(IStreamingHandler)`.**
  When a handler is configured, the step drives the streaming chat path
  and forwards tokens to the handler as a non-durable side-channel.
  Streaming funnels into the same terminal typed-result contract as the
  buffered path — tokens fire before `ApplyResult`, and the typed return
  shape is unchanged. A handler that throws mid-stream raises
  `AgentStreamingException` (`AGAG009`) with state untouched.

### Changed — Ontology hybrid retrieval polish (#78)

- **`HybridMeta.Degraded` gains `"sparse-empty"`** (#78 item 1). When the sparse
  keyword leg runs cleanly but returns zero candidates, `OntologyQueryTool` now
  surfaces `HybridMeta { Hybrid = false, Degraded = "sparse-empty" }` instead of a
  `Hybrid = true` envelope with a null `sparseTopScore`. This makes the wire honor
  the documented invariant that `Hybrid = true` ⇔ the sparse leg actually
  contributed to fusion (design §6.6, DIM-7). An empty sparse result is not a
  fault, so — unlike `"sparse-failed"` — it is not logged.
- **Missing-provider warn-once is now per-process** (#78 item 4). The
  "`HybridQueryOptions` supplied but no `IKeywordSearchProvider` registered"
  warning is latched on a process-wide `static` flag (moved off `OntologyQueryTool`
  onto the new `HybridQueryCoordinator`), so hosts that construct multiple tool
  instances no longer emit one warning per instance. Public API unchanged.
- **Internal refactors (#78 items 5/6).** `OntologyQueryTool`'s hybrid path was
  extracted into a sealed `HybridQueryCoordinator` (file back under the 500-line
  bar) and its private helpers now take an internal `SemanticQueryRequest`
  parameter object. No public surface change.

## [2.7.0-preview.1] - 2026-05-17

### G1 — Agent Identity Seam (DR-1..DR-10, supersedes #67/#68/#69, design 2026-05-16-g1-agent-identity-seam)

#### Added

- **`Strategos.Identity.Abstractions`** package debut. New
  netstandard2.0 package shipping the agent-identity ports consumed by
  the basileus SPIFFE adapter:
  - `WorkflowIdentity` and `AgentIdentity` sealed records — opaque,
    printable-ASCII-validated string values that ride on Wolverine
    envelope headers without additional encoding.
  - `IAgentIdentityProvider` — port for deriving per-step agent
    identities from `(workflow, phaseName)`. Strategos owns the
    contract; basileus is the SPIFFE-shaped adapter.
  - `IAgentIdentityAccessor` — read-only port that exposes the active
    envelope's identity to in-handler code. Returns `null` outside a
    Wolverine handler context (mirrors `IHttpContextAccessor`).
  - `IPhaseAwareSaga` — marker interface emitted on every generated
    saga so middleware can read `saga.CurrentPhaseName` without
    binding to the workflow's phase enum.
  - `StrategosHeaders` constants — `x-strategos-workflow-identity`
    and `x-strategos-agent-identity` wire-protocol header keys.

#### Changed

- **`Strategos.Generators`** emits a computed `CurrentPhaseName`
  property (`=> Phase.ToString()`) and adds `IPhaseAwareSaga` to the
  generated saga's base list. Both additions are additive — no
  breaking changes to the existing 2.6.0 generator surface. The
  generator ships `Strategos.Identity.Abstractions.dll` alongside
  itself in `analyzers/dotnet/cs` so the analyzer assembly loads at
  SG time.

#### Migration

- **Package consumption.** `LevelUp.Strategos.Identity.Abstractions`
  flows transitively from the `LevelUp.Strategos` core metapackage —
  consumers that already reference `LevelUp.Strategos` need no
  additional `PackageReference`. The dependency is routed through the
  core (not through `LevelUp.Strategos.Generators`) because the
  generator package is marked `<DevelopmentDependency>true</DevelopmentDependency>`,
  which causes NuGet to suppress transitive flow from that package. A
  CI consumer-build probe (`scripts/verify-generator-consumer-build.sh`,
  wired into the `pack-verify` job) guards against regression of this
  packaging contract.
- **Consumers register** the Wolverine header-propagation policy in
  their `UseWolverine` block to enable cross-message workflow-identity
  propagation:
  ```csharp
  opts.Policies.PropagateIncomingHeaderToOutgoing(
      StrategosHeaders.WorkflowIdentity);
  ```
  `StrategosHeaders.AgentIdentity` is per-message-derived and is NOT
  propagated — each handler stamps its own.
- The basileus SPIFFE adapter (lvlup-sw/basileus PR #184) is the
  reference `IAgentIdentityProvider` implementation. See
  `docs/coordination/2026-05-16-basileus-handoff.md` for the
  cross-repo handoff.

## [2.6.0] - 2026-05-17

### Ontology 2.6.0 — Hybrid Retrieval Seams (DR-1/DR-2/DR-3, #56/#57/#58, milestone #47)

#### Added

- **`IKeywordSearchProvider`** seam (PR-A) — extension point for sparse / BM25
  keyword search. Includes `KeywordSearchRequest`, `KeywordSearchResult`,
  `KeywordSearchException`. Strategos defines the contract and provides no
  default DI registration; consumers register an implementation in their
  composition root.
- **`RankFusion`** static utilities (PR-B) — `RankFusion.Reciprocal` (weighted
  Cormack 2009 RRF, bit-identical to the original paper when weights = null)
  and `RankFusion.DistributionBased` (Qdrant 2024 DBSF). DBSF matches
  `qdrant_client.hybrid.fusion.distribution_based_score_fusion`
  (qdrant-client 1.12.1) within `1e-9` on non-degenerate inputs; single-element
  and zero-variance lists use the documented Strategos 0.5-convention
  extension because qdrant raises `ZeroDivisionError` in those cases. A
  `dbsf-parity-guard` CI job (`.github/workflows/ci.yml`) mechanically rejects
  oracle drift on every PR. See issue #79 for the 2026-05-16 reconciliation
  that corrected an earlier overclaimed "parity ≤ 1e-9" wording.
  Supporting records: `RankedCandidate`, `ScoredCandidate`, `FusedResult`.
- **`HybridQueryOptions`** + **`FusionMethod`** enum (PR-C) — additive optional
  per-call configuration for `OntologyQueryTool.QueryAsync`.
- **`HybridQueryOptions.Validate()`** (PR-C) — public validation method invoked at
  `QueryAsync` entry. Throws `ArgumentOutOfRangeException` for a negative
  `SparseTopK` / `DenseTopK`, a non-positive `RrfK`, or an undefined `FusionMethod`;
  throws `ArgumentException` for `SourceWeights` with length ≠ 2 or a negative weight.
- **`HybridMeta`** typed sub-record (PR-C) — attached to `ResponseMeta.Hybrid`
  whenever the hybrid path was engaged. Omitted from JSON when null, preserving
  byte-for-byte 2.5.0 `_meta` snapshots.

#### Changed

- **`OntologyQueryTool` constructor** — gains optional
  `IKeywordSearchProvider? keywordProvider = null` as the last parameter. The
  4-arg 2.5.0 ctor remains source-compatible.
- **`OntologyQueryTool.QueryAsync`** — gains optional
  `HybridQueryOptions? hybridOptions = null` immediately before
  `CancellationToken ct`. Null preserves byte-identical 2.5.0 behavior
  (DIM-3 hard backward-compat gate).
- **`ResponseMeta`** — gains optional `HybridMeta? Hybrid { get; init; }`
  serialized as absent when null.

#### Migration

- **No migration required for 2.5.0 callers.** Every existing call site of
  `OntologyQueryTool.QueryAsync` continues to compile and produce
  byte-identical results without any changes. Hybrid retrieval is opt-in via
  the new `hybridOptions` parameter and requires consumers to register an
  `IKeywordSearchProvider` in DI; without one, supplying `hybridOptions`
  degrades automatically to dense-only with `HybridMeta { Hybrid = false,
  Degraded = "no-keyword-provider" }`, emitting a single warn-once log so
  operators can discover the missing DI registration.

## [1.1.1] - 2026-01-19

### Added

- **Sample Applications** — Three complete sample applications demonstrating core patterns:
  - AgenticCoder: TDD coding workflow with Plan → Code → Test → Iterate loop
  - ContentPipeline: Draft → Review → Approve → Publish with human-in-the-loop
  - MultiModelRouter: Thompson Sampling agent selection with learning feedback
- **Learning Paths** — Structured documentation paths for different experience levels
- **Educational Content** — Pattern documentation transformed into tutorials with exercises

### Fixed

- **RouterState Attribute** — Add missing `[WorkflowState]` attribute to MultiModelRouter sample
- **Documentation Build** — Escape generic type syntax (`SpanOwner<string>`) in markdown tables to fix VitePress build
- **CI Workflows** — Upgrade git-cliff-action from v3 to v4 (Debian Buster EOL)

## [1.1.0] - 2026-01-18

### Added

- **Benchmark Infrastructure** — New `Strategos.Benchmarks` project with BenchmarkDotNet, 53 benchmark classes covering all subsystems, and CI workflow for regression detection (#10)
- **BitFaster Cache Option** — Optional ConcurrentLru backend for StepExecutionLedger via configuration (#11)
- **Large-Scale Benchmarks** — 10K document and 500 candidate benchmark scenarios (#11)

### Changed

- **MemoryPack Serialization** — Replace System.Text.Json with MemoryPack for ledger hashing and cache operations (3-6x speedup) (#11)
- **ValueTask Migration** — IBeliefStore and IArtifactStore interfaces now return `ValueTask<T>` for zero-allocation sync paths (#11)
- **SpanOwner Pooling** — Use CommunityToolkit.HighPerformance SpanOwner in LoopDetector for temporary array pooling (#11)

### Performance

- **Thompson Sampling** — Parallel belief fetching with `Task.WhenAll()`, secondary indices for O(1) lookups (#9)
- **Loop Detection** — Early exit skipping expensive semantic similarity, ordinal string comparison (#9)
- **Budget & Ledgers** — Lazy scarcity caching, pre-allocated list capacities, stackalloc in BudgetGuard (#9, #11)
- **Source Generators** — WellKnownTypes metadata caching, HashSet for O(1) contains checks (#9)
- **Allocation Fixes** — Pre-sized lists in TaskLedger, HashSet indices in BeliefStore (#11)

## [1.0.0] - 2025-01-05

### Initial Public Release

First stable release of the Strategos library for building production-grade agentic workflows.

### Packages

- **Strategos** - Core DSL, abstractions, and Thompson Sampling types
- **Strategos.Generators** - Roslyn source generators for saga/event generation
- **Strategos.Infrastructure** - Infrastructure implementations (belief stores, selectors)
- **Strategos.Agents** - Agent-specific integrations (MAF, Semantic Kernel)
- **Strategos.Rag** - RAG integration with vector search adapters

### Features

#### Fluent DSL
- `Workflow<TState>.Create()` entry point
- `StartWith<T>()`, `Then<T>()`, `Finally<T>()` for linear flow
- `Branch()` for conditional routing with pattern matching
- `Fork()` / `Join<T>()` for parallel execution
- `RepeatUntil()` for iterative loops with exit conditions
- `AwaitApproval<T>()` for human-in-the-loop workflows
- `Compensate<T>()` for rollback handlers
- `OnFailure()` for error handling

#### Source Generators
- Phase enumeration generation
- Wolverine saga class generation with handlers
- Command and event type generation
- State reducer generation (`[Append]`, `[Merge]` attributes)
- Transition table generation for validation
- DI extension method generation
- Mermaid diagram generation for visualization

#### Thompson Sampling Agent Selection
- `IAgentSelector` interface for agent selection
- Contextual multi-armed bandit with Beta priors
- 7 task categories: Analysis, Coding, Research, Writing, Data, Integration, General
- `ITaskFeatureExtractor` for category classification
- `IBeliefStore` for persistence of agent beliefs

#### Loop Detection
- Exact repetition detection in sliding window
- Semantic repetition via cosine similarity
- Oscillation pattern detection (A-B-A-B)
- No-progress detection

#### Budget Guard
- Step count limits
- Token usage tracking
- Wall time enforcement
- Scarcity-based action scoring

#### Compiler Diagnostics
- AGWF001: Empty workflow name
- AGWF002: No steps found
- AGWF003: Duplicate step name
- AGWF004: Invalid namespace
- AGWF009: Missing StartWith
- AGWF010: Missing Finally
- AGWF012: Fork without Join
- AGWF014: Loop without body
- AGSR001: Invalid reducer attribute usage
- AGSR002: No reducers found

### Infrastructure

- Wolverine saga integration for durable state
- Marten event sourcing for audit trails
- PostgreSQL persistence
- Transactional outbox pattern
- Time-travel debugging via event replay

[2.7.0]: https://github.com/lvlup-sw/strategos/releases/tag/v2.7.0
[2.7.0-preview.1]: https://github.com/lvlup-sw/strategos/releases/tag/v2.7.0-preview.1
[2.6.0]: https://github.com/lvlup-sw/strategos/releases/tag/v2.6.0
[1.1.1]: https://github.com/lvlup-sw/strategos/releases/tag/v1.1.1
[1.1.0]: https://github.com/lvlup-sw/strategos/releases/tag/v1.1.0
[1.0.0]: https://github.com/lvlup-sw/strategos/releases/tag/v1.0.0

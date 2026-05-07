# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **Dependency refresh (2026-04-18)** — All centrally-managed packages in `src/Directory.Packages.props` bumped to latest stable. Behavior-preserving; test count (3430) and Verify snapshots unchanged.
  - `Microsoft.Extensions.Caching.Memory` 10.0.0 → 10.0.6
  - `Microsoft.Extensions.DependencyInjection` 10.0.0 → 10.0.6
  - `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0 → 10.0.6
  - `Microsoft.Extensions.Http` 10.0.0 → 10.0.6
  - `Microsoft.Extensions.Logging.Abstractions` 10.0.0 → 10.0.6
  - `Microsoft.Extensions.Options` 10.0.0 → 10.0.6
  - `Microsoft.Extensions.TimeProvider.Testing` 10.0.0 → 10.5.0 (10.0.x patch lineage skipped by the package; testing-only surface)
  - `Microsoft.Extensions.AI` 10.0.1 → 10.5.0 (version-locked with `.Abstractions`)
  - `Microsoft.Extensions.AI.Abstractions` 10.0.1 → 10.5.0
  - `Npgsql` 9.0.3 → 10.0.2 (pre-bump trigger scan clean: no date/time columns, no `NpgsqlCidr`, no `BeginText*Async`, no `DataTypeName`+`NpgsqlDbType` combined setters)
  - `Pgvector` 0.3.0 → 0.3.2
  - `MemoryPack` 1.21.3 → 1.21.4
  - `BitFaster.Caching` 2.5.2 → 2.5.4
  - `CommunityToolkit.HighPerformance` 8.4.0 → 8.4.2
  - `TUnit` 1.2.11 → 1.37.0 (336 mechanical `.HasCount(...)` → `.Count().IsEqualTo(...)` rewrites; 4 `.IsEqualTo(true/false)` → `.IsTrue()/.IsFalse()` rewrites)
  - `Microsoft.CodeAnalysis.CSharp` 4.14.0 → 5.3.0 (Roslyn 5)
  - `Microsoft.CodeAnalysis.Analyzers` 4.14.0 → 5.3.0 (Roslyn 5)

### Breaking

- **.NET SDK floor raised to 10.0.202** — Required by Roslyn 5.3 source generators (`CS9057` against the compiler 5.0.0.0 shipped by .NET 10.0.1xx SDKs). Enforced via `global.json` with `rollForward: latestFeature`. See `AGENTS.md` → Build Requirements.

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

[1.1.1]: https://github.com/lvlup-sw/strategos/releases/tag/v1.1.1
[1.1.0]: https://github.com/lvlup-sw/strategos/releases/tag/v1.1.0
[1.0.0]: https://github.com/lvlup-sw/strategos/releases/tag/v1.0.0

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

[AGENTS.md](AGENTS.md) is the canonical, tool-neutral instruction file (project overview, tech stack, security, known tech debt, scan preferences). This file adds what an agent needs in order to work here: the layout, the commands, where the architecture is written down, and the constraints that are enforced.

## Project Overview

Strategos is a compiled, durable-execution runtime for agentic workflows, shipped as NuGet packages under `LevelUp.Strategos*`. Three layers:

- **Workflow DSL + generators.** `Workflow<TState>.Create(...)` with `StartWith / Then / Branch / Fork / Join / RepeatUntil / AwaitApproval / OnFailure`. `Strategos.Generators` lowers the definition into a Wolverine saga, Marten persistence and DI registrations at build time; a `.workflow.json` file can enter the same lowering path. Invalid workflows fail compilation with stable `AGWF*` diagnostics.
- **Ontology.** A descriptor model (`Strategos.Ontology`) validated by Roslyn analyzers (`AONT*`), queried through object sets (in-memory and Postgres/pgvector providers), and exposed over MCP (`Strategos.Ontology.MCP`, `.MCP.Hosting`). Since 3.0 the action calculus gives every action a declared contract (subject, requires, ensures, touches, authority, inverse) that the compiler checks.
- **Contracts.** `LevelUp.Strategos.Contracts` is the TypeSpec-authored, language-neutral IR shared with the other runtimes: workflow definitions, gate classes, fork/compensation edges, abstention, action metadata and the AGWF catalog, generated to C# records, JSON Schema and Zod. basileus consumes the .NET packages; exarchos consumes the schemas; valkyrie and dynatoi consume the Ontology line.

The shop's .NET conventions (net10.0/.slnx, central package management, TUnit + NSubstitute with awaited assertions, guard clauses, `Result<T>`) apply — see the global USER-CONTEXT. **basileus is the reference implementation** and this repository follows its layout.

## Project Structure

```text
strategos/
├── strategos.slnx, Directory.Build.props, Directory.Build.targets, Directory.Packages.props, global.json
├── src/                          # 16 product projects, no test projects
│   ├── Strategos/                     core DSL, abstractions, steps, orchestration
│   ├── Strategos.Generators/          Roslyn source generator: saga / extensions / state-reducer emitters, JSON import
│   ├── Strategos.Contracts/           TypeSpec sources, generated records + JSON Schema, fixture corpus
│   ├── Strategos.Contracts.Codegen/   the TypeSpec -> C# / schema / docs emitter
│   ├── Strategos.Ontology*/           descriptor model + analyzers, MCP, MCP.Hosting, Npgsql, Embeddings
│   ├── Strategos.Agents*, .Infrastructure, .Rag, .Identity.Abstractions, .Benchmarks
│   └── Shared/                        source-shared helpers
├── tests/                        # every test project; one per product project, plus the cross-cutting ones
│   ├── Strategos.*.Tests/             17 unit and integration projects, named for the project under test
│   ├── Strategos.Architecture.Tests/  cross-cutting: invariants catalog + its deterministic checks
│   └── basileus-smoke/                PackageReference consumer probe (own .slnx, kept outside the props tree)
├── samples/                      # runnable examples building against src/ by ProjectReference
├── scripts/                      # codegen, schema diff, packed-artifact probes, drift checks
├── docs/                         # Astro + Starlight site (src/content/docs/) + the repo documents indexed below
├── .exarchos/invariants.md       # machine-readable invariants catalog (U-1..U-8), registered in .exarchos.yml
└── .agents/skills/               # agent skills; design-invariants wraps docs/architecture/invariants/
```

## Essential Commands

### Build and test

```bash
dotnet build strategos.slnx
dotnet test --solution strategos.slnx        # --solution is required on the .NET 10 SDK
```

Tests are **TUnit on Microsoft.Testing.Platform** (pinned in `global.json`). Assertions must be awaited — `await Assert.That(x).IsEqualTo(y);` — an un-awaited assertion silently passes. Warnings are errors (`LvlupTreatWarningsAsErrors`). `dotnet test --filter` does **not** select tests in this repo; run one project or one class with the tree-node filter:

```bash
dotnet run --project tests/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj -- --treenode-filter "/*/*/InMemoryTraversalIdentityTests/*"
```

Project-specific notes:

- `Strategos.Generators.Behavioral.Tests` runs real Wolverine + Marten hosts in Postgres containers (Podman). Run `podman container prune -f` before a full run; a few hundred stale containers make the whole suite fail with `ContainerRuntimeUnavailableException`.
- `Strategos.Ontology.Npgsql.Tests` DB-backed tests skip unless `STRATEGOS_PG_TEST_CONN` is set; everything else in that project is a generated-SQL comparison.
- `Strategos.Contracts.Tests` needs Node 22+ (`npx tsp compile`); CI runs it in its own Node-provisioned job.

### Contracts

```bash
bash scripts/contracts-codegen.sh                                   # TypeSpec -> JSON Schema -> Generated/*.g.cs; CI's codegen guard asserts a clean tree afterwards
node scripts/contracts-schema-diff.mjs <previous-dir> <current-dir> <previous-version> <candidate-version>   # --allowlist defaults to src/Strategos.Contracts/schemas/breaking-changes.allowlist.json
```

Every change under `src/Strategos.Contracts/` bumps `<ContractsVersion>` in `Strategos.Contracts.csproj` (pre-1.0: a breaking change advances the minor and needs an allowlist entry; an additive change passes after any increment). Hand-edits to `Generated/` or `schemas/` are rejected by CI. `AGWF` ids are single-sourced from `AgwfCatalog.tsp`; never quote one as a literal in production code.

### Public API and packed-artifact probes

```bash
bash scripts/check-unshipped-against-tag.sh           # PublicAPI.Unshipped.txt vs the last tag
bash scripts/check-builder-api-stability.sh
dotnet pack strategos.slnx -c Release -o ./packages && bash scripts/verify-generator-consumer-build.sh ./packages   # packaged analyzers really enforce #167/#169
bash scripts/pack-to-local-feed.sh ./local-feed && bash scripts/verify-basileus-smoke.sh ./local-feed              # the basileus-consumed surface still compiles from the nupkg
```

### Docs site

```bash
cd docs && npm ci && npm run dev     # npm run build for the production build; published at https://lvlup-sw.github.io/strategos/
```

Content lives under `docs/src/content/docs/` (Starlight content collections). `docs/diagnostics/agwf.md` is generated from the AGWF catalog — do not hand-edit it, and do not move it (the codegen guard keys on the path).

## Architecture

Read these; do not duplicate their content here:

- **Component map, glossary, diagnostics families, release trains** — `docs/adrs/system-index.md`.
- **The design** (why a compiled saga, the DSL vocabulary, persistence modes, the ontology layer) — `docs/adrs/design.md`, and the published `docs/src/content/docs/reference/platform-architecture.md` (§4.14 for the ontology).
- **The action calculus** (contracts on actions, the proof kernel, compensation, the static-proof boundary) — `docs/src/content/docs/reference/action-calculus.md`.
- **System design page** (diagrams) — `docs/system-design.html`.

Quick orientation only: the DSL produces *definitions*; only `Strategos.Generators` knows how Wolverine and Marten compose; the ontology is analyzers plus a runtime descriptor model and never a source generator; Contracts is the only place a shape both runtimes must agree on may live.

## Architectural Constraints (enforced)

The invariants catalog is `docs/architecture/invariants/` (U-1..U-8, one file each, with the deterministic checks) and its machine-readable twin `.exarchos/invariants.md`. `tests/Strategos.Architecture.Tests` keeps the two paired and runs the mechanical checks in CI.

1. **U-1** Workflows lower into Wolverine + Marten through the source generator; sagas are emitted, never hand-written.
2. **U-2** The ontology is analyzers + descriptors, self-contained; no Wolverine/Marten dependency, no source generator.
3. **U-3** MCP is first-class and tracks the current protocol revision (2026-07-28); `_meta`, `OutputSchema`, `resultType` everywhere.
4. **U-4** The workflow DSL uses domain words, never `Node` / `Edge` / `Vertex` / `Graph` on the authoring surface.
5. **U-5** Three validation tiers (builder-runtime, analyzer, emitter); every case has a stable `AGWF*` / `AONT*` id.
6. **U-6** DSL and descriptor types are sealed by default; extension is composition, not subclassing.
7. **U-7** Immutable record state; steps return a new state, never mutate their input.
8. **U-8** Descriptor identity is `ClrType` or `SymbolKey`, both first-class; no unconditional `ClrType` dereference.

## Releases

Two tag trains, contracts first: `contracts-vX.Y.Z` publishes `LevelUp.Strategos.Contracts` (must equal the pinned `<ContractsVersion>`); `vX.Y.Z` publishes the thirteen product packages (MinVer, `MinVerTagPrefix=v`, pre-release when the tag has a hyphen). Both publish through nuget.org Trusted Publishing behind the `nuget-publish` environment approval — see the Security section of `AGENTS.md`. `CHANGELOG.md` is hand-rolled; the consumer notice goes in `docs/handoffs/`.

## Documentation Index

| Topic | Location |
|---|---|
| System component map, glossary, diagnostics, release trains | `docs/adrs/system-index.md` |
| System design page (diagrams) | `docs/system-design.html` |
| The design, integrations, package inventory, theory | `docs/adrs/` |
| Architectural invariants (U-1..U-8) + deterministic checks | `docs/architecture/invariants/` and `.exarchos/invariants.md` |
| Workflow-definition kernel v1 (reserved kinds, carried-not-proved slots) | `docs/architecture/kernel-v1.md` |
| Cross-assembly binding proof (the portable proof catalog, its refusals, adoption) | `docs/architecture/cross-assembly-proof.md` |
| Performance targets and baselines | `docs/architecture/benchmarks/` |
| Design records (dated) | `docs/designs/` (superseded ones in `docs/designs/archive/`) |
| Implementation plans (dated) | `docs/plans/` (superseded roadmaps in `docs/plans/archive/`) |
| Specs and research (dated) | `docs/specs/`, `docs/research/` |
| Cross-repo handoffs and consumer upgrade notices | `docs/handoffs/` |
| Generated AGWF catalog and failure-mode enumeration | `docs/diagnostics/` |
| Published documentation source | `docs/src/content/docs/` (learn, guide, reference, examples) |
| Contracts package README | `src/Strategos.Contracts/README.md` |
| Roadmap tracker | lvlup-sw/strategos#153 |

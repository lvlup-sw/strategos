# AGENTS.md

## Project Overview

Strategos is a .NET library for building durable agentic workflows. It provides deterministic orchestration for probabilistic AI agents through event-sourced persistence, enabling complete audit trails and reproducible decision histories.

The library bridges agent frameworks and workflow engines by treating each agent decision as an immutable event, allowing time-travel debugging and full auditability.

## Tech Stack

- **Language**: C# (.NET 10)
- **Build**: MSBuild with central package management; `strategos.slnx`, `Directory.Build.props/.targets` and `Directory.Packages.props` sit at the repository root (the lvlup-sw .NET layout, basileus being the reference)
- **Testing**: TUnit with NSubstitute for mocking
- **Code Generation**: Roslyn Source Generators
- **Code Quality**: StyleCop Analyzers, .NET Analyzers
- **Documentation**: Astro + Starlight (`@astrojs/starlight`, content-collection layout under `docs/src/content/docs/`)

## Key Dependencies

- **Microsoft.Extensions.AI** - AI abstractions for LLM integration
- **Microsoft.CodeAnalysis** - Roslyn APIs for source generation
- **Wolverine** - Saga orchestration (external runtime dependency)
- **Marten** - Event sourcing with PostgreSQL (external runtime dependency)

## Code Organization

```
strategos.slnx, Directory.Build.props, Directory.Build.targets, Directory.Packages.props, global.json
src/
├── Strategos/                   # Core fluent DSL: Abstractions, Builders, Definitions, Steps, Orchestration, Selection, Contracts (wire projection)
├── Strategos.Generators/        # Roslyn source generator (saga / extensions / state-reducer emitters, .workflow.json import)
├── Strategos.Contracts/         # TypeSpec sources, Generated/*.g.cs, schemas/, fixture corpus — the shared IR spine
├── Strategos.Contracts.Codegen/ # TypeSpec -> C# / JSON Schema / docs emitter
├── Strategos.Ontology/          # Descriptor model, graph builder, object sets, action calculus
├── Strategos.Ontology.Generators/  # Roslyn ANALYZERS only (AONT*)
├── Strategos.Ontology.MCP/, .MCP.Hosting/, .Npgsql/, .Embeddings/
├── Strategos.Agents/, .Agents.Mcp/, .Infrastructure/, .Rag/, .Identity.Abstractions/, .Benchmarks/
└── Shared/                      # source-shared helpers
tests/
├── Strategos.*.Tests/              # one test project per product project (17), named for its subject
├── Strategos.Architecture.Tests/   # cross-cutting: invariants catalog + deterministic checks
└── basileus-smoke/                 # PackageReference consumer probe; own .slnx; empty props stoppers keep it outside the build tree
samples/                         # runnable examples (ProjectReference into src/)
scripts/                         # codegen, schema diff, packed-artifact probes, drift checks
docs/                            # Astro + Starlight site (src/content/docs/) + adrs/ architecture/ designs/ plans/ specs/ research/ handoffs/ diagnostics/
.exarchos/invariants.md          # machine-readable invariants catalog (U-1..U-8); .exarchos.yml registers it
.agents/skills/                  # agent skills (design-invariants indexes docs/architecture/invariants/)
```

Layout rules: `src/` holds product projects only, and every test project lives under `tests/`, named for the project it tests; dated documents are `YYYY-MM-DD-slug.md`; superseded material goes into an `archive/` subfolder of its own category, never a top-level `docs/archive/`. `docs/diagnostics/` is generated from the AGWF catalog and is a CI-guarded path — never move or hand-edit it.

## Security Considerations

- **Authentication**: Not applicable (library, not service)
- **Secrets Management**: No secrets stored in repository
- **External APIs**: Library consumers configure LLM providers via Microsoft.Extensions.AI
- **NuGet Publishing**: GitHub Actions trusted publishing (OIDC via `NuGet/login@v1`); short-lived nuget.org keys. The two policies (`publish.yml`, `publish-contracts.yml`, environment `nuget-publish`) went live with `contracts-v0.12.0` and `v3.0.0-rc.1` on 2026-09-09 and the long-lived `NUGET_API_KEY` secret was deleted afterwards. Repo var `NUGET_USER` is the nuget.org username of the person who *created* the two Trusted Publishing policies (`rsalus`), not the `lvlup` organization that owns the packages and the policies; the token exchange is a 401 otherwise.

## Known Tech Debt

- **Dependency automation is half-wired (2026-09-10).** `.github/dependabot.yml` covers only `github-actions`; npm (`docs/`, `src/Strategos.Contracts/`) and NuGet are assigned to Renovate by the org preset, but Renovate has never run on this repository (no Dependency Dashboard issue, no `renovate/*` branch — the app appears not to be installed) and Dependabot *security updates* are disabled at the repository level. Until one of them is switched on, npm advisories are cleared by hand (see the 2026-09-10 lockfile PR) and NuGet is clean by luck, not by process.
- `docs/diagnostics/agwf.md`'s `since` column still reads 2.11.0 / 2.13.0 for diagnostics that first shipped in 3.0.0-rc.1; correcting it narrows `AgwfCatalog.tsp` consts and is deferred to the Contracts 0.13.0 bump (#209).

## Scan Preferences

- **Focus Areas**: Security vulnerabilities, code quality, dependency hygiene
- **Ignore Patterns**: `**/bin/`, `**/obj/`, `**/node_modules/`, `.git/`, `TestResults/`, `coverage/`, `packages/`
- **Severity Threshold**: Report Medium and above

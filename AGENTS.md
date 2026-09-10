# AGENTS.md

## Project Overview

Strategos is a .NET library for building durable agentic workflows. It provides deterministic orchestration for probabilistic AI agents through event-sourced persistence, enabling complete audit trails and reproducible decision histories.

The library bridges agent frameworks and workflow engines by treating each agent decision as an immutable event, allowing time-travel debugging and full auditability.

## Tech Stack

- **Language**: C# (.NET 10)
- **Build**: MSBuild with central package management
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
src/
├── Strategos/           # Core fluent DSL and abstractions
│   ├── Abstractions/           # Core interfaces
│   ├── Builders/               # Fluent builder implementations
│   ├── Configuration/          # DI and setup
│   ├── Definitions/            # Workflow definition types
│   ├── Models/                 # Domain models
│   ├── Orchestration/          # Saga orchestration
│   ├── Primitives/             # Value types
│   ├── Selection/              # Agent selection (Thompson Sampling)
│   └── Steps/                  # Step implementations
├── Strategos.Generators/    # Compile-time code generation
├── Strategos.Agents/        # Microsoft Agent Framework integration
├── Strategos.Infrastructure/ # Production implementations
├── Strategos.Rag/           # Vector store adapters
└── *Tests/                         # Test projects (co-located)

docs/                           # Astro + Starlight documentation site (src/content/docs/)
packages/                       # NuGet package output
```

## Security Considerations

- **Authentication**: Not applicable (library, not service)
- **Secrets Management**: No secrets stored in repository
- **External APIs**: Library consumers configure LLM providers via Microsoft.Extensions.AI
- **NuGet Publishing**: GitHub Actions trusted publishing (OIDC via `NuGet/login@v1`); short-lived nuget.org keys. Repo var `NUGET_USER` is the nuget.org username of the person who *created* the two Trusted Publishing policies (`rsalus`), not the `lvlup` organization that owns the packages and the policies; the token exchange is a 401 otherwise.

## Known Tech Debt

- None. The two nuget.org Trusted Publishing policies (`publish.yml`, `publish-contracts.yml`, environment `nuget-publish`) went live with `contracts-v0.12.0` and `v3.0.0-rc.1` on 2026-09-09, and the long-lived `NUGET_API_KEY` secret was deleted afterwards.

## Scan Preferences

- **Focus Areas**: Security vulnerabilities, code quality, dependency hygiene
- **Ignore Patterns**: `**/bin/`, `**/obj/`, `**/node_modules/`, `.git/`, `TestResults/`, `coverage/`, `packages/`
- **Severity Threshold**: Report Medium and above

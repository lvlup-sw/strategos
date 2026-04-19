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
- **Documentation**: VitePress (Node.js-based static site)

## Build Requirements

- **.NET SDK**: **10.0.202 or newer** (pinned in `global.json` with `rollForward: latestFeature`). Required because Roslyn 5.3 source generators produce `CS9057` under the .NET 10.0.1xx SDKs, which ship the C# compiler at 5.0.0.0. SDK 10.0.202 ships compiler 5.3.0, satisfying the analyzer floor. CI enforces this via `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` (respects `global.json`).

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

docs/                           # VitePress documentation site
packages/                       # NuGet package output
```

## Security Considerations

- **Authentication**: Not applicable (library, not service)
- **Secrets Management**: No secrets stored in repository
- **External APIs**: Library consumers configure LLM providers via Microsoft.Extensions.AI
- **NuGet Publishing**: Uses GitHub Actions with NuGet API key (repository secret)

## Known Tech Debt

- None acknowledged

## Scan Preferences

- **Focus Areas**: Security vulnerabilities, code quality, dependency hygiene
- **Ignore Patterns**: `**/bin/`, `**/obj/`, `**/node_modules/`, `.git/`, `TestResults/`, `coverage/`, `packages/`
- **Severity Threshold**: Report Medium and above

# Contributing to Strategos

Thank you for your interest in contributing to Strategos! This document provides guidelines for contributing to the project.

## Code Style

We use StyleCop analyzers to enforce consistent code style across the codebase.

### General Guidelines

- Use file-scoped namespaces
- Use implicit usings (enabled globally)
- Enable nullable reference types
- All public members require XML documentation with `<summary>`, `<param>`, `<returns>`, and `<exception>` tags
- Use `Result<T>` pattern for operations that can fail (exceptions only for programmer errors)
- Use guard clauses at the top of methods

### Formatting

- Newlines begin with operators:
  ```csharp
  var isValid = condition1
      && condition2
      && condition3;
  ```
- Use expression-bodied members for simple properties and methods
- Maximum line length: 120 characters

### Naming Conventions

- PascalCase for public members, types, and namespaces
- camelCase for private fields (with `_` prefix)
- Interfaces prefixed with `I`
- Async methods suffixed with `Async`

## Cross-product API stability (builder interfaces)

The 7 `Strategos.Builders` interfaces — `IWorkflowBuilder<TState>`,
`IBranchBuilder<TState>`, `ILoopBuilder<TState>`, `IForkJoinBuilder<TState>`,
`IApprovalBuilder<TState, TApprover>`, `IFailureBuilder<TState>`, and
`IStepConfiguration<TState>` — are a **cross-product contract**. exarchos's
`strategos-api-mirror.test.ts` parses their signatures; an unguarded breaking
change here is invisible until exarchos CI fails downstream.

These interfaces are baselined in
`src/Strategos/PublicAPI/PublicAPI.Shipped.txt` and enforced by
`Microsoft.CodeAnalysis.PublicApiAnalyzers`. The baseline is scoped to **only**
those 7 interfaces (INV-1) via `src/Strategos/PublicApi.globalconfig` (silences
the analyzer assembly-wide, covering in-memory source-generator output) and
`src/Strategos/.editorconfig` (re-enables RS0016/RS0017 as build-breaking
errors for just the 7 interface files).

**When you change a builder signature**, the build fails (RS0016/RS0017) with:

> Update PublicAPI.Unshipped.txt and add a CHANGELOG entry under Cross-product breaking changes.

To satisfy the gate:

1. Run `dotnet format analyzers src/Strategos/Strategos.csproj --diagnostics
   RS0016` (or apply the IDE code-fix) to add the new/changed lines to
   `src/Strategos/PublicAPI/PublicAPI.Unshipped.txt`.
2. Add an entry under the `### Cross-product breaking changes` section of
   `CHANGELOG.md` describing the change.
3. Adding a member is a **minor** bump; renaming or removing one is a **major**
   bump (the diagnostic IDs and builder surface are public contract, INV-5).

On push to `main`, `.github/workflows/public-api-drift.yml` diffs the shipped
baseline against the previous tag and (if the `EXARCHOS_ISSUES_PAT` secret is
present) opens a tracking issue on `lvlup-sw/exarchos` so the downstream mirror
can re-baseline deliberately.

## Pull Request Process

1. **Fork the repository** and create a feature branch from `main`
2. **Write tests first** - We follow TDD practices
3. **Ensure all tests pass** - Run `dotnet test` before submitting
4. **Update documentation** - Add XML docs for new public APIs
5. **Create a pull request** with a clear description of changes

### PR Title Format

Use conventional commit format:

```
<type>: <description>
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `refactor`: Code refactoring
- `docs`: Documentation changes
- `test`: Test additions or modifications
- `chore`: Maintenance tasks

### PR Description

Include:
- Summary of changes
- Motivation and context
- Test plan
- Breaking changes (if any)

## Testing Requirements

We use TUnit with Microsoft.Testing.Platform.

### Running Tests

```bash
cd src && dotnet test
```

### Test Coverage

- Minimum **80% code coverage** for new code
- All public APIs must have unit tests
- Integration tests for workflow execution paths

### Writing Tests

```csharp
[Test]
public async Task Should_Execute_Step_When_Valid()
{
    // Arrange
    var step = new ValidateOrder();
    var state = new OrderState { Items = [new OrderItem()] };

    // Act
    var result = await step.ExecuteAsync(state, TestContext.Default, CancellationToken.None);

    // Assert
    await Assert.That(result.State.IsValid).IsTrue();
}
```

**Important:** All TUnit assertions must be awaited:

```csharp
// Correct
await Assert.That(result).IsEqualTo(expected);

// Wrong - will not fail the test
Assert.That(result).IsEqualTo(expected);
```

### Test Attributes

| TUnit | Description |
|-------|-------------|
| `[Test]` | Marks a test method |
| `[Arguments(1, 2)]` | Parameterized test |
| `[Property("Category", "Integration")]` | Test category |

## Commit Message Format

Use conventional commits:

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Examples

```
feat(dsl): add Fork/Join parallel execution support

Implements parallel step execution with automatic result synchronization.
Fork paths execute concurrently; Join step receives merged state.

Closes #42
```

```
fix(generators): resolve duplicate phase names in nested loops

Loop step phases now use full path naming (Outer_Inner_Step) to avoid
phase enumeration conflicts.

Fixes #87
```

## Development Setup

### Prerequisites

- .NET 10 SDK
- PostgreSQL (for integration tests)

### Building

```bash
cd src && dotnet build
```

### Running Locally

```bash
cd src && dotnet test
```

## Documentation Contributions

We welcome documentation improvements! The docs site is built with Astro + Starlight (`@astrojs/starlight`).

- **[Documentation Site](https://lvlup-sw.github.io/strategos/)** - Live documentation
- **[Docs Contributing Guide](docs/src/content/docs/contributing.md)** - Style guidelines and local setup

To contribute documentation:

1. Follow the setup in `docs/src/content/docs/contributing.md`
2. Markdown content lives under `docs/src/content/docs/` (Starlight's content collection layout)
3. Run `npm run dev` in the `docs/` directory to preview locally
4. Submit a PR with `docs:` prefix

## Questions?

Open an issue for questions or feature discussions. We're happy to help!

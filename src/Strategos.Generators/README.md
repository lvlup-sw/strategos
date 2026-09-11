# Strategos.Generators

Roslyn source generators for Strategos. They lower a fluent workflow definition into a Wolverine saga, Marten persistence and the dependency-injection registrations, at build time.

The package is a build-time dependency. It adds no assembly to the output of an application.

## What the generators emit

From one `Workflow<TState>.Create(...)` definition, the generators emit:

- the Wolverine saga that runs the workflow,
- the phase enum, the start command and the step events,
- the worker handlers for each step,
- the `Add{Name}Workflow()` extension that registers all of them.

A `.workflow.json` file passed as an `AdditionalFiles` item enters the same lowering path.

## Installation

Add the package. Set `PrivateAssets` so it does not flow to consumers.

```bash
dotnet add package LevelUp.Strategos.Generators
```

```xml
<PackageReference Include="LevelUp.Strategos.Generators" Version="3.0.0"
                  PrivateAssets="all" />
```

## Compile-time validation

An invalid workflow fails the build. It does not fail at run time. Each failure has a stable `AGWF` identifier that names the construct and its position.

The catalog holds 43 codes. Some identifiers are gaps, and the gaps stay gaps: an identifier is never reused for a different meaning.

Some codes cannot be suppressed. `<NoWarn>`, `.editorconfig` severities and `#pragma warning disable` do not silence them. A proof that a workflow refuses is not a style preference.

Read [the AGWF reference](https://lvlup-sw.github.io/strategos/reference/diagnostics/agwf-agsr/) for each code, its cause and its remedy.

## Related packages

- `LevelUp.Strategos` — the workflow DSL these generators read.
- `LevelUp.Strategos.Ontology.Generators` — the analyzers for ontology declarations.

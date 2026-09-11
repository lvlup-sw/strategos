# Strategos.Ontology.Generators

Roslyn diagnostic analyzers for Strategos ontology declarations. They check a domain ontology at compile time.

The package contains analyzers only. It contains no source generator, and the ontology layer never generates source. A descriptor model at run time and analyzers at compile time do the whole job.

The package is a build-time dependency. It adds no assembly to the output of an application.

## Installation

Add the package beside `LevelUp.Strategos.Ontology`. Set `PrivateAssets` so it does not flow to consumers.

```bash
dotnet add package LevelUp.Strategos.Ontology.Generators
```

```xml
<PackageReference Include="LevelUp.Strategos.Ontology.Generators" Version="3.0.0"
                  PrivateAssets="all" />
```

## What the analyzers check

Each failure has a stable `AONT` identifier. The identifiers fall into four families:

| Family | Subject |
|---|---|
| `AONT001`–`AONT037` | Registration and composition of the declarations |
| `AONT040`–`AONT042` | Link composition |
| `AONT200`-series | Drift, edge shape, typed action contracts and binding export |

Since 3.0 every action carries a declared contract: a subject, its requirements, its guarantees, the resources it touches, its authority and its inverse. The analyzers check that contract against the declaration.

Some codes cannot be suppressed. `<NoWarn>`, `.editorconfig` severities and `#pragma warning disable` do not silence them. The graph refuses them again when it freezes, so an application that disables analyzers does not admit them either.

Read [the AONT 200-series reference](https://lvlup-sw.github.io/strategos/reference/diagnostics/aont-200-series/) for each code, its cause and its remedy.

## Related packages

- `LevelUp.Strategos.Ontology` — the descriptor model these analyzers check.
- `LevelUp.Strategos.Generators` — the source generators for the workflow DSL.

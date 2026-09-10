---
# Strategos architectural invariants — user tier, schema-version 3.
#
# This is the machine-readable twin of docs/architecture/invariants/U-*.md. The
# prose files carry the acceptance questions, repo-grounded checks, severity
# guide and worked examples; this catalog carries the enforcement contract that
# exarchos loads at design and review time (registered in .exarchos.yml).
#
# Ids are U-N because exarchos reserves INV-* for its own substrate catalog and
# rejects INV-* ids in a user catalog. U-N here is the invariant this repo wrote
# as INV-N before 2026-09-10 (specs, CHANGELOG entries and issues keep the old
# spelling as history). Change an entry here and its U-N-*.md together;
# tests/Strategos.Architecture.Tests asserts the two stay paired and that every
# path under `references` exists.
#
# Every entry is `mode: audit`. The mechanical grep gates live in
# docs/architecture/invariants/deterministic-checks.md and run as tests in
# tests/Strategos.Architecture.Tests rather than as `mode: check` trees, so a
# gate that goes blind fails loudly in CI instead of returning zero hits.
schema-version: 3
invariants:
  - id: U-1
    dimension: workflow-durability
    axis: substrate
    cost-of-load: always-load
    integrity-class: user
    applies-to:
      - src/Strategos/**
      - src/Strategos.Generators/**
      - src/Strategos.Infrastructure/**
      - src/Strategos.Agents/**
      - src/*/*.csproj
    summary: >
      Workflows lower into Wolverine sagas and Marten event/document storage
      through Strategos.Generators only. Sagas are emitted by SagaEmitter, never
      hand-written; no project outside the generator takes a direct Wolverine or
      Marten reference; no workflow runtime state lives outside Marten.
    references:
      - docs/architecture/invariants/U-1-workflow-durability-via-wolverine-marten.md
      - docs/architecture/invariants/deterministic-checks.md
      - src/Strategos.Generators/Emitters/SagaEmitter.cs
      - src/Strategos.Generators/Emitters/ExtensionsEmitter.cs
    severity:
      default: blocking
    enforcement:
      mode: audit
      audit-prompt: >
        Does the diff introduce runtime state outside Marten document or event
        storage, declare a class deriving from Wolverine's Saga anywhere except
        SagaEmitter's emitted output, add a direct Wolverine.* or Marten.*
        PackageReference to a non-generator Strategos project, or add a
        PersistenceMode that bypasses SagaEmitter/ExtensionsEmitter with a
        parallel runtime? Cite the offending file + line.

  - id: U-2
    dimension: ontology-self-containment
    axis: substrate
    cost-of-load: always-load
    integrity-class: user
    applies-to:
      - src/Strategos.Ontology/**
      - src/Strategos.Ontology.*/**
    summary: >
      The ontology subsystem is a runtime descriptor model plus Roslyn analyzers
      (AONT* diagnostics). It never becomes a source generator and never takes a
      Wolverine or Marten dependency; integration with workflows is expressed at
      the workflow surface, one way.
    references:
      - docs/architecture/invariants/U-2-ontology-analyzer-only-self-contained.md
      - docs/architecture/invariants/deterministic-checks.md
      - src/Strategos.Ontology.Generators/Analyzers/OntologyDefinitionAnalyzer.cs
    severity:
      default: blocking
    enforcement:
      mode: audit
      audit-prompt: >
        Does the diff add a Wolverine or Marten PackageReference or using
        directive to any Strategos.Ontology* project, add an IIncrementalGenerator
        or ISourceGenerator (or a [Generator] class) under
        Strategos.Ontology.Generators, or couple the ontology back into the
        workflow runtime instead of the workflow surface consuming the ontology?
        Cite the offending file + line.

  - id: U-3
    dimension: mcp-protocol-currency
    axis: substrate
    cost-of-load: reference-only
    integrity-class: user
    applies-to:
      - src/Strategos.Ontology.MCP/**
      - src/Strategos.Ontology.MCP.Hosting/**
      - src/Strategos.Agents.Mcp/**
    summary: >
      MCP is first-class and tracks the current protocol revision (2026-07-28):
      every response record carries a _meta envelope, every tool descriptor an
      OutputSchema, every CallToolResult a resultType, and no code path downgrades
      to an older wire shape for compatibility.
    references:
      - docs/architecture/invariants/U-3-mcp-first-class-latest-spec.md
      - docs/architecture/invariants/deterministic-checks.md
      - src/Strategos.Ontology.MCP/ToolAnnotations.cs
      - src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs
    severity:
      default: advisory
    enforcement:
      mode: audit
      audit-prompt: >
        Does the diff add an MCP response record without a [JsonPropertyName("_meta")]
        ResponseMeta member, a tool descriptor without OutputSchema, a
        CallToolResult construction without ResultType, a placeholder icon where
        Icons should stay null, a pre-2026-07-28 protocol revision string, or a
        branch that omits any of these "for older clients"? Cite the offending
        file + line.

  - id: U-4
    dimension: dsl-nomenclature
    axis: substrate
    cost-of-load: reference-only
    integrity-class: user
    applies-to:
      - src/Strategos/Builders/**
      - src/Strategos/Abstractions/**
      - src/Strategos/Definitions/**
    summary: >
      The public workflow DSL uses the words workflow authors think in (StartWith,
      Then, Branch, Fork, Join, RepeatUntil, AwaitApproval, OnFailure) and never
      exposes graph-theory terms (Node, Edge, Vertex, Graph) on the authoring
      surface, in diagnostics, exception messages or trace names. The ontology DSL
      is a different surface and is out of scope.
    references:
      - docs/architecture/invariants/U-4-concrete-dsl-nomenclature.md
      - docs/architecture/invariants/deterministic-checks.md
      - src/Strategos/Abstractions/IWorkflowBuilder.cs
    severity:
      default: advisory
    enforcement:
      mode: audit
      audit-prompt: >
        Does the diff add a public type, member, diagnostic message, exception
        message or trace name under src/Strategos/{Builders,Abstractions,Definitions}
        whose name contains Node, Edge, Vertex or Graph, or borrow a name from
        the generator's internal model rather than from what a workflow author is
        doing? Cite the offending file + line and propose the domain-aligned name.

  - id: U-5
    dimension: tiered-validation-stable-ids
    axis: substrate
    cost-of-load: always-load
    integrity-class: user
    applies-to:
      - src/Strategos/Builders/**
      - src/Strategos.Generators/**
      - src/Strategos.Ontology.Generators/**
      - src/Strategos.Contracts/**/*.tsp
    summary: >
      Validation happens at three tiers (builder-runtime throw, Roslyn analyzer,
      emitter-time guard), preferring the earliest tier that can catch the error,
      and every validation case carries a stable AGWF*/AONT* diagnostic id.
      Ids are never reused, renumbered or removed outside a major; AGWF ids are
      single-sourced from AgwfCatalog.tsp, never hand-quoted in production code.
    references:
      - docs/architecture/invariants/U-5-three-tiered-validation-stable-diagnostic-ids.md
      - docs/architecture/invariants/deterministic-checks.md
      - src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs
      - src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnosticIds.cs
    severity:
      default: blocking
    enforcement:
      mode: audit
      audit-prompt: >
        Does the diff add a validation case without a new AGWF*/AONT* id, reuse or
        renumber an existing id, remove an id outside a major version, quote an
        AGWF literal in production diagnostics instead of consuming AgwfCodes.*,
        add a fourth validation tier without justification, or throw at runtime
        where an analyzer diagnostic could catch the same case at compile time?
        Cite the offending file + line.

  - id: U-6
    dimension: sealed-by-default
    axis: substrate
    cost-of-load: always-load
    integrity-class: user
    applies-to:
      - src/Strategos/Builders/**
      - src/Strategos/Definitions/**
      - src/Strategos/Steps/**
      - src/Strategos.Ontology/Descriptors/**
    summary: >
      DSL and descriptor types are sealed records or sealed classes by default.
      The source generator targets concrete types, so an override the generator
      does not know about is silently bypassed at the generated-code boundary;
      extension is expressed through interfaces, fluent extension methods and
      IOntologySource, not subclassing.
    references:
      - docs/architecture/invariants/U-6-sealed-by-default.md
      - docs/architecture/invariants/deterministic-checks.md
    severity:
      default: blocking
    enforcement:
      mode: audit
      audit-prompt: >
        Does the diff add a public non-sealed, non-static, non-abstract class or
        a public virtual member under src/Strategos/{Builders,Definitions,Steps}
        or src/Strategos.Ontology/Descriptors without a documented reason and an
        audit of the generator path that consumes the type? Could the extension
        point be an interface or extension method instead? Cite the offending
        file + line.

  - id: U-7
    dimension: immutable-state
    axis: substrate
    cost-of-load: always-load
    integrity-class: user
    applies-to:
      - src/Strategos/**
      - src/Strategos.Generators/**
      - samples/**
    summary: >
      IWorkflowState implementations are records with init-only properties and
      immutable collections; steps return an updated state through StepResult
      (state with { ... }) and never mutate their input, because Marten replay,
      time-travel debugging and saga snapshots all assume a deterministic
      event-to-state mapping.
    references:
      - docs/architecture/invariants/U-7-immutable-record-state.md
      - docs/architecture/invariants/deterministic-checks.md
      - src/Strategos/Abstractions/IWorkflowState.cs
      - src/Strategos/Steps/StepResult.cs
    severity:
      default: blocking
    enforcement:
      mode: audit
      audit-prompt: >
        Does the diff add an IWorkflowState type with a settable property or a
        mutable collection (List, Dictionary, HashSet) in state, or a step whose
        ExecuteAsync writes through a reference held by the input state instead
        of returning a new record? Cite the offending file + line.

  - id: U-8
    dimension: polyglot-identity
    axis: substrate
    cost-of-load: always-load
    integrity-class: user
    applies-to:
      - src/Strategos.Ontology/**
      - src/Strategos.Ontology.*/**
    summary: >
      Ontology descriptor identity is ClrType or SymbolKey, both first-class; at
      least one is present, SymbolKey wins at merge. Builder, graph, evaluator
      and MCP code never unconditionally dereference ClrType or reach for
      typeof(...) on a descriptor, and every new descriptor feature has a
      SymbolKey path or an explicit CLR-only diagnostic.
    references:
      - docs/architecture/invariants/U-8-polyglot-identity.md
      - docs/architecture/invariants/deterministic-checks.md
      - src/Strategos.Ontology/Descriptors/ObjectTypeDescriptor.cs
      - src/Strategos.Ontology/Sources/IOntologySource.cs
    severity:
      default: blocking
    enforcement:
      mode: audit
      audit-prompt: >
        Does the diff dereference descriptor.ClrType (including with the
        null-forgiving operator) or call typeof(...) on a descriptor's CLR side
        without a SymbolKey fallback or an explicit CLR-only diagnostic, type a
        new public API over descriptors in terms of System.Type, or add ontology
        tests with no SymbolKey-only descriptor? Cite the offending file + line.
---

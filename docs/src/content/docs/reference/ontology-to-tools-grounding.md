---
title: "Ontology-to-Tools Compilation: Grounding Analysis"
---

# Ontology-to-Tools Compilation: Grounding Analysis

A formal analysis mapping the Strategos `Strategos.Ontology` layer against Zhou et al.'s "Ontology-to-tools compilation for executable semantic constraint enforcement in LLM agents" (arXiv:2602.03439, 2025). This paper addresses the same core problem we solve: compiling formal domain knowledge into executable tool interfaces that constrain LLM agent behavior.

**Scope:** All 12 core primitives from `platform-architecture.md` section 4.14.4, analyzed against the paper's compilation framework, constraint enforcement model, and MCP integration.

**Conventions:** Paper citations use `[§N]` or `[§N.N, p.N]`. Our spec references use `[§4.14.X]`. The paper refers to its framework as "ontology-to-tools compilation" (OTC).

**Key OTC terminology:**
- **T-Box:** The ontology schema -- class definitions, relations, constraints, and axioms (OWL/RDF). Corresponds roughly to our `DomainOntology.Define()` method body.
- **A-Box:** Concrete instances of T-Box classes -- the populated knowledge graph. Corresponds to runtime entity instances persisted outside our ontology layer.
- **Hard constraints:** Formal T-Box axioms (class hierarchy, domain/range typing, cardinality). Enforced deterministically at tool-call time. Parallel to our `Requires()` preconditions.
- **Soft constraints:** Natural-language annotations (`rdfs:comment`) that guide but don't block. Parallel to our typed `RequiresSoft()` predicates; Strategos descriptions remain presentation metadata.
- **Constraint feedback:** Structured error responses from tool calls when violations are detected. The agent retries with corrected inputs.

---

## 1. Executive Summary

Zhou et al.'s paper is the closest published work to our `Strategos.Ontology` design. Both systems solve the same fundamental problem -- compiling domain ontologies into typed tool interfaces that constrain LLM agent action spaces -- but take different approaches reflecting different architectural contexts (runtime RDF knowledge graphs vs. compile-time .NET source generation).

**Three areas of strong convergence:**

1. **Ontology-to-tool compilation pipeline.** Both systems transform a declarative ontology specification into executable tool interfaces. OTC compiles OWL T-Box → Python MCP server with typed tools. We compile `DomainOntology.Define()` → Roslyn source generator → `IOntologyQuery` + `Strategos.Ontology.MCP` tool stubs [§4.14.11].

2. **Constraint enforcement at creation time.** Both enforce constraints during agent interaction rather than through post-hoc validation. OTC returns structured constraint feedback from tool calls. We express preconditions (`Requires()`) and postconditions (`Modifies()`, `CreatesLinked()`) that are checked at dispatch time [§4.14.5].

3. **Action space scoping.** Both use the ontology to constrain which tools agents can invoke. OTC groups tools by task step and exposes them through MCP. We filter actions by object type, lifecycle state, and preconditions via `IOntologyQuery.GetValidActions()` [§4.14.12].

**Areas where OTC still extends beyond our design:**

1. **LLM-driven compilation (meta-prompts).** OTC uses an LLM agent to
   *generate* tool implementations from the T-Box, using domain-agnostic
   meta-prompts. Strategos deliberately keeps compilation deterministic and
   represents non-decidable runtime policy as explicitly keyed `Custom`
   predicates.

The other two gaps identified by the original analysis are closed in 2.13.
`ConstraintStrength` distinguishes hard and soft predicates, tri-state
discovery preserves unknown information, and dispatch returns structured
constraint violations while failing closed when enforcement is enabled.

**Two areas where our design extends beyond OTC:**

1. **Compile-time validation.** Our Roslyn source generator and analyzer catch invalid ontology definitions (broken links, type mismatches, unreachable states, and incompatible explicit action sequences) through the versioned AONT diagnostic catalog [§4.14.11]. OTC validates only at runtime.

2. **Rich schema refinements.** Our lifecycle state machines, derivation chains, interface actions, and extension points have no counterparts in OTC. The paper's ontology model is simpler (classes, properties, constraints) without temporal, compositional, or cross-domain abstractions.

---

## 2. Concept Mapping Table

Alignment ratings: **Strong** = direct structural correspondence; **Moderate** = same intent, different mechanism; **Weak** = loose conceptual parallel; **Novel** = no paper equivalent.

| # | Strategos.Ontology Primitive | OTC Equivalent | Alignment | Notes |
|---|---------------------------|----------------|-----------|-------|
| 1 | **Object Type** `builder.Object<T>()` | OWL Class in T-Box | Strong | Both map domain entity types into the ontology. OTC uses `owl:Class` definitions; we use C# types registered via expression trees. The OTC T-Box is richer in formal axiomatics (OWL DL) but poorer in engineering guarantees (no compile-time validation). |
| 2 | **Property** `obj.Property(x => x.Prop)` | `owl:DatatypeProperty` / `owl:ObjectProperty` | Strong | OTC inherits OWL's property distinction (DatatypeProperty for literals, ObjectProperty for entity references) which we lack (see N&R gap §4.2 in the companion analysis). OTC also carries `rdfs:range` constraints that parallel our type-checked expression trees. |
| 3 | **Link** `obj.HasMany<T>()` | `owl:ObjectProperty` with domain/range | Strong | Both represent typed relationships between entities. OTC uses OWL object properties with `rdfs:domain`/`rdfs:range` constraints. Our Links carry cardinality (`HasOne`/`HasMany`/`ManyToMany`) which OTC expresses via `owl:cardinality` axioms. |
| 4 | **Action** `obj.Action("name")` | Generated MCP tool function | Strong | The paper's core contribution maps directly: OTC compiles T-Box class operations into MCP tool functions with typed inputs, validation logic, and constraint checks [§3, §6.1]. Our Actions are declared in the ontology DSL and bound to workflows or MCP tools [§4.14.4]. The key difference: OTC *generates* tool implementations; we generate tool *metadata* (stubs, descriptions) while implementations exist separately. |
| 5 | **Interface** `builder.Interface<T>()` | No equivalent | Novel | OTC operates within a single T-Box at a time. There is no cross-cutting polymorphic shape system. OWL supports abstract classes and intersection types, but OTC does not use them for tool dispatch. |
| 6 | **Cross-Domain Link** `builder.CrossDomainLink()` | Cross-ontology references (OntoSyn → OntoSpecies → OntoMOPs) | Moderate | OTC uses multiple complementary ontologies (OntoSynthesis, OntoSpecies, OntoMOPs) linked through shared IRIs [§2, p.8]. Our cross-domain links formalize this with explicit declarations and extension points. OTC's approach is more implicit -- ontologies share a namespace and reference each other's classes. |
| 7 | **Precondition** `.Requires()` | Hard constraints (T-Box axioms) | Strong | OTC's hard constraints are "class hierarchy, domain and range typing, datatype restrictions, and any modelled cardinalities" [§6.1, p.18] -- checked deterministically at tool-call time. Strategos uses immutable typed predicates, hard/soft strength, tri-state discovery, and fail-closed dispatch enforcement. |
| 8 | **Guarantee and frame** `.Ensures()`, `.Modifies()`, `.CreatesLinked<T>()` | Tool return values + Turtle store mutations | Moderate | OTC tools mutate a persistent Turtle store and return results + validation feedback [§6.2, p.19]. Strategos separates post-state facts (`Ensures`) from may-write frame metadata; `CreatesLinked` is the sole effect that soundly derives a predicate fact. |
| 9 | **Lifecycle** `obj.Lifecycle()` | JSON iteration plan with ordered steps | Weak | OTC's JSON task decomposition defines an ordered sequence of extraction steps [§6.1, p.17]. This is a process-level lifecycle (document → synthesis steps → grounding), not an entity-level state machine. Our Lifecycle is richer: it models entity state transitions with explicit triggers. |
| 10 | **Derivation Chain** `.Computed().DerivedFrom()` | No equivalent | Novel | OTC does not model property dependencies or staleness propagation. Derived entities (CBUs) are computed by downstream enrichment modules, not tracked as ontological metadata. |
| 11 | **Interface Action** `iface.Action("Search")` | No equivalent | Novel | OTC has no polymorphic action dispatch. Tools are bound to specific T-Box classes. |
| 12 | **Extension Point** `obj.AcceptsExternalLinks()` | No equivalent (cross-ontology linking is implicit) | Novel | OTC links across ontologies via shared IRIs and `owl:sameAs` assertions [§6.3]. There is no target-side declaration of acceptable incoming links. |

**Summary:** 4 strong, 2 moderate, 1 weak, 5 novel. The strong alignments cluster around the core compilation pipeline (Object Type, Property, Link, Action, Precondition). Our novel primitives (Interface, Derivation Chain, Interface Action, Extension Point) reflect engineering concerns specific to multi-domain .NET systems that OTC's single-knowledge-graph architecture does not face.

---

## 3. Alignment Analysis

### 3.1 The Compilation Pipeline

The deepest alignment between our systems is the compilation pipeline itself:

| Stage | OTC | Strategos.Ontology |
|-------|-----|------------------|
| **Input** | OWL T-Box + meta-prompts | `DomainOntology.Define()` method body |
| **Compiler** | LLM agent (preparation stage) | Roslyn incremental source generator |
| **Output** | Python MCP server + typed tools + JSON plan | `IOntologyQuery` service + descriptors + MCP stubs |
| **Validation** | Runtime (tool-call time) | Compile-time AONT diagnostics + runtime enforcement |
| **Target** | RDF/Turtle knowledge graph | .NET DI container + domain persistence |

OTC: "Ontological specifications are compiled into executable tool interfaces that LLM-based agents must use to create and modify knowledge graph instances, enforcing semantic constraints during generation rather than through post-hoc validation" [Abstract].

Our spec: "A compile-time ontology maps domain types into a unified type graph... Agents plan against the ontology rather than flat tool lists, directly reducing the CMDP action space" [§4.14, Design Principle 5].

The architectural insight is identical. The implementation strategies differ in two key ways:

1. **Deterministic vs. generative compilation.** Our Roslyn generator produces deterministic output from the same input every time. OTC uses an LLM to generate tool implementations, which introduces variability but enables handling of soft constraints that formal axioms cannot express.

2. **Build-time vs. runtime validation.** Our AONT catalog catches errors at compile time, including typed-contract failures through `AONT217`–`AONT221`. OTC catches errors only when tools are invoked at runtime. Our approach is safer for production systems; OTC's is more flexible for exploratory knowledge extraction.

### 3.2 Constraint Enforcement Model

Both systems enforce constraints at the point of agent interaction rather than post-hoc:

**OTC:** "The compilation layer treats the T-Box as a machine-readable contract. It specifies which classes, relations, attributes, and constraints are allowed. From this contract, the framework generates executable tool interfaces with explicitly specified inputs, outputs, and validation behaviour. These tools are the only way to create or modify structured instances, so constraints are checked and repaired during construction" [§3, p.9].

**Our system:** `Requires(p => p.Status == PositionStatus.Active)` lowers to a
typed action predicate and gates dispatch. `GetCandidateActions(objectType,
facts)` distinguishes available from indeterminate operations while excluding
those proven unavailable; `GetActionConstraintReport` exposes all three states
[§4.14.5, §4.14.12].

The key difference is the default policy rather than the shape of feedback. Both
systems can return structured constraint results, but Strategos keeps general
hard-predicate enforcement opt-in through
`ActionDispatchOptions.EnforcePreconditions`. Hard predicates containing a
relation are always enforced. When enforcement blocks dispatch,
`ActionResult.Violations` preserves satisfied/unsatisfied/indeterminate
distinctions so an agent can correct or escalate the call. OTC enables its
feedback loop as the normal construction path; ablating that loop drops
synthesis-step F1 significantly [§4.2].

### 3.3 MCP as the Integration Layer

Both systems use MCP as the protocol for exposing ontology-aware tools to LLM agents:

**OTC:** "The Model Context Protocol (MCP) standardize[s] this interaction by providing a common interface for registering tools and exchanging typed inputs and outputs" [§1, p.5]. Generated MCP servers expose "each function as an MCP tool with an ontology-derived name and a typed argument schema" [§6.1, p.18].

**Our system:** `Strategos.Ontology.MCP` enriches progressive disclosure stubs with ontology metadata -- "including preconditions, lifecycle states, derivation chains, and extension points -- so agents discover typed action signatures with planning constraints rather than flat tool descriptions" [§4.14.15].

Both systems use MCP to bridge between symbolic ontological knowledge and LLM agent capabilities. The difference is granularity: OTC generates full tool *implementations* (Python functions with validation logic); we generate tool *descriptions* (metadata stubs that describe pre-existing tool implementations).

---

## 4. Gap Analysis

### 4.1 Runtime Constraint Feedback Protocol

**Status: Implemented in 2.13**

**Paper concept:** OTC tools return structured constraint violation messages: "If a violation is detected, for example a missing required field, a type mismatch, or an invalid unit, the tool returns an error with an explanation. The agent then retries with corrected inputs" [§6.2, p.19-20]. This feedback loop is critical -- ablating it causes "a substantial drop in synthesis-step F1" [§4.2].

**Our design:** `ConstraintEvaluation` records the predicate, truth value,
strength, failure reason, and optional expected shape. Dispatch failures carry a
`ConstraintViolationReport` in `ActionResult.Violations`; query and discovery
surfaces expose the same three-valued results. Missing authoritative facts stay
`Indeterminate`, so enforcing dispatch fails closed without pretending the
predicate is false.

**Remaining difference:** Strategos supplies semantic failure data, but choosing
and retrying a corrected invocation remains the consuming agent's responsibility.

**Assessment:** The contract and enforcement substrate is complete. Consumers
can build the OTC-style retry loop without parsing exception text.

### 4.2 Typed Soft Constraints Without Prompt Generation

**Status: Predicate layer implemented in 2.13; prompt generation is deferred**

**Paper concept:** OTC distinguishes hard constraints (formal axioms, deterministically enforced) from soft constraints (natural-language annotations in `rdfs:comment`, used to guide but not block). Soft constraints capture "operational definitions and heuristic decision rules that guide boundary setting and classification during extraction" [§6.1, p.18]. These are compiled into task-specific prompts, not tool validators.

**Our design:** `Requires()` creates a hard predicate and `RequiresSoft()`
creates a typed advisory predicate. Soft predicates participate in discovery and
constraint reports but never block dispatch or enter static composition proofs.
Descriptions remain presentation metadata and are deliberately excluded from
semantic identity. Strategos does not yet compile soft predicates into an
agent-specific prompt.

**Impact:** Low. The hard/soft contract distinction is explicit; only automatic
prompt synthesis remains absent.

**Usage:** Express machine-evaluable guidance with `RequiresSoft()`:

```csharp
obj.Action("ExecuteTrade")
    .Requires(p => p.Status == PositionStatus.Active)
    .RequiresSoft(ActionPredicate.Custom(
        "trading.prefer-market-hours",
        readSet: [ActionResource.External("market-clock")]),
        description: "Prefer executing during market hours");
```

### 4.3 No Tool Implementation Generation

**Paper concept:** OTC uses an LLM to generate the actual tool implementations (Python functions) from the T-Box and meta-prompts. The preparation agent "generates an ontology-aware Python script that supports the classes and properties needed for the extraction scenario" [§6.1, p.18]. This means the ontology *fully determines* the tool code.

**Our design:** Our source generator produces metadata descriptors and the `IOntologyQuery` service, but tool implementations (`BoundToWorkflow`, `BoundToTool`) are written separately by developers. The ontology maps to existing tools; it does not generate them.

**Impact:** Low. This is a deliberate architectural difference, not a gap. Our tools have complex business logic (execute trades, manage portfolios, ingest knowledge) that cannot be generated from ontological metadata alone. OTC's tools are simpler (create RDF triples, add properties, link instances) and are amenable to generation.

**Assessment:** Not applicable as a recommendation. However, the OTC approach could inspire *scaffolding* generation: the source generator could emit tool interface stubs with typed parameters, validation hooks, and documentation, even if the business logic must be hand-written.

---

## 5. Architectural Recommendations

### 5.1 Structured Constraint Feedback to Action Dispatch

**Status: Implemented in 2.13**

The paper's most impactful finding is that structured constraint feedback improves agent performance. Adapt this for our system:

```csharp
public sealed record ConstraintEvaluation(
    ActionPrecondition Precondition,
    PredicateTruthValue TruthValue,
    ConstraintStrength Strength,
    string? FailureReason,
    IReadOnlyDictionary<string, object?>? ExpectedShape);
```

`ActionResult.Violations` carries a `ConstraintViolationReport` instead of
throwing. An agent can distinguish `Unsatisfied` from `Indeterminate`, inspect
hard and soft entries, and take corrective action.

**OTC precedent:** "Each tool call returns both results and validation feedback. The feedback reports whether the requested update satisfies the ontology constraints" [§6.2, p.19].

### 5.2 Hard and Soft Constraint Taxonomy

**Status: Implemented in 2.13**

Formalize the hard/soft distinction from the paper:

```csharp
public enum ConstraintStrength
{
    Hard,
    Soft,
}
```

`ActionPrecondition.Strength` is immutable. Soft predicates are reported but
never block; hard relation-bearing formulas remain mandatory even when general
precondition enforcement is disabled.

### 5.3 MCP Tool Constraint Metadata

**Status: Implemented in 2.13 / Contracts 0.10**

`Strategos.Ontology.MCP` progressive-disclosure metadata now includes:

- structured, versioned `requires` predicates with canonical display text;
- structured `ensures` post-state guarantees;
- effect/frame, authority, client, confirmation, and safe relation-facade
  metadata.

Consumers must interpret the tagged predicate model and never parse the display
expression. Unknown discriminators are rejected.

---

## 6. Comparison with Nirenburg & Raskin Analysis

This analysis complements the [Nirenburg & Raskin theoretical grounding](/strategos/reference/ontology-theoretical-grounding/). The two sources address different aspects of our ontology layer:

| Aspect | N&R (2004) | Zhou et al. (2025) |
|--------|-----------|-------------------|
| **Focus** | Ontology structure and knowledge representation theory | Ontology-to-tool compilation for LLM agents |
| **Relevance** | Foundational: how to structure ontological knowledge | Applied: how to operationalize ontologies as agent constraints |
| **Key insight for us** | IS-A hierarchy, RELATION/ATTRIBUTE distinction, facet system | Constraint feedback loops, hard/soft constraint taxonomy, MCP integration |
| **Recommendations overlap** | Property kind discriminator (N&R §6.2) aligns with OWL's DatatypeProperty/ObjectProperty distinction used by OTC | Constraint feedback (OTC §5.1) would strengthen our precondition system recommended by N&R alignment |
| **Era** | Pre-LLM, NLP-focused knowledge engineering | Contemporary, LLM-agent-focused applied research |

The N&R analysis identifies *structural* gaps in our ontology model (hierarchy, property types, facets). The Zhou et al. analysis identifies *operational* gaps in how our ontology interacts with agents at runtime (constraint feedback, soft constraints, tool metadata richness). Both sets of recommendations are complementary.

---

## 7. References

### Paper Citations

| Citation | Content | Relevance |
|----------|---------|-----------|
| [Abstract] | Core contribution: ontology → executable tool interfaces | Pipeline alignment §3.1 |
| [§1, p.5] | MCP standardizes tool interaction | MCP alignment §3.3 |
| [§1, p.5-6] | Central contribution: compilation mechanism for symbolic → executable | Pipeline alignment §3.1 |
| [§2, p.8] | Multiple complementary ontologies (OntoSyn, OntoSpecies, OntoMOPs) | Cross-domain parallel §2 row 6 |
| [§3, p.9] | T-Box as machine-readable contract; tools enforce constraints | Constraint enforcement §3.2 |
| [§4.2] | Constraint feedback ablation: significant F1 drop without it | Feedback gap §4.1 |
| [§6.1, p.17-18] | Preparation stage: JSON plan, script generation, MCP server construction | Pipeline alignment §3.1 |
| [§6.1, p.18] | Hard vs. soft constraints; design principles for MCP servers | Constraint taxonomy §4.2, §5.2 |
| [§6.2, p.19-20] | Instantiation stage: ReAct loop, tool-call validation, constraint feedback | Feedback gap §4.1 |
| [Supp §7.1.2] | MCP protocol background: open client-server, typed schemas | MCP alignment §3.3 |

### Platform Architecture Citations

| Citation | Content |
|----------|---------|
| [§4.14.4] | Core primitives (12 concepts) |
| [§4.14.5] | Preconditions and postconditions |
| [§4.14.11] | Source generator and analyzer diagnostic pipeline |
| [§4.14.12] | IOntologyQuery interface |
| [§4.14.15] | Basileus adoption and MCP integration |

### Converted Paper Files

All files in `docs/reference/ontology-to-tools-compilation/`:

- [main-paper.md](/strategos/reference/ontology-to-tools-compilation/main-paper/) — Sections 1-6
- [supplementary.md](/strategos/reference/ontology-to-tools-compilation/supplementary/) — Background, methods, prompts, traces
- [references.md](/strategos/reference/ontology-to-tools-compilation/references/) — Bibliography

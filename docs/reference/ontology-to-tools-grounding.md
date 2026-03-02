# Ontology-to-Tools Compilation: Grounding Analysis

A formal analysis mapping the Strategos `Agentic.Ontology` layer against Zhou et al.'s "Ontology-to-tools compilation for executable semantic constraint enforcement in LLM agents" (arXiv:2602.03439, 2025). This paper addresses the same core problem we solve: compiling formal domain knowledge into executable tool interfaces that constrain LLM agent behavior.

**Scope:** All 12 core primitives from `platform-architecture.md` section 4.14.4, analyzed against the paper's compilation framework, constraint enforcement model, and MCP integration.

**Conventions:** Paper citations use `[§N]` or `[§N.N, p.N]`. Our spec references use `[§4.14.X]`. The paper refers to its framework as "ontology-to-tools compilation" (OTC).

**Key OTC terminology:**
- **T-Box:** The ontology schema -- class definitions, relations, constraints, and axioms (OWL/RDF). Corresponds roughly to our `DomainOntology.Define()` method body.
- **A-Box:** Concrete instances of T-Box classes -- the populated knowledge graph. Corresponds to runtime entity instances persisted outside our ontology layer.
- **Hard constraints:** Formal T-Box axioms (class hierarchy, domain/range typing, cardinality). Enforced deterministically at tool-call time. Parallel to our `Requires()` preconditions.
- **Soft constraints:** Natural-language annotations (`rdfs:comment`) that guide but don't block. Parallel to our `Description()` metadata.
- **Constraint feedback:** Structured error responses from tool calls when violations are detected. The agent retries with corrected inputs.

---

## 1. Executive Summary

Zhou et al.'s paper is the closest published work to our `Agentic.Ontology` design. Both systems solve the same fundamental problem -- compiling domain ontologies into typed tool interfaces that constrain LLM agent action spaces -- but take different approaches reflecting different architectural contexts (runtime RDF knowledge graphs vs. compile-time .NET source generation).

**Three areas of strong convergence:**

1. **Ontology-to-tool compilation pipeline.** Both systems transform a declarative ontology specification into executable tool interfaces. OTC compiles OWL T-Box → Python MCP server with typed tools. We compile `DomainOntology.Define()` → Roslyn source generator → `IOntologyQuery` + `Agentic.Ontology.MCP` tool stubs [§4.14.11].

2. **Constraint enforcement at creation time.** Both enforce constraints during agent interaction rather than through post-hoc validation. OTC returns structured constraint feedback from tool calls. We express preconditions (`Requires()`) and postconditions (`Modifies()`, `CreatesLinked()`) that are checked at dispatch time [§4.14.5].

3. **Action space scoping.** Both use the ontology to constrain which tools agents can invoke. OTC groups tools by task step and exposes them through MCP. We filter actions by object type, lifecycle state, and preconditions via `IOntologyQuery.GetValidActions()` [§4.14.12].

**Three areas where OTC extends beyond our design:**

1. **Runtime constraint feedback loop.** OTC tools return structured error messages on constraint violations, and the agent iteratively retries. Our preconditions are currently metadata-only (enforcement is opt-in) with no structured feedback protocol for constraint violations.

2. **LLM-driven compilation (meta-prompts).** OTC uses an LLM agent to *generate* the tool implementations from the T-Box, using domain-agnostic meta-prompts. Our compilation is deterministic (Roslyn source generator). The LLM-driven approach enables handling soft constraints that cannot be expressed as formal axioms.

3. **Hard/soft constraint distinction.** OTC explicitly separates formal axioms (hard) from annotation-based guidance (soft). Our system has preconditions (hard, expressible as predicates) and descriptions (soft, free-text) but no formal taxonomy of constraint types.

**Two areas where our design extends beyond OTC:**

1. **Compile-time validation.** Our Roslyn source generator catches invalid ontology definitions (broken links, type mismatches, unreachable states) at build time with 35 diagnostic codes [§4.14.11]. OTC validates only at runtime.

2. **Rich schema refinements.** Our lifecycle state machines, derivation chains, interface actions, and extension points have no counterparts in OTC. The paper's ontology model is simpler (classes, properties, constraints) without temporal, compositional, or cross-domain abstractions.

---

## 2. Concept Mapping Table

Alignment ratings: **Strong** = direct structural correspondence; **Moderate** = same intent, different mechanism; **Weak** = loose conceptual parallel; **Novel** = no paper equivalent.

| # | Agentic.Ontology Primitive | OTC Equivalent | Alignment | Notes |
|---|---------------------------|----------------|-----------|-------|
| 1 | **Object Type** `builder.Object<T>()` | OWL Class in T-Box | Strong | Both map domain entity types into the ontology. OTC uses `owl:Class` definitions; we use C# types registered via expression trees. The OTC T-Box is richer in formal axiomatics (OWL DL) but poorer in engineering guarantees (no compile-time validation). |
| 2 | **Property** `obj.Property(x => x.Prop)` | `owl:DatatypeProperty` / `owl:ObjectProperty` | Strong | OTC inherits OWL's property distinction (DatatypeProperty for literals, ObjectProperty for entity references) which we lack (see N&R gap §4.2 in the companion analysis). OTC also carries `rdfs:range` constraints that parallel our type-checked expression trees. |
| 3 | **Link** `obj.HasMany<T>()` | `owl:ObjectProperty` with domain/range | Strong | Both represent typed relationships between entities. OTC uses OWL object properties with `rdfs:domain`/`rdfs:range` constraints. Our Links carry cardinality (`HasOne`/`HasMany`/`ManyToMany`) which OTC expresses via `owl:cardinality` axioms. |
| 4 | **Action** `obj.Action("name")` | Generated MCP tool function | Strong | The paper's core contribution maps directly: OTC compiles T-Box class operations into MCP tool functions with typed inputs, validation logic, and constraint checks [§3, §6.1]. Our Actions are declared in the ontology DSL and bound to workflows or MCP tools [§4.14.4]. The key difference: OTC *generates* tool implementations; we generate tool *metadata* (stubs, descriptions) while implementations exist separately. |
| 5 | **Interface** `builder.Interface<T>()` | No equivalent | Novel | OTC operates within a single T-Box at a time. There is no cross-cutting polymorphic shape system. OWL supports abstract classes and intersection types, but OTC does not use them for tool dispatch. |
| 6 | **Cross-Domain Link** `builder.CrossDomainLink()` | Cross-ontology references (OntoSyn → OntoSpecies → OntoMOPs) | Moderate | OTC uses multiple complementary ontologies (OntoSynthesis, OntoSpecies, OntoMOPs) linked through shared IRIs [§2, p.8]. Our cross-domain links formalize this with explicit declarations and extension points. OTC's approach is more implicit -- ontologies share a namespace and reference each other's classes. |
| 7 | **Precondition** `.Requires()` | Hard constraints (T-Box axioms) | Strong | OTC's hard constraints are "class hierarchy, domain and range typing, datatype restrictions, and any modelled cardinalities" [§6.1, p.18] -- checked deterministically at tool-call time. Our preconditions are expression-tree predicates. Both serve the same purpose: gate actions on semantic validity. OTC's constraint feedback loop (return error → agent retries) goes further than our metadata-only default. |
| 8 | **Postcondition** `.Modifies()`, `.CreatesLinked<T>()` | Tool return values + Turtle store mutations | Moderate | OTC tools mutate a persistent Turtle store and return results + validation feedback [§6.2, p.19]. Our postconditions are declarative metadata. OTC's approach is more operational (tools actually perform mutations); ours is more analytical (metadata enables staleness reasoning). |
| 9 | **Lifecycle** `obj.Lifecycle()` | JSON iteration plan with ordered steps | Weak | OTC's JSON task decomposition defines an ordered sequence of extraction steps [§6.1, p.17]. This is a process-level lifecycle (document → synthesis steps → grounding), not an entity-level state machine. Our Lifecycle is richer: it models entity state transitions with explicit triggers. |
| 10 | **Derivation Chain** `.Computed().DerivedFrom()` | No equivalent | Novel | OTC does not model property dependencies or staleness propagation. Derived entities (CBUs) are computed by downstream enrichment modules, not tracked as ontological metadata. |
| 11 | **Interface Action** `iface.Action("Search")` | No equivalent | Novel | OTC has no polymorphic action dispatch. Tools are bound to specific T-Box classes. |
| 12 | **Extension Point** `obj.AcceptsExternalLinks()` | No equivalent (cross-ontology linking is implicit) | Novel | OTC links across ontologies via shared IRIs and `owl:sameAs` assertions [§6.3]. There is no target-side declaration of acceptable incoming links. |

**Summary:** 4 strong, 2 moderate, 1 weak, 5 novel. The strong alignments cluster around the core compilation pipeline (Object Type, Property, Link, Action, Precondition). Our novel primitives (Interface, Derivation Chain, Interface Action, Extension Point) reflect engineering concerns specific to multi-domain .NET systems that OTC's single-knowledge-graph architecture does not face.

---

## 3. Alignment Analysis

### 3.1 The Compilation Pipeline

The deepest alignment between our systems is the compilation pipeline itself:

| Stage | OTC | Agentic.Ontology |
|-------|-----|------------------|
| **Input** | OWL T-Box + meta-prompts | `DomainOntology.Define()` method body |
| **Compiler** | LLM agent (preparation stage) | Roslyn incremental source generator |
| **Output** | Python MCP server + typed tools + JSON plan | `IOntologyQuery` service + descriptors + MCP stubs |
| **Validation** | Runtime (tool-call time) | Compile-time (35 diagnostics) + optional runtime |
| **Target** | RDF/Turtle knowledge graph | .NET DI container + domain persistence |

OTC: "Ontological specifications are compiled into executable tool interfaces that LLM-based agents must use to create and modify knowledge graph instances, enforcing semantic constraints during generation rather than through post-hoc validation" [Abstract].

Our spec: "A compile-time ontology maps domain types into a unified type graph... Agents plan against the ontology rather than flat tool lists, directly reducing the CMDP action space" [§4.14, Design Principle 5].

The architectural insight is identical. The implementation strategies differ in two key ways:

1. **Deterministic vs. generative compilation.** Our Roslyn generator produces deterministic output from the same input every time. OTC uses an LLM to generate tool implementations, which introduces variability but enables handling of soft constraints that formal axioms cannot express.

2. **Build-time vs. runtime validation.** Our 35 diagnostic codes catch errors at compile time (AONT001-AONT035). OTC catches errors only when tools are invoked at runtime. Our approach is safer for production systems; OTC's is more flexible for exploratory knowledge extraction.

### 3.2 Constraint Enforcement Model

Both systems enforce constraints at the point of agent interaction rather than post-hoc:

**OTC:** "The compilation layer treats the T-Box as a machine-readable contract. It specifies which classes, relations, attributes, and constraints are allowed. From this contract, the framework generates executable tool interfaces with explicitly specified inputs, outputs, and validation behaviour. These tools are the only way to create or modify structured instances, so constraints are checked and repaired during construction" [§3, p.9].

**Our system:** `Requires(p => p.Status == PositionStatus.Active)` gates action dispatch. `GetValidActions(objectType, knownProperties)` filters the action space to only semantically valid operations [§4.14.5, §4.14.12].

The key difference is OTC's **constraint feedback loop**: when a tool call violates a constraint, the tool returns a structured error and the agent retries. Our preconditions are metadata-only by default (`ActionDispatchOptions.EnforcePreconditions = true` is opt-in). OTC's feedback loop is central to their results -- ablating it drops synthesis-step F1 significantly [§4.2].

### 3.3 MCP as the Integration Layer

Both systems use MCP as the protocol for exposing ontology-aware tools to LLM agents:

**OTC:** "The Model Context Protocol (MCP) standardize[s] this interaction by providing a common interface for registering tools and exchanging typed inputs and outputs" [§1, p.5]. Generated MCP servers expose "each function as an MCP tool with an ontology-derived name and a typed argument schema" [§6.1, p.18].

**Our system:** `Agentic.Ontology.MCP` enriches progressive disclosure stubs with ontology metadata -- "including preconditions, lifecycle states, derivation chains, and extension points -- so agents discover typed action signatures with planning constraints rather than flat tool descriptions" [§4.14.15].

Both systems use MCP to bridge between symbolic ontological knowledge and LLM agent capabilities. The difference is granularity: OTC generates full tool *implementations* (Python functions with validation logic); we generate tool *descriptions* (metadata stubs that describe pre-existing tool implementations).

---

## 4. Gap Analysis

### 4.1 No Runtime Constraint Feedback Protocol

**Paper concept:** OTC tools return structured constraint violation messages: "If a violation is detected, for example a missing required field, a type mismatch, or an invalid unit, the tool returns an error with an explanation. The agent then retries with corrected inputs" [§6.2, p.19-20]. This feedback loop is critical -- ablating it causes "a substantial drop in synthesis-step F1" [§4.2].

**Our design:** Preconditions are metadata. When `EnforcePreconditions = true`, a precondition failure throws an exception or returns an error, but there is no structured protocol for communicating *what* failed and *how* to fix it. The agent receives a generic failure, not actionable guidance.

**Impact:** High. The paper's empirical results demonstrate that constraint feedback materially improves agent performance. Without structured feedback, agents cannot self-correct -- they must either abandon the action or retry blindly.

**Recommendation:** Add a `ConstraintViolation` response type to `IActionDispatcher` that includes: which precondition failed, the current property values, and suggested corrections. This turns our metadata-only preconditions into an interactive constraint enforcement system matching OTC's feedback loop.

### 4.2 No Generative/Soft Constraint Layer

**Paper concept:** OTC distinguishes hard constraints (formal axioms, deterministically enforced) from soft constraints (natural-language annotations in `rdfs:comment`, used to guide but not block). Soft constraints capture "operational definitions and heuristic decision rules that guide boundary setting and classification during extraction" [§6.1, p.18]. These are compiled into task-specific prompts, not tool validators.

**Our design:** We have `Requires()` (hard, predicate-based) and `Description()` (soft, free-text). But the `Description()` strings are not structured as constraint guidance -- they're documentation. There is no mechanism to express "prefer this behavior" vs. "require this behavior."

**Impact:** Medium. For our current use case (tool dispatch in a typed .NET system), hard constraints suffice. But as agents become more autonomous, soft constraints (preferences, heuristics, best practices) would improve decision quality without rigidly blocking actions.

**Recommendation:** Consider adding an optional `Guidance()` method to the action builder that expresses soft constraints separately from `Description()`:

```csharp
obj.Action("ExecuteTrade")
    .Requires(p => p.Status == PositionStatus.Active)   // hard: blocks if false
    .Guidance("Prefer executing during market hours")     // soft: advisory
    .Guidance("Verify strategy alignment before large trades"); // soft: advisory
```

### 4.3 No Tool Implementation Generation

**Paper concept:** OTC uses an LLM to generate the actual tool implementations (Python functions) from the T-Box and meta-prompts. The preparation agent "generates an ontology-aware Python script that supports the classes and properties needed for the extraction scenario" [§6.1, p.18]. This means the ontology *fully determines* the tool code.

**Our design:** Our source generator produces metadata descriptors and the `IOntologyQuery` service, but tool implementations (`BoundToWorkflow`, `BoundToTool`) are written separately by developers. The ontology maps to existing tools; it does not generate them.

**Impact:** Low. This is a deliberate architectural difference, not a gap. Our tools have complex business logic (execute trades, manage portfolios, ingest knowledge) that cannot be generated from ontological metadata alone. OTC's tools are simpler (create RDF triples, add properties, link instances) and are amenable to generation.

**Assessment:** Not applicable as a recommendation. However, the OTC approach could inspire *scaffolding* generation: the source generator could emit tool interface stubs with typed parameters, validation hooks, and documentation, even if the business logic must be hand-written.

---

## 5. Architectural Recommendations

### 5.1 Add Structured Constraint Feedback to Action Dispatch

**Priority: High** | **Effort: Medium** | **Breaking: No**

The paper's most impactful finding is that structured constraint feedback improves agent performance. Adapt this for our system:

```csharp
public sealed record ConstraintViolationResult
{
    public required string ActionName { get; init; }
    public required IReadOnlyList<ViolatedPrecondition> Violations { get; init; }
    public string? SuggestedCorrection { get; init; }
}

public sealed record ViolatedPrecondition
{
    public required string Expression { get; init; }
    public required string Description { get; init; }
    public required object? ActualValue { get; init; }
    public required object? RequiredValue { get; init; }
}
```

When `IActionDispatcher` enforces preconditions and a violation occurs, return `ConstraintViolationResult` instead of throwing. The agent can inspect the violations and take corrective action (modify the object, choose a different action, or escalate).

**OTC precedent:** "Each tool call returns both results and validation feedback. The feedback reports whether the requested update satisfies the ontology constraints" [§6.2, p.19].

### 5.2 Add Constraint Kind Taxonomy

**Priority: Medium** | **Effort: Low** | **Breaking: No**

Formalize the hard/soft distinction from the paper:

```csharp
public enum ConstraintKind
{
    Hard,       // Must be satisfied; blocks dispatch
    Soft,       // Advisory; included in agent context but does not block
    Structural  // Link existence, type compatibility (always hard)
}
```

Add `ConstraintKind` to `ActionPrecondition`. This enables `IOntologyQuery.GetValidActions()` to separately report hard failures (action blocked) vs. soft warnings (action allowed but not recommended).

### 5.3 Enrich MCP Tool Stubs with Constraint Metadata

**Priority: High** | **Effort: Low** | **Breaking: No**

OTC's MCP tools carry ontology-derived validation descriptions. Ensure our `Agentic.Ontology.MCP` progressive disclosure stubs include:

- Precondition expressions (human-readable) in tool descriptions
- Lifecycle state requirements ("requires Position in Active state")
- Postcondition summaries ("modifies Quantity, creates linked TradeOrder")

This is already partially described in §4.14.15 but should be made explicit in the MCP tool schema generation to match OTC's approach of "ontology-derived name and a typed argument schema" with "short usage instructions drawn from T-Box annotations" [§6.1, p.18].

---

## 6. Comparison with Nirenburg & Raskin Analysis

This analysis complements the [Nirenburg & Raskin theoretical grounding](./ontology-theoretical-grounding.md). The two sources address different aspects of our ontology layer:

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
| [§4.14.11] | Source generator pipeline (35 diagnostics) |
| [§4.14.12] | IOntologyQuery interface |
| [§4.14.15] | Basileus adoption and MCP integration |

### Converted Paper Files

All files in `docs/reference/ontology-to-tools-compilation/`:

- [main-paper.md](ontology-to-tools-compilation/main-paper.md) — Sections 1-6
- [supplementary.md](ontology-to-tools-compilation/supplementary.md) — Background, methods, prompts, traces
- [references.md](ontology-to-tools-compilation/references.md) — Bibliography

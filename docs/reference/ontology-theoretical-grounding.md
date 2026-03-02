# Ontology Theoretical Grounding: Nirenburg & Raskin Analysis

A formal analysis mapping the Strategos `Agentic.Ontology` layer against the theoretical framework of Nirenburg & Raskin's *Ontological Semantics* (MIT Press, 2004). This document examines where our design aligns with the theory, where it diverges, what concepts from the literature we are missing, and what concrete improvements are warranted.

**Scope:** All 12 core primitives from `platform-architecture.md` section 4.14.4, analyzed against Chapters 1, 5, 6, and 7 of the textbook.

**Conventions:** Textbook citations use the format `[Ch.N §X.Y, p.NNN]`. Our spec references use `[§4.14.X]`. Textbook concept names appear in SMALL-CAPS (e.g., OBJECT, EVENT, RELATION). Our type names appear in code font (e.g., `ObjectTypeDescriptor`).

**Key N&R terminology used in this document:**
- **TMR (Text Meaning Representation):** The structured output of semantic analysis in N&R's framework -- a formal representation of text meaning expressed using ontological concepts, their instances, and relations between them [Ch.6 §6.1].
- **Society of microtheories:** N&R's modular architecture where specialized knowledge modules (for temporal reasoning, spatial reasoning, modality, etc.) compose into a unified system. Each microtheory handles a distinct aspect of meaning [Preface].
- **Case roles:** Semantic labels that describe participants' relationships to an event -- e.g., AGENT (who performs), THEME (what is acted upon), PATIENT (who is affected), INSTRUMENT (by what means). N&R use these as typed slots on EVENT concepts [Ch.7 §7.1].
- **SEM-STRUC (Semantic Structure):** The portion of a lexicon entry that maps a natural language word to its ontological meaning -- specifying which concepts, properties, or property values must be instantiated in a TMR to represent that word's meaning [Ch.7 §7.3].

---

## 1. Executive Summary

The Strategos ontology layer is architecturally well-grounded in the traditions that Nirenburg & Raskin (N&R) describe. Our frame-based Object Types with typed Properties and Links correspond directly to N&R's concept frames with property-value pairs organized in an IS-A hierarchy. Our Precondition/Postcondition system parallels the textbook's PRECONDITION and EFFECT slots on EVENT concepts. Our Lifecycle state machines capture the temporal structure that N&R represent through process/state ontological categories.

Three areas of strong alignment stand out:

1. **Frame-based knowledge representation.** Our `builder.Object<T>()` with `Property()` and `HasMany<T>()` is a typed, compile-time analog of N&R's concept frames with RELATION and ATTRIBUTE slots [Ch.7 §7.1.1].
2. **Action preconditions and effects.** Our `.Requires()` / `.Modifies()` / `.CreatesLinked<T>()` DSL directly mirrors N&R's PRECONDITION and EFFECT slot facets on EVENT concepts [Ch.7 §7.1.5].
3. **Cross-domain composition.** Our `ComposedOntology` with cross-domain links and extension points solves the same problem N&R address with their "society of microtheories" architecture [Ch.1, Preface].

Five significant gaps were identified (three now addressed):

1. ~~**No IS-A hierarchy.**~~ **Addressed (T-020).** `IsA<TParent>()` adds optional IS-A relationships with parent validation, cycle detection, and subsumption queries.
2. ~~**No RELATION/ATTRIBUTE distinction.**~~ **Addressed (T-021).** `PropertyKind` enum (`Scalar`, `Reference`, `Computed`) is auto-inferred at graph build time.
3. **No Fact Database analog.** N&R's four-resource architecture (ontology, fact database, lexicon, onomasticon) separates type-level knowledge from instance-level remembered facts. We have no equivalent of the Fact Database within the ontology layer.
4. **No facet system.** N&R's properties carry multiple facets (VALUE, SEM, DEFAULT, RELAXABLE-TO, NOT) that express selectional restrictions and constraint relaxation. Our properties carry only a single value type. *Partially addressed by T-024 (hard/soft constraint distinction) and T-025 (structured constraint feedback).*
5. **No lexicon/semantic mapping layer.** N&R's lexicon bridges between natural language terms and ontological concepts. *Partially addressed by T-026 (constraint-enriched MCP tool descriptions).*

Recommendations are concrete and prioritized. The IS-A hierarchy gap (#1) is the most impactful: it would enable inheritance-based property propagation, polymorphic subsumption queries, and a more principled integration with our existing Interface system.

---

## 2. Concept Mapping Table

Alignment ratings: **Strong** = direct structural correspondence; **Moderate** = same intent, different mechanism; **Weak** = loose conceptual parallel; **Novel** = no textbook equivalent.

| # | Agentic.Ontology Primitive | N&R Equivalent | Alignment | Notes |
|---|---------------------------|----------------|-----------|-------|
| 1 | **Object Type** `builder.Object<T>()` | CONCEPT (OBJECT or EVENT frame) | Strong | Both represent typed entities as named collections of property-value pairs. N&R further divides concepts into OBJECT, EVENT, and PROPERTY subtrees [Ch.7 §7.1.1]. Our Object Types map to their OBJECT and EVENT categories; the `ObjectKind` discriminator (T-022) now distinguishes `Entity` from `Process`. |
| 2 | **Property** `obj.Property(x => x.Prop)` | ATTRIBUTE (literal/scalar filler) | Strong | N&R distinguish RELATIONs (concept-valued) from ATTRIBUTEs (literal/scalar-valued) [Ch.7 §7.1.1]. The `PropertyKind` discriminator (T-021) now auto-infers `Scalar`, `Reference`, or `Computed` at graph build time. N&R properties also carry a multi-facet system (SEM, DEFAULT, RELAXABLE-TO) that we do not fully model, though hard/soft constraint strength (T-024) addresses the most important aspect. |
| 3 | **Link** `obj.HasMany<T>()` | RELATION (concept-valued slot) | Strong | N&R RELATIONs are slots whose fillers are other concepts, with DOMAIN and RANGE constraints [Ch.7 §7.1.1]. Our Links are typed directional relationships with cardinality. The `.Inverse()` declaration (T-023) now supports bidirectional traversal, matching N&R's INVERSE slot. |
| 4 | **Action** `obj.Action("name")` | EVENT concept with case-role slots (AGENT, THEME, etc.) | Moderate | N&R model actions as EVENT concepts with case-role properties (AGENT, PATIENT, THEME, INSTRUMENT, etc.) [Ch.7 §7.1]. Our Actions are operations bound to Object Types with `Accepts<T>`/`Returns<T>` signatures. The N&R case-role system is richer: it explicitly types the participants, whereas our input/output types are opaque to the ontology. |
| 5 | **Interface** `builder.Interface<T>()` | No direct equivalent; closest is multiple inheritance in the IS-A hierarchy | Moderate | N&R's ontology allows multiple parents via IS-A [Ch.7 §7.1.2], enabling a concept to inherit from multiple branches. Our Interfaces serve a similar cross-cutting purpose. With IS-A hierarchy now implemented (T-020), Interfaces and IS-A serve complementary roles: IS-A for vertical hierarchy, Interfaces for horizontal cross-cutting shapes. |
| 6 | **Cross-Domain Link** `builder.CrossDomainLink()` | Inter-ontology references in the "society of microtheories" | Moderate | N&R describe a society of microtheories where specialized knowledge modules interconnect [Preface, Ch.1]. Our cross-domain links formalize this with explicit source/target resolution. N&R's approach is more implicit -- concepts from different knowledge areas share a single hierarchy rooted at ALL. |
| 7 | **Precondition** `.Requires(p => p.Status == Active)` | PRECONDITION facet on EVENT slots | Strong | N&R's EVENTs have PRECONDITION slots specifying conditions that must hold [Ch.7 §7.1.5, Ch.6 §6.2]. Our `Requires()` expressions are a compile-time, predicate-based formalization of the same concept. `ConstraintStrength` (T-024) now distinguishes `Hard` from `Soft` constraints, paralleling N&R's distinction between strict preconditions and abductively relaxable SEM facet constraints. `GetActionConstraintReport` (T-025) provides structured feedback with failure reasons and expected shapes. |
| 8 | **Postcondition** `.Modifies()`, `.CreatesLinked<T>()`, `.EmitsEvent<T>()` | EFFECT facet on EVENT slots | Strong | N&R's EVENTs have EFFECT slots declaring state changes [Ch.7 §7.1.5]. Our postconditions decompose into three subtypes (property modification, link creation, event emission) which is more granular than N&R's single EFFECT slot. This is a case where our engineering design improves on the theoretical model. |
| 9 | **Lifecycle** `obj.Lifecycle(p => p.Status, ...)` | PROCESS ontological category with temporal phases | Moderate | N&R model temporal progression through PROCESS concepts with temporal case roles (TIME, DURATION, PRECONDITION, EFFECT) and through the TMR's temporal ordering [Ch.6 §6.2]. Our Lifecycle is a finite state machine -- more constrained and more formally analyzable than N&R's open-ended temporal representation. This is a deliberate engineering trade-off: we sacrifice expressiveness for decidability. |
| 10 | **Derivation Chain** `.Computed().DerivedFrom()` | No direct equivalent | Novel | N&R do not model property dependency graphs or staleness propagation. Their ontology assumes properties are defined at concept-definition time and instantiated at processing time. Our derivation chains address an engineering concern (data freshness in a mutable agent environment) that the textbook's NLP-focused framework does not encounter. |
| 11 | **Interface Action** `iface.Action("Search")` | No direct equivalent | Novel | N&R have no mechanism for declaring actions at an abstract interface level and dispatching polymorphically. Their actions (EVENTs) are always concrete concepts in the hierarchy. Our Interface Actions are a software-engineering refinement without a theoretical precedent in the textbook. |
| 12 | **Extension Point** `obj.AcceptsExternalLinks()` | No direct equivalent; closest is RANGE constraints on RELATIONs | Weak | N&R's RANGE slots on RELATIONs constrain which concepts can fill a relation [Ch.7 §7.1.1], serving a similar gatekeeper function. But extension points are advisory and target-initiated, while N&R's RANGE constraints are definitional and source-initiated. |

**Summary (post-revision):** 6 strong alignments, 2 moderate, 1 weak, 3 novel. The implementations in T-020 through T-026 closed the hierarchy, property richness, link bidirectionality, and constraint expressiveness gaps. Remaining gaps center on the fact database and full lexicon/semantic mapping layer.

---

## 3. Alignment Analysis

### 3.1 Frame-Based Knowledge Representation

Our Object Types are structurally equivalent to N&R's concept frames. Both represent entities as named collections of property-value pairs:

| N&R Concept Frame | Agentic.Ontology |
|-------------------|------------------|
| `pay definition "..." agent sem human theme sem commodity` | `builder.Object<TradeOrder>(obj => { obj.Property(o => o.Side); obj.Property(o => o.Price); })` |

N&R: "Concepts are frames and properties are slots in these frames -- this is the standard interpretation of concepts and properties in all frame-based representation schemata" [Ch.7 §7.1.1, p.159]. Our builder DSL is a compile-time, expression-tree-based formalization of this same pattern. The key architectural choice is identical: entities are defined by their properties, not by their names.

### 3.2 Preconditions and Effects

Our Precondition/Postcondition system [§4.14.5] closely parallels N&R's PRECONDITION and EFFECT slots on EVENT concepts. N&R describe EVENTs with "preconditions that must hold for the event to occur and effects that describe the state of the world after the event" [Ch.7 §7.1.5]. Our DSL:

```csharp
obj.Action("ExecuteTrade")
    .Requires(p => p.Status == PositionStatus.Active)  // PRECONDITION
    .Modifies(p => p.Quantity)                          // EFFECT
    .CreatesLinked<TradeOrder>("Orders")                 // EFFECT
```

This is a direct formalization. Where we improve on the textbook is in the *decomposition* of effects: N&R's EFFECT is a single slot with free-form fillers, while our postconditions distinguish property modification (`Modifies`), link creation (`CreatesLinked`), and event emission (`EmitsEvent`). This granularity enables the cross-refinement interactions described in §4.14.14 (postconditions + derivation chains = automatic staleness hints), which N&R's coarser representation cannot support.

### 3.3 Compositional Architecture

N&R describe "a 'society' of microtheories" in the Preface and organize their knowledge architecture into four static resources: ontology, fact database, lexicon, and onomasticon [Ch.1, p.6-7]. Our multi-domain `ComposedOntology` with cross-domain links is an engineering analog:

| N&R Architecture | Our Architecture |
|-----------------|------------------|
| Multiple microtheories composed at runtime | Multiple `DomainOntology` subclasses composed at build time |
| Single ontology with branches for different domains | Separate domain assemblies with `CrossDomainLink` declarations |
| Fact DB links instances to ontological concepts | No equivalent (see Gap Analysis §4.1) |

The "society of microtheories" concept validates our multi-domain composition model. N&R's microtheories cover different aspects of the same world (temporal reasoning, spatial reasoning, modality, etc.) while our domains cover different business areas (Trading, Knowledge, StyleEngine). The composability principle is the same.

### 3.4 Ontology as Agent World Model

N&R's foundational claim is that "the ontology provides a metalanguage for describing meaning" and serves as the backbone for intelligent agent processing [Ch.1, p.6]. This aligns precisely with our Design Principle 5 (Semantic Type Safety): "A compile-time ontology maps domain types into a unified type graph... Agents plan against the ontology rather than flat tool lists" [§4.14]. Both architectures use the ontology to constrain the agent's action space -- N&R through selectional restrictions on EVENTs, and we through preconditions and lifecycle state filtering via `IOntologyQuery`.

---

## 4. Gap Analysis

### 4.1 No IS-A Inheritance Hierarchy

**Textbook concept:** N&R's ontology is fundamentally organized as an IS-A hierarchy rooted at a single concept ALL, which branches into OBJECT, EVENT, and PROPERTY [Ch.7 §7.1.1, p.158-160]. Every concept except ALL has at least one IS-A parent. Properties are inherited from parents to children with override semantics: "All slots that have not been overtly specified in X, with their facets and fillers, but are specified in Y, are inherited into X" [Ch.7 §7.1.2, p.168].

**Our design:** Object Types are registered flatly with `builder.Object<T>()`. There is no parent-child relationship between Object Types. Interfaces provide cross-cutting shared shapes but do not carry the inheritance semantics of IS-A (no property propagation, no subsumption).

**Impact:** Without IS-A, we cannot express that a `TradeOrder` *is a* `FinancialTransaction`, or that all financial transactions share certain properties (e.g., timestamp, counterparty, settlement status). Each Object Type must declare all its properties explicitly, leading to repetition across related types. More critically, agents cannot reason at higher abstraction levels: "find all financial transactions" requires knowing every concrete type, rather than querying a single parent concept.

**Recommendation:** See §6.1.

### 4.2 No RELATION/ATTRIBUTE Distinction

**Textbook concept:** N&R make a fundamental distinction between two kinds of properties [Ch.7 §7.1.1, p.162-163]:
- **RELATIONs**: Slots whose fillers are references to other concepts. They have DOMAIN, RANGE (concept names), and INVERSE slots. Example: `agent sem human` -- the AGENT relation links an EVENT to an OBJECT.
- **ATTRIBUTEs**: Slots whose fillers are literal values (numbers, strings, symbolic ranges). They have DOMAIN and RANGE (literal/numerical values). Example: `color value red`.

This distinction is not just taxonomic -- it determines the *kind of reasoning* applicable. RELATIONs enable graph traversal and subsumption checking. ATTRIBUTEs enable scalar comparison and range matching.

**Our design:** `obj.Property(x => x.Prop)` treats all properties uniformly. The C# type system implicitly distinguishes reference-typed properties from value-typed ones, but this distinction is not surfaced in the ontology metadata. The `PropertyDescriptor` has no field indicating whether the property's value is another Object Type or a scalar.

**Impact:** The ontology layer cannot distinguish "this property points to another entity" from "this property holds a scalar value." This limits the expressiveness of queries like `GetLinks()` (which only returns declared `HasMany`/`HasOne` relationships, not implicit concept-valued properties) and makes it harder for agents to reason about the semantic structure of objects.

**Recommendation:** See §6.2.

### 4.3 No Fact Database / Instance Layer

**Textbook concept:** N&R's Fact Database stores remembered instances of ontological concepts -- "if the ontology has a concept for CITY, the Fact DB may contain entries for London, Paris or Rome" [Ch.7 §7.1, p.155]. The Fact DB is a first-class knowledge source alongside the ontology, with its own representation format (instances carry INSTANCE-OF slots pointing back to concepts) and temporal truth maintenance (TIME-RANGE facets for tracking when facts are valid).

**Our design:** The ontology layer explicitly does not own storage: "Ontology maps types, does not own storage" [§4.14.2]. Domain persistence (Marten, pgvector) is handled outside the ontology. There is no ontology-aware mechanism for representing or querying instances.

**Impact:** This is a *deliberate* architectural decision, not an oversight. N&R's fact database exists because their system needs to remember and reason about specific instances encountered during text processing. Our system delegates instance management to domain-specific persistence layers, which is appropriate for a software platform where each domain has different storage requirements (event sourcing for trades, document store for knowledge, etc.).

**Assessment:** This gap is well-justified. However, the ontology layer could benefit from a lightweight instance metadata capability -- not full storage, but a way to annotate Object Types with "instances of this type are stored in {Marten collection X}" or "instances are queryable via {this service}." This would strengthen the agent's ability to navigate from ontological type knowledge to actual data.

### 4.4 No Facet System for Constraint Relaxation

**Textbook concept:** N&R's properties carry multiple facets [Ch.7 §7.1.1, p.164-167]:
- **VALUE**: Actual filled value
- **SEM**: Selectional restriction (what *can* fill this slot)
- **DEFAULT**: Most expected filler
- **RELAXABLE-TO**: How far constraints can be relaxed (e.g., for metaphor)
- **NOT**: Explicitly excluded fillers

This facet system enables graceful degradation: "The program first attempts to match on DEFAULT, then SEM, then RELAXABLE-TO" [Ch.7 §7.1.1, p.167]. This graduated matching is crucial for N&R's NLP processing where input text may not perfectly match ontological expectations.

**Our design:** Properties have a single CLR type and optional `Required()` / `Computed()` markers. There is no mechanism for expressing selectional restrictions, defaults, or relaxation boundaries.

**Impact:** For NLP-oriented ontologies, this gap would be critical. For our software engineering use case, the impact is lower -- our agents deal with typed data structures, not ambiguous natural language. However, a simplified form of the facet system could improve agent reasoning: knowing that a Position's `Quantity` has a SEM constraint of `> 0` (which we partially capture via `Requires(p => p.Quantity > 0)` on actions) or that a `Symbol` has a DEFAULT value of `"UNKNOWN"` could help agents make better planning decisions.

**Assessment:** Low priority for the current system. The Precondition system [§4.14.5] already captures the most important constraint semantics. A future enhancement could add optional value constraints to `PropertyDescriptor` (min/max ranges, allowed values) without implementing the full facet system.

### 4.5 No Lexicon / Semantic Mapping Layer

**Textbook concept:** The lexicon maps natural language words to ontological concepts via SEM-STRUC (semantic structure) entries: "the ontological semantic lexicon specifies what concept, concepts, property or properties of concepts defined in the ontology must be instantiated in the TMR to account for the meaning of a particular lexical unit" [Ch.7 §7.1, p.155]. This is the bridge between human language and machine knowledge.

**Our design:** Object Types, Properties, and Actions have `Description` fields (free-text strings) but no structured mapping between agent-facing names/descriptions and ontological concepts. The `Agentic.Ontology.MCP` package provides progressive disclosure descriptions, but these are string-based, not semantically structured.

**Impact:** When an agent reads a tool description like "Execute a trade against this position," it must parse the natural language to understand that this maps to the `ExecuteTrade` action on a `Position` Object Type. N&R's lexicon would provide a structured SEM-STRUC mapping: `execute-trade → ExecuteTrade.Action(AGENT: agent-instance, THEME: Position-instance)`. Without this, the burden of semantic interpretation falls entirely on the LLM's language understanding.

**Assessment:** Medium priority. The current system works because LLMs are good at natural language understanding. But as the ontology grows, a structured mapping between tool descriptions and ontological concepts would improve precision and reduce the token cost of tool discovery. This could be implemented as a `ToolDescription` → `ActionDescriptor` mapping layer, extending the existing progressive disclosure system.

---

## 5. Terminology Review

| Our Term | N&R Term | Assessment |
|----------|----------|------------|
| **Object Type** | CONCEPT (specifically OBJECT or EVENT) | Acceptable. N&R use "concept" for the general category and OBJECT/EVENT for the two main branches. Our "Object Type" is unambiguous in a software context. The risk is that "Object Type" obscures the EVENT branch -- we have no way to distinguish object-like vs. event-like types in the ontology metadata. |
| **Property** | ATTRIBUTE or RELATION (depending on filler type) | Acceptable but imprecise. N&R's distinction between ATTRIBUTE (scalar-valued) and RELATION (concept-valued) carries semantic weight. Our blanket "Property" term loses this distinction. Consider adding a `PropertyKind` enum (see §6.2). |
| **Link** | RELATION (binary, concept-valued) | Good. Our "Link" corresponds precisely to N&R's binary RELATIONs with concept fillers. The term "Link" is more intuitive in a software context than "Relation." |
| **Action** | EVENT (with case-role slots) | Acceptable divergence. N&R use "event" because their ontology models real-world occurrences. Our "Action" emphasizes agency and invocability, which is appropriate for a tool-oriented system. The terminology shift correctly reflects our design intent: actions are things agents *do*, not just things that *happen*. |
| **Interface** | (No equivalent; closest: abstract concept in IS-A with multiple inheritance) | Novel term, well-chosen. N&R's IS-A hierarchy handles the polymorphism that our Interfaces provide, but "Interface" is the standard software engineering term. |
| **Precondition** | PRECONDITION | Direct match. N&R use the same term [Ch.7 §7.1.5]. |
| **Postcondition** | EFFECT | Minor divergence. N&R call these "effects." Our "postcondition" is the standard formal methods term. Both are widely understood. |
| **Lifecycle** | PROCESS / temporal-ordering in TMR | Acceptable. N&R describe temporal progression through PROCESS concepts and TMR temporal relations. Our "Lifecycle" narrows this to a finite state machine, which is accurate for our constrained model. |
| **Derivation Chain** | (No equivalent) | Novel term. |
| **Cross-Domain Link** | (Inter-microtheory reference) | Novel term. N&R's society of microtheories uses implicit cross-references through a shared hierarchy. Our explicit "Cross-Domain Link" is more formal. |
| **Extension Point** | (No equivalent; loosely: RANGE constraints) | Novel term. |
| **Domain** / **DomainOntology** | Microtheory | Good correspondence. N&R's "microtheory" is a specialized knowledge module; our "domain" is a bounded context with its own ontology definition. The terms serve the same architectural purpose. |
| **ComposedOntology** | "Society of microtheories" / full ontology | Good. Our composition at build time mirrors N&R's runtime integration of microtheories. |

**Key terminology recommendation:** ~~Consider adding an `ObjectKind` discriminator~~ **Implemented (T-022).** `ObjectKind` (`Entity` vs `Process`) is now available on `ObjectTypeDescriptor`, distinguishing object-like types from event/process-like types. This aligns with N&R's fundamental OBJECT/EVENT split [Ch.7 §7.1.1]. Additionally, `PropertyKind` (T-021) addresses the terminology gap around ATTRIBUTE vs RELATION.

---

## 6. Architectural Recommendations

### 6.1 Add Optional IS-A Hierarchy Support

**Priority: High** | **Effort: Medium** | **Breaking: No** | **Status: Implemented (T-020)**

N&R's most fundamental architectural principle is the IS-A hierarchy: "The inheritance hierarchy, which is implemented using IS-A and SUBCLASSES slots, is the backbone of the ontology" [Ch.7 §7.1.2, p.168].

**Proposed DSL addition:**

```csharp
builder.Object<TradeOrder>(obj =>
{
    obj.IsA<FinancialTransaction>();  // NEW: declares parent type
    obj.Key(o => o.OrderId);
    // Properties unique to TradeOrder...
});

builder.Object<FinancialTransaction>(obj =>
{
    // Shared properties inherited by all financial transactions
    obj.Property(t => t.Timestamp);
    obj.Property(t => t.Counterparty);
    obj.Property(t => t.SettlementStatus);
});
```

**Behavior:**
- `IsA<T>()` registers a parent-child relationship in the ontology graph
- The source generator validates that the parent type is also registered
- `IOntologyQuery.GetObjectTypes()` gains an `includeSubtypes: bool` parameter
- Agent queries can reason at higher abstraction levels: "find all FinancialTransactions" returns TradeOrders and any other subtypes
- Property inheritance is metadata-only (the C# types are unchanged; the ontology records which properties are inherited vs. declared)

**N&R precedent:** "When two concepts, X and Y, are linked via an IS-A relation, then X inherits slots from Y" [Ch.7 §7.1.2, p.169]. Inheritance rules: (1) all non-overridden properties inherit from parent, (2) locally declared properties take precedence, (3) NOTHING blocks inheritance on a specific property.

**Interaction with Interfaces:** IS-A and Interfaces serve complementary purposes. IS-A represents "is a kind of" (vertical hierarchy), while Interfaces represent "has the shape of" (horizontal cross-cutting). Both are needed. N&R achieve both through multiple inheritance in a single hierarchy; our two-mechanism approach is cleaner from a software engineering perspective.

> **Implementation note:** `IsA<TParent>()` was added to `IObjectTypeBuilder` in T-020. `OntologyGraphBuilder` validates parent existence and detects IS-A cycles. `ObjectTypeDescriptor` carries `ParentType` and `ParentTypeName`. `IOntologyQuery.GetObjectTypes()` accepts `includeSubtypes: bool` for subsumption queries. `OntologyGraph.GetSubtypes()` enables downward traversal. See `IsAHierarchyTests.cs` for 10 tests covering registration, validation, transitive chains, and subtype queries.

### 6.2 Add Property Kind Discriminator

**Priority: Medium** | **Effort: Low** | **Breaking: No** | **Status: Implemented (T-021)**

N&R's RELATION/ATTRIBUTE distinction [Ch.7 §7.1.1] carries semantic information that aids reasoning. Add a `PropertyKind` to `PropertyDescriptor`:

```csharp
public enum PropertyKind
{
    Scalar,      // ATTRIBUTE: value is a literal, number, or enum
    Reference,   // RELATION: value references another Object Type
    Computed     // Our extension: value derived from other properties
}
```

The source generator can infer `PropertyKind` from the C# type: properties whose type is a registered Object Type (or collection thereof) are `Reference`; others are `Scalar`; properties with `.Computed()` are `Computed`.

**Benefit:** Agents can distinguish "properties I can filter on" (Scalar) from "properties that link to other entities" (Reference) without inspecting the C# type system. This improves the quality of `IOntologyQuery` responses and enables N&R-style distinction between structural navigation (follow References) and value comparison (filter on Scalars).

> **Implementation note:** `PropertyKind` enum (`Scalar`, `Reference`, `Computed`) was added to `PropertyDescriptor` in T-021. The kind is auto-inferred at graph build time by `OntologyGraphBuilder.InferPropertyKinds()`: property CLR type matching a registered Object Type → `Reference`, `.Computed()` → `Computed`, else → `Scalar`. See `PropertyKindTests.cs` for 5 tests.

### 6.3 Add Object Kind Discriminator (Entity vs. Process)

**Priority: Medium** | **Effort: Low** | **Breaking: No** | **Status: Implemented (T-022)**

N&R's top-level split between OBJECT and EVENT [Ch.7 §7.1.1] reflects a fundamental ontological distinction: objects persist through time; events occur and complete. Add an optional `ObjectKind` to the builder:

```csharp
builder.Object<Position>(obj =>
{
    obj.Kind(ObjectKind.Entity);  // Persists through time, has lifecycle
    // ...
});

builder.Object<TradeExecution>(obj =>
{
    obj.Kind(ObjectKind.Process);  // Occurs and completes, has temporal extent
    // ...
});
```

**Benefit:** Agents can reason differently about entities (query current state, modify properties) vs. processes (check completion, trace temporal ordering). This aligns with N&R's claim that "the first difference among the concepts is that of 'free-standing' versus 'bound' concepts" [Ch.7 §7.1.1, p.160].

> **Implementation note:** `ObjectKind` enum (`Entity`, `Process`) was added in T-022. `IObjectTypeBuilder.Kind(ObjectKind)` sets the discriminator (defaults to `Entity`). `ObjectTypeDescriptor.Kind` exposes it. See `ObjectKindTests.cs` for 3 tests.

### 6.4 Add INVERSE to Link Declarations

**Priority: Medium** | **Effort: Medium** | **Breaking: No** | **Status: Implemented (T-023)**

N&R's RELATIONs include an INVERSE slot: "the inverse of the relation PART-OF is the relation HAS-PARTS" [Ch.7 §7.1.1, p.171]. Our Links are unidirectional. While Extension Points [§4.14.9] partially address bidirectional awareness, they are advisory and target-initiated. Explicit inverse declarations would improve graph navigability:

```csharp
builder.Object<Position>(obj =>
{
    obj.HasMany<TradeOrder>("Orders")
        .Inverse("Position");  // NEW: declares the inverse link name
});
```

**Benefit:** `IOntologyQuery` could offer `GetInverseLinks(objectType)` enabling agents to traverse relationships in both directions. Currently, an agent at a `TradeOrder` has no ontology-supported way to navigate back to its parent `Position` unless the TradeOrder explicitly declares a `HasOne<Position>` link.

> **Implementation note:** `ILinkBuilder` with `.Inverse(string)` was added in T-023. `LinkDescriptor.InverseLinkName` stores the inverse. `OntologyGraphBuilder.ValidateInverseLinks()` validates symmetric declarations (if A→B declares inverse "X", then B must have link "X" with inverse pointing back). `IOntologyQuery.GetInverseLinks()` enables bidirectional traversal. See `InverseLinkTests.cs` for 7 tests.

### 6.5 Consider Lightweight Instance Metadata

**Priority: Low** | **Effort: Low** | **Breaking: No**

While we correctly do not own instance storage, adding metadata about *where* instances live would bridge the gap between ontological type knowledge and runtime data access:

```csharp
builder.Object<Position>(obj =>
{
    obj.InstanceStore("marten", "positions");  // NEW: metadata only
    // ...
});
```

**Benefit:** Agents could use `IOntologyQuery` to discover not just what types exist and what actions are available, but also how to access actual instances. This bridges the gap between N&R's ontology (type definitions) and Fact Database (instance storage) in a minimally invasive way.

### 6.6 Add Case-Role Semantics to Action Parameters

**Priority: Low** | **Effort: Medium** | **Breaking: No**

N&R's EVENTs use case roles (AGENT, THEME, PATIENT, INSTRUMENT, etc.) to semantically type their participants [Ch.7 §7.1]. Our Actions use opaque `Accepts<T>`/`Returns<T>` types. Adding optional case-role annotations would improve agent reasoning:

```csharp
obj.Action("ExecuteTrade")
    .Accepts<TradeExecutionRequest>(input =>
    {
        input.Agent(r => r.TraderId);     // Who initiates
        input.Theme(r => r.Position);      // What is acted upon
        input.Instrument(r => r.Strategy); // How/with what
    })
    .Returns<TradeExecutionResult>();
```

**Benefit:** Agents could reason about action semantics at a higher level: "I need an action where the THEME is a Position" rather than parsing description strings. This directly implements N&R's case-role system [Ch.7 §7.1] in a typed, compile-time fashion.

**Assessment:** Low priority because LLMs already excel at inferring participant roles from natural language descriptions. Implement only if agent planning precision becomes a bottleneck.

---

## 7. References

### Textbook Citations

| Citation | Content | Relevance |
|----------|---------|-----------|
| [Ch.1, p.6-7] | Architecture of ontological semantics: ontology, fact database, lexicon, onomasticon | Four-resource architecture, gap analysis §4.3 |
| [Ch.1, p.10-11] | Intelligent agent model with static and dynamic knowledge sources | Agent world model alignment §3.4 |
| [Ch.5 §5.1, p.116-118] | Ontology and metaphysics: ontological categories, realism vs. nominalism | Philosophical grounding for our category system |
| [Ch.5 §5.2, p.121-130] | Formal ontology: mereology, inheritance, formal basis | IS-A hierarchy recommendation §6.1 |
| [Ch.6 §6.1, p.131-134] | TMR structure: propositions, discourse relations, style, reference | Meaning representation concepts |
| [Ch.6 §6.2, p.135-150] | Extended TMR: modality, temporal ordering, causal chains | Lifecycle and temporal representation alignment §3 |
| [Ch.7 §7.1, p.155-158] | Ontology overview: frames, properties, world model | Frame-based representation alignment §3.1 |
| [Ch.7 §7.1.1, p.158-170] | BNF syntax and semantics: CONCEPT, OBJECT-OR-EVENT, PROPERTY, RELATION, ATTRIBUTE, FACET | Core mapping table §2, property gap §4.2, facet gap §4.4 |
| [Ch.7 §7.1.2, p.168-175] | Inheritance: IS-A hierarchy, property propagation, NOTHING blocker | IS-A hierarchy gap §4.1, recommendation §6.1 |
| [Ch.7 §7.1.5, p.185-190] | Complex events: scripts, PRECONDITION, EFFECT | Precondition/postcondition alignment §3.2 |
| [Ch.7 §7.2, p.190-195] | Fact Database: instances, INSTANCE-OF, TIME-RANGE | Fact database gap §4.3 |
| [Ch.7 §7.3, p.195-200] | Lexicon: SEM-STRUC, syntactic-semantic mapping | Lexicon gap §4.5 |
| [Preface, p.2-3] | Society of microtheories architecture | Cross-domain composition alignment §3.3 |

### Platform Architecture Citations

| Citation | Content |
|----------|---------|
| [§4.14.1] | Problem statement: domain silos, flat tool lists, unconstrained action space |
| [§4.14.2] | Palantir concept mapping table |
| [§4.14.4] | Core primitives: 12 concepts in the DSL |
| [§4.14.5] | Action preconditions and postconditions |
| [§4.14.6] | Lifecycle state machines |
| [§4.14.7] | Derivation chains |
| [§4.14.8] | Interface-level actions |
| [§4.14.9] | Extension points |
| [§4.14.10] | Domain definition examples |
| [§4.14.11] | Source generator pipeline |
| [§4.14.12] | IOntologyQuery interface |
| [§4.14.14] | Cross-refinement interactions |

### Converted Textbook Chapters

All chapter files are located in `docs/reference/ontological-semantics/`:

- [00-preface.md](ontological-semantics/00-preface.md) -- Society of microtheories
- [01-introduction.md](ontological-semantics/01-introduction.md) -- Agent model, four knowledge sources
- [05-formal-ontology.md](ontological-semantics/05-formal-ontology.md) -- IS-A hierarchy, categories, properties
- [06-meaning-representation.md](ontological-semantics/06-meaning-representation.md) -- TMR, preconditions, temporal structure
- [07-static-knowledge-sources.md](ontological-semantics/07-static-knowledge-sources.md) -- Ontology BNF, Fact DB, lexicon

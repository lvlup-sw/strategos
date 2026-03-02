# Implementation Plan: Ontology Layer

## Source Design
Link: `docs/designs/2026-02-24-ontology-layer.md`

## Scope
**Target:** Sections 4-11 — all three Strategos packages (Strategos.Ontology, Strategos.Ontology.Generators, Strategos.Ontology.MCP)
**Excluded:**
- Section 10.1 (Marten projection implementations) — Basileus consumer-side
- Section 11.3 (OntologyMetricsView) — Marten projection, Basileus consumer-side
- Section 12 (Platform Integration) — Basileus-specific: Phronesis ThinkStep, Execution Profiles, ControlPlane hosting
- Section 13 (Basileus Adoption Strategy) — consumer-side changes
- Section 14 (Illustrative Example) — reference only
- Section 15 (Future Considerations) — deferred by design

## Summary
- Total tasks: 63
- Parallel groups: 6 teams across 4 phases
- Estimated test count: ~130
- Design coverage: 8 of 8 in-scope sections covered

## Spec Traceability

### Scope Declaration

**Target:** Full design (in-scope sections)
**Excluded:** Consumer-side implementations (see above)

### Traceability Matrix

| Design Section | Key Requirements | Task ID(s) | Status |
|----------------|-----------------|------------|--------|
| **4. Package Architecture** | 3 NuGet packages, dependency graph | 001-003 | Covered |
| **5.1 Runtime Model — Builder** | EF Core pattern, startup sequence, fail-fast | 024-025, 040-042 | Covered |
| **5.2 Runtime Model — OntologyGraph** | Immutable registry, fast lookups, traversal | 043-045 | Covered |
| **6.1 DSL — DomainOntology** | Abstract base, DomainName, Define() | 025 | Covered |
| **6.2 DSL — Object Types** | Object<T>(), Key(), Property(), Required(), Computed() | 004, 010-011, 015, 018, 022 | Covered |
| **6.3 DSL — Links** | HasOne, HasMany, ManyToMany, edge types | 005, 015, 022 | Covered |
| **6.4 DSL — Actions** | Action chain, BoundToWorkflow, BoundToTool, unbound | 006, 012, 019 | Covered |
| **6.5 DSL — Events** | Event<T>(), MaterializesLink, UpdatesProperty, Severity | 007, 013, 020 | Covered |
| **6.6 DSL — Interfaces** | Interface<T>(), property mapping, Implements<T>() | 008, 014, 021, 022 | Covered |
| **6.7 DSL — Cross-Domain Links** | CrossDomainLink(), From/ToExternal, string refs | 016, 023 | Covered |
| **6.8 DSL — Workflow Integration** | Consumes<T>(), Produces<T>() extension methods | 059-060 | Covered |
| **7.1 Object Set — Core** | ObjectSet<T>, Where, TraverseLink, OfInterface | 033-034 | Covered |
| **7.2 Object Set — Inclusion** | ObjectSetInclusion flags, progressive loading | 031, 035 | Covered |
| **7.3 Object Set — Provider** | IObjectSetProvider abstraction | 026 | Covered |
| **7.4 Object Set — Materialization** | ExecuteAsync, StreamAsync, ApplyAsync, EventsAsync | 036-037 | Covered |
| **8.1 Source Generator — Role** | DiagnosticAnalyzer, zero runtime code | 046 | Covered |
| **8.2 Source Generator — Diagnostics** | ONTO001-ONTO010 catalog | 047-053 | Covered |
| **9.1 MCP — Design Principles** | Progressive disclosure, problem-first | 054 | Covered |
| **9.2 MCP — Tool Catalog** | ontology_query, ontology_action, ontology_explore | 055-057 | Covered |
| **9.3 MCP — Stub Generation** | Enriched .pyi stubs from OntologyGraph | 058 | Covered |
| **10.2 Event — Temporal Queries** | IEventStreamProvider contract | 027 | Covered |
| **10.3 Event — Causality** | OntologyEvent, EventQuery | 032 | Covered |
| **11.1 Telemetry — Context** | OntologyTelemetryContext record | 038 | Covered |
| **11.2 Telemetry — Metrics** | IOntologyMetrics interface | 038 | Covered |
| **DI Registration** | AddOntology, AddOntologyMcpTools | 061-062 | Covered |
| **Integration Validation** | Full registration + graph freeze + validation | 063 | Covered |

## Task Breakdown

---

### Phase 0: Project Scaffolding

> Sequential — creates project structure on main branch before teams diverge.

---

### Task 001: Create Strategos.Ontology project and test project
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write test: `Build_StrategosOntologyProject_Compiles`
   - File: `src/Strategos.Ontology.Tests/ProjectSetupTests.cs`
   - Expected failure: Project does not exist
   - Run: `dotnet test` — MUST FAIL

2. [GREEN] Create project structure
   - Files:
     - `src/Strategos.Ontology/Strategos.Ontology.csproj` (net10.0, PackageId, InternalsVisibleTo)
     - `src/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj` (TUnit, NSubstitute)
     - `src/Strategos.Ontology.Tests/GlobalUsings.cs`
   - Add both to `src/strategos.sln`
   - Add `Microsoft.Extensions.DependencyInjection.Abstractions` to Ontology csproj
   - Run: `dotnet test` — MUST PASS

**Verification:**
- [ ] Both projects compile and are in the solution
- [ ] Test project references Strategos.Ontology

**Dependencies:** None
**Parallelizable:** No (Phase 0 prerequisite)
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 002: Create Strategos.Ontology.Generators project and test project
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write test: `Build_StrategosOntologyGeneratorsProject_Compiles`
   - File: `src/Strategos.Ontology.Generators.Tests/ProjectSetupTests.cs`
   - Expected failure: Project does not exist
   - Run: `dotnet test` — MUST FAIL

2. [GREEN] Create project structure
   - Files:
     - `src/Strategos.Ontology.Generators/Strategos.Ontology.Generators.csproj` (netstandard2.0, IsRoslynComponent, DiagnosticAnalyzer pack config)
     - `src/Strategos.Ontology.Generators.Tests/Strategos.Ontology.Generators.Tests.csproj` (TUnit, references Generators as Analyzer + ReferenceOutputAssembly, references Strategos.Ontology)
   - Add both to `src/strategos.sln`
   - Run: `dotnet test` — MUST PASS

**Dependencies:** 001
**Parallelizable:** No
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 003: Create Strategos.Ontology.MCP project and test project
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write test: `Build_StrategosOntologyMcpProject_Compiles`
   - File: `src/Strategos.Ontology.MCP.Tests/ProjectSetupTests.cs`
   - Expected failure: Project does not exist
   - Run: `dotnet test` — MUST FAIL

2. [GREEN] Create project structure
   - Files:
     - `src/Strategos.Ontology.MCP/Strategos.Ontology.MCP.csproj` (net10.0, references Strategos.Ontology)
     - `src/Strategos.Ontology.MCP.Tests/Strategos.Ontology.MCP.Tests.csproj` (TUnit, NSubstitute)
   - Add both to `src/strategos.sln`
   - Run: `dotnet test` — MUST PASS

**Dependencies:** 001
**Parallelizable:** No
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Phase 1A: Language Layer — Descriptors + Builders

> Team 1A — runs in parallel with Team 1B after Phase 0 completes.
> Branch: `feat/ontology-language-layer`

---

### Task 004: PropertyDescriptor record
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `PropertyDescriptor_Create_HasNameAndType`: Verify Name, PropertyType are set
   - `PropertyDescriptor_Required_DefaultsFalse`: Verify IsRequired defaults false
   - `PropertyDescriptor_Computed_DefaultsFalse`: Verify IsComputed defaults false
   - File: `src/Strategos.Ontology.Tests/Descriptors/PropertyDescriptorTests.cs`
   - Expected failure: Type does not exist
   - Run: `dotnet test` — MUST FAIL

2. [GREEN] Implement PropertyDescriptor
   - File: `src/Strategos.Ontology/Descriptors/PropertyDescriptor.cs`
   - Sealed record with `Name`, `PropertyType`, `IsRequired`, `IsComputed`, `ExpressionPath`

**Dependencies:** 001
**Parallelizable:** Yes (within Team 1A: sequential)
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 005: LinkDescriptor and LinkCardinality enum
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `LinkCardinality_HasExpectedValues`: Verify OneToOne, OneToMany, ManyToMany
   - `LinkDescriptor_Create_HasNameCardinalityAndTargetType`: Verify construction
   - `LinkDescriptor_EdgeProperties_DefaultsEmpty`: Verify empty edge collection
   - File: `src/Strategos.Ontology.Tests/Descriptors/LinkDescriptorTests.cs`
   - Expected failure: Types do not exist
   - Run: `dotnet test` — MUST FAIL

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Descriptors/LinkCardinality.cs`
   - File: `src/Strategos.Ontology/Descriptors/LinkDescriptor.cs`
   - Sealed record: `Name`, `TargetTypeName`, `Cardinality`, `EdgeProperties` (list of PropertyDescriptor)

**Dependencies:** 004
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 006: ActionDescriptor and ActionBindingType enum
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ActionBindingType_HasExpectedValues`: Workflow, Tool, Unbound
   - `ActionDescriptor_Create_HasNameAndDescription`: Verify construction
   - `ActionDescriptor_BoundToWorkflow_SetsBindingType`: Verify workflow binding
   - `ActionDescriptor_BoundToTool_SetsToolReference`: Verify tool name + method
   - `ActionDescriptor_Unbound_DefaultBinding`: Verify default is Unbound
   - File: `src/Strategos.Ontology.Tests/Descriptors/ActionDescriptorTests.cs`
   - Expected failure: Types do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Descriptors/ActionBindingType.cs`
   - File: `src/Strategos.Ontology/Descriptors/ActionDescriptor.cs`
   - Sealed record: `Name`, `Description`, `AcceptsType`, `ReturnsType`, `BindingType`, `BoundWorkflowName`, `BoundToolName`, `BoundToolMethod`

**Dependencies:** 004
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 007: EventDescriptor and EventSeverity enum
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `EventSeverity_HasExpectedValues`: Info, Warning, Alert, Critical
   - `EventDescriptor_Create_HasEventTypeAndDescription`
   - `EventDescriptor_MaterializesLink_RecordsLinkName`
   - `EventDescriptor_UpdatesProperty_RecordsPropertyName`
   - `EventDescriptor_Severity_DefaultsInfo`
   - File: `src/Strategos.Ontology.Tests/Descriptors/EventDescriptorTests.cs`
   - Expected failure: Types do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Descriptors/EventSeverity.cs`
   - File: `src/Strategos.Ontology/Descriptors/EventDescriptor.cs`
   - Sealed record: `EventType`, `Description`, `Severity`, `MaterializedLinks` (list), `UpdatedProperties` (list)

**Dependencies:** 004
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 008: InterfaceDescriptor record
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `InterfaceDescriptor_Create_HasNameAndInterfaceType`
   - `InterfaceDescriptor_Properties_DefaultsEmpty`
   - File: `src/Strategos.Ontology.Tests/Descriptors/InterfaceDescriptorTests.cs`
   - Expected failure: Type does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Descriptors/InterfaceDescriptor.cs`
   - Sealed record: `Name`, `InterfaceType`, `Properties` (list of PropertyDescriptor)

**Dependencies:** 004
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 009: DomainDescriptor record
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `DomainDescriptor_Create_HasDomainName`
   - `DomainDescriptor_ObjectTypes_DefaultsEmpty`
   - File: `src/Strategos.Ontology.Tests/Descriptors/DomainDescriptorTests.cs`
   - Expected failure: Type does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Descriptors/DomainDescriptor.cs`
   - Sealed record: `DomainName`, `ObjectTypes` (list of ObjectTypeDescriptor)

**Dependencies:** 010
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 010: ObjectTypeDescriptor record (composite)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ObjectTypeDescriptor_Create_HasNameAndClrType`
   - `ObjectTypeDescriptor_Properties_DefaultsEmpty`
   - `ObjectTypeDescriptor_Links_DefaultsEmpty`
   - `ObjectTypeDescriptor_Actions_DefaultsEmpty`
   - `ObjectTypeDescriptor_Events_DefaultsEmpty`
   - `ObjectTypeDescriptor_Interfaces_DefaultsEmpty`
   - `ObjectTypeDescriptor_KeyProperty_IsNull`
   - File: `src/Strategos.Ontology.Tests/Descriptors/ObjectTypeDescriptorTests.cs`
   - Expected failure: Type does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Descriptors/ObjectTypeDescriptor.cs`
   - Sealed record: `Name`, `ClrType`, `DomainName`, `KeyProperty`, `Properties`, `Links`, `Actions`, `Events`, `ImplementedInterfaces`

**Dependencies:** 004-008
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 011: IPropertyBuilder interface
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write test:
   - `IPropertyBuilder_Required_ReturnsSelf`: Interface method exists
   - `IPropertyBuilder_Computed_ReturnsSelf`: Interface method exists
   - File: `src/Strategos.Ontology.Tests/Builder/IPropertyBuilderTests.cs`
   - Expected failure: Interface does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/IPropertyBuilder.cs`
   - Methods: `Required()`, `Computed()` — both return `IPropertyBuilder` for chaining

**Dependencies:** 004
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 012: IActionBuilder interface (fluent chain)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `IActionBuilder_Description_ReturnsSelf`
   - `IActionBuilder_Accepts_ReturnsSelf`
   - `IActionBuilder_Returns_ReturnsSelf`
   - `IActionBuilder_BoundToWorkflow_ReturnsSelf`
   - `IActionBuilder_BoundToTool_ReturnsSelf`
   - File: `src/Strategos.Ontology.Tests/Builder/IActionBuilderTests.cs`
   - Expected failure: Interface does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/IActionBuilder.cs`
   - Methods: `Description(string)`, `Accepts<T>()`, `Returns<T>()`, `BoundToWorkflow(string)`, `BoundToTool(string, string)`

**Dependencies:** 006
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 013: IEventBuilder interface
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `IEventBuilder_Description_ReturnsSelf`
   - `IEventBuilder_MaterializesLink_AcceptsLinkNameAndExpression`
   - `IEventBuilder_UpdatesProperty_AcceptsPropertyAndExpression`
   - `IEventBuilder_Severity_AcceptsEventSeverity`
   - File: `src/Strategos.Ontology.Tests/Builder/IEventBuilderTests.cs`
   - Expected failure: Interface does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/IEventBuilder.cs`
   - Generic: `IEventBuilder<TEvent>` with expression-based methods

**Dependencies:** 007
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 014: IInterfaceBuilder interface + mapping
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `IInterfaceBuilder_Property_AcceptsExpression`
   - `IInterfaceMapping_Via_MapsSourceToTarget`
   - File: `src/Strategos.Ontology.Tests/Builder/IInterfaceBuilderTests.cs`
   - Expected failure: Interface does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/IInterfaceBuilder.cs`
   - File: `src/Strategos.Ontology/Builder/IInterfaceMapping.cs`
   - `IInterfaceBuilder<TInterface>`: `Property(Expression<Func<TInterface, object>>)`
   - `IInterfaceMapping<TObject, TInterface>`: `Via(sourceExpr, targetExpr)`

**Dependencies:** 008
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 015: IObjectTypeBuilder interface
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `IObjectTypeBuilder_Key_AcceptsExpression`
   - `IObjectTypeBuilder_Property_ReturnsPropertyBuilder`
   - `IObjectTypeBuilder_HasOne_AcceptsLinkName`
   - `IObjectTypeBuilder_HasMany_AcceptsLinkName`
   - `IObjectTypeBuilder_ManyToMany_AcceptsLinkName`
   - `IObjectTypeBuilder_Action_ReturnsActionBuilder`
   - `IObjectTypeBuilder_Event_ReturnsEventBuilder`
   - `IObjectTypeBuilder_Implements_AcceptsMapping`
   - File: `src/Strategos.Ontology.Tests/Builder/IObjectTypeBuilderTests.cs`
   - Expected failure: Interface does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/IObjectTypeBuilder.cs`
   - Generic: `IObjectTypeBuilder<T>` with all DSL methods from design section 6.2-6.6

**Dependencies:** 011-014
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 016: ICrossDomainLinkBuilder interface
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ICrossDomainLinkBuilder_From_SetsSourceType`
   - `ICrossDomainLinkBuilder_ToExternal_SetsDomainAndType`
   - `ICrossDomainLinkBuilder_ManyToMany_SetsCardinality`
   - `ICrossDomainLinkBuilder_WithEdge_AllowsEdgeProperties`
   - File: `src/Strategos.Ontology.Tests/Builder/ICrossDomainLinkBuilderTests.cs`
   - Expected failure: Interface does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/ICrossDomainLinkBuilder.cs`
   - Fluent chain: `From<T>() → ToExternal(domain, type) → ManyToMany() → WithEdge(Action<IEdgeBuilder>)`

**Dependencies:** 005
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 017: IOntologyBuilder interface
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `IOntologyBuilder_Object_AcceptsConfigAction`
   - `IOntologyBuilder_Interface_AcceptsConfigAction`
   - `IOntologyBuilder_CrossDomainLink_ReturnsLinkBuilder`
   - File: `src/Strategos.Ontology.Tests/Builder/IOntologyBuilderTests.cs`
   - Expected failure: Interface does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/IOntologyBuilder.cs`
   - Methods: `Object<T>(Action<IObjectTypeBuilder<T>>)`, `Interface<T>(string, Action<IInterfaceBuilder<T>>)`, `CrossDomainLink(string)` returns `ICrossDomainLinkBuilder`

**Dependencies:** 015, 016
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 018: PropertyBuilder implementation
**Phase:** RED → GREEN → REFACTOR

**TDD Steps:**
1. [RED] Write tests:
   - `PropertyBuilder_Build_ProducesDescriptorWithName`
   - `PropertyBuilder_Required_SetsIsRequired`
   - `PropertyBuilder_Computed_SetsIsComputed`
   - `PropertyBuilder_ChainedCalls_AllApplied`
   - File: `src/Strategos.Ontology.Tests/Builder/PropertyBuilderTests.cs`
   - Expected failure: Implementation does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/PropertyBuilder.cs`
   - Internal class implementing `IPropertyBuilder`, produces `PropertyDescriptor`

3. [REFACTOR] Extract common builder pattern if needed

**Dependencies:** 011
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["idempotence: calling Required() twice produces same descriptor as once"] }`

---

### Task 019: ActionBuilder implementation
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ActionBuilder_Build_ProducesDescriptorWithName`
   - `ActionBuilder_Description_SetsDescription`
   - `ActionBuilder_Accepts_SetsAcceptsType`
   - `ActionBuilder_Returns_SetsReturnsType`
   - `ActionBuilder_BoundToWorkflow_SetsBindingAndWorkflowName`
   - `ActionBuilder_BoundToTool_SetsBindingAndToolReference`
   - `ActionBuilder_Unbound_DefaultsToUnbound`
   - File: `src/Strategos.Ontology.Tests/Builder/ActionBuilderTests.cs`
   - Expected failure: Implementation does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/ActionBuilder.cs`
   - Internal class implementing `IActionBuilder`, produces `ActionDescriptor`

**Dependencies:** 012
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["completeness: every fluent method is reflected in the produced descriptor"] }`

---

### Task 020: EventBuilder implementation
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `EventBuilder_Build_ProducesDescriptorWithEventType`
   - `EventBuilder_Description_SetsDescription`
   - `EventBuilder_MaterializesLink_AddsLinkToDescriptor`
   - `EventBuilder_UpdatesProperty_AddsPropertyToDescriptor`
   - `EventBuilder_Severity_SetsSeverity`
   - `EventBuilder_MultipleMaterializesLink_AllRecorded`
   - File: `src/Strategos.Ontology.Tests/Builder/EventBuilderTests.cs`
   - Expected failure: Implementation does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/EventBuilder.cs`
   - Internal class implementing `IEventBuilder<TEvent>`, produces `EventDescriptor`

**Dependencies:** 013
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["additivity: each MaterializesLink call adds to the collection without replacing"] }`

---

### Task 021: InterfaceBuilder implementation
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `InterfaceBuilder_Build_ProducesDescriptorWithProperties`
   - `InterfaceBuilder_Property_AddsToPropertyList`
   - `InterfaceMapping_Via_RecordsMappingPair`
   - File: `src/Strategos.Ontology.Tests/Builder/InterfaceBuilderTests.cs`
   - Expected failure: Implementation does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/InterfaceBuilder.cs`
   - File: `src/Strategos.Ontology/Builder/InterfaceMapping.cs`
   - Internal classes implementing `IInterfaceBuilder<T>` and `IInterfaceMapping<TObj, TIface>`

**Dependencies:** 014
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 022: ObjectTypeBuilder implementation
**Phase:** RED → GREEN → REFACTOR

**TDD Steps:**
1. [RED] Write tests:
   - `ObjectTypeBuilder_Key_SetsKeyProperty`
   - `ObjectTypeBuilder_Property_AddsPropertyDescriptor`
   - `ObjectTypeBuilder_HasOne_AddsLinkWithOneToOneCardinality`
   - `ObjectTypeBuilder_HasMany_AddsLinkWithOneToManyCardinality`
   - `ObjectTypeBuilder_ManyToMany_AddsLinkWithManyToManyCardinality`
   - `ObjectTypeBuilder_ManyToManyWithEdge_RecordsEdgeProperties`
   - `ObjectTypeBuilder_Action_AddsActionDescriptor`
   - `ObjectTypeBuilder_Event_AddsEventDescriptor`
   - `ObjectTypeBuilder_Implements_RecordsInterfaceMapping`
   - File: `src/Strategos.Ontology.Tests/Builder/ObjectTypeBuilderTests.cs`
   - Expected failure: Implementation does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/ObjectTypeBuilder.cs`
   - Internal class implementing `IObjectTypeBuilder<T>`, produces `ObjectTypeDescriptor`
   - Delegates to PropertyBuilder, ActionBuilder, EventBuilder as needed

3. [REFACTOR] Ensure clean composition with sub-builders

**Dependencies:** 018-021
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["composition: every builder method adds to correct collection without side effects on others"] }`

---

### Task 023: CrossDomainLinkBuilder implementation
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `CrossDomainLinkBuilder_Build_ProducesLinkWithNameAndSource`
   - `CrossDomainLinkBuilder_FromToExternal_SetsSourceAndTarget`
   - `CrossDomainLinkBuilder_ManyToMany_SetsCardinality`
   - `CrossDomainLinkBuilder_WithEdge_RecordsEdgeProperties`
   - File: `src/Strategos.Ontology.Tests/Builder/CrossDomainLinkBuilderTests.cs`
   - Expected failure: Implementation does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/CrossDomainLinkBuilder.cs`
   - Internal class implementing `ICrossDomainLinkBuilder` fluent chain

**Dependencies:** 016
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 024: OntologyBuilder implementation (root builder)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyBuilder_Object_CollectsObjectTypeDescriptor`
   - `OntologyBuilder_Interface_CollectsInterfaceDescriptor`
   - `OntologyBuilder_CrossDomainLink_CollectsLinkDefinition`
   - `OntologyBuilder_MultipleObjects_AllCollected`
   - File: `src/Strategos.Ontology.Tests/Builder/OntologyBuilderTests.cs`
   - Expected failure: Implementation does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Builder/OntologyBuilder.cs`
   - Internal class implementing `IOntologyBuilder`, collects all descriptors

**Dependencies:** 017, 022, 023
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["collection integrity: N Object() calls produce exactly N ObjectTypeDescriptors"] }`

---

### Task 025: DomainOntology abstract base class
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `DomainOntology_DomainName_ReturnsSubclassValue`
   - `DomainOntology_Define_ReceivesOntologyBuilder`
   - `DomainOntology_Subclass_CanDefineObjectTypes`
   - File: `src/Strategos.Ontology.Tests/DomainOntologyTests.cs`
   - Expected failure: Type does not exist
   - Create a `TestDomainOntology : DomainOntology` test double

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/DomainOntology.cs`
   - Abstract class: `abstract string DomainName`, `abstract void Define(IOntologyBuilder)`

**Dependencies:** 017
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Phase 1B: Engine Layer — Contracts + Object Set Algebra

> Team 1B — runs in parallel with Team 1A after Phase 0 completes.
> Branch: `feat/ontology-engine-layer`

---

### Task 026: IObjectSetProvider interface
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write test:
   - `IObjectSetProvider_ExecuteAsync_MethodSignatureExists`
   - `IObjectSetProvider_StreamAsync_MethodSignatureExists`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/IObjectSetProviderTests.cs`
   - Expected failure: Interface does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/ObjectSets/IObjectSetProvider.cs`
   - `ExecuteAsync<T>(ObjectSetExpression, CancellationToken)`, `StreamAsync<T>(ObjectSetExpression, CancellationToken)`

**Dependencies:** 001
**Parallelizable:** Yes (within Team 1B: sequential)
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 027: IEventStreamProvider + IOntologyProjection interfaces
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `IEventStreamProvider_QueryEventsAsync_MethodSignatureExists`
   - `IOntologyProjection_InterfaceExists`
   - File: `src/Strategos.Ontology.Tests/Events/EventProviderTests.cs`
   - Expected failure: Interfaces do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Events/IEventStreamProvider.cs`
   - File: `src/Strategos.Ontology/Events/IOntologyProjection.cs`
   - `IEventStreamProvider`: `QueryEventsAsync(domain, objectType, objectId?, since?, eventTypes?, ct)`
   - `IOntologyProjection`: marker interface for projection registration

**Dependencies:** 001
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 028: IActionDispatcher + ActionContext + ActionResult
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ActionContext_Create_HasDomainObjectTypeAndAction`
   - `ActionResult_Success_IsSuccessTrue`
   - `ActionResult_Failure_IsSuccessFalseWithError`
   - `IActionDispatcher_DispatchAsync_MethodSignatureExists`
   - File: `src/Strategos.Ontology.Tests/Actions/ActionDispatcherTests.cs`
   - Expected failure: Types do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Actions/IActionDispatcher.cs`
   - File: `src/Strategos.Ontology/Actions/ActionContext.cs`
   - File: `src/Strategos.Ontology/Actions/ActionResult.cs`
   - `IActionDispatcher`: `DispatchAsync(ActionContext, object request, CancellationToken)`
   - `ActionContext`: record with Domain, ObjectType, ObjectId, ActionName, TelemetryContext
   - `ActionResult`: record with IsSuccess, Result, Error

**Dependencies:** 001
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 029: IOntologyQuery + OntologyQueryResult
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `IOntologyQuery_InterfaceExists`
   - `OntologyQueryResult_Create_HasObjectTypes`
   - File: `src/Strategos.Ontology.Tests/Query/OntologyQueryTests.cs`
   - Expected failure: Types do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Query/IOntologyQuery.cs`
   - File: `src/Strategos.Ontology/Query/OntologyQueryResult.cs`
   - Schema-level query interface for agents exploring the ontology graph

**Dependencies:** 001
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 030: ObjectSetExpression node types
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `FilterExpression_Create_HasPredicateAndObjectType`
   - `TraverseLinkExpression_Create_HasLinkNameAndSourceExpression`
   - `InterfaceNarrowExpression_Create_HasInterfaceType`
   - `RootExpression_Create_HasObjectType`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetExpressionTests.cs`
   - Expected failure: Types do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/ObjectSets/ObjectSetExpression.cs`
   - Abstract base + concrete nodes: `RootExpression`, `FilterExpression`, `TraverseLinkExpression`, `InterfaceNarrowExpression`, `IncludeExpression`

**Dependencies:** 001
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["tree structure: expression tree depth increases by 1 for each chained operation"] }`

---

### Task 031: ObjectSetInclusion enum + ObjectSetResult<T>
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ObjectSetInclusion_Flags_CanBeCombined`: Schema = Properties | Actions | Links | Interfaces
   - `ObjectSetInclusion_Full_IncludesAll`: Full = Schema | Events | LinkedObjects
   - `ObjectSetResult_Create_HasItemsAndTotalCount`
   - `ObjectSetResult_Empty_HasZeroItems`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetTypesTests.cs`
   - Expected failure: Types do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/ObjectSets/ObjectSetInclusion.cs`
   - File: `src/Strategos.Ontology/ObjectSets/ObjectSetResult.cs`
   - `[Flags]` enum per design section 7.2
   - `ObjectSetResult<T>`: `Items`, `TotalCount`, `Inclusion`

**Dependencies:** 001
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["flags algebra: Schema & Properties == Properties", "flags algebra: Full | Schema == Full"] }`

---

### Task 032: OntologyEvent + EventQuery
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyEvent_Create_HasTypeTimestampAndPayload`
   - `EventQuery_Create_HasDomainAndObjectType`
   - `EventQuery_WithSince_FiltersByTime`
   - `EventQuery_WithEventTypes_FiltersByType`
   - File: `src/Strategos.Ontology.Tests/Events/EventQueryTests.cs`
   - Expected failure: Types do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Events/OntologyEvent.cs`
   - File: `src/Strategos.Ontology/Events/EventQuery.cs`
   - `OntologyEvent`: record with Domain, ObjectType, ObjectId, EventType, Timestamp, Payload
   - `EventQuery`: record with Domain, ObjectTypeName, ObjectId, Since, EventTypes

**Dependencies:** 001
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 033: ObjectSet<T> — construction + Where
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ObjectSet_Create_HasRootExpression`
   - `ObjectSet_Where_ReturnsNewObjectSetWithFilterExpression`
   - `ObjectSet_Where_PreservesOriginalObjectSet` (immutable)
   - `ObjectSet_MultipleWheres_ChainsExpressions`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetTests.cs`
   - Expected failure: Type does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/ObjectSets/ObjectSet.cs`
   - `ObjectSet<T>` with internal expression tree, `Where(Expression<Func<T, bool>>)` returns new ObjectSet

**Dependencies:** 030, 031
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["immutability: Where() returns new instance, original unchanged", "composition: Where(a).Where(b) is equivalent to Where(a && b) structurally"] }`

---

### Task 034: ObjectSet<T> — TraverseLink + OfInterface
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ObjectSet_TraverseLink_ReturnsObjectSetOfLinkedType`
   - `ObjectSet_TraverseLink_AddsTraverseLinkExpression`
   - `ObjectSet_OfInterface_ReturnsObjectSetOfInterfaceType`
   - `ObjectSet_OfInterface_AddsInterfaceNarrowExpression`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetTraversalTests.cs`
   - Expected failure: Methods do not exist

2. [GREEN] Implement in `ObjectSet.cs`:
   - `TraverseLink<TLinked>(string linkName)` returns `ObjectSet<TLinked>`
   - `OfInterface<TInterface>()` returns `ObjectSet<TInterface>`

**Dependencies:** 033
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["type narrowing: OfInterface changes result type parameter"] }`

---

### Task 035: ObjectSet<T> — Include
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ObjectSet_Include_SetsInclusionOnExpression`
   - `ObjectSet_Include_Schema_IncludesPropertiesActionsLinksInterfaces`
   - `ObjectSet_Include_Full_IncludesEverything`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetIncludeTests.cs`
   - Expected failure: Method does not exist

2. [GREEN] Implement in `ObjectSet.cs`:
   - `Include(ObjectSetInclusion)` returns new `ObjectSet<T>` with `IncludeExpression`

**Dependencies:** 033
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 036: ObjectSet<T> — ExecuteAsync + StreamAsync
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ObjectSet_ExecuteAsync_DelegatesToProvider`
   - `ObjectSet_StreamAsync_DelegatesToProvider`
   - `ObjectSet_ExecuteAsync_PassesExpressionTree`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetMaterializationTests.cs`
   - Expected failure: Methods do not exist
   - Use NSubstitute mock for `IObjectSetProvider`

2. [GREEN] Implement in `ObjectSet.cs`:
   - `ExecuteAsync(CancellationToken)` calls `IObjectSetProvider.ExecuteAsync<T>(expression, ct)`
   - `StreamAsync(CancellationToken)` calls `IObjectSetProvider.StreamAsync<T>(expression, ct)`

**Dependencies:** 026, 033
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 037: ObjectSet<T> — ApplyAsync + EventsAsync
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ObjectSet_ApplyAsync_DelegatesToActionDispatcher`
   - `ObjectSet_EventsAsync_DelegatesToEventStreamProvider`
   - `ObjectSet_ApplyAsync_PassesActionNameAndRequest`
   - `ObjectSet_EventsAsync_PassesSinceAndEventTypes`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetActionEventTests.cs`
   - Expected failure: Methods do not exist
   - Use NSubstitute mocks for `IActionDispatcher` and `IEventStreamProvider`

2. [GREEN] Implement in `ObjectSet.cs`:
   - `ApplyAsync(string actionName, object request, CancellationToken)`
   - `EventsAsync(TimeSpan? since, IReadOnlyList<string>? eventTypes)`

**Dependencies:** 027, 028, 033
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 038: OntologyTelemetryContext + IOntologyMetrics
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyTelemetryContext_Create_HasAllRequiredFields`
   - `OntologyTelemetryContext_TraversedLinks_DefaultsEmpty`
   - `OntologyTelemetryContext_ProducedEvents_DefaultsEmpty`
   - `IOntologyMetrics_InterfaceExists`
   - File: `src/Strategos.Ontology.Tests/Telemetry/TelemetryTests.cs`
   - Expected failure: Types do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Telemetry/OntologyTelemetryContext.cs`
   - File: `src/Strategos.Ontology/Telemetry/IOntologyMetrics.cs`
   - `OntologyTelemetryContext`: sealed record per design section 11.1
   - `IOntologyMetrics`: interface for per-action/type metrics collection

**Dependencies:** 001
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Phase 2: Runtime Layer — OntologyGraph

> Team 1C — starts after Teams 1A and 1B merge.
> Branch: `feat/ontology-runtime-layer`

---

### Task 039: ResolvedCrossDomainLink + WorkflowChain records
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ResolvedCrossDomainLink_Create_HasSourceAndTargetDomains`
   - `ResolvedCrossDomainLink_Create_HasResolvedObjectTypes`
   - `WorkflowChain_Create_HasWorkflowNameAndConsumedProducedTypes`
   - File: `src/Strategos.Ontology.Tests/ResolvedTypesTests.cs`
   - Expected failure: Types do not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/ResolvedCrossDomainLink.cs`
   - File: `src/Strategos.Ontology/WorkflowChain.cs`
   - Records that OntologyGraph produces after validation

**Dependencies:** 010 (Team 1A), 001 (Team 1B)
**Parallelizable:** Yes (within Team 1C: sequential)
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 040: OntologyGraphBuilder — domain registration
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyGraphBuilder_AddDomain_RegistersDomainOntology`
   - `OntologyGraphBuilder_AddDomain_Multiple_AllRegistered`
   - `OntologyGraphBuilder_Build_ProducesOntologyGraph`
   - `OntologyGraphBuilder_Build_DomainDescriptorsPopulated`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphBuilderTests.cs`
   - Expected failure: Type does not exist
   - Use TestDomainOntology subclasses with known descriptors

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/OntologyGraphBuilder.cs`
   - Internal class: `AddDomain<T>()` where T : DomainOntology, `Build()` returns `OntologyGraph`
   - Executes `DomainOntology.Define(IOntologyBuilder)` for each registered domain

**Dependencies:** 024, 025, 039
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["collection integrity: N AddDomain calls produce exactly N DomainDescriptors"] }`

---

### Task 041: OntologyGraphBuilder — cross-domain link validation
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyGraphBuilder_Build_ValidCrossDomainLink_Succeeds`
   - `OntologyGraphBuilder_Build_InvalidCrossDomainLink_ThrowsOntologyCompositionException`
   - `OntologyGraphBuilder_Build_UnresolvableDomain_ThrowsWithDomainName`
   - `OntologyGraphBuilder_Build_UnresolvableObjectType_ThrowsWithTypeName`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphBuilderValidationTests.cs`
   - Expected failure: Validation logic does not exist

2. [GREEN] Implement validation in `OntologyGraphBuilder.Build()`:
   - Resolve all cross-domain links against registered domain object types
   - Throw `OntologyCompositionException` on unresolvable references

**Dependencies:** 040
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 042: OntologyGraphBuilder — interface + workflow validation
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyGraphBuilder_Build_ValidInterfaceImpl_Succeeds`
   - `OntologyGraphBuilder_Build_IncompatibleInterfacePropertyType_Throws`
   - `OntologyGraphBuilder_Build_ValidWorkflowChain_Succeeds`
   - `OntologyGraphBuilder_Build_OrphanedProduces_DoesNotThrow` (warning only)
   - File: `src/Strategos.Ontology.Tests/OntologyGraphBuilderInterfaceValidationTests.cs`
   - Expected failure: Validation logic does not exist

2. [GREEN] Implement validation in `OntologyGraphBuilder.Build()`:
   - Validate interface property type compatibility
   - Validate `Produces<T>` → `Consumes<T>` workflow chains (warning if no consumer)

**Dependencies:** 041
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 043: OntologyGraph — construction + freeze
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyGraph_Create_HasDomains`
   - `OntologyGraph_Create_HasObjectTypes`
   - `OntologyGraph_Create_HasInterfaces`
   - `OntologyGraph_Create_HasCrossDomainLinks`
   - `OntologyGraph_Create_HasWorkflowChains`
   - `OntologyGraph_IsFrozen_CollectionsAreImmutable`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphTests.cs`
   - Expected failure: Frozen graph logic does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/OntologyGraph.cs`
   - Sealed class with IReadOnlyList collections
   - Constructed by OntologyGraphBuilder.Build(), immutable after construction

**Dependencies:** 042
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["immutability: post-construction, all collections are read-only"] }`

---

### Task 044: OntologyGraph — GetObjectType + GetImplementors lookups
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyGraph_GetObjectType_ExistingType_ReturnsDescriptor`
   - `OntologyGraph_GetObjectType_UnknownType_ReturnsNull`
   - `OntologyGraph_GetObjectType_WithDomain_FiltersCorrectly`
   - `OntologyGraph_GetImplementors_ExistingInterface_ReturnsImplementors`
   - `OntologyGraph_GetImplementors_UnknownInterface_ReturnsEmpty`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphLookupTests.cs`
   - Expected failure: Methods do not exist

2. [GREEN] Implement in `OntologyGraph.cs`:
   - Pre-computed dictionaries built at freeze time
   - `GetObjectType(domain, name)` → O(1) lookup
   - `GetImplementors(interfaceName)` → O(1) lookup

**Dependencies:** 043
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: true, performanceSLAs: [{ "operation": "GetObjectType-lookup", "metric": "p99_ms", "threshold": 1 }] }`

---

### Task 045: OntologyGraph — TraverseLinks + FindWorkflowChains
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyGraph_TraverseLinks_Depth1_ReturnsDirectLinks`
   - `OntologyGraph_TraverseLinks_Depth2_ReturnsTransitiveLinks`
   - `OntologyGraph_TraverseLinks_MaxDepth_Respected`
   - `OntologyGraph_TraverseLinks_CircularLinks_DoesNotLoop`
   - `OntologyGraph_FindWorkflowChains_ExistingWorkflow_ReturnsChains`
   - `OntologyGraph_FindWorkflowChains_UnknownWorkflow_ReturnsEmpty`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphTraversalTests.cs`
   - Expected failure: Methods do not exist

2. [GREEN] Implement in `OntologyGraph.cs`:
   - `TraverseLinks(domain, objectType, maxDepth)` — BFS/DFS with cycle detection
   - `FindWorkflowChains(targetWorkflow)` — lookup from pre-computed chains

**Dependencies:** 044
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: true, properties: ["cycle safety: circular links terminate at maxDepth", "monotonicity: TraverseLinks(depth=N) results are superset of TraverseLinks(depth=N-1)"], performanceSLAs: [{ "operation": "TraverseLinks-depth2", "metric": "p99_ms", "threshold": 5 }] }`

---

### Phase 3A: Source Generator — DiagnosticAnalyzer

> Team 2 — runs in parallel with Teams 3 and 4 after Phase 2 completes.
> Branch: `feat/ontology-generators`

---

### Task 046: OntologyDiagnosticAnalyzer scaffold
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write test:
   - `OntologyDiagnosticAnalyzer_SupportedDiagnostics_ReturnsAllOntoDiagnostics`
   - File: `src/Strategos.Ontology.Generators.Tests/OntologyDiagnosticAnalyzerTests.cs`
   - Expected failure: Analyzer class does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.Generators/OntologyDiagnosticAnalyzer.cs`
   - File: `src/Strategos.Ontology.Generators/OntologyDiagnostics.cs` (diagnostic descriptors)
   - `[DiagnosticAnalyzer(LanguageNames.CSharp)]` entry point
   - Register all 10 diagnostic descriptors (ONTO001-ONTO010)

**Dependencies:** 002 (Phase 0)
**Parallelizable:** Yes (within Team 2: sequential)
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 047: ONTO001 (no Key) + ONTO007 (duplicate type)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ONTO001_ObjectTypeWithoutKey_ReportsError`
   - `ONTO001_ObjectTypeWithKey_NoError`
   - `ONTO007_DuplicateObjectType_ReportsError`
   - `ONTO007_UniqueObjectTypes_NoError`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/ObjectTypeAnalyzerTests.cs`
   - Expected failure: Analyzer logic does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.Generators/Analyzers/DomainOntologyAnalyzer.cs`
   - Analyze `obj.Key()` invocations in `Object<T>()` callbacks
   - Track type names per domain for duplicates

**Dependencies:** 046
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 048: ONTO002 (non-existent property member)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ONTO002_PropertyExpressionValidMember_NoError`
   - `ONTO002_PropertyExpressionInvalidMember_ReportsError`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/PropertyAnalyzerTests.cs`
   - Expected failure: Analyzer logic does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.Generators/Analyzers/PropertyAnalyzer.cs`
   - Analyze expression trees in `Property()` and `Key()` calls for member resolution

**Dependencies:** 047
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 049: ONTO005 (interface incompatible types)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ONTO005_CompatiblePropertyMapping_NoError`
   - `ONTO005_IncompatiblePropertyTypes_ReportsError`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/InterfaceAnalyzerTests.cs`
   - Expected failure: Analyzer logic does not exist

2. [GREEN] Implement in `DomainOntologyAnalyzer.cs` or new `InterfaceAnalyzer.cs`:
   - Analyze `Via()` calls in `Implements<T>()` for type compatibility

**Dependencies:** 048
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 050: ONTO009 (MaterializesLink undeclared link)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ONTO009_MaterializesLinkDeclaredLink_NoError`
   - `ONTO009_MaterializesLinkUndeclaredLink_ReportsError`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/EventAnalyzerTests.cs`
   - Expected failure: Analyzer logic does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.Generators/Analyzers/EventAnalyzer.cs`
   - Cross-reference `MaterializesLink("linkName")` against `HasOne`/`HasMany`/`ManyToMany` declarations

**Dependencies:** 047
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 051: ONTO003 (unknown cross-domain, Warning) + ONTO004 (no actions, Info)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ONTO003_CrossDomainLinkUnknownDomain_ReportsWarning`
   - `ONTO003_CrossDomainLinkKnownDomain_NoWarning` (limited — can't validate cross-assembly)
   - `ONTO004_ObjectTypeNoActions_ReportsInfo`
   - `ONTO004_ObjectTypeWithActions_NoInfo`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/CrossDomainLinkAnalyzerTests.cs`
   - Expected failure: Analyzer logic does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.Generators/Analyzers/CrossDomainLinkAnalyzer.cs`
   - ONTO003: Warn when `ToExternal()` references domain not visible in same assembly
   - ONTO004: Info when `Object<T>()` has no `Action()` calls

**Dependencies:** 047
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 052: ONTO006 (Produces/Consumes, Warning) + ONTO008 (undeclared event, Warning)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ONTO006_ProducesWithNoConsumer_ReportsWarning`
   - `ONTO006_ProducesWithConsumer_NoWarning`
   - `ONTO008_EventTypeNotDeclaredOnObjectType_ReportsWarning`
   - `ONTO008_EventTypeDeclared_NoWarning`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/WorkflowChainAnalyzerTests.cs`
   - Expected failure: Analyzer logic does not exist

2. [GREEN] Implement analysis for workflow chain and event references

**Dependencies:** 050
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 053: ONTO010 (events without IEventStreamProvider, Warning)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `ONTO010_EventsWithoutProviderRegistration_ReportsWarning`
   - `ONTO010_EventsWithProviderRegistration_NoWarning`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/EventProviderAnalyzerTests.cs`
   - Expected failure: Analyzer logic does not exist

2. [GREEN] Implement analysis:
   - Detect object types with events but no `UseEventStreamProvider<>()` call visible in same assembly

**Dependencies:** 050
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Phase 3B: MCP Tool Surface

> Team 3 — runs in parallel with Teams 2 and 4 after Phase 2 completes.
> Branch: `feat/ontology-mcp`

---

### Task 054: OntologyToolDiscovery
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyToolDiscovery_Discover_ReturnsOntologyTools`
   - `OntologyToolDiscovery_Discover_IncludesQueryActionExplore`
   - `OntologyToolDiscovery_EnrichWithOntology_AddsSemanticMetadata`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyToolDiscoveryTests.cs`
   - Expected failure: Type does not exist
   - Use NSubstitute mock for `OntologyGraph`

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.MCP/OntologyToolDiscovery.cs`
   - Produces enriched tool descriptors from the OntologyGraph for progressive disclosure

**Dependencies:** 003, 043
**Parallelizable:** Yes (within Team 3: sequential)
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 055: ontology_explore MCP tool (schema discovery)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyExplore_Domains_ReturnsAllDomains`
   - `OntologyExplore_ObjectTypes_ReturnsTypesInDomain`
   - `OntologyExplore_Actions_ReturnsActionsOnObjectType`
   - `OntologyExplore_Links_ReturnsLinksFromObjectType`
   - `OntologyExplore_Events_ReturnsEventsOnObjectType`
   - `OntologyExplore_TraverseFrom_ReturnsGraphTraversal`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyExploreToolTests.cs`
   - Expected failure: Tool does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.MCP/OntologyMcpTools.cs` (partial: explore)
   - Implements `ontology_explore` tool per design section 9.2
   - Routes scope parameter to OntologyGraph queries

**Dependencies:** 054
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 056: ontology_query MCP tool (read operations)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyQuery_ByObjectType_ReturnsInstances`
   - `OntologyQuery_WithFilter_FiltersResults`
   - `OntologyQuery_TraverseLink_ReturnsLinkedObjects`
   - `OntologyQuery_WithInterface_NarrowsToImplementors`
   - `OntologyQuery_Include_ControlsReturnedData`
   - `OntologyQuery_Events_ReturnTemporalEvents`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyQueryToolTests.cs`
   - Expected failure: Tool does not exist
   - Use NSubstitute mocks for IObjectSetProvider, IEventStreamProvider

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.MCP/OntologyMcpTools.cs` (partial: query)
   - Implements `ontology_query` tool per design section 9.2
   - Translates JSON parameters to ObjectSet<T> expression tree

**Dependencies:** 055
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 057: ontology_action MCP tool (write operations)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyAction_SingleObject_DispatchesToActionDispatcher`
   - `OntologyAction_WithFilter_AppliesBatch`
   - `OntologyAction_UnknownAction_ReturnsError`
   - `OntologyAction_UnknownObjectType_ReturnsError`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyActionToolTests.cs`
   - Expected failure: Tool does not exist
   - Use NSubstitute mock for IActionDispatcher

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.MCP/OntologyMcpTools.cs` (partial: action)
   - Implements `ontology_action` tool per design section 9.2
   - Routes to IActionDispatcher with ActionContext

**Dependencies:** 056
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 058: OntologyStubGenerator (.pyi generation)
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `OntologyStubGenerator_Generate_ProducesValidPython`
   - `OntologyStubGenerator_Generate_IncludesProperties`
   - `OntologyStubGenerator_Generate_IncludesActions`
   - `OntologyStubGenerator_Generate_IncludesLinks`
   - `OntologyStubGenerator_Generate_IncludesEvents`
   - `OntologyStubGenerator_Generate_IncludesInterfaces`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyStubGeneratorTests.cs`
   - Expected failure: Type does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.MCP/OntologyStubGenerator.cs`
   - Generates enriched `.pyi` stubs from OntologyGraph per design section 9.3

**Dependencies:** 054
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: true, benchmarks: false, properties: ["completeness: every ObjectTypeDescriptor produces a stub class", "roundtrip: generated stub contains all declared properties/actions/links/events"] }`

---

### Phase 3C: Workflow Extensions + DI Registration

> Team 4 — runs in parallel with Teams 2 and 3 after Phase 2 completes.
> Branch: `feat/ontology-extensions-di`

---

### Task 059: Consumes<T> extension method
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `Consumes_RegistersConsumedTypeOnWorkflow`
   - `Consumes_ReturnsBuilderForChaining`
   - File: `src/Strategos.Ontology.Tests/Extensions/WorkflowOntologyExtensionsTests.cs`
   - Expected failure: Extension method does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Extensions/WorkflowOntologyExtensions.cs`
   - `Consumes<T>()` extension on `IWorkflowBuilder<TState>` — registers consumed type metadata

**Dependencies:** 043 (needs OntologyGraph awareness)
**Parallelizable:** Yes (within Team 4: sequential)
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 060: Produces<T> extension method
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `Produces_RegistersProducedTypeOnWorkflow`
   - `Produces_ReturnsBuilderForChaining`
   - `Produces_EnablesWorkflowChainInference`
   - File: `src/Strategos.Ontology.Tests/Extensions/ProducesExtensionTests.cs`
   - Expected failure: Extension method does not exist

2. [GREEN] Implement in `WorkflowOntologyExtensions.cs`:
   - `Produces<T>()` extension on `IWorkflowBuilder<TState>` — registers produced type metadata

**Dependencies:** 059
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 061: AddOntology ServiceCollection extension
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `AddOntology_RegistersOntologyGraphAsSingleton`
   - `AddOntology_ExecutesDomainOntologyDefine`
   - `AddOntology_FreezesGraphAfterRegistration`
   - `AddOntology_AddDomain_RegistersDomainOntology`
   - `AddOntology_UseObjectSetProvider_RegistersProvider`
   - `AddOntology_UseEventStreamProvider_RegistersProvider`
   - `AddOntology_UseActionDispatcher_RegistersDispatcher`
   - File: `src/Strategos.Ontology.Tests/Configuration/AddOntologyTests.cs`
   - Expected failure: Extension method does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology/Configuration/OntologyServiceCollectionExtensions.cs`
   - File: `src/Strategos.Ontology/Configuration/OntologyOptions.cs` (options class)
   - `services.AddOntology(Action<OntologyOptions>)` per design section 5.1

**Dependencies:** 040, 043
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 062: AddOntologyMcpTools ServiceCollection extension
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `AddOntologyMcpTools_RegistersToolDiscovery`
   - `AddOntologyMcpTools_RegistersMcpTools`
   - File: `src/Strategos.Ontology.MCP.Tests/Configuration/AddOntologyMcpToolsTests.cs`
   - Expected failure: Extension method does not exist

2. [GREEN] Implement
   - File: `src/Strategos.Ontology.MCP/Configuration/McpServiceCollectionExtensions.cs`
   - `services.AddOntologyMcpTools()` registers tool discovery + MCP tool implementations

**Dependencies:** 054-057, 061
**Parallelizable:** Yes
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

### Task 063: End-to-end integration test
**Phase:** RED → GREEN

**TDD Steps:**
1. [RED] Write tests:
   - `Ontology_FullRegistration_GraphFreezes`
   - `Ontology_FullRegistration_CrossDomainLinksResolved`
   - `Ontology_FullRegistration_InterfaceImplementorsDiscoverable`
   - `Ontology_FullRegistration_ObjectSetQueriesWork`
   - `Ontology_InvalidCrossDomainLink_FailsFast`
   - File: `src/Strategos.Ontology.Tests/Integration/OntologyIntegrationTests.cs`
   - Expected failure: End-to-end flow not wired up
   - Create two test DomainOntology subclasses with cross-domain links

2. [GREEN] Wire up the full flow:
   - `AddOntology` → `OntologyGraphBuilder` → `DomainOntology.Define()` → `OntologyGraph` (frozen)
   - Verify lookups, traversals, and provider delegation work end-to-end

**Dependencies:** 061, 062
**Parallelizable:** No (integration)
**Testing Strategy:** `{ exampleTests: true, propertyTests: false, benchmarks: false }`

---

## Parallelization Strategy

### Phase 0: Scaffolding (Sequential)
```text
001 → 002 → 003
```
Creates all project files on main branch.

### Phase 1: Language + Engine (Parallel Teams)

| Team 1A (Language Layer) | Team 1B (Engine Layer) |
|--------------------------|------------------------|
| Branch: `feat/ontology-language-layer` | Branch: `feat/ontology-engine-layer` |
| Tasks 004-025 (22 tasks) | Tasks 026-038 (13 tasks) |
| Descriptors → Builders → DomainOntology | Contracts → ObjectSet → Telemetry |

Teams 1A and 1B have **zero shared files** — they create in different subdirectories:
- 1A: `Descriptors/`, `Builder/`, `DomainOntology.cs`
- 1B: `ObjectSets/`, `Events/`, `Actions/`, `Query/`, `Telemetry/`

### Phase 2: Runtime Layer (Single Team)
```text
Team 1C: Tasks 039-045 (7 tasks)
Branch: feat/ontology-runtime-layer
```
Depends on merge of Teams 1A + 1B. Builds OntologyGraph on top of both layers.

### Phase 3: Tooling + Extensions (Three Parallel Teams)

| Team 2 (Generators) | Team 3 (MCP) | Team 4 (Extensions + DI) |
|----------------------|--------------|--------------------------|
| Branch: `feat/ontology-generators` | Branch: `feat/ontology-mcp` | Branch: `feat/ontology-extensions-di` |
| Tasks 046-053 (8 tasks) | Tasks 054-058 (5 tasks) | Tasks 059-063 (5 tasks) |
| DiagnosticAnalyzer + ONTO001-010 | MCP tools + stub gen | Workflow ext + DI + integration |

### Dependency Graph

```text
Phase 0:  [001] → [002] → [003]
              ↓
Phase 1:  [Team 1A: 004-025] ‖ [Team 1B: 026-038]
              ↓                      ↓
Phase 2:  [Team 1C: 039-045] ←—— merge
              ↓
Phase 3:  [Team 2: 046-053] ‖ [Team 3: 054-058] ‖ [Team 4: 059-063]
              ↓                      ↓                      ↓
Final:    merge all → integration test (063)
```

### Critical Path
```text
001 → 004 → 010 → 015 → 022 → 024 → 025 → 040 → 041 → 042 → 043 → 044 → 045 → 061 → 063
```
Critical path length: 15 tasks (through Language → Runtime → DI → Integration)

## Deferred Items

| Item | Rationale |
|------|-----------|
| Section 10.1 Marten projections | Consumer-side (Basileus.Ontology.Marten) — not in this repo |
| Section 11.3 OntologyMetricsView | Marten projection — belongs to Basileus |
| Section 12 Platform Integration | Phronesis, Execution Profiles, ControlPlane — Basileus-specific |
| Section 13 Basileus Adoption | Consumer-side changes tracked in Basileus issue #99 |
| Section 15 Future Considerations | Explicitly deferred by design: security policies, versioning, visualization, cost profiles, streaming, multi-tenant |
| Property-based testing for analyzers | Roslyn analyzer testing is inherently example-based (fixed code snippets) |

## Completion Checklist
- [ ] All tests written before implementation
- [ ] All tests pass
- [ ] Code coverage meets 80% threshold
- [ ] All 10 ONTO diagnostics implemented and tested
- [ ] OntologyGraph freeze produces immutable instance
- [ ] Cross-domain link validation fails fast on bad references
- [ ] ObjectSet expression tree builds correctly
- [ ] MCP tools route to correct providers
- [ ] DI registration wires everything correctly
- [ ] End-to-end integration test passes
- [ ] Ready for review

# Cross-assembly binding proof

**Status:** merged for 3.0.0 (#204); not yet released. Manifest schema `ProofCatalogV1`
1.0, frozen in `LevelUp.Strategos.Contracts` 0.13.0.

## The hole this closes

The #167 workflow-binding proof reads `Compilation.SyntaxTrees`. That is complete for a
binding whose action contract and workflow are compiled together, and blind to one whose
halves are not — which is the normal layout for a consumer, because basileus consumes
Strategos as a package and its ontology and its workflows do not share a project.

Before #204, which half lived where silently decided whether the guarantee applied:

| Layout | What happened |
|---|---|
| Both halves in one compilation | Proved. |
| Ontology behind the boundary | **Nothing.** The consuming build was clean, the saga lowered, and no diagnostic said the proof had not run. |
| Workflow behind the boundary | AGWF039, which the project file then silenced — there was no other way to ship the assembly. |
| Ontology with the analyzer but no generator | **Nothing.** No generator ran to notice. |

Two of those four are silence. The third is a suppression that every such project needs,
which makes it indistinguishable from a typo that has been silenced.

## The shape of the fix

The obligation travels with the contract.

1. **The declaring assembly exports.** Whatever action contracts it declares are emitted
   as a `ProofCatalogV1` document, and the binding is **deferred** rather than reported.
2. **The assembly that lowers the workflow imports and proves.** Imported contracts are
   merged into the same `OntologyActionCatalog` the proof already reads, so an imported
   binding is discharged by exactly the same code as a local one.
3. **An assembly that can neither prove nor export is refused** — AONT222 on the
   declaring side, AGWF046 when a specific contract cannot be projected.

## How the catalog crosses

As an assembly-level attribute carrying its JSON:

```csharp
[assembly: global::Strategos.Generated.StrategosProofCatalogAttribute("{\"schemaVersion\":\"1.0\",...}")]
```

This is the only channel that reaches a **referencing** compilation's analyzer. A NuGet
content file does not; neither does an AdditionalFile, which is per-project. Assembly
metadata arrives through the same `MetadataReference` the consumer already has, and is
read with Roslyn symbols — no file I/O, no reflection, no user code executed, which are
the three things #204's acceptance excludes.

The attribute **type** is emitted per assembly and matched by full metadata name rather
than by type identity. The two kinds of producing assembly reference different Strategos
packages and no assembly is common to both: an ontology-only project references
`Strategos.Ontology`, which U-2 keeps free of dependencies. A shared attribute type would
need either an assembly that does not exist or a dependency U-2 forbids.

## What the catalog carries, and what it refuses to carry

Ordinal identities, predicates in the frozen `ActionPredicateV1` vocabulary, the frame,
the authority **and the lattice that orders it**, the inverse, and the workflow binding.
No CLR type appears anywhere on the shape, by construction.

The authority coordinate is resolved **at export**, where the declaring lattice is in
hand. Two requirements then compare downstream without either side resolving a name
against a lattice it may not have. The name travels alongside as provenance, and a reader
holding both checks them against each other.

Two contract shapes are refused at export rather than carried lossily:

- **An opaque term.** An opaque contract is unprovable wherever it is read, so exporting
  one hands every consumer a contract that can only ever produce "cannot be proved".
  Refusing at the declaring assembly puts the diagnostic in front of the author who can
  fix it, once, instead of in front of every consumer who cannot.
- **A link or relation atom.** Its atom key and its declared read set are *different*
  strings — a relation atom keyed `relation|relation:…` reads `link|X` — and
  `ActionPropertyReferenceV1` has one name and no read set. Writing it as a comparison
  would drop the read set, and the read set is precisely what the frame check projects a
  guarantee through. The contract would be proved downstream against a frame it never
  declared.

Both produce **AGWF046** at the declaring assembly, and the binding then stays
unresolved there — deferral is per action, not per assembly.

## Fail-closed, on both sides

Every one of these is a refusal, never an omission. A catalog that were merely skipped
would silently drop every obligation it carries and leave the build green.

| Condition | Diagnostic |
|---|---|
| A contract cannot be projected into the portable form | **AGWF046** (declaring side) |
| Unknown `schemaVersion`, malformed JSON, unknown predicate / literal / operator / scalar discriminator, an authority the catalog's own lattice does not define, or a `contentHash` that does not match the bytes | **AGWF047** |
| One ordinal action identity declared by two catalogs, or by a catalog and this compilation | **AGWF048** |
| A binding declared by an assembly that exports no catalog at all | **AONT222** (declaring side) |
| An imported contract outside the closed proof fragment | AGWF042, unchanged |
| An imported binding the workflow does not refine | AGWF041, unchanged |

AGWF048 has no tie-break by design: preferring either side would make the proved contract
depend on reference order.

## AGWF039 and AGWF040 are no longer configurable

They were configurable because a cross-assembly layout had no other exit and the
alternative was deleting the binding. Exporting the contract is that exit. An unresolved
binding now means the obligation reaches nobody, which is not a thing to silence.

Their meaning narrows to match. A local binding whose workflow is absent here is
**deferred** when that action's contract was exported, and **reported** when it was not.

This removed the suppression matrix's only configurable generator-produced control.
`scripts/verify-generator-consumer-build.sh` now uses **AGWF010** — a configurable
warning from the same generator, promoted to an error by `-warnaserror`. A control has to
be configurable *and* generator-produced: `csc` reports generator errors and then skips
analyzer execution entirely, so no analyzer diagnostic can control a generator arm.

## Deferral has a limit, and an opt-in

A library cannot know whether some consumer will supply the workflow its action binds, so
deferral is the default and a binding to a name nothing ever declares is never proved by
anyone.

An application knows it is the last compilation, and says so:

```xml
<PropertyGroup>
  <StrategosProofRequireLocalBindings>true</StrategosProofRequireLocalBindings>
</PropertyGroup>
```

With the property set, a deferral is an error, because there is no later build to
discharge it. Leaf applications should set it; libraries should not.

## Adoption

**A project that declares ontology action contracts with `BoundToWorkflow` must reference
`LevelUp.Strategos.Generators`.** Without it, nothing exports the contract and AONT222
refuses the build. The generator runs and emits nothing else for a project with no
workflows, so the cost is a development-time package reference.

**basileus.** `Basileus.Knowledge` is the known case: it references the ontology analyzer
and not the workflow generator, and `KnowledgeOntology.IngestDocument →
BoundToWorkflow("knowledge-ingestion")` compiled silent and dangling on 3.0.0-rc.1. Add
the generator reference; the binding is then exported and proved in whichever project
lowers `knowledge-ingestion`. Set `StrategosProofRequireLocalBindings` in the host
application.

**exarchos.** `ProofCatalogV1` is emitted JSON Schema like every other contract and is
carried in the Contracts package. Nothing in exarchos must change for 3.0.0; the manifest
is available when a TypeScript arm wants to read a catalog.

## Where the evidence is

- `tests/Strategos.Generators.Tests/Proof/CrossAssemblyBindingHarnessTests.cs` — the
  two-assembly harness. A producer is emitted to a real PE image and handed to the
  consumer as a `MetadataReference`, so the consumer sees metadata and nothing else.
  Export, defer, import, merge, prove, refute, and each refusal.
- `tests/Strategos.Generators.Tests/Proof/ProofCatalogWireTests.cs` — the emitted bytes
  validate against `ProofCatalogV1`, and every exportable contract round-trips to the
  *same* formula (`StableKey`, not logical equivalence: equivalence would pass for a
  projection that normalized on the way through).
- `tests/Strategos.Ontology.Generators.Tests/Analyzers/AONT222BindingExportTests.cs` —
  the declaring-side refusal, and the pin that the two analyzer assemblies agree on an
  attribute name neither can reference.
- `scripts/verify-generator-consumer-build.sh` arm 7 — the same seam at the packed tier,
  through the real nupkgs.

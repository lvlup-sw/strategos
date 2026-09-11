# Cross-assembly binding proof — consumer adoption notice (#204)

**Date:** 2026-09-10
**Status:** merged for 3.0.0 GA. Not yet released; this notice is written ahead of the
`v3.0.0` tag so the four consumers can plan the one project-file change it needs.

Strategos 3.0.0 closes the hole where a workflow binding whose action contract lived in a
**referenced assembly** was never proved and never reported. The mechanism is in
[`docs/architecture/cross-assembly-proof.md`](../architecture/cross-assembly-proof.md);
this file records who has to do what.

---

## The one change a consumer may need

**A project that declares ontology action contracts with `BoundToWorkflow` must reference
`LevelUp.Strategos.Generators`.**

```xml
<PackageReference Include="LevelUp.Strategos.Generators" Version="3.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

Without it, nothing exports the contract, no compilation can ever discharge the binding,
and the build is refused with **AONT222**. The generator emits nothing else for a project
that declares no workflows, so the cost is a development-time package reference.

## The one property a leaf application should set

```xml
<PropertyGroup>
  <StrategosProofRequireLocalBindings>true</StrategosProofRequireLocalBindings>
</PropertyGroup>
```

A library cannot know whether a consumer will supply the workflow its action binds, so an
unresolved binding whose contract was exported is **deferred** by default. An application
is the last compilation; with this property set, a deferral is an error, because there is
no later build to discharge it.

Set it in host/entry-point projects. Do not set it in libraries — it would make every
legitimate cross-assembly layout fail.

---

## Per consumer

### basileus — one project, known in advance

`Basileus.Knowledge` references the ontology analyzer and not the workflow generator, and
`KnowledgeOntology.IngestDocument → BoundToWorkflow("knowledge-ingestion")` compiles
silent and dangling on 3.0.0-rc.1. On 3.0.0 it is refused with AONT222.

1. Add the generator reference to `Basileus.Knowledge`.
2. Expect the binding to be **proved** (or refuted) in whichever project lowers
   `knowledge-ingestion`. A refutation there is a real finding, not a migration artifact:
   it is the first time anything has checked that workflow against that contract.
3. Set `StrategosProofRequireLocalBindings` in the host application.

Also check every project file for a silenced **AGWF039** or **AGWF040**. Both are now
`NotConfigurable`, so the suppression is inert — and it was very likely silencing exactly
this layout. Remove it and let the new behavior decide.

### exarchos — nothing required

`ProofCatalogV1` is emitted JSON Schema like every other contract and travels in the
Contracts package. Nothing must change for 3.0.0. The manifest is available if a
TypeScript arm ever wants to read a catalog; `ActionContractV1` gained an optional
`boundWorkflow`, which is additive and structurally non-breaking.

### valkyrie, dynatoi — check for a silenced id

Both consume the Ontology line. Neither is known to declare a workflow binding. The check
is the same one-liner: grep the project files for `AGWF039`, `AGWF040` and `AONT222`. If
none appears, there is nothing to do.

---

## What this notice does not claim

- **It does not claim every deferred binding is proved somewhere.** Deferral is honest
  about what a library can know, and `StrategosProofRequireLocalBindings` is the only
  thing that turns "nobody claimed this" into an error. A consumer that does not set it in
  its leaf application keeps a gap this release does not close.
- **It does not claim the manifest carries every contract.** An opaque predicate, and a
  link or relation atom, are refused at export with **AGWF046** rather than carried
  lossily. A domain that uses them across an assembly boundary must either keep the
  binding local or move to the closed predicate vocabulary. The diagnostic names which
  action and why.
- **It has not been exercised against a real consumer repository.** The evidence is the
  two-assembly harness, the wire round trip, and arm 7 of
  `scripts/verify-generator-consumer-build.sh`, which builds a producer and a consumer
  against the packed nupkgs. The basileus arm above is a prediction from reading its
  layout, not an observation.

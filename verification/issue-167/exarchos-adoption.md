Tracking issue: [lvlup-sw/exarchos#1893](https://github.com/lvlup-sw/exarchos/issues/1893)

Upstream dependency: lvlup-sw/strategos#167

Current evidence status: coordination exists; adoption implementation and consumer-test results remain
`Unproven`.

The unreleased Strategos Contracts 0.11.0 candidate adds the optional, occurrence-scoped
ActionReferenceV1 wire object, and Strategos #167 extends the guarded fluent
builder surface so every reachable workflow step can name the ontology action
it performs. The future `contracts-v0.11.0` run and released package digest remain
`Indeterminate`; a local candidate nupkg is not adoption evidence.

Scope:

- after publication, upgrade the extracted Strategos Contracts schemas/models to 0.11.0;
- mirror the optional action object on every supported workflow step variant,
  with required domainName, objectTypeName, and actionName when action is
  present;
- update the Strategos builder/API mirror for Performs and the affected
  workflow, fork, loop, approval-rejection, and approval-escalation
  continuation surfaces;
- consume the closed AGWF039-AGWF043 diagnostic vocabulary;
- preserve ordinal ontology identities exactly and reject partial action
  objects.

Acceptance:

- Zod/schema validation accepts an omitted action and a fully populated action,
  but rejects partial or blank identities;
- Strategos API-mirror tests cover all newly guarded entrypoints and
  continuations;
- a fixture round-trips the ActionReferenceV1 shape without rewriting ontology
  names;
- compatibility tests continue to accept legacy workflows with no action
  property.

Link the implementing PR back to lvlup-sw/strategos#167.

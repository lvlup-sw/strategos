Tracking issue: [lvlup-sw/basileus#493](https://github.com/lvlup-sw/basileus/issues/493)

Upstream dependency: lvlup-sw/strategos#167

Current evidence status: coordination exists; adoption implementation and consumer-test results remain
`Unproven`.

The unreleased Strategos Contracts 0.11.0 candidate adds the optional, occurrence-scoped
ActionReferenceV1 wire object. Strategos uses its three ordinal ontology names
to prove that every reachable workflow step refines the action bound to the
workflow. Basileus must deliberately adopt that contract before it emits the
new field. The future `contracts-v0.11.0` run and released package digest remain
`Indeterminate`; a local candidate nupkg is not adoption evidence.

Scope:

- after publication, upgrade to the Strategos Contracts 0.11.0 schema/model package;
- populate domainName, objectTypeName, and actionName for every reachable step
  in workflows that participate in BoundToWorkflow proof;
- keep the action property absent for legacy or intentionally unbound
  workflows;
- preserve the three ontology names exactly; do not substitute CLR type names;
- consume the expanded AGWF039-AGWF043 diagnostic vocabulary where Basileus
  exposes Strategos diagnostics.

Acceptance:

- exported workflow JSON round-trips a configured ActionReferenceV1 without
  changing any of its three ordinal names;
- unconfigured workflow JSON remains byte-compatible and omits action;
- tests cover missing, malformed, and fully populated action references;
- a pinned Strategos #167 consumer fixture proves one legal bound workflow and
  one deliberately illegal seam.

Link the implementing PR back to lvlup-sw/strategos#167.

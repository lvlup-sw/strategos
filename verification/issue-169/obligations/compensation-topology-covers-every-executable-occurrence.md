# compensation-topology-covers-every-executable-occurrence

## Why this obligation exists

Repository history shows multiple cases where accepted fluent callbacks were absent or under-scoped
in generated routing. `CompensationTopology` centralizes occurrence, concrete scope, lane/path,
ordinal, action identity, and parent ownership, but start/completion/failure ingress remains
distributed across many emitters.

## Failure scenario and boundary

A low-confidence, approval, diagnostic-fork, loop, or failure-handler leaf executes but is absent from
topology proof or lacks a failure capability route. A central component can still be correct while
that omitted ingress makes the shipped composition incomplete.

## Assigned proof

- Rung: R3, deterministic structural analysis.
- Existing artifacts: `CompensationTopology.Build`, `TopologyClosureInspector`, AGWF045,
  topology-semantics cases at `WorkflowBindingTopologySemanticsTests.cs:403` through `:772`, and
  emitter route examples.
- Missing artifact: machine-readable executable-node/ingress policy consumed by an exhaustive
  route-to-topology closure guard.
- Current disposition: Unproven. Tests exist, but a policy-data closure guard and route mutation are
  not yet protected/bound.

## Refutation attempts for Stage 3

1. Remove confidence-handler occurrence construction.
2. Remove terminal approval or diagnostic-fork failure ingress.
3. Weaken occurrence key to phase/CLR type.
4. Compare policy inventory to every emitted handler family and require missing edges to fail.

## Open questions

- Does the permanent guard read an explicit accepted-route inventory, or duplicate a prose list in a
  test that can drift with implementation?

## Investigation Log

### Is topology itself the only authority?

- Read: topology model and Stage 1 authority/production traces.
- Found: proof and compensation emitter share topology, but many independent emitters generate ingress.
- Conclusion: centralization reduces risk but does not prove integration completeness; guard remains owed.

## Stage 3 disposition

PF-7 requires the proof record to distinguish the existing examples from the nonexistent exhaustive
policy guard. COV-03 owns the separate post-completion authority transition.

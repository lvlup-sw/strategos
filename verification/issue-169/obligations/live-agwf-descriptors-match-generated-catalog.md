# live-agwf-descriptors-match-generated-catalog

## Why this obligation exists

Stage 3 PF-2 confirmed that TypeSpec generates the public AGWF representations, while
`WorkflowDiagnostics` remains hand-authored. Generation cannot make the live Roslyn descriptor
metadata agree with the generated catalog.

## Failure scenario and boundary

A live AGWF044/045 descriptor keeps its ID but changes severity, title, message, or required
`NotConfigurable` enforcement, or a generated entry has no live descriptor. Schemas and Markdown
remain internally generated and green while consumers observe different analyzer behavior.

## Assigned proof

- Rung: R4, provider/consumer contract test.
- Existing artifact: `AgwfCatalogParityTests`, which resolves each generated catalog entry and
  compares ID, severity, title, and message format; `ProofOutcomeDiagnostics_AreNotConfigurable`
  requires fail-closed AGWF041 through AGWF045 to resist `NoWarn`/severity downgrades.
- Proposed kill: alter/remove one live severity/title/message entry and require a specific parity
  failure.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` passed Generators.Tests
  1,915/1,915 after the independent-review P1 added AGWF044/045 `NotConfigurable`;
  final-revision/protected binding and live-root mutation sensitivity are absent.

## Stage 3 disposition

The original generated-diagnostic row is now the narrow R1 authority. This file owns only the
independent live-descriptor root.

## Investigation Log

No unresolved question.

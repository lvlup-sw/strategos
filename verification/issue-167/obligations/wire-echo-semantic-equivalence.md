# wire-echo-semantic-equivalence — Duplicate wire occurrences are lossless projection echoes

## Origin

The Stage 3 wire refutation attacked `WireToModelBridge` with the same stable step ID represented
once in the top-level step list and once in a fork path. The original echo rule compared only a
weaker identity. That allowed list order to select one of two contradictory runtime/proof carriers.

## Discriminating reproduction

- Before stable-id/action comparison, different stable IDs and action tuples could be treated as an
  echo.
- After that first repair, seven of twelve focused cases still passed incorrectly: compensation,
  retry, timeout, confidence, instance, terminal/runtime, and gate differences were discarded.
- The final rule accepts only one top-level plus one fork-path representation whose complete parsed
  DTO graphs have equal structural fingerprints. All conflicting cases emit AGWF042 and no saga;
  an exact fully configured echo remains legal.

## Proof and guard

`ImportIdentityGateTests` is the production-path regression. `WireStepFingerprintCoverageTests`
discovers every DTO type reachable from each `StepDefinition` arm, mutates every declared public
property, and requires `CreateWireStepFingerprint` to change. This turns a future DTO field omission
into an immediate red test instead of relying on a manually synchronized property list.

Historical pre-commit observation at product fingerprint
`515b3a3bbe878dfe574ab495ead7041d1fa51c0653ea07f393d3b95002905b1c`:

- generator-test Release build: succeeded, 0 warnings, 0 errors;
- `ImportIdentityGateTests`: 13 passed;
- `WireStepFingerprintCoverageTests`: 1 passed; and
- `MinimalJsonReaderTests`: 14 passed.

These discovery results are preserved as history. They were superseded by the complete local run in
`../final-evidence.md`, which passed at exact-current commit `98fabb4` together with bound package
digests. The obligation remains `Indeterminate` because protected execution and review have not yet
been recorded.

## Residual boundary

The dependency-free reader's general duplicate-JSON-member policy is last-member-wins, matching the
current parser model. That policy is distinct from two separately represented occurrences with the
same stable step ID and is not promoted into this obligation.

### Investigation Log

#### Can two equal action identities still hide different occurrence behavior?

- Read: all public properties on `StepDefinition` arms and their nested action, configuration,
  retry, compensation, confidence, validation, and handler DTOs; identity collection and bridge
  composition; the parser depth control.
- Found: the weaker first repair allowed seven concrete configuration/runtime conflicts.
- Action: replaced the manual partial key with deterministic structural traversal and added a
  reflective property-mutation kill test.
- Conclusion: the concrete class is locally supported at exact-current `98fabb4`; protected binding
  and review are still pending.

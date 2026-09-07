# generated-nonblank-validation-semantic — Generated nonblank validation is semantic

| | |
|---|---|
| **Claim** | Every schema property newly projected by `RecordEmitter` as the exact nonblank pattern rejects blank values on JSON read and write, while optional null/absence remains legal and unrelated patterns do not acquire this validator. |
| **Scope** | `RecordEmitter` recognition of the exact direct-property pattern `.*\S.*`, generated `IJsonOnDeserialized`/`IJsonOnSerializing` callbacks, `ActionReferenceV1`, `WorkflowDefinitionV1.Name`, and unrelated-regex/scalar-alias boundaries. |
| **Consequence** | Generated C# can accept wire values the schema rejects, reject previously valid optional values, or silently introduce a broader breaking behavior across old contract types. Regeneration remains green because expected output is produced by the same faulty emitter. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Table-driven emitter/serialization tests covering required and optional string properties, exact/nonmatching patterns, read and write, union arms, and at least one previously existing regenerated contract; mutate pattern recognition and callback emission to prove each vector can fail. |
| **Why not cheaper** | Rung 1 clean regeneration only establishes self-consistency between the changed emitter and committed output. Rung 2 sees generated interfaces but not JSON callback semantics. Rung 3 can inspect emitted text/schema keywords, not execute serializer lifecycle behavior. |
| **Failure signal** | Downstream `JsonException` or validator rejection, often only when a particular legacy payload is serialized. There is no central production alert and a regeneration pass cannot distinguish correct from consistently wrong generation. |
| **Rollback** | Revert the generic emitter behavior and version any intentionally stricter contract, or constrain validation to the new contract until compatible coverage exists. Re-running codegen is not a rollback. |
| **Lenses** | False-green shapes: circular oracle, cannot-fail regeneration, collateral subject. |

**Confidence:** High.

**Current proof posture:** Indeterminate. At exact-current commit `98fabb4`, `ActionReferenceV1` and `WorkflowDefinitionV1.Name` receive exact-pattern callbacks, and their tests exercise missing, empty, and whitespace behavior on read and write; Contracts passed 185/185 and regeneration was stable. `RecordEmitter_NonWhitespaceBoundary_IsExact` independently synthesizes required and optional exact-pattern properties, an unrelated regex, and a scalar alias, then compiles and executes the emitted consumer to prove the generic boundary; it passed in the exact-current 1,761/1,761 generator portfolio. Protected execution and review remain pending.

**Open questions:**

- None. The workflow-name mismatch and exact-current local portfolio are closed; protected execution and review remain.

### Investigation Log

#### Do regeneration and current tests prove the generic emitter behavior rather than one generated result?

- Read: the full `RecordEmitter` diff, schema classification and callback emission, generated output inventory, `StepDefinitionSchemaTests`, `WorkflowIrRootTests`, and the synthetic emitter-boundary fixture.
- Found: exact direct-property pattern matching drives callbacks on `ActionReferenceV1` and, after the `98fabb4` repair, `WorkflowDefinitionV1.Name`; both have read/write behavior tests. Unrelated regexes and scalar-alias constraints remain nonmatches. The synthetic fixture independently exercises required/optional exact matches and both nonmatch classes through compiled emitted code; it passed at exact-current `98fabb4`.
- Not found: protected execution or completed review bound to `98fabb4`.
- Conclusion: the prior generic-collateral concern is closed by the committed proof design and exact-current local execution, while the formal verdict remains `Indeterminate` pending protected binding.

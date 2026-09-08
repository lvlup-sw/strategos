# binding-proof-fixtures-compile-intended-subject — Binding proof fixtures compile the intended subject

| | |
|---|---|
| **Claim** | Every #167 generator or parser proof runs over compiler-valid authored input, observes the generator's updated compilation, and rejects unrelated Error diagnostics instead of proving a selected diagnostic on an invalid subject. |
| **Scope** | `GeneratorTestHelper`, `ParserTestHelper`, the workflow-binding proof suites, and the C#/JSON import harnesses. |
| **Consequence** | A typo, missing reference, broken generated `.g.cs`, or unrelated generator failure can coexist with the expected AGWF code or syntax-tree assertion; CI appears green while a real consumer cannot compile. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | One shared real-generator harness that rejects input compiler errors, generator-driver errors outside the expected set, and updated-compilation errors; parser helpers must reject invalid compilations before creating semantic models. Add kill fixtures for each channel. |
| **Why not cheaper** | Rung 1 cannot help because the harness and fixtures are hand-authored. Rung 2 only helps after the harness asks Roslyn for both input and output diagnostics; merely constructing a `Compilation` does not fail a test. Rung 3 can inventory calls but cannot establish the generator run and diagnostic partition. |
| **Failure signal** | In production, the consumer compiler reports C# errors or a generator exception. That channel does not distinguish a product defect from an invalid consumer source, and the current in-memory proofs can suppress it entirely. |
| **Rollback** | Revert the #167 generator/extractor slice or disable typed workflow binding until the harness is authoritative. Re-running only the filtered AGWF assertions is not a rollback. |
| **Lenses** | False-green shapes: wrong subject, unrelated error, cannot-fail. |

**Confidence:** High.

**Current proof posture:** Indeterminate, locally supported at exact-current `98fabb4`. `RunGeneratorWithValidInput` rejects authored-input, driver/generator, and updated-compilation errors, and four helper self-tests kill those channels. Validated parser entry points reject invalid source before semantic-model use. All raw import runners and approval-continuation parser calls use the validated paths; the six directly affected suites passed 100/100 and the complete generator portfolio passed 1,761/1,761. Protected execution and review remain pending.

**Open questions:**

- None. The remaining question is protected evidence binding, not the harness design.

### Investigation Log

#### Do the #167 proof fixtures prove a compilable input and output rather than selected syntax or diagnostics?

- Read: `GeneratorTestHelper.RunGeneratorWithValidInput`, `RunGenerator<TGenerator>`, and `GetCompilationDiagnostics`; all `ParserTestHelper` compilation entry points; local runners in `ImportedWorkflowBindingProofTests`, `ImportFrontEndRobustnessTests`, `ImportIdentityGateTests`, and `ImportRejectionTests`; approval extraction call sites; and the proof-suite diagnostic filters.
- Found: the shared generator helper asserts authored input, driver/generator diagnostics, and updated compilation, with four self-tests. Validated parser helpers and an invalid-source kill exist. The previously raw import runners and approval-continuation calls route through those helpers, use exclusive expected-error assertions, and passed the complete exact-current generator portfolio.
- Not found: protected execution or completed review bound to `98fabb4`.
- Conclusion: the former harness violation is closed in committed, exact-current locally exercised code, but remains `Indeterminate` until protected execution and review.

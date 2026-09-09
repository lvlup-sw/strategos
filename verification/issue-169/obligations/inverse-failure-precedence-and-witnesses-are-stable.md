# inverse-failure-precedence-and-witnesses-are-stable

## Why this obligation exists

COV-02 found that semantic obligations and generated metadata did not own multi-fault precedence,
diagnostic ownership/count, or normalized witnesses. PF-8 independently found no neutral corpus
across runtime, AONT216, and AGWF044.

## Failure scenario and boundary

The same invalid inverse becomes Invalid, Refuted, or Opaque depending on front end/source order;
AGWF044 and AGWF045 both fire for one root cause; counterexamples reorder between runs.

## Assigned proof

- Rung: R4, shared provider/consumer contract tests.
- Proposed artifact: checked-in policy corpus with multi-fault inputs and expected status,
  obligations, diagnostic owner/count, and normalized witness, executed through all adapters.
- Current disposition: Unproven; examples exist independently, the corpus does not.

## Investigation Log

### Where will the neutral decision table live?

- Read: shared analyzer proof sources, all three inverse orchestrators, guard register, PF-8, and
  COV-02.
- Found: closed enums and independent tests; the guard register calls the corpus missing.
- Not found: a checked-in neutral vector file consumed by all adapters.
- Conclusion: `(needs human input)` on format/location; keep the artifact Proposed.

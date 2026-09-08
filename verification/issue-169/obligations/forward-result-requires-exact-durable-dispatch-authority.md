# forward-result-requires-exact-durable-dispatch-authority

## Why this obligation exists

Generated start handlers mint a nonempty execution ID and `ForwardDispatchClaim` before yielding the
worker. `SagaCompensationComponentEmitter.cs:1001` emits structural claim validation and `:1034`
exact lookup; completion/failure consumes the claim. The persistent-order clause additionally depends
on Wolverine/Marten session/outbox semantics.

## Failure scenario and boundary

A topology-shaped forged result or stale retry creates a journal entry without a dispatched worker.
More subtly, a worker observes its command before the claim commits and returns a result that the
recovered saga cannot authenticate.

## Assigned proof

- Rung: R5, production-path integration.
- R4 backstops: exact nonempty identity at `DerivedCompensationRuntimeTests.cs:2125`, different stable
  occurrence at `:2212`, never-dispatched trigger at `:2091`, null/corrupt claims at `:2295`.
- Missing R5 artifact: fault-injected commit/visibility/recovery fixture on the supported host.
- Current disposition: Unproven. `mutation-evidence.md` records a local kill on historical precursor
  `42b4ed7` when stable occurrence comparison was weakened. Exact-final mutation and source
  order/in-memory execution still do not establish the R5 durability clause or protected binding.

## Refutation attempts for Stage 3

1. Remove exact occurrence-key or execution-ID comparison.
2. Return a worker command before adding the dispatch claim.
3. Crash between mutation and dispatch under supported transaction mode and exhibit recovery.

## Open questions

- Which Wolverine/Marten transactional mode guarantees claim persistence before command visibility?
  `(partial: source ordering and one supported host exist; crash-window behavior is not exhibited)`

## Investigation Log

### Does emitted statement order prove durability?

- Read: start emitter, claim helpers, host fixture, Stage 1 production path.
- Found: mutation precedes yield in generated source.
- Not found: a crash-window fixture or primary host-semantics evidence bound to this revision.
- Conclusion: R5 clause stays Unproven; R4 can prove only exact handler authority.

## Stage 3 disposition

PF-7 marks the crash-window host artifact Missing rather than implying it exists. COV-03 owns the
distinct reducer/journal/post-completion-failure transition across all producers.

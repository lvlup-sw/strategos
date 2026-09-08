# executable-inverse-effects-remain-a-consumer-trust-boundary

## Why this obligation exists

The wildcard survey found a discriminating example: the packed legal consumer declares ontology
stage transitions and exact inverse contracts, while its CLR `ProbeStep.ExecuteAsync` methods return
an unrelated `FlowState` unchanged. AGWF044 correctly compiles the legal declaration and rejects a
contradictory declaration. Static proof therefore concerns descriptors, not arbitrary method bodies.

## Failure scenario and boundary

Release/PR language says “the inverse is proved” without naming the unary contract region. A consumer
then treats a green compile as proof of concrete pre-state/event-history reversal, a refund/deletion,
or exactly-once execution despite explicit at-least-once delivery.

## Assigned proof

- Rung: R6, human judgment.
- Existing candidate corpus: precise contract-set docs/changelog wording; non-singleton and event-frame
  counterexamples; packed no-op declaration versus real `DerivedCompensationWorkflow` body; stable
  rollback identity/idempotency guidance.
- Current disposition: Unproven. The exact-final wording correction and explicit rollback identity are
  present, but the final semantic review is not bound.

## Refutation attempts for Stage 3

1. Read every use of “prove/proved inverse” and classify its subject.
2. Change only packed CLR body while preserving descriptors; analyzer outcome should not change.
3. Change only ontology inverse guarantee; AGWF044 must change despite no-op body.
4. Change the real UndoB returned stage; behavioral fixture must fail.

## Open questions

None. This is a deliberate proof boundary, not an omitted program-analysis feature promised by #169.

## Investigation Log

### Can Strategos prove arbitrary inverse effects?

- Read: analyzer inputs, package probe bodies, real behavioral workflow, public docs.
- Found: static identity maps CLR type to a declared action descriptor; no method-body/external-effect
  program logic exists. One real fixture proves one implementation only.
- Conclusion: universal correctness/idempotency remains a consumer obligation and must never be
  upgraded from Strategos-local evidence.

## Stage 3 disposition

W-1 adds a distinct retained limitation: even perfect implementations of the supported unary
contracts need only return to the forward requirement set, not the concrete pre-forward state. Exact
frame equality is a declared may-touch-set match, not an old/new round-trip law. W-2's explicit
step-visible rollback identity is a separate active obligation.

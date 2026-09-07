# configured-occurrence-extraction-complete — Configured occurrence extraction is complete

| | |
|---|---|
| **Claim** | Every public builder callback that accepts `IStepConfiguration<TState>` preserves exactly one names-only `WorkflowActionReference` into the proof IR, or fails closed for missing/dynamic/ambiguous authoring; no overload is inventoried without a behavioral extraction vector. |
| **Scope** | The 12 configured occurrence overloads, fluent builders, `StepExtractor`, approval/failure/loop/fork helpers, and `StepModel.Action`. |
| **Consequence** | A reachable runtime action is absent or misidentified in the static proof graph, causing silent under-proof, false AGWF040/042 rejection, or proof of a different occurrence. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | A table-driven real-parser/generator suite mechanically pairing every reflected callback signature with legal, absent, dynamic, duplicate, and nested-scope vectors; an added callback without its vector must fail the inventory test. |
| **Why not cheaper** | Rung 1 does not generate builders and extractors from one source. Rung 2 guarantees callback/action types but not Roslyn extraction. Rung 3 can enumerate public signatures, as the current reflection test does, but cannot establish identity and scope preservation through each syntax shape. |
| **Failure signal** | Often no direct signal: an unbound workflow still runs while static proof silently omits the occurrence. Some cases emit AGWF040/042, but absence of a diagnostic does not distinguish proof from analyzer skip. |
| **Rollback** | Remove or deprecate the uncovered overload from the #167 binding surface, or reject it explicitly until extraction is supported. Documentation alone does not reverse the runtime surface. |
| **Lenses** | False-green shapes: wrong subject, inventory-without-use, vacuous coverage. |

**Confidence:** High.

**Current proof posture:** Indeterminate, locally supported at exact-current `98fabb4`. `PublicConfiguredOccurrenceSurface_MatchesActionExtractionInventory` reflects and pins all 12 signatures; present behavioral vectors cover linear, branch, loop, fork, failure, confidence, join, and approval continuations. Transparent-receiver and structural-loop-path regressions kill the defects found during refutation. The extractor suite and all approval-continuation call sites use validated parser APIs, including an invalid-source kill. The affected suites and complete 1,761-test generator portfolio passed, but the signature inventory and behavior vectors remain separate hand-maintained authorities, so behavioral closure is convention-based. Protected execution and review remain pending.

**Open questions:**

- None. The desired forcing function can be derived from the same table used for the public-surface inventory.

### Investigation Log

#### Is public-surface inventory mechanically tied to extraction behavior?

- Read: all of `StepExtractorActionReferenceTests`, the configured approval extractor tests identified by the survey, the reflected interface list, and `ParserTestHelper`.
- Found: exact signature enumeration and substantial behavioral examples, but no data structure mechanically maps each reflected signature to a vector. Validated parser entry points reject invalid compilations; the remaining approval paths use them and passed the complete exact-current generator portfolio.
- Found: phase/display names are not injective encodings of loop ancestry. `InvocationExpression.SpanStart` is also shared by chained receivers, while `ArgumentList.SpanStart` uniquely identifies the individual `RepeatUntil` call. `SiblingLoop_WithPrefixedName_StillChecksIngress` and `PrefixedSiblingLoop_StartingWithNestedLoop_StillChecksNestedIngress` establish both the direct-sibling and nested-prefix cases.
- Found: invocation-chain walking must strip transparent parenthesized syntax before following a receiver. `WalkChain_ParenthesizedReceiver_ReturnsEarlierInvocations`, `ParenthesizedMainChainReceiver_DoesNotHideEntryOccurrence`, and `BranchPaths_WithParenthesizedChain_AreProved` bind helper, occurrence, and semantic behavior.
- Not found: a kill fixture that adds/removes an extraction case independently of the hand list, protected execution, or completed review bound to final subject `98fabb4`.
- Conclusion: surface and subject closure are committed and exact-current locally exercised, while behavioral closure is still convention-based; the current verdict remains `Indeterminate`.

---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, and documentation surface against merge-base 45c86a63a9437abd920240b4dc95b235c0f72d37
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative acceptance criteria for typed workflow binding
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: immediate downstream consumer of the workflow/action contract added here
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: merged typed action-calculus contract that this change consumes
---

# Stage 1 survey — intent and claims

## Disposition and reading rule

This is a **historical claim inventory** for the Intent and Claims lens only. Every quotation below is an
assertion to investigate; none is promoted here to a fact, a finding, or a correctness
obligation. In particular, documentation, comments, test names, a closed issue, and a green check
would all remain claims until evidence at the analyzed subject exhibits the behavior.

At inventory time, the analyzed subject was an uncommitted working tree whose HEAD was the base
revision, so no change-specific commit or PR/review prose was available. The frontmatter now names
current immutable subject `98fabb4`, but quotations and present-tense metadata below retain their
historical discovery meaning. `final-evidence.md` records the complete exact-current local evidence;
protected CI and review remain pending.

Issue standing is relevant but not dispositive:

- At inventory time, issue #167 was open, authored by maintainer Reed Salus, and had no comments as of
  2026-08-22. It was the strongest statement of requested behavior but supplied no independent
  confirmation. A later producer-linkage comment now exists; it is coordination evidence, not product
  proof.
- Issue #168 is closed, was authored by the same maintainer, and has one same-author maintainer
  comment sharpening its wildcard claim. Closure raises its standing as design context but does
  not exhibit this working tree's behavior.
- Issue #169 is open, was authored by the same maintainer, and has no comments. It is downstream
  intent rather than acceptance evidence for #167.
- The v2.13.0 milestone description is also authored project metadata. It claims the ordering
  “#168 → #167 → #169” and describes the work as “a redesign ... not a refactor”; it is not
  implementation evidence.

## Corpus read

The survey read the scope answer and stage-0 scope record; the complete then-current diff from
45c86a63a9437abd920240b4dc95b235c0f72d37; all then-untracked #167 source and test files; both
changelogs; every changed documentation page; all added and changed source comments; TypeSpec,
generated C#, JSON Schema, public-API baselines, project files, and the builder API gate; and the
full bodies, comments, and metadata of GitHub issues #167, #168, and #169. The mechanism survey was
read only as a navigation aid; none of its conclusions is imported as fact here. No tests, builds,
benchmarks, or reproductions were run by this lens.

## Claim inventory seed

### IC-SCOPE-001 — the user's requested slice and process boundary

Source: verification/issue-167/stage0.md:18-21, copied there word for word from the user.

> Implement the next slice #167 and #169. Use [$verify-code](/home/reedsalus/.agents/skills/verify-code/SKILL.md) to review your changes. After applying any fixes, open PRs. You may do a single round of review with coderabbitai before pressing for my final authorization to merge.

Claims to check or preserve:

- both #167 and #169 are requested, with #167 verified as the first stacked diff;
- verify-code review and fixes precede PR creation;
- one CodeRabbit round is permitted;
- PR creation is authorized, but merge still requires final user authorization.

The last two bullets are process interpretations of the exact wording, not product claims.

### IC-STAGE0-001 — scope and delivery claims added by Stage 0

Source: verification/issue-167/stage0.md:23-66.

Exact extracts:

> This run verifies the #167 diff first. Issue #169 is the next stacked diff and receives its own verification run. No merge is authorized by this instruction.

> High. The diff changes published and generated contracts, compiler diagnostics, a source generator that rejects consumer builds, graph serialization identity, and public runtime APIs. Its reverse dependency closure reaches the separately maintained Exarchos and Basileus contract consumers.

> The new workflow-step wire member is optional and omitted when absent.

These are run-scoping and expected-compatibility claims made by the verification setup. Stage 2
must not treat them as established merely because they appear in Stage 0.

## Authoritative issue #167 claims

### IC-167-001 — issue title and reported defect

Source: issue #167 title and “Overview” at
https://github.com/lvlup-sw/strategos/issues/167.

> Type the workflow binding — a bound saga is never checked against the action's declared postcondition

> ActionBindingType already has a Workflow member, and ActionBuilder.BoundToWorkflow(...) already sets both. So the join between the ontology's action model and the workflow runtime exists — it is just untyped, and nothing checks it.

> Two things are unchecked today:
>
> 1. The name resolves to nothing. BoundWorkflowName is a string. It is hashed into the graph version (OntologyGraphHasher.cs:198) and otherwise carried. No build step confirms a workflow of that name exists, let alone that it is the right one.
> 2. The saga is never checked against the action it implements. An action declares Postconditions. The bound workflow lowers to a Wolverine+Marten saga. Nothing verifies that running the saga establishes what the action promised.

This is a reported-defect claim. The ordinary competing explanation is that the issue describes an
older revision or overstates the missing check; it requires a base-revision production-path trace
or reproduction before it can be used as a factual premise.

### IC-167-002 — closure purpose

Source: issue #167, “Why this is the closure property.”

> Under the action calculus a workflow is an action — it carries requires, ensures, needs and touches like any leaf, because it was composed from leaves by ;, ∥ and ⁻¹. Closure is the property that makes the model an algebra rather than a vocabulary: the composite has the same shape as its parts, so it can be composed again.

> BoundWorkflowName : string is precisely where closure is currently broken. The library anticipated the join and left it as a string.

These are conceptual and problem-framing claims. “A workflow is an action” is broader than merely
checking a named workflow binding and should be tested against the delivered composite surface.

### IC-167-003 — binding-site refinement law

Source: issue #167, “Why this is the closure property.”

~~~text
ensures(saga)  ⟹  ensures(action)
requires(action) ⟹ requires(saga)
needs(saga) ⊑ needs(action)
~~~

> That is the refinement law at the binding site — the same rule as Liskov/Wing behavioral subtyping (1994), which is why it does not need to be invented, only applied.

The three formulae and the prior-art equivalence are separate claims. The issue later says the
needs clause is deferred until a capability lattice exists; that caveat is part of the inventory.

### IC-167-004 — requested implementation tasks

Source: issue #167, “Tasks.”

> - Replace BoundWorkflowName : string? with a typed reference that resolves against the workflow catalog at build time (Strategos.Contracts already carries WorkflowRef and WorkflowCatalog)
> - Emit a diagnostic when a bound workflow name does not resolve
> - Derive the saga's ensures from its steps' postconditions and check it implies the action's declared Postconditions
> - Check the refinement direction on requires (the action's precondition must be strong enough for the saga's first step)
> - Keep the string form as the wire/serialized representation — this is a compile-time typing change, not a contract break

This is the primary delivery claim set. The capitalized legacy “Postconditions” wording predates
#168's explicit guarantees and may have evolved; Stage 2 must state which present-day semantic
object discharges it rather than silently substituting terminology.

### IC-167-005 — dependencies and deferred capability clause

Source: issue #167, “Dependencies and ordering.”

> Needs the seam check to mean anything — see the predicate-fragment issue. Without a decidable implication test, “the saga establishes the postcondition” is not a machine-settleable claim.

> Independent of needs/touches, but the needs(saga) ⊑ needs(action) clause only lands once the capability lattice (#165) exists.

This claims that #168's implication capability is necessary and that the needs clause is explicitly
deferred. It does not itself establish that #168's implementation is present or suitable.

### IC-167-006 — acceptance criteria and enforcement class

Source: issue #167, “Acceptance criteria.”

> - An action bound to a non-existent workflow fails the build
> - An action whose bound saga does not establish its declared postcondition fails the build
> - Existing BoundToWorkflow("name") call sites keep compiling
> - The serialized graph hash is unchanged for an unchanged binding

> Enforcement class: machine-checked (source generator / analyzer at build time).

Each bullet is an independent guarantee claim. “Unchanged binding” needs a precisely fixed before
and after subject because #168 intentionally rolls action-bearing graph hashes once.

## Related issue and milestone claims

### IC-168-001 — prerequisite seam semantics

Source: issue #168, “Overview” and “Proposal.”

~~~text
A ; B is legal  ⟺  ensures(A) ⟹ requires(B)
~~~

> Decidability here is a design choice, not a hope.

> An action with an opaque precondition keeps working at runtime and drops out of every composition check. The trade is visible per action rather than hidden in the engine.

> Record the exclusions beside the grammar ... arithmetic over more than one term, quantification, and transitive closure beyond the declared link rewrites.

These claims define what #167 is expected to reuse. They do not show that this branch uses exactly
the same predicate grammar, solver, counterexample semantics, or opacity rule.

### IC-168-002 — prerequisite acceptance claims

Source: issue #168, “Acceptance criteria.”

> - ensures(A) ⟹ requires(B) is decidable and terminates for every pair in the closed fragment
> - An illegal seam is a build-time diagnostic naming both actions
> - An action carrying Custom is excluded from composition checks and says so
> - No precondition kind reaches a permissive wildcard by default; an unevaluable predicate is handled by a stated rule, not by _ => true
> - The excluded grammar is documented, not discovered

Issue #168 is closed, but these remain external claims until the merged implementation is bound to
the exact revision and exercised. The same-author comment additionally claims that the former
wildcard published a Custom-only action as valid unconditionally; that is historical context, not
evidence about the #167 diff.

### IC-169-001 — downstream inverse laws

Source: issue #169, “What the calculus says instead.”

~~~text
requires(A⁻¹) = ensures(A)
ensures(A⁻¹)  = requires(A)
needs(A⁻¹)    = needs(A)
touches(A⁻¹)  = touches(A)

(A ; B)⁻¹ = B⁻¹ ; A⁻¹
(A ∥ B)⁻¹ = A⁻¹ ∥ B⁻¹

failure at C in A ; B ; C  ⇒  rollback plan is B⁻¹ ; A⁻¹
~~~

> Given a forward graph and a point of failure there is exactly one rollback plan, and it is computed rather than remembered.

These are downstream contract expectations. The current #167 proof's decision to reject
Compensate<T> rather than model it is meaningful only relative to these claims.

### IC-169-002 — downstream propagation, nesting, and authored-inverse claims

Source: issue #169, “Three properties that do not exist today” and “Acceptance criteria.”

> A composite is compensable exactly when every part of it is. One non-compensable leaf makes every composite containing it non-compensable, and that travels upward on its own.

> A failure inside one rolls back the completed prefix of that scope, never the whole program, and an inner scope unwinds before the scope enclosing it.

> Where a compensating step is authored ... the derived inverse and the authored one can be compared, and a disagreement is a diagnostic rather than a production surprise.

> - A composite containing one non-compensable leaf cannot be declared rollback-safe
> - The rollback plan for a failed prefix is computed, not authored
> - An authored compensation contradicting the derived inverse is a diagnostic
> - An inner scope's failure does not unwind its enclosing scope

### IC-MILESTONE-001 — ordering and character of the program

Source: v2.13.0 milestone metadata attached to issues #167, #168, and #169.

> The action calculus — the operators. Earns composition on top of the object: the decidable predicate fragment and the seam check (#168), the typed workflow binding (#167), and derived compensation (#169). Ordered #168 → #167 → #169, and #164 gates #169. Gated: not to be started before v2.12.0 completes the object. This is a redesign of the action model and the workflow binding, not a refactor — see #172 for the honest cost and the failure mode.

## Claims added to the product changelog

### IC-CHANGELOG-001 — public API/source compatibility

Source: CHANGELOG.md:18-25.

> Workflow builder surface (#167). IStepConfiguration<TState> adds Performs(WorkflowActionReference); IForkJoinBuilder<TState> and ILoopForkJoinBuilder<TState> add Join<TStep>(configure); and the approval rejection and escalation builders add Then<TStep>(configure). Existing fluent call sites remain source-compatible, but external implementations of these interfaces and the exarchos builder-surface mirror must adopt the added members. The new signatures are staged in PublicAPI.Unshipped.txt until the release process rolls them into the shipped baseline.

This makes three distinct claims: call-site source compatibility, implementer source breakage, and
baseline staging.

### IC-CHANGELOG-002 — occurrence identity propagation

Source: CHANGELOG.md:29-33.

> Occurrence-scoped workflow action identity (#167). Typed workflow-step occurrences can declare .Performs(new WorkflowActionReference(domainName, objectTypeName, actionName)). The immutable name-only reference survives every fluent topology, source-generator extraction, wire projection, and JSON import; missing declarations remain distinct from dynamic or invalid declarations.

“Every fluent topology” is deliberately retained as written. Later documentation enumerates
topologies that are rejected or excluded, so the intended domain of “every” is open.

### IC-CHANGELOG-003 — workflow implementation proof

Source: CHANGELOG.md:34-41.

> Proved workflow implementations (#167). WorkflowBindingReference gives workflow-bound ontology actions an immutable catalog identity. The generator resolves each reachable occurrence against the ontology action catalog and proves entry contravariance, internal seams, successful-exit covariance, subject equality, frame and authority bounds, and fork noninterference. ActionCalculus.AnalyzeRefinement exposes the same behavioral-subtyping rules for runtime action and composite contracts. AGWF039–AGWF042 reject missing, ambiguous, refuted, opaque, or otherwise unprovable bindings.

This is the broadest delivery claim in the diff. “Same ... rules” might mean the same formulae, the
same proof kernel, or full behavioral parity; the claim does not say which.

### IC-CHANGELOG-004 — typed binding and graph-hash compatibility

Source: CHANGELOG.md:59-63.

> Workflow descriptor bindings are typed. The writable ActionDescriptor.BoundWorkflowName property is replaced by immutable BoundWorkflow: WorkflowBindingReference. The fluent BoundToWorkflow(string) overload remains supported and constructs the typed reference, preserving its exact ordinal identifier and graph-hash bytes.

### IC-CHANGELOG-005 — contracts 0.11 wire compatibility and consumer ordering

Source: CHANGELOG.md:64-68.

> Contracts package 0.11.0. Workflow step arms add the optional, backward-compatible ActionReferenceV1 wire field. Its three identity names are required non-empty strings when the field is present. The closed AGWF vocabulary also adds AGWF039–AGWF042; contract consumers must upgrade before producers emit those diagnostics.

“Backward-compatible” is a wire-shape claim. The same text also admits an ordered rollout
requirement for generated closed-enum consumers; those two compatibility dimensions must remain
separate.

## Claims added to the migration guide

### IC-MIGRATION-001 — workflow catalog identity semantics

Source: docs/src/content/docs/guide/ontology/migration-v2-13.md:265-281.

> Workflow-bound actions now carry an immutable WorkflowBindingReference instead of a writable workflow-name string.

> WorkflowBindingReference.WorkflowId is the exact ordinal name used to look up the workflow catalog. The constructor rejects null, empty, or whitespace-only identifiers and otherwise preserves the string as supplied; it does not trim or case-normalize it. The existing .BoundToWorkflow("publish-position") overload remains supported and constructs the same typed reference.

### IC-MIGRATION-002 — per-occurrence identity and analyzer-visible form

Source: docs/src/content/docs/guide/ontology/migration-v2-13.md:302-326.

> Then identify the ontology action performed by every reachable named step occurrence in that workflow. WorkflowActionReference contains the ontology domain, object type, and action names; .Performs(...) attaches it to one use of a step, not to the CLR step type.

> The same CLR step type may therefore name different actions at different occurrences, including occurrences inside branch, loop, fork, failure, and low-confidence paths. Declare .Performs(...) at most once per occurrence. All three identity names reject null, empty, or whitespace-only values.

> For static workflow-binding proof, use a direct new WorkflowActionReference(...) whose three arguments are compile-time constant strings. Factories, mutable locals, dynamic expressions, and delegate steps cannot provide the closed identity required by the analyzer. Imported workflow definitions may carry the equivalent structured action reference.

### IC-MIGRATION-003 — behavioral subtype obligations

Source: docs/src/content/docs/guide/ontology/migration-v2-13.md:328-341.

> The workflow must be a behavioral subtype of the bound action:

> Requirements (contravariant): requires(bound action) implies requires(workflow entry)

> Guarantees (covariant): Every successful workflow exit implies ensures(bound action)

> Frame: The union of leaf-action writes is a subset of the bound action's frame

> Authority: The join of leaf-action requirements is no stronger than the bound action's authority limit

> Every leaf action must have the bound action's subject, every internal workflow seam must compose, and parallel paths must not have write/write or write/predicate-read interference. A closed counterexample is a definite refinement failure; a custom predicate or dynamic contract is not treated as a successful proof.

### IC-MIGRATION-004 — topology closure, exclusions, and no-runtime-change claim

Source: docs/src/content/docs/guide/ontology/migration-v2-13.md:343-354.

> The proof also requires a statically closed workflow topology. Keep branch cases, loop bodies and names, fork paths, approval handlers, confidence handlers, and failure handlers inline and analyzer-visible. Dynamic callback or collection helpers fail with AGWF042 instead of being silently omitted. Declare at most one OnRejection and one OnTimeout callback per approval; duplicates also fail closed because the runtime builder uses last-wins semantics. Nonterminal workflow OnFailure, fork-path OnFailure, and nested EscalateTo approval routing also remain outside the proved v2.13 subset because their runtime lowering is not yet represented by the closed proof graph. Steps configured with Compensate<T> likewise report AGWF042; rollback-path refinement is deferred to #169. Unbound workflows retain their existing runtime behavior.

This contains both fail-closed proof claims and an explicit no-behavior-change claim for unbound
workflows.

### IC-MIGRATION-005 — diagnostic semantics

Source: docs/src/content/docs/guide/ontology/migration-v2-13.md:356-363.

> The new workflow diagnostics all have error severity and fail closed.

> AGWF039: The exact ordinal WorkflowId resolves to zero or multiple C# or imported workflows.

> AGWF040: A reachable step has a missing, dynamic, malformed, duplicate, or unresolved action reference.

> AGWF041: The closed workflow contract definitely fails a refinement, seam, subject, frame, authority, or fork-isolation obligation.

> AGWF042: The analyzer cannot construct a closed proof because a binding, workflow topology, contract, authority lattice, or predicate is dynamic, invalid, opaque, contradictory, unsupported by runtime lowering, or otherwise unprovable.

The guide additionally claims a runtime check is not accepted as a substitute for binding proof.

### IC-MIGRATION-006 — wire addition and consumer compatibility

Source: docs/src/content/docs/guide/ontology/migration-v2-13.md:391-409.

> Version 0.11.0 also adds ActionReferenceV1 as the optional action property shared by every workflow step kind.

> When action is present, all three name fields are required. The property is additive and occurrence-scoped; legacy or unconfigured workflow JSON continues to omit it byte-for-byte. Consumers that need the new identity should upgrade to the generated 0.11.0 models before producers begin populating it. The same release adds AGWF039–AGWF042; consumers of the generated closed AgwfCode enum must upgrade before Strategos can emit those tokens.

### IC-MIGRATION-007 — exact scope of graph-hash preservation

Source: docs/src/content/docs/guide/ontology/migration-v2-13.md:411-433.

> Every existing action-bearing graph receives a different OntologyGraph.Version after this upgrade even when its apparent business meaning is unchanged. This is intentional.

> The typed workflow-binding wrapper is not itself part of that rollover. For an unchanged binding, the hasher writes only BoundWorkflow.WorkflowId at the same byte position where it previously wrote BoundWorkflowName. Moving from the string property or overload to new WorkflowBindingReference(sameId) therefore preserves the legacy graph hash. Changing the identifier still changes the hash, as a routing change should.

> After the action-contract rollover, registration order and presentation-only edits remain hash-stable.

This narrows issue #167's “serialized graph hash is unchanged” claim to a controlled wrapper-only
comparison after accounting for #168's independent rollover.

## Claims added to the action-calculus reference

### IC-CALCULUS-001 — exact refinement semantics

Source: docs/src/content/docs/reference/action-calculus.md:263-281.

> Behavioral refinement proves that one executable action or composite is a safe substitute for a declared action specification.

> Requirements are contravariant: R_S implies R_I.

> Guarantees are covariant: G_I implies D_S.

> Frame is bounded: W_I is a subset of W_S.

> Authority is bounded: Authority_I <= Authority_S pointwise.

> Subjects must also be equal. G_I is the implementation's effective guarantee, including facts soundly preserved through its frame; D_S is the specification's declared guarantee. ModifiesProperty remains only a frame declaration and cannot satisfy a guarantee obligation. CreatesLink does contribute its sound LinkExists fact.

### IC-CALCULUS-002 — runtime refinement status contract

Source: docs/src/content/docs/reference/action-calculus.md:283-308.

> ActionRefinementStatus.Proven is the only successful substitution result. Refuted carries concrete counterexamples or frame/authority failures; Opaque means a custom predicate prevented a complete proof; and Invalid means a contract was malformed, already refuted, or otherwise not decidable by the closed kernel.

### IC-CALCULUS-003 — typed binding and occurrence resolution

Source: docs/src/content/docs/reference/action-calculus.md:310-347.

> WorkflowId is preserved exactly and resolved with ordinal comparison. The constructor rejects null, empty, or whitespace-only values. The string overload .BoundToWorkflow("publish-position") remains source compatible and maps to the same typed reference.

> This identity belongs to the occurrence, not the CLR step type, so different uses of one step type may perform different ontology actions. All three names are required and ordinal. For analyzer-visible bindings, construct the reference directly from compile-time constant strings; factories, dynamic expressions, multiple declarations, and unnamed delegate steps do not provide a closed occurrence identity.

### IC-CALCULUS-004 — graph-wide proof claims

Source: docs/src/content/docs/reference/action-calculus.md:349-365.

> The workflow-binding analyzer generalizes refinement across the workflow graph:
>
> - the bound action requirement implies the entry action requirement;
> - every non-failure internal edge satisfies the ordinary effective-guarantee to next-requirement seam proof;
> - every successful completion's effective guarantee implies the bound action's declared guarantee;
> - every leaf has the bound action's subject, the union of leaf frames stays within the bound frame, and the pointwise join of leaf authorities stays at or below the bound authority; and
> - fork paths have neither write/write interference nor write/predicate-read interference.

> All reachable main, branch, loop, fork, approval, confidence, and failure-path step occurrences participate. Custom predicates and other dynamic contract inputs fail closed for workflow bindings even though ordinary sequential analysis can report them as partially verified.

### IC-CALCULUS-005 — graph grammar caveat

Source: docs/src/content/docs/reference/action-calculus.md:367-373.

> Proof is limited to topology that the generator can close without executing user code. Branch cases, loops, fork paths, approval and confidence handlers, and failure handlers must use analyzer-visible inline forms. Dynamic callback or collection helpers produce AGWF042. Nonterminal workflow failure handlers, fork-path failure handlers, and nested EscalateTo approvals are also excluded from the proved v2.13 subset until their runtime routes have an equivalent closed proof representation.

This must be reconciled with “all reachable ... failure-path step occurrences participate” and
“every fluent topology”; the text may intend “all reachable occurrences in accepted closed
topology,” but that qualification is not present in the broader claims.

### IC-CALCULUS-006 — wire behavior and diagnostic meanings

Source: docs/src/content/docs/reference/action-calculus.md:497-500 and 506-525.

> Version 0.11.0 also adds the optional, occurrence-scoped action field to every workflow step kind. Its ActionReferenceV1 value contains required domainName, objectTypeName, and actionName strings. Legacy workflow JSON continues to omit the optional field byte-for-byte.

> AGWF039: A bound workflow identity resolves to zero or multiple exact ordinal workflow definitions.

> AGWF040: A reachable workflow step occurrence has no single closed action reference, or its three-name identity does not resolve exactly once.

> AGWF041: A closed workflow binding is definitely refuted, with a counterexample or a subject/frame/authority/fork-isolation failure.

> AGWF042: A workflow binding cannot be proved because an identity, topology, contract, lattice, or predicate is dynamic, invalid, opaque, unsupported by runtime lowering, or otherwise unprovable.

> Graph freeze is the exact merged-graph backstop and reports coverage over concrete executable actions.

The last sentence partly concerns #168 rather than the workflow-binding analyzer, but it is a claim
in the changed reference and therefore remains in this inventory.

## Claims added to workflow API and diagnostic references

### IC-WORKFLOW-API-001 — value-object identity contract

Source: docs/src/content/docs/reference/api/workflow.md:86-103.

> WorkflowActionReference in Strategos.Definitions is the immutable, language-neutral identity of the ontology action performed by one workflow step occurrence.

> The constructor rejects null, empty, and whitespace-only components and otherwise preserves each ordinal string as supplied. It carries no CLR type, so the identity can cross the workflow contract boundary without coupling a consumer to the producer's runtime type system.

### IC-WORKFLOW-API-002 — Performs behavior

Source: docs/src/content/docs/reference/api/workflow.md:105-150.

> Performs returns the same configuration builder, so it chains with retry, timeout, compensation, and confidence configuration. A step occurrence may declare it only once; a null reference throws ArgumentNullException and a second declaration throws InvalidOperationException.

> Configured overloads are available wherever a class-based structural or handler step needs an occurrence identity, including top-level and loop fork joins plus approval rejection and timeout paths.

> The identity is occurrence-scoped. Reusing the same CLR step type in two fork, branch, loop, failure, or confidence-handler positions does not imply that both occurrences perform the same ontology action. Configure each occurrence independently. There is no type-level/default action attribute.

### IC-WORKFLOW-API-003 — bound-versus-unbound and static resolution behavior

Source: docs/src/content/docs/reference/api/workflow.md:152-165.

> StepDefinition.Action exposes the resulting nullable reference. Ordinary unbound workflows may leave it null. When an ontology action is bound to the workflow, however, every reachable named step occurrence must supply one closed reference so the generator can prove the workflow implementation against the action contract. Lambda/delegate steps do not expose Performs and therefore cannot serve as a proved leaf in a bound workflow.

> For compile-time proof, use a direct new WorkflowActionReference(domainName, objectTypeName, actionName) with compile-time constant strings. The generator resolves that exact ordinal three-name tuple against the ontology action catalog. Missing references, factories, dynamic expressions, blank names, multiple declarations, and zero or multiple catalog matches fail with AGWF040 rather than being accepted for runtime-only resolution.

### IC-WORKFLOW-API-004 — projection behavior

Source: docs/src/content/docs/reference/api/workflow.md:167-183.

> The Contracts 0.11.0 workflow schema projects the value as the optional occurrence-level action object on every step kind.

> All three fields are required when action is present. Legacy and unconfigured workflow JSON omits the additive field byte-for-byte.

### IC-DIAGNOSTICS-001 — build-time prevention claim

Source: docs/src/content/docs/reference/diagnostics/agwf-agsr.md:7 and 35-57.

> The AGWF (workflow) and AGSR (state reducer) diagnostics are emitted by the Strategos workflow source generator at compile time. They catch workflow definition errors before runtime ... and unsound ontology-to-workflow bindings.

> An ontology action bound through WorkflowBindingReference is accepted only when the generator can construct and prove a closed contract for the referenced workflow.

> The refinement rule is contravariant in requirements and covariant in guarantees ... Internal seams and parallel-path interference are also proved.

### IC-DIAGNOSTICS-002 — exact diagnostic partition

Source: docs/src/content/docs/reference/diagnostics/agwf-agsr.md:42-47.

> AGWF039 ... exact ordinal WorkflowId matches zero or multiple C# or imported workflow definitions.

> AGWF040 ... missing .Performs(...); ... blank, duplicated, dynamic, or otherwise malformed; or ... zero or multiple actions.

> AGWF041 ... All inputs are closed, and the solver finds a definite counterexample or a subject, frame, authority, or fork-isolation violation.

> AGWF042 ... dynamic, invalid, opaque, contradictory, unrealizable, unsupported by runtime lowering, or otherwise outside the closed proof fragment.

The partition between AGWF040 identity failures, AGWF041 definite refutations, and AGWF042
indeterminate/unprovable input is itself a behavioral claim.

## Graph-versioning claims

### IC-HASH-001 — hashed binding field and routing sensitivity

Source: docs/src/content/docs/reference/ontology/graph-versioning.md:19-42 and 57-67.

> Actions ... BoundWorkflow.WorkflowId ... [is included].

> Rebinding an action's BoundWorkflow.WorkflowId ... [changes the hash] (a dispatch-routing change).

### IC-HASH-002 — wrapper-only compatibility guarantee

Source: docs/src/content/docs/reference/ontology/graph-versioning.md:76-88.

> Strategos 2.13 replaces the descriptor's writable BoundWorkflowName string with an immutable WorkflowBindingReference. This API shape change does not add a field to the canonical byte stream: the hasher writes exactly BoundWorkflow.WorkflowId through the existing length-prefixed string slot.

> Consequently, changing only .BoundToWorkflow("publish-position") to .BoundToWorkflow(new WorkflowBindingReference("publish-position")) preserves the legacy graph hash. The string overload maps to the same reference, and the identifier is neither trimmed nor case-normalized. A different identifier still changes the hash because it changes dispatch routing.

### IC-HASH-003 — broader hash guarantees that frame #167

Source: docs/src/content/docs/reference/ontology/graph-versioning.md:7, 21, 69-74, and 90-100.

> OntologyGraph.Version is a deterministic SHA-256 hash over the structural fields of a frozen graph.

> Identical DSL input produces an identical hash across processes and machines.

> Reordering registration calls ... [does not change the hash].

> Two graphs with the same hash are guaranteed to share every hashed structural field; two graphs with different hashes differ in at least one such field.

> Strategos 2.13 intentionally changes the action canonicalization once ... Every existing action-bearing graph therefore receives a new version on its first 2.13 build.

These broader claims are part of the context needed to specify a sound pre/post comparison for
IC-167-006 and IC-HASH-002.

## Contracts-package claims

### IC-CONTRACTS-001 — version and compatibility classification

Source: src/Strategos.Contracts/CHANGELOG.md:19-28 and 128-139.

> Workflow step action identity (0.11.0): ActionReferenceV1 carries the ontology domain, object type, and action names on an optional action field shared by every workflow step kind. The field is occurrence-scoped and additive; legacy workflow JSON omits it byte-for-byte (#167).

> Workflow binding diagnostics (0.11.0): the closed AgwfCode vocabulary adds AGWF039–AGWF042 ... The schema change is additive, but generated enum converters reject unknown members, so Exarchos and Basileus must adopt 0.11.0 before Strategos emits these codes (#167).

> 0.11.0: None. Workflow-step action identity and AGWF039–AGWF042 are additive. Older generated closed-enum consumers must still upgrade before receiving the new diagnostic codes.

### IC-CONTRACTS-002 — package version authority and embedded artifacts

Source: src/Strategos.Contracts/README.md:176-204.

> This package versions at 0.11.0 ... driven by the single ContractsVersion property and the contracts-v* publish tag.

> 0.11.0 adds the optional, occurrence-scoped ActionReferenceV1 on workflow steps and AGWF039–AGWF042 to the closed diagnostic vocabulary. Consumers must upgrade before receiving one of the new diagnostic tokens. The package embeds all schema families ... so Exarchos can extract both.

### IC-TYPESPEC-001 — language-neutral, required identity schema

Source: src/Strategos.Contracts/Workflow/ActionReference.tsp:7-27.

> Language-neutral identity of an ontology action performed by a workflow step occurrence. Every component is a name; no runtime or CLR type identity crosses the wire boundary.

The schema text separately claims each of domainName, objectTypeName, and actionName is required,
has minimum length one, and matches a non-whitespace pattern.

### IC-TYPESPEC-002 — optional field is single-sourced across every step arm

Source: src/Strategos.Contracts/Workflow/StepDefinition.tsp:22-48.

> Fields common to every wire step kind. Spread (...StepCommon) into each arm of the StepDefinition discriminated union so the shared shape stays single-sourced while each arm pins its own kind discriminator.

> Ontology action performed by this specific step occurrence.

The concrete claim is that StepCommon.action is optional and therefore reaches every union arm by
the spread.

### IC-AGWF-AUTHORITY-001 — diagnostic authority and versioning claims

Source: src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp:7-21.

> AGWF — the workflow source-generator diagnostic codes. Single canonical source (#52): the codes and their metadata live here in TypeSpec; the Roslyn analyzer ... sources its id strings from the generated AgwfCode C# enum, and the codegen pipeline emits the canonical agwf-catalog.json data artifact + docs/diagnostics/agwf.md reference.

> Ground truth = exactly 36 defined codes with gaps preserved as gaps ... Member NAMES are the wire identity ... a member may be added but never renamed or reordered without a major bump.

These claims concern authority topology as well as intent. They remain claims in this file and are
for the authority lens to validate.

### IC-AGWF-SEMANTICS-001 — emitted diagnostic contract

Source: src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp:383-420 and
src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs:638-676.

The two surfaces claim the same four error meanings:

- AGWF039: “Bound workflow name does not resolve exactly once.”
- AGWF040: “Workflow step action reference is invalid.”
- AGWF041: “Workflow binding refinement proof failed.”
- AGWF042: “Workflow contract cannot be proved statically.”

The Roslyn descriptions add the claims that lookup is compilation-wide and ordinal, every reachable
occurrence must resolve once, AGWF041 includes precondition/postcondition/frame/authority/subject/
fork-noninterference failures, and AGWF042 fails closed outside the exact finite fragment.

### IC-GENERATED-CONTRACT-001 — generated validation and generation authority

Source: src/Strategos.Contracts/Generated/ActionReferenceV1.g.cs:1-58.

> Generated by Strategos.Contracts.Codegen from TypeSpec-emitted JSON Schema. DO NOT EDIT — hand-edits are rejected by the codegen-guard CI workflow.

> Every component is a name; no runtime or CLR type identity crosses the wire boundary.

At inventory time, the generated callbacks claimed, by their shape and names, that serialization
and deserialization rejected null, empty, and whitespace-only values for all three required
properties. Exact-current Contracts/codegen now exercised both callback paths; formal status remains
`Indeterminate` pending protected execution and review.

### IC-CODEGEN-001 — generalized constraint projection

Source: src/Strategos.Contracts.Codegen/RecordEmitter.cs:364-499 and 602-627;
src/Strategos.Contracts/ContractJsonValidation.cs:11-97.

Correction to the historical inventory: the changed emitter maps only the exact direct-property
nonwhitespace pattern `.*\S.*` into generated serialization/deserialization callbacks. It does not
project general `minLength`, referenced-scalar, or array-item constraints. At the discovery snapshot,
`ActionReferenceV1` received the callback while `WorkflowDefinitionV1.Name` was a minLength-only
nonmatch. Current commit `98fabb4` adds the exact pattern to that workflow-name identity, intentionally
regenerates its callback, and pins read/write behavior. The committed synthetic emitter test still
checks required/optional exact matches, unrelated regex, and scalar-alias boundaries.

## Claims made by public API and implementation comments

### IC-COMMENT-001 — runtime identity object

Source: src/Strategos/Definitions/WorkflowActionReference.cs:9-25 and 37-54.

> Language-neutral identity of an ontology action performed by one workflow step occurrence.

> The reference intentionally carries names only. It never retains CLR System.Type handles, so it can cross the workflow wire-contract boundary without coupling consumers to the producer's type system.

The constructor documentation also claims ArgumentException for empty or whitespace components;
the properties and deconstructor claim exact three-name exposure.

### IC-COMMENT-002 — ontology workflow catalog identity

Source: src/Strategos.Ontology/Descriptors/WorkflowBindingReference.cs:3-14.

> Stable workflow-catalog identity used to bind an ontology action.

> Gets the workflow identifier exactly as declared in the workflow catalog.

### IC-COMMENT-003 — occurrence-scoped builder semantics

Source: src/Strategos/Abstractions/IStepConfiguration.cs:42-55;
src/Strategos/Definitions/StepDefinition.cs:74-81; and
src/Strategos/Builders/StepConfigurationBuilder.cs:24-54.

> Declares the ontology action performed by this specific step occurrence.

> The binding is occurrence-scoped: two uses of the same step type may perform different actions.

> Applies both the ordinary step configuration and occurrence-scoped action identity accumulated by this builder to step.

The API comments also claim null input throws ArgumentNullException and a second declaration throws
InvalidOperationException.

### IC-COMMENT-004 — closed-topology fail-closed signal and no-lowering-change claim

Source: src/Strategos.Generators/Helpers/TopologyClosureInspector.cs:13-20;
src/Strategos.Generators/Models/WorkflowModel.cs:88-100.

> Detects legal fluent topology that the syntax extractors cannot represent without loss.

> This inspector does not change generator lowering. It records a closed-world proof signal so workflow/action binding cannot certify a smaller graph than the one the fluent builder executes at runtime. Calls are identified by their Strategos symbols, not by method-name coincidence.

> Runtime emission intentionally keeps its existing best-effort behavior. Workflow/action binding proof consumes this immutable signal and fails closed instead of proving a contract over a topology from which legal, dynamically-authored paths were silently omitted.

> JSON-imported workflows normally leave this empty; the import bridge adds a reason when a schema-valid carrier (currently a root failure handler) is retained for runtime compatibility but is not represented in the closed proof topology.

These are explicit no-runtime-change and proof-soundness claims.

### IC-COMMENT-005 — shared merged-front-end proof path

Source: src/Strategos.Generators/WorkflowIncrementalGenerator.cs:116-134.

> #167 — bind ontology actions to the merged C#/JSON workflow catalog and prove every reachable action occurrence against the bound contract. The proof consumes WorkflowModel rather than either authoring syntax so both front ends share one fail-closed analysis path (INV-1).

### IC-COMMENT-006 — compilation-local proof boundary

Source: src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:15-16.

> Proves compilation-local workflow action refinements.

This is narrower than an unconstrained reading of “resolves against the workflow catalog.” Whether
compilation-local resolution is the intended catalog boundary is an open scope question.

### IC-COMMENT-007 — runtime refinement API and result invariants

Source: src/Strategos.Ontology/Descriptors/ActionCalculus.cs:7-18 and 58-66;
src/Strategos.Ontology/Descriptors/ActionRefinementAnalysis.cs:5-43 and 72-122.

> Proves that an executable action is a behavioral refinement of a declared action specification.

> Proves that a composed workflow contract is a behavioral refinement of a declared action specification.

> Proven: Every refinement obligation was proved.

> Refuted: At least one refinement obligation has a concrete counterexample.

> Opaque: A custom predicate prevents a complete static proof.

> Invalid: One of the contracts is malformed or already refuted.

> Gets whether the implementation is a proved substitute.

The result type also claims deterministic failure order, and its constructor invariants claim Proven
cannot carry failures while every non-Proven result must identify at least one failed or unprovable
obligation.

### IC-COMMENT-008 — authority order

Source: src/Strategos.Ontology/Descriptors/AuthorityLattice.cs:93-115.

> Returns whether candidate is pointwise no stronger than limit. This is the authority arm of action refinement.

### IC-COMMENT-009 — one Roslyn predicate grammar

Source: src/Strategos.Ontology.Generators/Analyzers/ActionCompositionAnalyzer.WorkflowBridge.cs:17-25.

> Shared, source-link-safe projections of the action parser used by workflow binding proof. Keeping these entry points on the composition analyzer means typed predicates have one Roslyn grammar in both analyzer assemblies.

The Strategos.Generators project-file comment at
src/Strategos.Generators/Strategos.Generators.csproj:37-48 separately claims source-linking the
parser and proof kernel prevents semantic drift. Source identity can prevent code-copy drift while
still allowing different orchestration or inputs, so that narrower competing explanation must be
tested.

### IC-COMMENT-010 — compensation deferral

Source: src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:201-210.

The emitted reason claims:

> step '{phase}' declares Compensate<T>; rollback proof is deferred to #169

This is both a caveat and the handoff claim to the next requested slice.

### IC-COMMENT-011 — builder API gate stability claim

Source: scripts/check-builder-api-stability.sh:30-33.

> MSBuild's parallel restore graph intermittently exits 1 without diagnostics in this repository. Keep the fail-closed gate deterministic so an actual PublicApiAnalyzer diagnostic, rather than restore scheduling, controls the result.

This is a new operational claim about a prior intermittent failure and about the effect of forcing
single-process build execution.

## Test and fixture claim labels added by the diff

Test prose and method names are claims about what their assertions prove, not evidence by name.
They are inventoried here so Stage 2 can inspect the actual assertion and evidence binding rather
than accepting the label.

### IC-TEST-001 — contracts and serialization claim cluster

Anchors:

- src/Strategos.Contracts.Tests/Workflow/StepDefinitionSchemaTests.cs:83-239 claims every step
  arm has the same optional action reference; generated round-trip retention; omitted required-name
  rejection; and generated enforcement of non-empty and non-blank identity strings.
- src/Strategos.Contracts.Tests/Diagnostics/AgwfCodeEnumTests.cs:56-59,
  AgwfCatalogSchemaTests.cs:35-36, AgwfCatalogEmitterTests.cs:31-32, and
  AgwfMarkdownTests.cs:26-27 claim the four diagnostic representations contain AGWF039–AGWF042.
- src/Strategos.Tests/Contracts/ProjectionTests.cs:20-69 claims configured identities project
  unchanged and legacy steps retain the prior omitted JSON shape.
- src/Strategos.Generators.Tests/Import/RoundTripIrFidelityTests.cs:133-290 contains exact-round-trip and
  blank-identity claims across builder projection, canonical JSON, dependency-free read, and import.

### IC-TEST-002 — authoring and occurrence claim cluster

Anchors:

- src/Strategos.Tests/Builders/WorkflowActionReferenceTests.cs:9-237 claims identity immutability,
  component validation, exact-value preservation, duplicate/null rejection, configuration chaining,
  and occurrence independence across the supported builder surface.
- src/Strategos.Tests/Builders/ApprovalEscalationBuilderTests.cs:115,
  ApprovalRejectionBuilderTests.cs:101, and WorkflowBuilderAwaitApprovalTests.cs claim the new
  configured handler overloads retain both ordinary configuration and action identity and reject
  null callbacks.
- src/Strategos.Generators.Tests/Helpers/StepExtractorActionReferenceTests.cs:13-395 claims direct constant
  extraction, missing/dynamic/invalid distinction, no nested-lambda leakage, and distinct identities
  for distinct uses of one CLR step type.
- src/Strategos.Generators.Tests/Helpers/ApprovalExtractorTests.cs:678-799 and
  src/Strategos.Generators.Tests/Helpers/InvocationChainWalkerTests.cs:173-211 claim configured continuations retain the full IR, nested
  continuations remain parent-owned, and both statement and fluent-receiver shapes retain source
  order.

### IC-TEST-003 — import/topology closure claim cluster

Anchors:

- src/Strategos.Generators.Tests/Import/ImportFrontEndRobustnessTests.cs:251-300 claims a present malformed
  action is never collapsed into omission and omitted action remains distinct/importable.
- src/Strategos.Generators.Tests/Import/ImportRejectionTests.cs:526-607 claims unbound root failure
  handlers retain existing import/runtime behavior, bound root handlers become unprovable,
  fork-path failures are rejected rather than losing executable recovery, and nested approval in a
  confidence handler is rejected rather than silently removed.
- src/Strategos.Generators.Tests/Proof/TopologyClosureProofTests.cs:13-571 claims dynamic branch, loop,
  fork, approval, confidence, failure, delegate, and extension topologies become AGWF042 rather than
  allowing proof over a smaller graph.
- src/Strategos.Generators.Tests/Emitters/TransitionGraphLoweringTests.cs:339-416 claims consecutive
  branches expose every possible case/rejoin successor and exhaustive heads suppress fall-through.

### IC-TEST-004 — catalog and workflow proof claim cluster

Anchors:

- src/Strategos.Generators.Tests/Proof/OntologyActionCatalogFailClosedTests.cs:13-484 claims dynamic,
  factored, conditional, ambiguous, and unknown descriptor/lattice declarations fail closed while
  supported direct/named-argument forms resolve.
- src/Strategos.Generators.Tests/Proof/WorkflowBindingProofAnalyzerTests.cs:13-940 claims valid legacy
  string binding has no diagnostic; absent/ambiguous workflow gives AGWF039; bad occurrence identity
  gives AGWF040; seam/exit/frame/authority/fork-interference refutations give AGWF041 with stable
  witnesses; and opacity, compensation, recursion, or other unresolved inputs give AGWF042.

### IC-TEST-005 — runtime refinement and hash claim cluster

Anchors:

- src/Strategos.Ontology.Tests/Descriptors/ActionRefinementTests.cs:12-365 claims requirement
  contravariance, guarantee covariance, authority/frame bounds, may-write not implying a fact,
  CreatesLink implying LinkExists, opaque-not-success, result invariants, precedence among statuses,
  composite refinement, and unknown-authority invalidity.
- src/Strategos.Ontology.Tests/Descriptors/WorkflowBindingReferenceTests.cs:8-49 claims null/blank
  rejection, exact ordinal preservation, equality, and sealed/read-only shape.
- src/Strategos.Ontology.Tests/OntologyGraphVersionTests.cs:291-333 and 835-900 claims the string overload and typed
  wrapper with the same ID hash identically, a changed ID changes the hash, and registration order
  remains stable.

No execution result is attached to any of these labels by this lens.

## Consolidated no-behavior-change and compatibility claims

These are collected separately because the lens specifically requires every “no behavior change”
claim to be visible:

1. Issue #167: “Keep the string form as the wire/serialized representation — this is a
   compile-time typing change, not a contract break.”
2. Issue #167: “Existing BoundToWorkflow(\"name\") call sites keep compiling.”
3. Issue #167: “The serialized graph hash is unchanged for an unchanged binding.”
4. CHANGELOG.md:22: “Existing fluent call sites remain source-compatible,” qualified by external
   interface implementers having to adopt new members.
5. CHANGELOG.md:62-63: the string overload “remains supported” and preserves “its exact ordinal
   identifier and graph-hash bytes.”
6. CHANGELOG.md:64-68: ActionReferenceV1 is “optional, backward-compatible,” qualified by closed
   enum consumers needing an upgrade.
7. migration-v2-13.md:354: “Unbound workflows retain their existing runtime behavior.”
8. migration-v2-13.md:404-406: “legacy or unconfigured workflow JSON continues to omit it
   byte-for-byte.”
9. migration-v2-13.md:425-430: the wrapper “is not itself part of that rollover” and the same ID
   preserves the legacy graph hash.
10. action-calculus.md:499-500 and workflow.md:180-181 repeat byte-for-byte omission for legacy
    workflow JSON.
11. graph-versioning.md:78-88 repeats that the API shape change adds no canonical field and the
    typed wrapper with the same ID preserves the legacy graph hash.
12. src/Strategos.Contracts/CHANGELOG.md:19-28 and 137-139 call the step field/diagnostics additive,
    while requiring older generated closed-enum consumers to upgrade.
13. TopologyClosureInspector.cs:17 and WorkflowModel.cs:93 claim the new proof signal does not
    change lowering/runtime emission and retains best-effort behavior.
14. ImportRejectionTests.cs:526 labels the unbound imported root-handler behavior as preserved.
15. ProjectionTests.cs:51 labels the JSON shape for an unconfigured legacy step as preserved.

Each required a before/after comparison bound to the base and an immutable candidate artifact. The
complete exact-current `98fabb4` local portfolio supplies those comparisons where recorded;
protected execution and review remain pending.

## Caveats and open items stated by the change

The diff itself states these limits; they are not inferred omissions:

- compile-time occurrence resolution accepts a direct constructor with constant names and rejects
  factories, mutable locals, dynamic expressions, multiple declarations, and delegates
  (migration-v2-13.md:322-326; workflow.md:159-165);
- proof needs analyzer-visible inline branch, loop, fork, approval, confidence, and failure-handler
  forms; dynamic helpers produce AGWF042 (migration-v2-13.md:343-346;
  action-calculus.md:367-370);
- nonterminal workflow failure handlers, fork-path failure handlers, and nested EscalateTo approval
  routing are outside the proved v2.13 subset (migration-v2-13.md:349-351;
  action-calculus.md:370-373);
- Compensate<T> produces AGWF042 and rollback refinement is deferred to #169
  (migration-v2-13.md:352-353; WorkflowBindingProofAnalyzer.cs:201-210);
- custom/dynamic contracts are not proof success (migration-v2-13.md:339-341;
  action-calculus.md:362-365);
- the proof analyzer describes its boundary as compilation-local
  (WorkflowBindingProofAnalyzer.cs:15);
- Contracts 0.11 cannot author effect/frame metadata, so its guarantee must already follow from a
  hard requirement (migration-v2-13.md:385-389; action-calculus.md:489-495);
- external interface implementers and the Exarchos builder mirror must adopt the added members
  (CHANGELOG.md:18-25);
- Exarchos and Basileus must adopt the 0.11 generated enum before new diagnostic tokens are emitted
  (src/Strategos.Contracts/CHANGELOG.md:23-28).

## Competing explanations to preserve for Stage 2

| Claim family | Competing explanation that must be ruled out | Discriminating evidence needed |
|---|---|---|
| The original defect exists | The issue describes an older or narrower path, while another build-time check already covered the binding | Base-revision call/diagnostic trace or reproducer showing a missing workflow and mismatched saga compile successfully |
| “Every fluent topology” and “all reachable ... failure-path occurrences” | The implementation covers every occurrence only inside an accepted closed subset and rejects other legal runtime topology | Enumerate runtime-legal topology against proof IR; show each is represented faithfully or deterministically rejected before proof |
| Runtime and generator use “the same behavioral-subtyping rules” | They share formulae/parser/kernel but feed them different graph shapes, effective guarantees, authority, or failure precedence | Paired runtime/analyzer proof vectors over the same contracts and topology, including negative and indeterminate cases |
| The wrapper preserves graph hash | A whole-graph comparison also includes #168's intentional hash rollover, or serialization order changed elsewhere | Fix all non-binding bytes, compare old string binding with new wrapper/same ID, and separately demonstrate different ID sensitivity |
| The wire addition is backward-compatible | Optional action is structurally additive, while new diagnostic enum tokens break older generated consumers | Schema diff plus old-consumer/new-payload round trips; report wire-shape and generated-model compatibility separately |
| Existing fluent call sites remain source-compatible | Interface implementations and mirrored surfaces break even if ordinary calls compile | Compile old call-site corpus and compile known external implementations/mirror separately |
| Unbound workflows retain runtime behavior | Shared parser, extractor, import bridge, branch successor, or codegen changes alter unbound output despite proof being gated | Golden before/after generated source, imported IR, and representative runtime behavior for every touched topology |
| Workflow identity “resolves against the workflow catalog” | Resolution is only within the current Roslyn compilation and does not see referenced-assembly/catalog entries | Explicit in-compilation, imported JSON, referenced assembly, duplicate, and absent cases matched to intended boundary |
| Every bound saga establishes the action guarantee | The analyzer proves a finite effective action-contract graph, not the emitted Wolverine/Marten runtime saga behavior | Bind proof IR nodes/edges/exits to emitted saga routes and step contract semantics; demonstrate no runtime route is absent from proof |
| The #167 needs clause is delivered | The branch substitutes frame and authority checks for the issue's deferred capability-lattice needs relation | Explicit design decision or capability-lattice proof showing equivalence; otherwise retain the issue's stated deferral |
| Generated constraints mirror TypeSpec | The generalized minLength/pattern code affects existing contracts unexpectedly or only recognizes one exact regex spelling | Regeneration drift check plus positive/negative round trips for direct/ref/array constraints and existing constrained types |
| A green suite establishes the claims | A suite result, if later supplied, could belong to another commit, omit untracked files, or assert only labels/substrings | Revision/artifact-bound command log and inspection that each test can fail for the stated defect |

## Assumptions and questions not settled by this lens

1. Does “all reachable ... failure-path step occurrences participate” mean all occurrences in the
   accepted closed grammar, or all runtime-reachable occurrences? The adjacent exclusions make the
   unqualified reading ambiguous.
2. Is compilation-local lookup the intended meaning of “workflow catalog,” including C# and
   AdditionalFiles JSON in one compilation but excluding referenced assemblies? Issue #167 does not
   state that boundary.
3. Is the original needs(saga) subset needs(action) clause still deferred, replaced by frame and
   authority checks, or implemented under a different name? The diff narrative does not explicitly
   resolve that issue-language-to-current-model mapping.
4. Does “derive the saga's ensures from its steps' postconditions” now mean the composed effective
   guarantees introduced by #168? If so, which postcondition effects are soundly derived and which
   remain frame-only must be treated as part of acceptance.
5. What exact pair of artifacts defines “unchanged binding” for the hash criterion in a release that
   independently rolls every action-bearing graph hash?
6. Is the optional action field backward-compatible for every supported consumer, or only at JSON
   Schema/wire shape? The required closed-enum rollout shows source/model compatibility is distinct.
7. Do the broad parser, importer, branch graph, and generated-record validation changes preserve all
   unbound behavior, or only the examples named by tests and comments?
8. Are external implementers beyond the named Exarchos mirror known, and is their required adoption
   acceptable under the release's source-compatibility policy?
9. Was the generalized code-generator projection of minLength, item minLength, and non-whitespace
   pattern intentionally part of #167? It is required to make generated C# honor the new TypeSpec
   constraints, but its scope reaches other schemas.
10. Does the action catalog find every supported ontology declaration shape without evaluating user
    code, and are ambiguous duplicates rejected rather than silently ordered?
11. Does the workflow proof cover every successful exit and every executable internal route emitted
    by the saga generator, including loop exits, consecutive branches, approval handlers, confidence
    paths, and joins?
12. Are write/read interference checks defined at the same resource identity granularity as frame
    hashing and predicate read sets, including links and relations?
13. When closed and opaque/refuted/invalid conditions coexist, is status and diagnostic precedence
    stable and documented consistently between runtime and analyzer?
14. Issue #169 remains part of the user's requested program but is intentionally excluded from this
    #167 survey. Its own stacked diff and verification run remain outstanding.

## Stage 2 derivation note

The IDs above are the complete seed set from this lens. Stage 2 may derive obligations from a claim
only after naming and reading evidence that distinguishes it from the competing explanation. A
claim whose meaning remains ambiguous should become an open investigation question, not a silently
weakened obligation and not a pass.

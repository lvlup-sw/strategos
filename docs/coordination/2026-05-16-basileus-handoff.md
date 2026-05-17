# Basileus G1 coordination handoff — Strategos 2.7.0-preview.1

**Date:** 2026-05-16
**Author drafted for:** lvlup-sw/strategos maintainer to action manually
**Status:** Drafted — not yet filed / commented / closed

This document holds the three pieces of cross-repo text required by
[DR-9](../designs/2026-05-16-g1-agent-identity-seam.md#dr-9--cross-repo-coordination-basileus)
and T13 of [the implementation plan](../plans/2026-05-16-g1-agent-identity-seam.md#task-13-dr-9-basileus-coordination-handoff).
The implementer agent does NOT have permission to file issues or post
comments on behalf of the maintainer; this file is the staging area
for the actions the maintainer will take after merge.

---

## (a) Basileus tracking issue to file

**Repository:** `lvlup-sw/basileus`
**Title:** `G1-strategos integration: re-cut PR #184 against Strategos.Identity.Abstractions 2.7.0-preview.1`
**Labels:** `g1`, `integration`, `breaking-change`, `phase-0`

### Body

```markdown
## Context

Strategos has shipped the G1 agent-identity seam in
`LevelUp.Strategos.Identity.Abstractions 2.7.0-preview.1`. Identity
storage now lives on Wolverine envelope headers rather than on saga
fields — this supersedes the saga-emit approach the basileus G1 Phase 0
draft proposed.

- Strategos plan:
  `docs/plans/2026-05-16-g1-agent-identity-seam.md`
- Strategos design (canonical):
  `docs/designs/2026-05-16-g1-agent-identity-seam.md`
- Strategos releases the abstractions, generator, and core packages at
  `2.7.0-preview.1`.

## Required basileus changes

PR #184 must be re-cut to:

1. Reference `LevelUp.Strategos.Identity.Abstractions 2.7.0-preview.1`
   and `LevelUp.Strategos.Generators 2.7.0-preview.1` (or higher).
2. Move
   `Basileus.Core.Contracts.Identity.{SpiffeId, WorkflowId, WorkflowIdentity, AgentIdentity, IAgentIdentityProvider}`
   to `Basileus.Identity.Spiffe.*` as adapter implementations, or
   delete them and consume the new Strategos types directly.
3. Implement
   `Basileus.Identity.Spiffe.SpiffeAgentIdentityProvider : Strategos.Identity.Abstractions.IAgentIdentityProvider`
   producing values of the SPIFFE shape
   `spiffe://td/workflow/<id>/step/<phase>`.
4. Implement `Basileus.AgentHost.Middleware.StrategosHeaderMiddleware`
   per Strategos design §9:
   - Read `IMessageContext.Envelope.Headers[StrategosHeaders.WorkflowIdentity]`
     or generate a new identity keyed to `sagaId` when missing
     (Strategos DR-8 row 2 is a basileus contract).
   - After Wolverine resolves the saga (as method parameter
     `IPhaseAwareSaga saga`), read `saga.CurrentPhaseName`.
   - Call `provider.DeriveStepIdentity(workflowId, saga.CurrentPhaseName)`.
   - Stamp
     `context.Envelope.Headers[StrategosHeaders.AgentIdentity] = agent.Value`.
5. Configure
   `opts.Policies.PropagateIncomingHeaderToOutgoing(StrategosHeaders.WorkflowIdentity)`
   in the basileus `UseWolverine` registration so workflow identity
   survives across handler hops without per-handler stamping.
6. Revise basileus design doc
   `docs/designs/2026-05-13-g1-implementation-phase-0.md` to reflect
   envelope-header storage. Preserve §4 INV-8 (derivation-from-saga-state)
   rationale; update §6 Strategos-side deliverables to read:
   > Strategos owns identity ports; this PR is the basileus adapter only.

## Acceptance criteria

- [ ] PR #184 builds against the published `2.7.0-preview.1`.
- [ ] Integration test on the basileus side verifies that an outbound
      envelope carries both `x-strategos-workflow-identity` and
      `x-strategos-agent-identity` headers populated after handler
      completion.
- [ ] basileus design doc revision is committed in the same PR.
- [ ] Acceptance criteria mirrored from Strategos DR-9 are checked off.
```

---

## (b) Comment to post on basileus PR #184

```markdown
Update from lvlup-sw/strategos: the G1 agent-identity seam shipped in
`LevelUp.Strategos.Identity.Abstractions 2.7.0-preview.1` — published
alongside `LevelUp.Strategos.Generators 2.7.0-preview.1` and
`LevelUp.Strategos 2.7.0-preview.1`. Identity lives on Wolverine
envelope headers, not on saga fields; the Strategos-side saga-emit
work that this PR's Phase 0 draft anticipated has been descoped.

This PR will need to be re-cut to consume the new abstractions
package; see the tracking issue at lvlup-sw/basileus#[ISSUE-NUMBER]
for the full task list.

Reference material:
- Strategos design — `docs/designs/2026-05-16-g1-agent-identity-seam.md`
- Strategos plan — `docs/plans/2026-05-16-g1-agent-identity-seam.md`

Once 2.7.0-preview.1 lands on nuget.org, this PR can pin to it.
```

(Replace `[ISSUE-NUMBER]` with the basileus tracking-issue number
filed under section (a).)

---

## (c) Strategos issue closeout text

### Comment to post on #71 (G1 epic)

```markdown
G1 Slice (D) — agent-identity seam — has shipped in v2.7.0-preview.1.

Three packages now exist:
- `LevelUp.Strategos.Identity.Abstractions 2.7.0-preview.1` (new)
- `LevelUp.Strategos.Generators 2.7.0-preview.1`
- `LevelUp.Strategos 2.7.0-preview.1`

Cross-repo work tracked at lvlup-sw/basileus#[ISSUE-NUMBER]
(re-cut PR #184 against the new abstractions package).

Closing #67, #68, #69 as superseded — the basileus-draft A1/A2/A3
emit was descoped in favor of one computed `CurrentPhaseName` property
+ `IPhaseAwareSaga` marker interface. See
`docs/designs/2026-05-16-g1-agent-identity-seam.md` §10 for the
before/after table.
```

### #67 closeout comment + close as `not planned` (superseded)

```markdown
Superseded by the v2.7.0-preview.1 envelope-header design. The
generator no longer needs to emit a `CurrentAgentIdentity` property —
the basileus middleware reads `IMessageContext.Envelope.Headers`
through `IAgentIdentityAccessor`. See
`docs/designs/2026-05-16-g1-agent-identity-seam.md` §10 (Option E vs
Option C) for the rationale, and the Phase 2 ideation that ruled out
Option C on INV-7 / outbox-survival grounds.

Closing as superseded.
```

### #68 closeout comment + close as `not planned` (superseded)

```markdown
Superseded by the v2.7.0-preview.1 envelope-header design. There is
no `InitializeIdentity` helper to emit — identity stamping happens in
the basileus `StrategosHeaderMiddleware` once per handler invocation,
not at saga construction. See
`docs/designs/2026-05-16-g1-agent-identity-seam.md` §10.

The DR-7 negation tests in `Strategos.Generators.Tests/SagaIdentityNegationTests.cs`
mechanically guard against any future drift toward this descoped
shape.

Closing as superseded.
```

### #69 closeout comment + close as `not planned` (superseded)

```markdown
Superseded by the v2.7.0-preview.1 envelope-header design. The
compile-test-with-stubs work this issue tracked is replaced by:

- `Strategos.Identity.Abstractions.Tests` — full T2/T3/T4/T5/T6/T7
  contract tests for every port + every value record.
- `Strategos.Generators.Tests/EndToEndStubIntegrationTests.cs` —
  T11 end-to-end stub seam verification (generator emit + fake
  middleware stamp + fake accessor read-back).

See `docs/designs/2026-05-16-g1-agent-identity-seam.md` for the full
design.

Closing as superseded.
```

---

## Maintainer action checklist

After merge of the Strategos v2.7.0-preview.1 release tag:

- [ ] Publish the three nupkg files to nuget.org (or your private feed).
- [ ] File the basileus tracking issue (section (a)). Capture its
      number.
- [ ] Post the PR #184 comment (section (b)) with the captured issue
      number substituted.
- [ ] Comment on #71 (section (c)) with the basileus tracking issue
      linked.
- [ ] Close #67, #68, #69 as `not planned` with the respective
      closeout comments.

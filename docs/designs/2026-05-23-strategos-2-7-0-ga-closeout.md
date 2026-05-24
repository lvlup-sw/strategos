# Strategos 2.7.0 — GA closeout

**Date:** 2026-05-23 · **Milestone:** Strategos 2.7.0 — Agent Capabilities (GA) · **Tracks:** [#85](https://github.com/lvlup-sw/strategos/issues/85), [#92](https://github.com/lvlup-sw/strategos/issues/92), [#84](https://github.com/lvlup-sw/strategos/issues/84), [#83](https://github.com/lvlup-sw/strategos/issues/83), [#86](https://github.com/lvlup-sw/strategos/issues/86), [#87](https://github.com/lvlup-sw/strategos/issues/87), [#70](https://github.com/lvlup-sw/strategos/issues/70); closes-as-superseded [#67](https://github.com/lvlup-sw/strategos/issues/67), [#68](https://github.com/lvlup-sw/strategos/issues/68), [#69](https://github.com/lvlup-sw/strategos/issues/69)

## Problem Statement

The 2.7.0 "agent capabilities" build is complete on `main`: MEAI 10.5 adoption (PR #82, #45), streaming + in-process tool sources (PR #93, #89/#90/#91), and the agent-identity seam shipped at `2.7.0-preview.1` (PR #81). What remains in the open milestone is **closeout debt**, not feature work — and it must be discharged before cutting GA.

Two of the open items carry real design content; the rest are mechanical. This document settles the two design forks and defines the closeout sequence.

| Item | Nature | This doc |
|---|---|---|
| **#85** | `IToolSource` redaction port-contract | **Design** — §Fork 1 |
| **#92** | `AgentToolLoopException` trace payload vs OTel | **Settle** — §Fork 2 |
| #84 | Remove dead `AgentStepConfiguration.ChatClientConfigurator` field | Mechanical — §Closeout |
| #83 / #86 / #87 | Design-diagram & docs reconciliation | Mechanical — §Closeout |
| #67 / #68 / #69 | Close-as-superseded (PR #81 implemented A1–A3 differently) | Mechanical — §Closeout |
| #70 | CHANGELOG + cut 2.7.0 GA | Release — §Sequence |

## Fork 1 — #85: redaction at the tool-source boundary

### The gap

`StrategosFunctionsChatClient.ResolveToolsAsync` propagates each source's exception **unchanged** — a deliberate contract shipped in PR #93 ("the failing source's own exception propagates UNCHANGED … this middleware does not reclassify"). The two in-tree adapters self-redact:

- `McpToolSource` (`Strategos.Agents.Mcp`) → `AgentMcpException` (AGAG004) with `RedactedEndpoint` stripped of user-info credentials (DR-10).
- `AgentToolSource` (in-process) → `AgentToolSourceException` (AGAG007).

A **third-party `IToolSource`** is under no type-level obligation to redact. It can throw a raw `HttpRequestException` whose message embeds `https://user:pass@host/…`; the middleware then propagates it verbatim into logs and traces. The issue as filed proposes a doc-only contract ("adapters MUST redact"). A documented-by-convention contract is not a mitigation — it is unenforceable against the exact actor (a foreign adapter author) it targets.

### Chosen approach — redaction boundary (posture B)

Make `ResolveToolsAsync` the single enforcement point. The boundary distinguishes **conforming** from **foreign** failures by type:

- An exception that is already an `AgentException` subtype — the in-tree adapters — has self-redacted by contract. Propagate **unchanged**, preserving #93's behavior and keeping `McpToolSource` a full-fidelity peer per [[INV-3]].
- Any other (foreign) exception is wrapped in `AgentToolSourceException` (AGAG007, no new diagnostic ID per [[INV-5]]) with a **redaction pass** applied to the surfaced message: URI-shaped substrings have their user-info component (`user:pass@`) stripped before the message is composed. The original foreign exception is retained as `InnerException` for diagnosis, but the *redacted* AGAG007 message is what tooling logs first.

This narrows #93's "propagate unchanged" contract to **`AgentException`-subtypes-only** — a deliberate, documented tightening (CHANGELOG entry). Redaction becomes a property of the seam, not adapter goodwill.

### Technical design

```text
 StrategosFunctionsChatClient.ResolveToolsAsync
   foreach (source in _toolSources):
     try:
       resolved = await source.GetToolsAsync(ct)
     catch (OperationCanceledException):  throw            // cancellation unwrapped (unchanged)
     catch (AgentException):              throw            // conforming adapter self-redacted → unchanged
     catch (Exception foreign):
       throw new AgentToolSourceException(
                 RedactUserInfo(foreign.Message),          // strip user:pass@ from any URI substring
                 sourceType: source.GetType().FullName,
                 innerException: foreign)                   // AGAG007; inner kept for diagnosis
```

`RedactUserInfo` is a small internal helper in `Strategos.Agents` (sibling to `McpToolSource.RedactEndpoint`'s intent, but operating on free-text messages, not a parsed `Uri`): it locates `scheme://…@` spans and elides the user-info segment. **Honest scope:** this redacts the URI user-info credential — the documented DR-10 threat — and does **not** claim to catch arbitrary secrets embedded in non-URI form. That boundary is stated in the XML doc so no false assurance is implied.

The `IToolSource` XML doc gains a `### Failure contract` note: in-tree/conforming adapters should throw an `AgentException` subtype with redaction applied in-adapter (propagated unchanged); foreign adapters that throw anything else are wrapped-and-redacted at the boundary. This is documentation of an **enforced** behavior, not a substitute for it.

### Design rationale — invariants & dimensions applied

This design was audited against `/axiom:design` (DIM-1..8) and `/strategos-design-invariants` (INV-1..8) during ideation.

| Decision | Governing invariant / dimension | Consequence |
|---|---|---|
| Foreign exceptions wrapped+redacted at the seam; `AgentException` subtypes propagate unchanged | DIM-2 (observability) · DIM-3 (contracts) | No raw foreign exception escapes the tool-source boundary; the contract is enforced, not asserted. Satisfies the no-handwavy-mitigations rule. |
| MCP adapter keeps self-redaction + unchanged propagation | [[INV-3]] (MEDIUM) | MCP stays a full-fidelity peer adapter, not a downgraded special case. |
| Reuse `AgentToolSourceException` / AGAG007 for the wrap | [[INV-5]] (HIGH) | No new diagnostic ID minted for an existing failure class; ID stability preserved. |
| `RedactUserInfo` scoped to URI user-info only, scope stated in doc | DIM-8 (prose) · DIM-2 | Documentation names the exact threat covered and the limit; avoids over-claiming. |
| No new public type; helper is `internal` | DIM-5 (hygiene) · [[INV-6]] | Smallest surface; the port interface is unchanged. |

## Fork 2 — #92: `AgentToolLoopException` trace payload

OpenTelemetry gen-ai semantic conventions model tool invocations as **span events** (`gen_ai.tool.name`, `gen_ai.tool.call.id`, `gen_ai.tool.type`) on the inference span — they are orthogonal to an in-exception diagnostic payload, not a replacement for it. The current `AgentToolLoopException` payload is `IReadOnlyList<ChatMessage>` (stored as a defensive copy after PR #82's fix-cycle), which is a faithful, already-immutable capture of the partial loop.

**Settled:** keep `IReadOnlyList<ChatMessage>`. Add an XML-doc note mapping the payload to the OTel gen-ai event model (so an exporter author knows how to project it), and **defer** a structured `ToolCallTrace` record until a concrete consumer needs the narrower shape (YAGNI / DIM-5 — do not mint a type with no caller). This is a settle-and-document, not a code change to the payload type. [[INV-5]] is unaffected (AGAG005 unchanged).

## Closeout — mechanical items

- **#84** — Remove `AgentStepConfiguration<TState, TResult>.ChatClientConfigurator`. It is populated by the builder but never read by `AgentStepBase.ExecuteAsync` (the configurator is applied at compose-time inside `AgentStepBuilder.ComposeChatClient`). Dead data on an immutable record (DIM-5 / [[INV-6]]). Audit tests for references before deletion.
- **#83** — Reconcile the DR-1/DR-2 diagram in `docs/designs/2026-05-17-strategos-agents-meai-10-5.md`: the shipped surface is `AgentStepBuilder.Build(IChatClient)`, not a no-arg `Build()` with `IChatClient` on the orchestrator ctor. Update the diagram to match code (DIM-8).
- **#86** — Document the `AgentStepBuilder` tool-list bound expectation.
- **#87** — Document direct `ChatClientBuilder` use in `AgentStepBuilder.ComposeChatClient`.
- **#67 / #68 / #69** — Close as **superseded**: these were the original A1–A3 generator subtasks (emit `CurrentAgentIdentity`, `InitializeIdentity` helper, e2e compile test). PR #81 implemented the identity seam differently — the generator emits `CurrentPhaseName` + the `IPhaseAwareSaga` base-list addition, validated by the shipped generator tests. Close each with a comment pointing to PR #81 as the superseding implementation.

## Sequence

1. **PR α — agent closeout** (one PR): #85 redaction boundary + tests; #84 dead-field removal; #92 doc note; #83/#86/#87 docs. Audited by `/exarchos:review` + `/strategos-design-invariants`.
2. **Issue housekeeping** (no PR): close #67/#68/#69 as superseded with PR #81 references.
3. **PR β — release (#70)**: CHANGELOG (incl. the #85 "propagate-unchanged narrowed to `AgentException`-subtypes" note), bump to 2.7.0 GA per [[MinVer version override gotcha]] discipline, cut artifacts.

PR α before PR β so the CHANGELOG reflects the final shipped surface.

## Non-goals

- No new feature pulled forward from 2.8.0 (Contracts 0.2.0, Workflow Builder convergence stay in 2.8.0).
- #78 (Ontology 2.6.0 polish) is off the agent-capabilities theme; remains "opportunistic," not part of this unit.
- No structured `ToolCallTrace` record (deferred, §Fork 2).
- `RedactUserInfo` does not attempt to scrub arbitrary (non-URI) secrets.

## Alternatives considered (Fork 1)

- **A — doc-only port contract** (the issue as filed): rejected. Unenforceable against the foreign-adapter author it targets; conflicts with the no-handwavy-mitigations rule.
- **C — accept the violation**: viable fallback (declare third-party adapters unsupported for GA). Rejected in favor of B because the enforcement chokepoint already exists (`ResolveToolsAsync`) and the cost is ~15 LOC + tests — cheap enough that explicit non-support is not worth the security-relevant gap.
- **Type-level enforcement** (abstract `ToolSourceBase` template-method with a redacting `catch`): rejected. Forces inheritance over the clean hexagonal `IToolSource` interface (tension with [[INV-6]] composition-over-inheritance) and breaks the just-shipped port signature for a LOW-severity, third-party-only gap. Over-engineered.

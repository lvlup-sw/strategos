# wild-resulttype-not-error-channel

Hosting sets `resultType: complete` on success and on `IsError` payloads. The surface depends on clients treating `resultType` as a completion discriminator and `IsError` as the error channel. Nobody states that client obligation, and no test models a client that keys off `resultType` alone.

## What led here

Residue and INV-3 require `resultType` on every constructed `CallToolResult` (`CHANGELOG.md:187-190`; INV-3 acceptance: "every `CallToolResult` construction set `ResultType` (`\"complete\"` unless MRTR `input_required`)"). Check 3.4 is a file-level `grep -L ResultType` (`deterministic-checks.md:118-124`). One assignment anywhere in the file satisfies it.

`OntologyServerToolFactory` documents the producer intent: always `"complete"` here, "final content, including `isError: true`" (`OntologyServerToolFactory.cs:50-57`). `MapTraversalResult` sets `ResultType = CompletedResultType` on success (`:384-387`). `ErrorResult` sets the same constant with `IsError = true` (`:410-412`).

Four discovered tools (`ExploreHandler`, `QueryHandler`, `ActionHandler`, `ValidateHandler` at `:155-225`) return domain objects. Hosting does not assign `ResultType` on that path; SDK 2.2.0 wrap is assumed to. Check 3.4 cannot see those constructions.

The factory comment is a claimed guarantee, not a protocol fact. INV-3's external grounding says clients treat an **absent** field as `"complete"` for older servers, and that servers MUST include `resultType`. It does not say a client must ignore `resultType` when classifying errors. The deny-list cannot distinguish complete-on-error from complete-on-success.

## Failure scenario

An MCP client, or an INV-3-style checker, treats `resultType === "complete"` as success and does not read `IsError`. Traversal validation failures arrive as completed successful tool calls. The new deny-list still passes: `ResultType` is present.

A later edit that sets `resultType` to an error-shaped string on `ErrorResult` would satisfy a reader who wanted a discriminator and would violate the factory's "always complete" comment — with no test that a client using only `resultType` is wrong, and no test that a client using only `IsError` is right beyond the Hosting unit assertions.

## Code paths read (rev `324768f`)

- `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs:50-57`, `:155-225`, `:376-416`
- `.agents/skills/strategos-design-invariants/references/INV-3-mcp-first-class-latest-spec.md:11`, `:23`, `:36`
- `.agents/skills/strategos-design-invariants/references/deterministic-checks.md:112-124`
- `CHANGELOG.md:187-190`

## Why not cheaper

- **Rung 1.** `CompletedResultType` is a constant, not a generated pairing of `(resultType, isError)`.
- **Rung 2.** `CallToolResult` is an SDK type. It permits `ResultType = "complete"` with `IsError = true`. That combination is representable and, per the factory comment, intended.
- **Rung 3.** A grep that `ResultType` appears cannot express "clients must not treat this field as the error channel."
- **Rung 4 is the cheapest sound rung.** A contract fixture that a consumer classifier using only `resultType` cannot separate `ErrorResult` from `MapTraversalResult` success — and that `IsError` can — locks the unstated client obligation. Hosting tests that assert `ResultType == "complete"` on both paths currently make the collapse look like coverage.

## What is expensive to find again

The factory comment reads as if the protocol question is settled. INV-3 Check 3.4 then rewards any `ResultType` assignment, including the one on `ErrorResult`. The unstated dependency is on the other side of the network.

## Open questions

- Is `ErrorResult`'s `resultType: complete` required by MCP 2026-07-28, or an accidental collapse of error into the success-looking discriminator? The factory comment asserts the former. The spec text in this repository is a lead. If the spec requires a different `resultType` on `isError`, this obligation inverts: Hosting must not set `"complete"` on `ErrorResult`. If the spec agrees with the factory, the obligation is the client contract above.
- Does SDK 2.2.0 wrap of the four discovered tools set `ResultType`, and does it set `IsError` on domain-level failures? If the wrap omits `IsError` or `ResultType`, the four-tool path is a second unstated dependency, not covered by the two manual constructions.

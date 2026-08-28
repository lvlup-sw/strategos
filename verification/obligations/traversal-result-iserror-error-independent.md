# traversal-result-iserror-error-independent

Lens: **3. Representable Invalid States**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Claim

`TraversalResult` must not represent `IsError: false` with `Error` present, or `IsError: true` with a non-empty `Endpoints` / `Truncated` success payload. The hosting map must not emit a success `CallToolResult` that still carries an error string.

## What led here

This wave sets `ResultType = "complete"` on both factory construction sites, including `ErrorResult` (`IsError = true`). That pair is the 2026-07-28 finished-call discriminator plus SEP-1303 and is **not** this obligation (see below). The related boolean shape on the same path is `TraversalResult.{IsError, Error, Endpoints, Truncated}`: independent `init` properties, classic `{ passed, error }`.

## Code at this revision

- `src/Strategos.Ontology.MCP/TraversalResult.cs:37-67` — `IsError` defaults false; `Error` is nullable; `Endpoints` defaults `[]`; `Truncated` / `NextCursor` independent. No constructor invariant.
- `src/Strategos.Ontology.MCP/OntologyTraverseTool.cs:184-185` — production error helper sets both `IsError = true` and `Error = message`. Success paths (`:173-181`) set neither. The helper is not the type.
- `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs:376-416` — `MapTraversalResult`: if `result.IsError`, `ErrorResult(meta, result.Error ?? "traversal failed validation.")` — missing error still becomes an error call (fallback). Else serialize the whole `TraversalResult` into `StructuredContent` with `ResultType = complete`. An `IsError: false, Error: "…"` object takes the success path and the error string is in the JSON (`JsonIgnore` only when null, `:50`).
- `ErrorResult` (`:410-416`) sets `ResultType = CompletedResultType` (`"complete"`, `:57`) and `IsError = true`. INV-3 and the factory remarks say `complete` means finished, including `isError: true`. That combination is protocol-legal per the in-repo spec lead and is **not** recorded as an invalid state.

`Truncated: true, NextCursor: null` is a second pair: `MapTraversalResult` then adds a `resource_link` whose URI is `"strategos:ontology/traversal/" + ""` (`:397`).

## Failure scenario

A future caller (or a deserialized tool result) constructs `new TraversalResult(meta) { Error = "bad link" }` without `IsError`. The host returns a successful `CallToolResult` (`ResultType: complete`, `IsError` unset) whose structured content contains `"error": "bad link"`. Clients that key on `isError` treat it as success. Clients that read the body see a failure. Evidence is present; the result says pass.

The inverse (`IsError: true`, `Error: null`, `Endpoints` nonempty) is mapped to `ErrorResult` and drops the endpoints. The factory fails closed on the flag, not on the payload. The type still permits the contradictory object.

## Why not cheaper

Rung 1: not a generated type.

Rung 2: a discriminated result (`Success(endpoints, cursor)` | `Error(message)`) makes the mixed states unrepresentable. The `{ bool IsError; string? Error }` shape is the one this lens assigns to rung 2. Do not test it.

Rung 4: factory tests that go through `OntologyTraverseTool.Error` never construct the mixed state.

## Failure signal

Nothing. MCP clients see a complete success. The error string is in structured content. No page.

## Rollback

Close the type (discriminated union or required `Error` when `IsError`). Does not reverse already-emitted protocol payloads.

## Examined and not an obligation

`CallToolResult` `{ ResultType: "complete", IsError: true }` on `ErrorResult`. INV-3 (`CallToolResult.resultType` is `complete` | `input_required`) and `OntologyServerToolFactory.cs:51-56, 410-413` treat this as the finished-error encoding. This lens does not claim that pair must be unrepresentable.

## Open questions

- Does any production path construct `TraversalResult` besides `OntologyTraverseTool`? If only the helper and the two success returns exist, the mixed state is latent on the type and on any deserializer. Stakes: a wire/test fixture can still introduce it because `init` is public.
- Should `MapTraversalResult` treat `Error is not null` as error regardless of `IsError`? That would paper over the type at the boundary (rung 4/5) without closing the shape.

## What is expensive to find again

The new `ResultType` assignments sit on both arms and look like they closed the protocol shape. They did not close the core result record the success arm serializes verbatim.

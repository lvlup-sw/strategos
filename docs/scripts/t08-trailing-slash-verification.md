# T08 — Trailing-slash verification record

Captured: 2026-05-15 (worktree-agent-a9d3c9972b06cf569)

## What was tested

After T07 completed (`docs/dist/` rebuilt clean), checked the output of
`docs/dist/guide/installation/index.html` and curled both URL forms
against `astro preview` on `http://localhost:4321`.

## Internal Starlight-generated links inside the page

Every internal `href="/strategos/..."` reference inside the rendered
`guide/installation/` page ends in a trailing slash. Sample (sorted,
deduped):

```
/strategos/examples/
/strategos/examples/approval-flow/
/strategos/guide/
/strategos/guide/installation/
/strategos/learn/
/strategos/reference/
```

## curl against `npm run preview`

| URL                                              | HTTP |
|--------------------------------------------------|------|
| `/strategos/guide/installation` (no slash)       | 404  |
| `/strategos/guide/installation/` (with slash)    | 200  |

The Astro preview server enforces strict trailing-slash routing per
`trailingSlash: 'always'` in `astro.config.mjs` — bare paths 404 rather
than 301-redirecting. This is local-only behaviour.

## What GitHub Pages will serve

The static output is `docs/dist/guide/installation/index.html`. GitHub
Pages serves `foo/index.html` for both `/foo` and `/foo/` requests
(issuing a 301 redirect for the former), so external links missing the
trailing slash will resolve in production. This is called out as a
known consequence in design §5 ("External links to the old form will
receive a 301 from GitHub Pages' default trailing-slash redirect").

## Verification gate status

- Internal Starlight links all end in `/`: PASS
- With-slash URL returns 200 under preview: PASS
- No-slash URL returns non-error under preview: FAIL (404)

The no-slash failure under preview does not represent a deployment
defect; the design explicitly relies on GitHub Pages' default redirect
behaviour, which Astro's preview server does not emulate. Confirming
this end-to-end requires a draft PR deploy (covered by T22, which is
not in this agent's scope).

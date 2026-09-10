// docs/astro.config.mjs
import { defineConfig } from 'astro/config'
import { unified } from '@astrojs/markdown-remark'
import starlight from '@astrojs/starlight'

export default defineConfig({
  site: 'https://lvlup-sw.github.io',
  base: '/strategos/',
  trailingSlash: 'always',  // see decision below
  markdown: {
    // Astro 7 defaults to the Sätteri markdown pipeline, whose smart
    // punctuation differs from remark-smartypants: `--` renders as an en dash
    // instead of an em dash, LaTeX-style ``quotes'' are left verbatim, and
    // ellipsis handling changes. That drifted the rendered text of 18 pages on
    // the 6 -> 7 upgrade. Keep the unified (remark/rehype) processor so the
    // site renders byte-for-byte as before; migrating to Sätteri is a separate,
    // deliberate change that normalises `--` in the sources first.
    processor: unified(),
  },
  redirects: {
    // The 2.13 migration guide was folded into the 3.0 guide for 3.0.0-rc.1.
    // Astro does not prepend `base` to a redirect target (verified against the
    // built dist/), so the target carries the /strategos/ prefix explicitly.
    '/guide/ontology/migration-v2-13/': '/strategos/guide/ontology/migration-v3/',
  },
  integrations: [
    starlight({
      title: 'Strategos',
      description: 'Deterministic, auditable AI agent workflows for .NET',
      logo: { src: './src/assets/logo.svg' },
      favicon: '/logo.svg',
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/lvlup-sw/strategos' },
      ],
      editLink: {
        baseUrl: 'https://github.com/lvlup-sw/strategos/edit/main/docs/',
      },
      pagefind: true,
      sidebar: [
        { label: 'Learn', items: [{ autogenerate: { directory: 'learn' } }] },
        {
          label: 'Guide',
          items: [
            { autogenerate: { directory: 'guide', collapsed: false } },
            { label: 'Ontology', items: [{ autogenerate: { directory: 'guide/ontology' } }] },
          ],
        },
        {
          label: 'Reference',
          items: [
            { autogenerate: { directory: 'reference' } },
            { label: 'Ontology', items: [{ autogenerate: { directory: 'reference/ontology' } }] },
            { label: 'Diagnostics', items: [{ autogenerate: { directory: 'reference/diagnostics' } }] },
          ],
        },
        { label: 'Examples', items: [{ autogenerate: { directory: 'examples' } }] },
      ],
    }),
  ],
})

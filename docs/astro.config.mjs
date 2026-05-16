// docs/astro.config.mjs
import { defineConfig } from 'astro/config'
import starlight from '@astrojs/starlight'

export default defineConfig({
  site: 'https://lvlup-sw.github.io',
  base: '/strategos/',
  trailingSlash: 'always',  // see decision below
  integrations: [
    starlight({
      title: 'Strategos',
      description: 'Deterministic, auditable AI agent workflows for .NET',
      logo: { src: './public/logo.svg' },
      favicon: '/logo.svg',
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/lvlup-sw/strategos' },
      ],
      editLink: {
        baseUrl: 'https://github.com/lvlup-sw/strategos/edit/main/docs/src/content/docs/',
      },
      pagefind: true,
      sidebar: [
        { label: 'Learn', autogenerate: { directory: 'learn' } },
        {
          label: 'Guide',
          items: [
            { autogenerate: { directory: 'guide', collapsed: false } },
            { label: 'Ontology', autogenerate: { directory: 'guide/ontology' } },
          ],
        },
        {
          label: 'Reference',
          items: [
            { autogenerate: { directory: 'reference' } },
            { label: 'Ontology', autogenerate: { directory: 'reference/ontology' } },
            { label: 'Diagnostics', autogenerate: { directory: 'reference/diagnostics' } },
          ],
        },
        { label: 'Examples', autogenerate: { directory: 'examples' } },
      ],
    }),
  ],
})

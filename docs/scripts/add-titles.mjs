#!/usr/bin/env node
// One-shot helper used during T06 to add `title:` frontmatter to markdown
// files that VitePress didn't require but Starlight's docsSchema does.
// Derives the title from an existing H1; falls back to a filename-derived
// title.

import fs from 'node:fs'
import path from 'node:path'
import process from 'node:process'

const root = path.resolve(process.argv[2] || 'src/content/docs')

function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name)
    if (entry.isDirectory()) walk(p)
    else if (entry.isFile() && p.endsWith('.md')) processFile(p)
  }
}

function titleFromFilename(p) {
  const base = path.basename(p, '.md')
  const dir = path.basename(path.dirname(p))
  if (base === 'index') {
    // capitalize directory name
    return dir.charAt(0).toUpperCase() + dir.slice(1)
  }
  // Convert kebab-case to Title Case
  return base
    .split('-')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ')
}

function processFile(file) {
  const text = fs.readFileSync(file, 'utf8')

  // Skip if title is already present anywhere in a leading frontmatter block.
  if (/^---\n[\s\S]*?\ntitle:\s/m.test(text)) return
  if (/^title:\s/m.test(text.split('---').slice(0, 3).join('---'))) return

  let title
  const h1 = text.match(/^#\s+(.+?)\s*$/m)
  if (h1) {
    title = h1[1].trim()
  } else {
    title = titleFromFilename(file)
  }
  // Escape any double-quotes in the title for safe YAML.
  const safeTitle = title.replace(/"/g, '\\"')

  let updated
  if (text.startsWith('---\n')) {
    // Insert title after the opening fence.
    updated = text.replace(/^---\n/, `---\ntitle: "${safeTitle}"\n`)
  } else {
    updated = `---\ntitle: "${safeTitle}"\n---\n\n${text}`
  }
  fs.writeFileSync(file, updated)
  console.log(`titled: ${path.relative(root, file)} -> ${title}`)
}

walk(root)

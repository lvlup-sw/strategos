#!/usr/bin/env node
// One-shot helper used during T07 to fix dead markdown links surfaced by
// the move to Starlight. Three cases:
//
//   1. Same-collection link to an existing .md file
//      -> rewrite to the Starlight slug (drop .md, add trailing slash,
//         resolve `index.md` to `./`).
//
//   2. Cross-collection link to a file that exists in docs/ but was
//      excluded from the Starlight build (designs/, plans/, archive/,
//      theory/, standalone top-level *.md)
//      -> convert to https://github.com/lvlup-sw/strategos/blob/main/docs/<file>
//
//   3. Link target does not exist anywhere reachable
//      -> remove the markdown link, leaving the link text in place.
//
// External http(s) links are left alone, including ones that point at
// remote .md files on GitHub.

import fs from 'node:fs'
import path from 'node:path'
import process from 'node:process'

const collectionRoot = path.resolve('src/content/docs')
const docsRoot = path.resolve('.')
const repoRoot = path.resolve('..')
const ghBlobBase = 'https://github.com/lvlup-sw/strategos/blob/main/docs'

// Build the set of slugs (Starlight-style) for files inside the collection.
const collectionFiles = new Set()
function gatherCollection(dir, prefix = '') {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    const rel = prefix ? `${prefix}/${entry.name}` : entry.name
    if (entry.isDirectory()) gatherCollection(full, rel)
    else if (entry.isFile() && rel.endsWith('.md')) collectionFiles.add(rel)
  }
}
gatherCollection(collectionRoot)

// Build the set of files that live elsewhere under docs/ (excluded from build
// but still present in the repo) — for GitHub link conversion.
function existsUnderDocs(relFromDocs) {
  const full = path.resolve(docsRoot, relFromDocs)
  return fs.existsSync(full) && fs.statSync(full).isFile()
}

// Resolve a relative link target against a source file's directory.
function resolveTarget(sourceFile, target) {
  // Strip query/hash for resolution but keep them for output.
  const hashIdx = target.search(/[#?]/)
  const cleanTarget = hashIdx === -1 ? target : target.slice(0, hashIdx)
  const suffix = hashIdx === -1 ? '' : target.slice(hashIdx)
  const sourceDir = path.dirname(sourceFile)
  const resolved = path.normalize(path.join(sourceDir, cleanTarget))
  return { resolved, suffix }
}

// Convert a normalized absolute path under the collection root to a Starlight URL slug.
function toStarlightSlug(absInCollection) {
  let rel = path.relative(collectionRoot, absInCollection).replace(/\\/g, '/')
  // Drop .md
  if (rel.endsWith('.md')) rel = rel.slice(0, -3)
  // index -> directory
  if (rel === 'index') return '/strategos/'
  if (rel.endsWith('/index')) rel = rel.slice(0, -'/index'.length)
  return `/strategos/${rel}/`
}

let stats = { rewritten: 0, ghLink: 0, removed: 0, kept: 0 }

function processFile(file) {
  const text = fs.readFileSync(file, 'utf8')
  // Match standard markdown links: [text](target) — skip image refs (![...](...)
  // and link references like [text][ref].
  const linkRe = /(?<!\!)\[([^\]]+)\]\(([^)\s]+)\)/g
  let out = ''
  let lastIdx = 0
  let m
  while ((m = linkRe.exec(text)) !== null) {
    const [whole, linkText, target] = m
    out += text.slice(lastIdx, m.index)
    lastIdx = m.index + whole.length

    // Leave external links (http/https/mailto/etc) alone.
    if (/^(https?:|mailto:|ftp:|#)/i.test(target)) {
      out += whole
      stats.kept++
      continue
    }
    // Leave plain anchors and root-absolute non-md links alone.
    if (!target.includes('.md')) {
      out += whole
      stats.kept++
      continue
    }

    const { resolved, suffix } = resolveTarget(file, target)

    // Case 1: target lands inside the collection on an existing file.
    if (resolved.startsWith(collectionRoot + path.sep) || resolved === collectionRoot) {
      const relInColl = path.relative(collectionRoot, resolved).replace(/\\/g, '/')
      if (collectionFiles.has(relInColl)) {
        const slug = toStarlightSlug(resolved) + suffix
        out += `[${linkText}](${slug})`
        stats.rewritten++
        continue
      }
    }

    // Case 2: target is somewhere under docs/ but not in the collection — link to GitHub.
    if (resolved.startsWith(docsRoot + path.sep)) {
      const relFromDocs = path.relative(docsRoot, resolved).replace(/\\/g, '/')
      if (existsUnderDocs(relFromDocs)) {
        const ghUrl = `${ghBlobBase}/${relFromDocs}${suffix}`
        out += `[${linkText}](${ghUrl})`
        stats.ghLink++
        continue
      }
    }

    // Case 2b: try resolving the link as if the source file had been at its
    // pre-T06 location under docs/ (e.g. `./design.md` from a file now at
    // src/content/docs/learn/maf-deep-dive.md was originally docs/design.md).
    if (file.startsWith(collectionRoot + path.sep)) {
      const relInColl = path.relative(collectionRoot, file).replace(/\\/g, '/')
      const originalSourceFile = path.resolve(docsRoot, relInColl)
      const { resolved: legacyResolved } = resolveTarget(originalSourceFile, target)
      if (legacyResolved.startsWith(docsRoot + path.sep)) {
        const relFromDocs = path.relative(docsRoot, legacyResolved).replace(/\\/g, '/')
        if (existsUnderDocs(relFromDocs)) {
          // First check if the legacy-resolved file is now in the collection.
          if (collectionFiles.has(relFromDocs)) {
            const slug = toStarlightSlug(path.resolve(collectionRoot, relFromDocs)) + suffix
            out += `[${linkText}](${slug})`
            stats.rewritten++
            continue
          }
          // Otherwise it's an excluded file — link to GitHub.
          const ghUrl = `${ghBlobBase}/${relFromDocs}${suffix}`
          out += `[${linkText}](${ghUrl})`
          stats.ghLink++
          continue
        }
      }
    }

    // Case 3: target unresolvable — strip the link, keep the text.
    // Set DEBUG_REMOVED=1 in the environment to print every removed link
    // for spot-checking.
    if (process.env.DEBUG_REMOVED) {
      console.error(`removed in ${path.relative(docsRoot, file)}: ${target}`)
    }
    out += linkText
    stats.removed++
  }
  out += text.slice(lastIdx)

  if (out !== text) {
    fs.writeFileSync(file, out)
    return true
  }
  return false
}

function walk(dir) {
  let changed = []
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name)
    if (entry.isDirectory()) changed = changed.concat(walk(p))
    else if (entry.isFile() && p.endsWith('.md')) {
      if (processFile(p)) changed.push(p)
    }
  }
  return changed
}

const changed = walk(collectionRoot)
console.log(`Files changed: ${changed.length}`)
console.log(`Links rewritten (in-collection): ${stats.rewritten}`)
console.log(`Links converted to GitHub:        ${stats.ghLink}`)
console.log(`Links removed (target missing):   ${stats.removed}`)
console.log(`Links untouched:                  ${stats.kept}`)

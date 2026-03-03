# Contributing to Documentation

Thank you for your interest in improving the Strategos documentation! This guide will help you get started with local development and submitting changes.

## Prerequisites

- **Node.js 20+** - Required for VitePress
- **npm** - Package manager (comes with Node.js)
- **Git** - Version control

## Local Development

### 1. Clone the Repository

```bash
git clone https://github.com/lvlup-sw/strategos.git
cd strategos
```

### 2. Install Dependencies

```bash
cd docs
npm install
```

### 3. Start Development Server

```bash
npm run docs:dev
```

This starts the VitePress development server at `http://localhost:5173`. Changes to markdown files will hot-reload automatically.

### 4. Build for Production

```bash
npm run docs:build
```

The built site will be in `docs/.vitepress/dist/`.

### 5. Preview Production Build

```bash
npm run docs:preview
```

## File Structure

```
docs/
├── .vitepress/
│   ├── config.ts        # VitePress configuration
│   └── theme/           # Custom theme components
├── public/              # Static assets (images, files)
├── learn/               # Core concepts and value proposition
├── guide/               # Step-by-step tutorials
├── reference/           # API documentation
├── examples/            # Real-world workflow examples
├── contributing.md      # This file
└── index.md             # Homepage
```

### Content Directories

| Directory | Purpose |
|-----------|---------|
| `learn/` | Conceptual content explaining the "why" |
| `guide/` | Practical tutorials showing the "how" |
| `reference/` | API documentation and technical specs |
| `examples/` | Complete, runnable workflow examples |

## Writing Guidelines

### Markdown Conventions

- Use ATX-style headers (`#`, `##`, `###`)
- Include a title (`# Page Title`) at the top of every page
- Use fenced code blocks with language identifiers

### Code Examples

Always specify the language for syntax highlighting:

~~~markdown
```csharp
// C# code here
```
~~~

For shell commands:

~~~markdown
```bash
dotnet add package LevelUp.Strategos
```
~~~

### Frontmatter

Each page can have YAML frontmatter for metadata:

```markdown
---
title: Page Title
description: Brief description for SEO
---

# Page Title
```

### Links

Use relative links for internal pages:

```markdown
[See the guide](../guide/getting-started.md)
```

### Images

Place images in `docs/public/` and reference them with absolute paths:

```markdown
![Architecture diagram](/images/architecture.png)
```

## Style Guidelines

### Voice and Tone

- **Be concise** - Developers prefer scannable content
- **Use active voice** - "Configure the service" not "The service should be configured"
- **Be specific** - Include concrete examples, not just abstract concepts

### Structure

- **Lead with value** - Start sections with why it matters
- **Show, then tell** - Code example first, explanation after
- **Use progressive disclosure** - Simple case first, advanced options later

### Headings

- Use sentence case for headings
- Keep headings under 60 characters
- Make headings descriptive and scannable

## Submitting Changes

### 1. Create a Branch

```bash
git checkout -b docs/your-topic
```

Use the `docs/` prefix for documentation-only changes.

### 2. Make Your Changes

Edit the markdown files and verify with the dev server.

### 3. Commit

```bash
git add .
git commit -m "docs: describe your change"
```

Use conventional commit messages:
- `docs: add guide for branching workflows`
- `docs: fix broken link in API reference`
- `docs: improve clarity of quick start`

### 4. Push and Open PR

```bash
git push origin docs/your-topic
```

Open a pull request against `main`. Include:
- What you changed and why
- Screenshots if adding visual content
- Link to related issues if applicable

## Questions?

- Open an issue for documentation bugs or suggestions
- Check existing issues before creating new ones
- Tag documentation issues with the `documentation` label

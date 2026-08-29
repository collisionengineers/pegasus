---
id: DOCS-015
type: ticket
title: Extract and normalize the complete EVA API PDF as Markdown
status: verifying
area: documents-reports
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-28T14:47:11.297Z'
  review: '2026-08-28T14:58:12.530Z'
  verifying: '2026-08-29T20:55:39.489Z'
taken_at: '2026-08-28T14:48:51.601Z'
branch: task/docs-015-eva-api-markdown
worktree: ../pegasus-worktrees/docs-015-eva-api-markdown
labels:
  - documentation
  - eva
  - pdf-extraction
  - json-parity
links:
  - TICK-077
refs:
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
commits:
  - c84c7a05019e1b56db7c47f0b806eed3d615c456
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/600'
deployment: n/a
archived: false
created: '2026-08-28T14:47:06.673Z'
updated: '2026-08-29T20:55:39.489Z'
---

## What

Extract `docs/json-extraction-parity/eva-api-docs.pdf` into a formatted and
normalized Markdown document beside the source.

## Why

The EVA API reference needs a searchable, diffable text representation for JSON
extraction parity work. The conversion must preserve every source datum while
normalizing headings, whitespace, lists, tables, code blocks and repeated page
furniture.

## Boundaries

Do not reinterpret the API contract, remove repeated-but-meaningful content, or
modify [[TICK-077]]'s implementation. Preserve the source PDF unchanged.

## Verification

Compare the complete Markdown output against all PDF pages, including tables,
examples, headers, footers, annotations and images containing text.

## Outcome

---
id: DOCS-013
type: ticket
title: Strike the invented manifest from FRD-07 and the operator notes
status: verifying
area: documents-reports
assignee: claude-code
profile: chore
stageEntered:
  implementing: '2026-08-24T08:44:28.332Z'
  review: '2026-08-24T08:46:59.052Z'
  verifying: '2026-08-24T14:56:54.885Z'
taken_at: '2026-08-24T08:42:35.076Z'
branch: task/docs-013-strike-eva-manifest
worktree: ../pegasus-worktrees/docs-013
labels:
  - qdos26015
  - eva
  - export
  - governance
links: []
blocks:
  - ENG-014
  - ENG-015
refs:
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
archived: false
created: '2026-08-24T08:19:34.397Z'
updated: '2026-08-24T14:56:54.885Z'
---

## What

Remove the SHA-256 manifest from every governing doc that asserts it. It was
never an operator requirement.

While in FRD-07, also say which reference the `Reference` key carries, and
describe the [[CASE-019]] operator export alongside the gated hand-off.

## Why

Operator direction (2026-08-24): the manifest is an AI invention that entered
the governing docs and was never asked for. Checked before acting, and the
claim holds:

- The word "manifest" appears **nowhere** in `reference/` — not in the EVA
  schema, the screenshots, the EVA information notes, or either of the two
  retained JSON examples. That is the whole operator-supplied corpus.
- It entered FRD-07 via `2e3db7aa` ("adopt PRD/FRD/ADR taxonomy") and
  `operator-notes.md` via `3f4a35ba` ("release-3 record") — internal doc
  restructuring, not an operator statement.
- The predecessor extractor, whose output EVA actually accepts, emits a bare
  JSON and nothing else.

The docs must stop mandating it before [[ENG-014]] stops producing it. Today
FRD-07 says:

> …and **writes a SHA-256 manifest** over the JSON and image identities and
> bytes.

> The container format is intentionally unspecified: its selection must evaluate
> whether a single archive is the clearest usable representation **without
> changing the exact package contents, manifest**, or manual-handoff boundary.

FRD-07 also lists `Reference` as a key but has never said *which* reference —
that choice has only ever existed in code, which is how the export came to carry
the Pegasus case reference instead of the provider's ([[ENG-015]]).

## Every place that repeats it

Wider than FRD-07 — six files assert it:

| File | Line | What it says |
| --- | --- | --- |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | 12, 42, 45 | the mandate itself, plus the clause forbidding its removal |
| `docs/operator-notes.md` | 505 | "a reviewed JSON/image/manifest download" |
| `docs/capabilities.md` | 175 | EXT-03 row: "and a SHA-256 manifest" |
| `docs/design/README.md` | 697 | "EVA JSON/image/manifest generation" |
| `docs/open-decisions.md` | 29, 240 | "13-key JSON + images + SHA-256 manifest"; "manual JSON/image/manifest handoff" |
| `docs/current-architecture.md` | 526 | as-built: "provenance and manifest" — **left to [[ENG-014]]**, which changes the as-built shape |

**Do not touch** the release-artifact manifests in `docs/operations.md` and
`docs/adr/0007`, `0015` — different concept, same word.

## Approach

- FRD-07: remove the manifest mandate and the "without changing … manifest"
  clause. The package is the ordered 13-key UTF-8 JSON plus every eligible
  image.
- FRD-07: state that `Reference` carries the work provider's own reference
  (EVA's `Claim no`), not the Pegasus case reference (EVA's `Case/Po`) — per
  `reference/eva_information/eva_information.md:31-45`.
- FRD-07: add the operator export as a distinct artefact — same package, but not
  a hand-off: it records no revision and no `First sent to Engineer` proxy.
- `capabilities.md`, `design/README.md`, `open-decisions.md`: strike the
  manifest from the EXT-03 description and the two handoff descriptions.
- `docs/operator-notes.md:505`: "a reviewed JSON/image/manifest download" becomes
  "a reviewed JSON/image download".

**`operator-notes.md` is protected.** This edit changes its meaning, and the
rails require user resolution before that. The resolution is the operator
direction recorded above, given 2026-08-24 in response to the traced evidence.
Note it in the PR description explicitly rather than letting it pass as a
routine wording change.

Docs-only: the simplification pass records "n/a — docs-only".

## Verification

- [ ] `grep -rn -i manifest docs/ --include=*.md` returns only release-artifact
      hits (`operations.md`, `adr/0007`, `adr/0015`) and the
      `current-architecture.md` line owned by [[ENG-014]]
- [ ] FRD-07 names both artefacts and no longer reads as forbidding the change
- [ ] FRD-07 says which reference `Reference` carries
- [ ] `docs/index.md` authority chain still resolves
- [ ] The PR description names the protected-file edit and its authority

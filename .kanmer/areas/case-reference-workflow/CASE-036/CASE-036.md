---
id: CASE-036
type: ticket
title: >-
  Salvage the Case workspace one-list refactor stranded on
  task/case-012-case-workspace-parallel
status: backlog
area: case-reference-workflow
order: 160
assignee: ''
profile: fix
labels:
  - simplification
  - one-list-per-concept
  - salvage
  - case
groups:
  - EPIC-011
links:
  - CASE-012
refs:
  - docs/frd/frd-12-operator-experience.md
deployment: not-deployed
archived: false
created: '2026-08-30T20:32:56.589Z'
updated: '2026-09-03T15:15:27.226Z'
---

## What

Commit **`866fe459`** on branch `task/case-012-case-workspace-parallel` —
*"refactor(ui): one section list, one Due rule, one editing flag on the Case
workspace model (CASE-012)"* — is unmerged, unclaimed by any ticket, and is the
only copy of that work.

It is the branch's **single unique commit**: its four ancestors
(`54da5583`, `ac5bb48f`, `f70b9fb8`, `5d89f0c7`) reached `main` through
[[CASE-012]]'s own PRs #599 / #615. Only the refactor on top did not.

```
git rev-list --count origin/main..task/case-012-case-workspace-parallel  -> 5
git log --oneline origin/main..task/case-012-case-workspace-parallel
  866fe459 refactor(ui): one section list, one Due rule, one editing flag ...
  (+4 already-merged ancestors)
```

## Why it is worth keeping

It is the repository's own **one list per concept** rail applied to the Case
workspace model: a single section list, a single Due rule, and a single editing
flag, replacing copies spread across the page model and `_CaseWorkspaceNav`.

That is a named rail in `CLAUDE.md` — *"An exception taxonomy, a state
vocabulary, a label table, a precedence order lives in exactly one place. A
second copy in another layer is duplication even when it is 'just strings'."*
Discarding a written implementation of it and re-deriving it later is waste.

## Why it was stranded

[[CASE-012]] was worked on a **different** branch,
`task/case-012-eva-send-salvage`, which is what merged. Nothing on the board
records this one, so the board-hygiene sweep of 2026-08-30 found it as an
orphan: a branch with unmerged commits and no ticket claim. It was deliberately
**excluded from that sweep's branch deletion** and left in place — this ticket
is what keeps it from being collected next time.

## Approach

- Rebase `866fe459` onto current `dev` and read it as a fresh diff. Release 37
  rewrote large parts of the Case workspace since it was written, so treat it as
  a **proposal**, not a patch to replay: the duplication it removes may have
  moved, been partly fixed, or grown.
- Confirm against `main` which of the three duplications still exist before
  changing anything — the premise is a fact about the code, so check it rather
  than argue it.
- Behaviour-preserving only. If the rebase surfaces a behaviour change, that is a
  finding to report, not to carry.
- If the duplication has already been resolved by later lanes, **close this
  ticket as superseded and delete the branch** — that is a legitimate outcome and
  is cheaper than a forced merge.

## Boundaries

- Do **not** delete `task/case-012-case-workspace-parallel` until this ticket is
  resolved either way.
- This is quality, not correctness. A bug found in passing goes to its own
  ticket.

## Verification

- [ ] Each of the three duplications is either removed or shown to be already gone
- [ ] `dotnet build ./Pegasus.slnx --configuration Release` and the Case test suites pass
- [ ] No behaviour change in the Case workspace, proven by the existing assertions
      passing unmodified
- [ ] The branch is deleted once the work lands or is judged superseded

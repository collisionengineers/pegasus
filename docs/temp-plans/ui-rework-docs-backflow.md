# UI rework documentation backflow

Task slug: `ui-rework-docs-backflow`. Branch: `task/ui-rework-docs-backflow`.
Taken 2026-08-04.

## The defect

A full branch and worktree audit on 2026-08-04 found one body of work that
never travelled the `dev` → `main` → deploy path.

`main` commit `440ab5c` ("ui-rework-docs", 2026-08-04 17:57 +0100) is a
non-merge commit sitting directly on `main`'s first-parent chain. It was
committed straight onto `main` rather than merged from `dev`, so `dev` never
received it. It is the only non-merge commit in `origin/dev..origin/main`:

```
git log --oneline --no-merges origin/dev..origin/main
440ab5c ui-rework-docs
```

`docs/ui-work/` therefore exists on `main` and is absent from `dev`
(`git ls-tree -d origin/dev docs/ui-work` returns nothing). The direction of
travel is backwards: the deployment branch holds documentation the working
trunk has never seen, and every future task branching from `dev` is blind to
it.

The remaining four commits in `origin/dev..origin/main` are the `dev` → `main`
release merges (PRs 320, 330, 334) and the merge that carried `440ab5c` onto
the pushed tip. They introduce no content that `dev` lacks.

## What this task restores

All 202 files of `docs/ui-work/`, byte-for-byte from `440ab5c`:

- `ui-standards-and-review.md`, `durable-rules-proposal.md`,
  `defects-and-non-functional.md`, `additions-hidden-features.md`.
- 31 per-page directories (`page-1-operations` … `page-31-admin-automation-
  activity`), each holding some combination of a `review.md`, captured
  screenshots, and a `proposed-changes-and-mockup/` set of
  `alteration-plan.md`, `wireframe.md`, `mockup-hardened.html`, and
  `mockup-refreshed.html`.

Restoration is `git checkout 440ab5c -- docs/ui-work`. Nothing is edited,
reworded, renamed, or reformatted, and the resulting `docs/ui-work` tree
hashes identically to `main`'s.

## What this task deliberately excludes

`440ab5c` also carried two files this branch does not take:

- `docs/temp-plans/mcp-assessment-toolset.md`
- `docs/temp-plans/send-to-claude-channel-integration.md`

Both are the pre-implementation snapshot of those plans. The open PR 332
(`task/send-to-ai-round-trip`) carries newer copies of the same two files,
each with a `Status: implemented by task/send-to-ai-round-trip (2026-08-03)`
header recording which slices shipped and where the still-open decisions
moved. Taking `main`'s older copies here would put a stale status in front of
PR 332 and hand it a merge conflict for no gain. PR 332 remains the route by
which those two plans reach `dev`.

## Scope boundaries

- Documentation move only. No source, project, test, or configuration file is
  touched, so no build or behaviour changes.
- Restoring these proposals is **not** adopting them. `docs/ui-work/` holds
  reviews, proposals, wireframes and mockups; none of it is accepted design
  authority, none of it supersedes the canonical documentation in the
  [index](../index.md), and nothing here allocates or claims a capability.
- The mockup `.html` files are static review artefacts. They are not wired
  into `Pegasus.Web`, not served, and not referenced by any caller.
- No ADR is required: no new top-level directory, project, store, runtime,
  migration stream, or deployment unit is created — `docs/` already exists and
  already carries the content on `main`.

## Verification

- `git diff --stat origin/dev origin/task/ui-rework-docs-backflow` shows only
  `docs/ui-work/` additions plus this plan.
- The branch's `docs/ui-work` tree object equals `main`'s
  (`6930d0aba9d1a7c6c4a1d1be99e25605b0763d40`), proving a byte-exact restore.
- `dotnet build --configuration Release` — unchanged by a docs-only branch,
  and CI runs it regardless.
- After merge, `git ls-tree -d origin/dev docs/ui-work` resolves, and
  `git log --oneline --no-merges origin/dev..origin/main` is empty: the
  divergence that motivated this task is gone.

## Close-out

On merge, delete this plan and the `NOW.md` claim line. Nothing is queued from
this task; deciding what to do with the proposals in `docs/ui-work/` is
separate work that needs its own claim.

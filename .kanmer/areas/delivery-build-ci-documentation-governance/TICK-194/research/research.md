# Research — TICK-194: detect non-merge pushes to main

## Question

How can the existing `main` push workflow detect history that violates the
repository's merge-only release rule without touching the active UI revamp?

## Findings

- `docs/engineering.md` ("Branches and delivery") is the current normative
  rule: `dev` merges into `main` through a PR as a merge commit, and neither
  branch may be rebased, reset, or force-pushed.
- `.github/workflows/ci.yml` already runs `repository-check` for every push
  to `main`, checks out full history in its always-running `changes` job,
  and derives the push range from `github.event.before` and `github.sha`.
  That job is therefore the earliest existing caller for this guard.
- The current workflow has no history-shape validation. A direct single-parent
  commit pushed to `main` proceeds into the same path-detection/build lanes as
  an allowed merge.
- The correct range is the new first-parent segment from the push event's
  `before` revision to its head. Requiring every commit on that segment to
  have two parents detects both a direct commit and a batch push containing a
  non-merge mainline commit, while ignoring ordinary task commits brought in
  through the merge's second parent.
- The guard must fail closed when the before revision is missing from the
  checkout, is the all-zero sentinel, or is not an ancestor of the pushed head;
  those shapes cannot prove a permitted append-only merge.
- Local history contains examples of both shapes: merge commits such as
  `2ebca4a1` and `c62dceb4` have two parents, while `609fa900` is a
  single-parent commit. These are research evidence only; tests should create
  synthetic repositories rather than bind to mutable project history.
- A reusable PowerShell script is preferable to embedding the policy entirely
  in YAML: it permits deterministic positive and negative tests using temporary
  Git repositories and keeps the workflow step small.
- This control is detection, not prevention: a push-triggered workflow reports
  the violation after GitHub has accepted the push. Branch protection remains
  the prevention boundary and is outside this ticket unless separately
  authorised.
- `EPIC-001/context.md` excludes `src/Pegasus.Web/**`, UI browser/snapshot
  tests, `design/**`, and `.stitch/**`. None is needed for this guard.
- Current active worktree diffs for `task/doc-01-box-resolution`,
  `task/report-renderer-integration`, `task/simpli-009`, and
  `task/simpli-010` do not touch the proposed workflow/script/test paths.
  The active UI revamp has uncommitted `design/**` and `.stitch/**` paths,
  also with no overlap.
- `KANMER-001` owns retargeting tickets that cite retired the retired pre-Kanmer tracker (historical evidence), so it
  may rewrite this ticket's stale source note; implementation must not treat
  that note as authority. `KANMER-002` is a broad documentation-cleanup claim,
  so an edit to `docs/engineering.md` requires coordination or should be
  omitted if the existing rule plus workflow comments are sufficient.

## Implications

Implement the check in the existing `changes` job immediately after the
full-history checkout, backed by a repository script that accepts explicit
before/head revisions. Test an allowed merge-only append, a direct commit, a
mixed batch containing a direct first-parent commit, a non-ancestor/forced
rewrite, and an unavailable or zero before revision. Keep all UI and design
paths out of scope. Treat a governance-doc edit as conditional on clearing the
`KANMER-002` ownership boundary.

## Open questions

None. The repository already fixes the allowed history shape and this ticket
only adds post-push CI detection.

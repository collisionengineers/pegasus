# Proof — PLAT-006

## Merge

PR #409, merge commit `feda958fd863f1eec6fd5c7eede811a91e4faf0d` on `dev`/`main`.

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`, deployed
2026-08-19 ~22:40–22:52Z). `feda958f` is a verified ancestor of `ed3be51c`.
See [[DELIV-012]] proof for the release-12 deployment readbacks (revision
`--ed3be51c95bc` Healthy, `/diagnostics/version` match, smoke exit 0).

## Production evidence (this ticket's own behaviour)

Per [[DELIV-012]] proof's signed-in production browser verification:

- **Centred shell**: the content region beside the rail renders centred at
  wide viewports (the shell defect this ticket fixed).
- **Upload redesign**: `/Upload` renders the redesigned, centred page with a
  real whole-area drop target and "up to 20 files per submission" copy,
  replacing the old floating card and non-functional dashed dropzone.

## Qualification

The visual sweep across every page family (Dashboard, Inbox, Queues, Cases,
Case detail, Assessment, Administration, Operations, Upload status, New
case) at 1440/1920 was the ticket's own stated verification step; the
release-12 production check above confirms Upload and the shell directly.
The Upload page itself was superseded in **release 13** by [[INTK-010]]
(operator-directed rework of file rows / confirmation step) — that later
change does not contradict this ticket's shell-centring fix, which remains
live.

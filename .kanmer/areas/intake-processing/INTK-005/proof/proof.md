# Proof — INTK-005

## Merge

PR #416, merge commit `e18512a683d60c68e4019621b05191b426fa4169` on `dev`/`main`.

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`, deployed
2026-08-19 ~22:40–22:52Z). `e18512a6` is a verified ancestor of `ed3be51c`.
See [[DELIV-012]] proof for the release-12 deployment readbacks.

## Production evidence

Multi-file Upload browser-verified in production: the redesigned `/Upload`
page accepts multiple selected files in one submission and states "up to 20
files per submission" — the grouped-submission capability this ticket built.
The real production group `G6KDL-01` ([[INTK-006]]/[[INTK-008]] evidence)
is itself proof of a durable submission-group identity created through this
ticket's upload path.

## Qualification

The Upload page's file-selection UI was subsequently reworked in **release
13** by [[INTK-010]] (per-file rows with spinner/tick, confirmation step);
that is a presentation change on top of this ticket's group-submission
contract, not a reversal of it — the durable submission-group identity and
multi-file acceptance this ticket delivered remain the underlying mechanism.

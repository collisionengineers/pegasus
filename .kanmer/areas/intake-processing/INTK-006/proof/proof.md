# Proof — INTK-006

## Merge

PR #417, merge commit `df19475815443b74111084cae35838acd77a5e91` on `dev`/`main`.

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`, deployed
2026-08-19 ~22:40–22:52Z). `df194758` is a verified ancestor of `ed3be51c`.
See [[DELIV-012]] proof for the release-12 deployment readbacks.

## Production evidence

The real production group `G6KDL-01` (VRM `G6KDL`, `awaiting_instruction`)
registered from a real two-image upload on 2026-08-19 — direct evidence of
this ticket's grouped-recognition/routing policy running against production
data: one usable VRM registered the group into an Image-initiated Case per
the one-existing-Case/no-match branching this ticket implements.

## Honest qualification — the split-race defect

The same production event proved a genuine concurrency defect in the group
outcome: the group's two members were processed concurrently and one
(ordinal 0, the PNG) registered into `G6KDL-01` while its sibling (ordinal 1,
the JPEG) fell through to the generic instruction-fallback `needs_sorting`
path instead of joining the same group outcome — stranding it outside both
the case and Unidentified. This is **not** a defect in INTK-006's routing
*policy* (`ImageIntakeGroupRoutingPolicy.Evaluate`, confirmed pure and
correct by [[INTK-011]]'s root-cause analysis); it is a concurrency gap in
how per-member work items apply the group's outcome, closed by
**[[INTK-011]] in release 13** (`2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`,
deployed 2026-08-20). The stranded JPEG was recovered by INTK-011's
reconciliation mechanism as `U6` (its own escalation branch, since it
predated the fix — see INTK-011's proof).

# Proof — INTK-008

## Merge

PR #423, merge commit `a907ecd26b5586fa467b4a9a736d5ff1ad9256bc` on `dev`/`main`.

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`, deployed
2026-08-19 ~22:40–22:52Z). `a907ecd2` is a verified ancestor of `ed3be51c`.
See [[DELIV-012]] proof for the release-12 deployment readbacks.

## Production evidence

The real production Image-initiated Case `G6KDL-01` sitting in the
`awaiting_instruction` lifecycle state ([[INTK-006]] production evidence) is
the live evidence for this ticket's lifecycle contract: the ImageIntake
aggregate exposed as a searchable Image-initiated Case with an explicit
lifecycle state (awaiting instruction / merged-subsumed / staff-closed),
separate from the formal Case/PO and Unidentified reference sequences.

## Qualification

Production evidence covers the `awaiting_instruction` state only — the
merge/subsumption-into-an-Instruction-initiated-Case and staff-closure paths
are not independently exercised by the `G6KDL-01` evidence (no matching
instruction has arrived for that VRM). The Queues surface presenting
Image-initiated Cases (originally this ticket's UI) was rebuilt by
[[INTK-009]] in release 13; that is a presentation change on the same
underlying lifecycle data, not a reversal.

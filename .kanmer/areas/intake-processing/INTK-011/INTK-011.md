---
id: INTK-011
type: ticket
title: >-
  Make the image-group outcome atomic: one readable VRM registers the whole
  group, no member terminal-decides alone
status: done
area: intake-processing
order: 1160
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-19T23:34:56.406Z'
  review: '2026-08-20T00:39:12.877Z'
  verifying: '2026-08-20T00:57:20.511Z'
  done: '2026-08-20T01:29:45.404Z'
labels:
  - defect
  - concurrency
  - image-intake
  - grouped-upload
  - production
  - operator-reported
links:
  - DELIV-012
  - INTK-005
  - INTK-006
  - INTK-008
  - INTK-010
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
commits:
  - ad1ecb1f
  - 4f1503eb
  - ef5186a0
  - 777d2762
prs:
  - '434'
deployment: production
archived: false
created: '2026-08-19T23:21:58.876Z'
updated: '2026-08-26T14:34:44.575Z'
---

## What

A real production upload on 2026-08-19 proved the grouped-image contract is broken by a concurrency race. Fix it so the group outcome is atomic: when a group yields one usable VRM, **every member** lands in the same Image-initiated Case; no member ever terminal-decides through the instruction fallback while its group is still resolving. Recover the currently stranded member.

## Production evidence (read-only, 2026-08-19 ~23:15Z, release 12 `ed3be51c`)

The operator uploaded two images as one submission (their report, verbatim: *"I uploaded 2 images, but it didnt even create an image initiated case. They are not even in unidentified. Its not clear what happened to them."*). What actually happened:

- Group `63971830-51b1-4955-b2c1-5778f8ab15b4`, 2 members: ordinal 0 `{4D7C134E-1F83-4576-A5A7-BFE51847A376}.png`, ordinal 1 `WhatsApp Image 2026-05-20 at 9.47.24 AM (1).jpeg`. Both received `23:14:37`.
- **Both receipts carry `ProcessedAtUtc = 23:15:13`** — processed concurrently by independent work items.
- ImageIntake **`G6KDL-01`** (VRM `G6KDL`, `awaiting_instruction`) created at `23:15:15` — **after** both members had already terminal-decided — with `OriginReceiptId` = the PNG only.
- The PNG's decision: `image_intake_registered` ("Image intake G6KDL-01 was registered"). The JPEG's decision: `needs_sorting` — *"No accepted intake route established the principal for automatic case creation"* — the generic **instruction-route** fallback, and it received **no U-reference** either.

Net effect: the group split. One member registered alone; the sibling is stranded invisible (not in the case, not Unidentified, reachable only by its receipt). The operator could see none of this — also why it *looked* like nothing happened (compounded by ImageIntake having no navigation entry, which [[INTK-009]] fixes).

## Root cause shape (verify in code, do not assume)

Two concurrent per-member work items each ran the full pipeline; the group aggregation ran inside one member's pass and registered the ImageIntake from that member's receipt after the sibling's pass had already fallen through to the instruction fallback and committed `needs_sorting`. This is the exact edge INTK-006's review left as its one unapplied finding — *"Retry incomplete group registration … needs a durable per-group outcome record"* — dispositioned as delegated to [[INTK-008]], where it was never implemented. The gap fell between tickets; this ticket closes it.

## Required behaviour

- A durable per-group outcome: members of a pending group must not terminal-decide through the instruction fallback; they wait (retry/defer via the existing durable-work conventions) until the group resolves, then **all** members take the group outcome (associate to the located case / register into the one Image-initiated Case / one grouped `U<n>`).
- The registered Image-initiated Case carries every group member's image, per the [[INTK-006]] contract ("the group is the evidence unit").
- **Reconciliation for stragglers**: a group that resolved while a member sits in a stale terminal state gets re-evaluated — which also recovers receipt `5b4c8cbd-c40a-43a0-b5c0-73c1c447ada2` (the stranded JPEG) into `G6KDL-01` without manual SQL. Recovery must be by the product's own mechanism.
- Fail-closed rules unchanged: ambiguity still hands off; nothing here weakens INT-28's accepted-bar rules.
- Tests must cover the actual race: two members processed concurrently (the existing concurrency-test conventions, e.g. the parallel-retry style in `QdosAllocationRecoveryTests`), asserting one atomic group outcome and zero instruction-fallback escapes.

## Verification

- [ ] Concurrent two-member group with one readable VRM → one Image-initiated Case containing both members, deterministically, across repeated runs.
- [ ] A member of a pending group can never commit a `needs_sorting` instruction-fallback decision.
- [ ] The reconciliation path pulls the production straggler `5b4c8cbd…` into `G6KDL-01` (verified on production after the fix deploys).
- [ ] Core + integration + Browser suites green.

## Outcome

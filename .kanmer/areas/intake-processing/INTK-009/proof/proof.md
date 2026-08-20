# Proof — INTK-009

## Merge

PR #432, merge commit `0b43190d66414bf0fb0b21d79e271bf0f7114478` on `dev`/`main`.

## Deployment

Shipped in **release 13** (`2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`,
deployed 2026-08-20 ~01:10–01:20Z). `0b43190d` is a verified ancestor of
`2325ed4a`. See [[DELIV-012]] proof (Appendix — Release 13) for the
deployment readbacks.

## Production evidence

Browser-verified on production per [[DELIV-012]] proof: Unidentified as a
Queues tab (`Not ready 1 · Review 0 · Held 0 · Triage 0 · Unidentified 6`)
with All/Images/E-mails filters and one-line no-GUID rows (e.g. `U1 | E-mail
| (No subject) — from nduncombe@qdosassist.co.uk | 12 Aug 2026 15:26 | No
usable identification`); Not-ready origin filters (Instruction-initiated /
Image-initiated) live on the tab. This is exactly the operator's three
verbatim complaints against release 12 (tab-not-page, no image/e-mail
filters, GUID/"intake" slop) verified fixed.

## Qualification

The ticket's own post-implementation report states honestly that the
manual 1920px visual pass listed in its verification checklist was **not
performed** — only the automated Browser/AccessibilityTests suite ran (both
green, including a new `TriageQueuesWebTests` assertion that the rendered
tab carries no banned vocabulary or raw GUIDs). The production DOM
verification above is a real substitute for that specific manual step, not
a retroactive claim that the manual pass happened.

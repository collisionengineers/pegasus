---
id: TICK-035
type: ticket
title: Activate evidenced principal routes through automatic intake
status: done
area: intake-processing
order: 910
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-09-07T21:12:54.514Z'
  review: '2026-09-08T01:41:18.011Z'
  implementing: '2026-09-08T01:48:12.223Z'
  verifying: '2026-09-08T02:21:10.573Z'
  done: '2026-09-08T02:34:38.664Z'
review_round: 1
labels:
  - capability
  - INT-04
groups:
  - EPIC-014
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
commits:
  - 56566371a5b80ef59c4f98e377c8e8ff6469b5f7
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/692'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: 56566371a5b80ef59c4f98e377c8e8ff6469b5f7
delivery_recorded_at: '2026-09-08T02:36:54.607Z'
archived: false
created: '2026-08-12T15:03:53.493Z'
updated: '2026-09-08T02:38:50.544Z'
---

## What

Activate the evidenced top-15 principal routes through the existing automatic
intake workflow, including direct and proved staff-forwarded email. Reuse the
single Core route/classification/matching boundary and existing fifteen
instruction extraction profiles; do not introduce parallel QDOS and generic
business-policy implementations.

## Why

The operator's 7 September 2026 v1 remediation request and Astra Stream C03's
TICK-035 residual supersede this ticket's historical post-alpha deferral.
Fourteen additional extraction profiles are present but ordinary mail intake
still binds routing and extraction to QDOS. Domain/reference candidates are
evidence, not permission to invent mappings.

## Acceptance

- [x] Exact evidenced domain routes identify one active principal; unknown,
  conflicting, shared or intermediary evidence remains explicit and fail-closed.
- [x] The selected existing extraction profile agrees with the accepted route and
  drives the existing Case/Triage/Unidentified destination logic.
- [x] Direct, forwarded, ambiguous and replay cases use genuine existing fixtures
  and prove the destination, principal and lifecycle state, not just selection.
- [x] Canonical FRD-02 and production callers agree. No second principal catalog,
  rules engine, mailbox onboarding or separate pipeline is introduced.

## Historical coordination

[[PLAT-028]] owns customer contracts, DI and Settings until its announced
merge. Preparation may proceed; overlapping implementation waits for that base.
[[INTK-061]] owns durable routing recovery; Triage automatic linking and
Engineer handoff are separately assigned remediation. [[TICK-036]],
[[TICK-037]] and [[TICK-038]] retain mailbox onboarding ownership.

Only test email recipient is digital@collisionengineers.co.uk. No live mail,
provider or cloud write is part of this preparation. Root is the sole heavy
verification owner.

## Outcome

Integrated and accepted on dev via [PR692](https://github.com/collisionengineers/pegasus/pull/692), merged 2026-09-08T02:20:31Z at 56566371a5b80ef59c4f98e377c8e8ff6469b5f7. Root read the full exact-merge PASS proof and moved Done on 2026-09-08T02:34:38.664Z. Locked restore/full Release build passed; 247 Core and 28 integration cases passed with no skips. The single Top15 case internally covers fifteen original documents, not fifteen xUnit cases.

One Core route/profile/classification/matching chain now serves the evidenced identities and actual durable destinations. Persisted candidate provenance and physical-document table identity are retained; non-QDOS automatic mail matching requires an agreeing selected profile while QDOS fallback and declared Provider API remain separate supported paths. All three review findings are fixed. Original failed attempts remain in the report/review/proof.

ALS/FW/SBL genuine mail allocation/replay and YML later-report no-Case are the exact tested outcomes. No genuine initial YML envelope allocation, live OCR/mail/provider call, hosted-CI or deployment is claimed. Settings verification is reused only for identical source/snapshots. Mailbox onboarding remains with [[TICK-036]], [[TICK-037]] and [[TICK-038]]; no new closeout follow-up is created. Generated index ownership was already handed to [[UIIMP-017]].

Before cleanup, all 15 TRXs and 40 capture files were archived and source/copy hashes verified under pegasus_pack/current/proofs/TICK-035. The only approved cleanup roots are this ticket's author worktree and exact-merge verifier; other tickets and shared state are preserved. Current delivery is integrated dev, not deployed. Earlier coordination and pending handoff paragraphs are historical records, not current ownership holds.

---
id: DOCS-020
type: ticket
title: Keep report snapshots consistent and invalidate changed report inputs
status: done
area: documents-reports
assignee: pack_reconcile
profile: fix
stageEntered:
  preparing: '2026-09-07T20:21:48.709Z'
  review: '2026-09-07T21:40:35.885Z'
  verifying: '2026-09-07T21:51:25.688Z'
  implementing: '2026-09-07T22:43:54.189Z'
  done: '2026-09-07T23:18:03.038Z'
labels: []
groups:
  - EPIC-014
links:
  - CASE-047
  - DELIV-050
  - ENG-041
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
commits:
  - 522e67f270ab4d6086d9fba04095988db3598888
  - 4d7ad4a0d2593300fd02527838aa2f1cf6555860
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/682'
  - 'https://github.com/collisionengineers/pegasus/pull/685'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: 4d7ad4a0d2593300fd02527838aa2f1cf6555860
delivery_recorded_at: '2026-09-07T23:21:37.118Z'
archived: false
created: '2026-09-07T19:58:09.568Z'
updated: '2026-09-08T02:16:45.232Z'
---

## What

Repair PR675 report review findings: freeze report inputs from one guarded Case version; invalidate report generations on source-document and signatory changes using existing contracts; refuse stale delivery; use LondonCalendar for civil report dates and operator presentation.

## Acceptance

Focused existing persistence tests cover a concurrent source mutation, adding/removing source documents, changing eligibility/name/qualifications/signature and refusing stale delivery. A BST-midnight test proves report dates. No new framework or domain policy owner. Current operator permission includes implementation, merge and deployment; return an independently reviewable PR.

## Outcome

Integrated and accepted on dev through [PR682](https://github.com/collisionengineers/pegasus/pull/682) merge522e67f270ab4d6086d9fba04095988db3598888 and test-only [PR685](https://github.com/collisionengineers/pegasus/pull/685) merge4d7ad4a0d2593300fd02527838aa2f1cf6555860. Root read whole PASS proof7231e7c73e129f2c and approved Done/closeout. Report snapshots now guard consistent source/version/signatory inputs, invalidate material changes atomically and use London dates; actual-role and focused runtime checks passed. Original failed migration-list consumer and all earlier compiler/harness/capture failures remain recorded; the test-only correction passed on its exact follow-up merge and unchanged52 passes were reused only with source evidence.

Not deployed; no live report delivery or manual visual PASS. Subsequent integrated checks identified two follow-ups: [[DELIV-050]] corrected the bootstrap migration census annotation (Done), and [[ENG-041]] corrected the automatic custody helper's unintended Case-version increment and edit-lease clearing in PR691, integrated at cc441645b0a62a806e34367ad75e9eaff4df8b11. That production defect broke two actual Glass callback completions, not just fixtures. Its 51 exact-follow-up integration checks and canonical Case snapshot checks now pass; proof0453cf98fcbb0a20 preserves earlier failures, and the ticket is Done and closed. The original report-specific PASS remains historical bounded evidence, not full lifecycle acceptance. Related [[CASE-047]] remains context. All three hash-verified TRXs retained in ignored pegasus_pack/current/proofs/DOCS-020 before scoped Git cleanup. Unreachable squash author SHAs remain in report/review history, not the delivered commit list.

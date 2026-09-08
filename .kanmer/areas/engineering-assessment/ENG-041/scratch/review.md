---
kind: review-attestation
pr: "691"
head_sha: "b253306f048dfb8e1dd635e9994835b32c0090d1"
verdict: pass
reviewer: "pack_reconcile"
independent: true
plan_hash: "c096b7ddf32366de"
ticket_updated: "2026-09-08T01:15:01.685Z"
board_sha: "9e3b6bea2f2abfe2cb5240e21791bee1919d670b"
expected_reviewers: ["pack_reconcile"]
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Historical PR683 resumed fresh provider writes lacked current Case authority."
    disposition: fixed
  - id: F-002
    severity: major
    summary: "Historical PR683 bounded response overflow lost uncertain-write semantics."
    disposition: fixed
  - id: F-003
    severity: major
    summary: "Automatic source custody consumed Glass import authority while report freshness depended on that Case mutation."
    disposition: fixed
---

# ENG-041 independent post-merge correction review

PASS for PR691 at b253306f048dfb8e1dd635e9994835b32c0090d1.
Root assigned pack_reconcile as the sole expected reviewer; it settled through
[exact-head public review5136361089](https://github.com/collisionengineers/pegasus/pull/691#pullrequestreview-5136361089).
This correction was authored by principal_delivery_audit, not this reviewer.
The same GitHub credential does not merge those separate agent roles.

This is a consolidated review of the new seven-file post-merge correction,
+210/-71 against accepted dev19e6f523bf6760cab39104b4dca3674b0ac8a512.
Original author/reviewer roles for PR683 are historical. I do not self-review
the original PR683 implementation or replace its independent acceptance.
Its F-001/F-002 dispositions are carried from independent review
27fbd21c5f009f14 by /root/principal_delivery_audit, not newly inferred here.

## Inputs, scope and preserved evidence

Read whole current plan c096b7ddf32366de, files1d4c177e23c8d8b9,
checklist2c0b2ecf017b0c3a, report66303a1fa5639466, resolved questions
bd0271f101c183d6, original FAIL proof73d3327f6364834c and previous review
27fbd21c5f009f14. EPIC-014 and governing FRD-06/11 bind the correction.
Earlier progress paragraphs are chronological attempt records; final author
handoff supersedes their pending statements, not their failed evidence.

Exact author HEAD matches GitHub and the tree is clean. Normal base merge
188d3e16fd0b93a78e37a0dbc5483f5bd06410f5 preserves prior author8bbceb4 and
accepted19e6f5; the correction includes only three production files, three
test files and the linked FRD-06 paragraph. No Core/Web/UI/snapshot/schema/
grant/package/runtime/provider change. CASE-049's later dev merge is a
separate disjoint correction; root owns converged acceptance.

## F-003 — fixed in b253306f048dfb8e1dd635e9994835b32c0090d1

EfCaseArtifactCustody.RecordConfirmedSourceChangeAsync no longer loads and
completes CaseWorkflow merely to confirm automatic source evidence. Immediate
RetainCaseAsync confirmation and ReconcilePendingArtifactCustody both call
the same helper inside their existing serializable transactions. Source
invalidation and its permanent stale event still commit atomically with
confirmation. Actual GeneratedCaseArtifact operation identity retains its
existing exclusion. Explicit staff addition/removal remains a Case mutation.

EfAssessmentReportProjectionSource extracts its existing exact confirmed
occurrence/version query and mappings in-place; there is no new interface or
parallel selector. It retains CaseId and matching DocumentId/VersionId,
current/not-removed/Confirmed predicates, generated-output exclusion and
ordinal order. The projection and transactional freeze are its two real
production consumers.

EfCaseReportGenerationStore.FreezeAsync retains ordinary actor/Case version/
live-lease and captured-input Case-version checks, then re-queries the complete
source set inside its existing serializable transaction. Ordered
AcceptedReportSource equality catches source identity/hash/name/Box changes;
the full occurrence-keyed DocumentVersion dictionary also catches membership,
media/length/current eligibility. Dictionary enumeration order is not used as
identity. The complete census includes all confirmed sources, not just chosen
report images. Mismatch refuses with a content-safe retry message before a new
generation/artifact or current-generation reuse; existing exact-operation
artifact replay remains an immutable replay, not a new freeze.

Signatory checks, image preparation/readiness, generation staleness,
provider protected authority, expired/foreign/lost lease refusal and
ImportRawEstimate's normal mutation remain intact. Rendering/custody bytes
remain outside the freeze transaction. The actual callback path retains its
two sources, then imports one Draft with the original still-valid Engineer
authority; duplicate callback changes no version or identity.

The FRD-06 addition accurately describes that boundary. It does not promise
that staff edits or expired authority can be bypassed and does not change
FRD-11's source-currentness requirement.

## Tests and correction of the observation helper

The seven-file assertions exercise immediate and recovered custody,
unchanged token/holder/expiry and version, exact identities on replay,
runtime-role invalidation and stale history, and twelve pre-freeze source
race classes. The stale source fixture captures all three confirmed rows
once, before independent persisted mutations. It does not reread current
values to fabricate a stale-input pass, and independent ID/Box/hash snapshot
assertions remain.

Both original failing actual callback assertions still require Completed,
one Draft with lines, both retained document kinds and no duplicate session/
import. Added version assertions require exactly one workflow increment from
import and no increment on duplicate callback.

The final two-line helper correction queries CaseWorkflows by CaseId instead
of Cases by Id. This matches the actual EfGlassRepairEstimateCaseAuthority
and EfRepairSpecificationStore.Guard, whose version++ and lease clearing
operate on CaseWorkflowEntity. The +1/replay assertions were not weakened.
Only that helper changed after the 48-case run; its four calling methods
(five cases) were rerun. No production change followed that earlier run.

## Runtime evidence and failed attempts

Reviewer ran no build/test. Root is sole heavy verifier. Actual retained TRX
counters, timestamps and SHA-256 values were inspected independently.

- Original exact PR683 merge baafa29e0f7002b8235aa43bf333f5d9bb172828:
  restore/build/Core56 PASS; Integration157 PASS/2 FAIL, callbacks ended
  AwaitingImport. FAIL proof73d3327f6364834c remains unchanged.
- Correction attempt1: build PASS51.75s, zero warnings; Integration48 total,
  47 PASS/1 FAIL/0skip, reported2m53s. The failure was the new +1 assertion
  reading Cases.Version. TRX E32170BDAD1A4F74B29E11309C2F96041CCF933AA007378D3BDF9E2CD8994D81.
- After only the helper correction: build PASS17.14s, zero warnings/errors;
  all five affected caller cases PASS/0skip, reported57s.
  TRX C3AD73073CD8FC5EBA55F957624C0F08174D33D4A8A0B16EBE96F0711C691FA9.
- Two scoped snapshot verification tests PASS, reported6s, reusing the three
  genuine fresh captures from attempt1; no recapture or generated diff.
  TRX7F1CD86E6486623820372DB9A48A96049FA72A7686B8AEC8A72A359D0BD5F7C3.
  Root catalogue PASS60 routes/67 prototypes/0 broken references.

Files are artifacts/verification/eng-041-custody-correction.trx,
eng-041-workflow-version.trx and eng-041-custody-snapshots.trx in the retained
author worktree. Exact commands and UTC/+01:00 instants remain in the whole
report. This acceptance composes the unchanged47 passes with the corrected
five-case cohort; it does not relabel the failed attempt PASS or claim a fresh
48/48 rerun. The original bad capture-variable attempt and all earlier
compiler/clock/snapshot/CA1068 failures also remain historical evidence.

## F-001 and F-002 — retained fixed history

Both remain fixed by8bbceb4fd190ae80a8b656540fd0ae5973f49895 under independent
PR683 review27fbd21c5f009f14/public review5135725065. F-001 uses the existing
Case authority before resumed fresh provider writes and retains regained import
authority. F-002 preserves outcomeUnknown through bounded response reads.
Their production files are unchanged by PR691. This correction neither
releases unknown external accounts nor introduces repeated provider writes.
Original needs-changes recordcc5b9a51b2e4e5d3 and public5135577047 remain in
history; the whole report records their accepted remedies.

## Current GitHub and board facts

Gather after public reviewer settlement: exact head unchanged,
OPEN/MERGEABLE/CLEAN. GitHub reviewThreads returned a complete empty page;
no unresolved/outdated thread is omitted. The one bot summary
IC_kwDOThBrk88AAAABTHMaxQ is informational (security review running with
mergeGateEnabled:false), has no finding, and is not an expected reviewer.
Its disposition is acknowledged as non-gating status, not a clean security
audit. A later actual thread requires a fresh whole attestation.

Live dev branch is unprotected; active rules[], head check_runs[] and status
contexts[]. Aggregate status pending with no context is not a required
pending check, nor a claimed CI PASS. Root-authorized [skip ci] avoids
duplicate broad runs; final converged CI and exact-follow-up proof remain.
The bound board tip was pushed with ahead0/behind0 and equal local/remote SHA.

## Stop and handoff

No open finding remains for this bounded correction. No merge, source edit,
test, live Glass/cloud/mail call, proof overwrite or cleanup by this reviewer.
Root must read this whole record and refresh head/plan/ticket/checks/threads/
board sync immediately before its separate merge decision. Keep the author
claim/worktree and failed proof. Only after confirmed merge may the ticket
move to Verifying and exact-follow-up-SHA evidence be written.

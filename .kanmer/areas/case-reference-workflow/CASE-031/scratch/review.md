---
kind: review-attestation
pr: "694"
head_sha: "9863dd4264440ef228a0766d3e2949faf6a4e12b"
verdict: pass
reviewer: "root"
independent: true
plan_hash: "ecb1dab6abfa296b"
ticket_updated: "2026-09-08T03:13:22.847Z"
board_sha: "4427f55940667a773d66b98b36588bb799b4bbda"
expected_reviewers: ["root"]
threads_snapshot: []
findings: []
---

# CASE-031 independent whole-PR review

PASS at exact9863dd4264440ef228a0766d3e2949faf6a4e12b. Author intake_audit
and reviewer root are distinct agents; root ran checks and approved scope but
authored none of these ten files. Shared GitHub credentials do not collapse
that boundary. Sole expected reviewer settled via exact-head
[public review5136946957](https://github.com/collisionengineers/pegasus/pull/694#pullrequestreview-5136946957)
at2026-09-08T03:15:51Z before this whole record.

## Inputs and entire diff

Read current ticket/gates, full planecb1dab6abfa296b, files77b89acba62aface,
research084f62058746126a, checklist79cf52b91e83aaa6 and whole final
report1b2a5c54c15b392b. Read EPIC-014/current authority, canonical Case field
selection, supplied EVA ClmAdd model, FRD-01/02/07 and ADR-0038. No unresolved
user question or file-owner conflict remains in this bounded address scope.

Full diff is ten approved files,329 additions/33 deletions. API payload has
ClaimantAddress, API mapping passes that exact value separately from inspection
location and advances only API version1 to2. Existing serializer emits ClmAdd.
EvaSubmissionPolicy alone validates CaseField.Current/IsAccepted, preserving
Confirmed-over-Fact and refusing a missing/suggestion-only/invalid current
value without falling back. Its 40 UTF-16-unit bound and Rune control/format
check preserve normal punctuation and valid text exactly, without truncation,
flattening or postal inference. No ZIP contract, extraction, schema or UI edit.

Production caller EvaSubmissionStore.ExecuteAsync retains its mode/staff/
Engineer and existing outcome rules. Known operation replay returns before
the new guard. Invalid new address returns the existing blocking result before
LoadEligibleImagesAsync or SubmitInstructionAsync, with no persistence work.
FRD-07 documents only that API prerequisite; it adds no readiness or ZIP gate.
No new port, service, queue, fallback, dependency or parallel policy exists.

The existing Core policy/mapping tests cover status, precedence, ordinary
punctuation, 40/41 length and BMP/supplementary format/control characters.
Transport tests assert exact ClmAdd and separate inspection address. The real
SQL/store caller retains all original manual first-send, resend, rejection/
unknown/partial outcomes, version/lease/race and ZIP/image assertions. New
invalid-address probes assert zero image/transport calls, no submission/history
and identical state/version/lease; replay after invalidation adds no attempt.

## Verification and failure disposition

Root3667: locked restore all7 PASS(max1.48s), solution build127.88s0warnings;
Core57PASS127ms. Integration12PASS/1FAIL35s at earlier fixture intake setup,
expected CaseCreated versus NeedsSorting. This is retained, not counted PASS.
The fixture used literal invalid synthetic PDF bytes and lacked the instruction
work-type tell. Current QDOS fallback remains; restoring an inferred Inspection
would contradict the current classifier. No production intake code was changed.

Root approved the existing QdosCorpus resolution/hash-check convention and the
supplied EREF10 original:
3063FF9ECB31878F582FB439047D999A41A7C6FE5B978CFBEE5C7E7F277553B4.
Its genuine envelope/body/letter remain; only the two existing test JPEGs are
appended in memory, explicitly a derived export probe. Actual letter evidence
supports InspectionAndAudit and AMA/47857/1. CaseCreated remains mandatory,
with richer failure output; no downstream assertion was weakened or removed.
The local-evidence attribute is honest, but a skip is never acceptance here.

Root73724: incremental Integration/dependency build42.27s0warnings and the
one corrected actual-caller method PASS1/1,37s,zero skips,script0.
TRX CE49FFEF3436A0FC7051737DDC0B8B271BD950E82A9D285FF5F9155046DF22AD;
execution03:07:53.2717524Z through03:08:33.0496455Z. Original Core
F0859E4685461865D0376F0562F33F2F3E146345E4F187A9B3F60133B0D9DC2A and
failed Integration E2AC1683BA4DDFEA610B7F808148CC89AE8886DB19FDB6555E5D88AE21CCEF94
remain separate. Only the one failed fixture changed before correction; the
57 Core/12 transport passes remain source-equivalent. Exact commands are in
the report. No author-source change followed root checks before commit.

## GitHub and merge boundary

Live PR is OPEN, non-draft, MERGEABLE/CLEAN, source repo collisionengineers/
pegasus, CASE-031-eva-claimant-address to dev, exact head above and correct
Kanmer footer. Complete GraphQL threads gather has nodes[] and
hasNextPage:false. Dev active rules[] and head rollup[] have no required
context; empty checks are not CI PASS. Root authorized bounded skip-ci work;
the converged solution/release obligation remains.

Non-gating bot summary5578537252 is running since03:12:43.151633Z with
mergeGateEnabled:false and no actual finding. Acknowledge it, do not present
it as settled security review or an expected reviewer. Gather any new
comment/thread before merge and disposition any actual finding.

No open finding remains. After fresh head/plan/ticket timestamp/threads/checks
and synchronized-board revalidation, root may perform the separately
authorized guarded merge and move Review to Verifying. Exact integrated
acceptance/closeout are separate; retain the claim and original failures.
No full-suite CI, live EVA, mail, Azure write or deployment was performed.

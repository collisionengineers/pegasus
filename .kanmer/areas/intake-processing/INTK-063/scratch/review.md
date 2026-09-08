---
kind: review-attestation
pr: "696"
head_sha: "e7db237e47322d2378ccf44749d97024db377aeb"
verdict: pass
reviewer: "codex-v1-remediation-root"
independent: true
plan_hash: "10543d55d4090f66"
ticket_updated: "2026-09-08T04:25:42.240Z"
board_sha: "1592eb0cfa90800f866ec8c606a23676bb745f14"
expected_reviewers:
  - "codex-v1-remediation-root"
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "The automatic-link request fingerprint omitted the observed staff-origin association version."
    disposition: fixed
    reason: "The exact anonymous fingerprint projection conditionally binds the version at e7db237e47322d2378ccf44749d97024db377aeb; ordinary null identity remains unchanged and bounded grouped tests prove changed-version conflict without mutation."
  - id: F-002
    severity: note
    summary: "The initial image-origin principal hypothesis was not established."
    disposition: rejected-with-reason
    reason: "ImageIntakeOrigin and its actual resolver carry no principal; registration does not set one. Registered replay preserves immutable VRM and the known-principal guard. No invented initial provenance or unsupported behavior change is warranted."
---

# Independent consolidated review — INTK-063

PASS for the exact source above, not integrated or deployed acceptance.
Root is distinct from author pack_reconcile and reviewed the approved plan,
whole source/test/doc delta and report. The 19-file PR census matches the
reviewed local commit (+1222/-290); the author worktree is clean.

## Contract and implementation

FRD-02, EPIC-014 and plan10543d55d4090f66 remain aligned. Current accepted
CaseMatchIndex registration and known principal govern registered automatic
pairing. Candidate selection is shared and rechecked inside automatic writes.
Singles retain exact precedence; persisted multi-member groups retain their
stricter ambiguity rule. No original-draft identity or permanent no-match flag
becomes a second authority.

Every durable group image must be linked before one merge. Interrupted
automatic and reasoned staff groups recover through the existing pairing
owner. Staff authority derives from the current active origin, exact group,
target, actor/reason and association version, not a caller-supplied bypass.
Prior sibling history and origin changes, including same-target unlink/relink,
are refused. Final serializable merge checks current members, target
eligibility, edit leases and origin version. Existing operation/custody/history
identities remain intact.

Actual callers are initial/registered image automation, first/replayed
acceptance, reasoned LinkIntake and the existing reconciliation function.
Failure outcomes surface through existing logging/activity evidence; no empty
catch, duplicate public sync path, queue, schema, migration, grant, DI
registration or service was added. No routed UI changed.

## Verification and failure dispositions

Root executed the focused author lane. The initial 69.39s compile failure
(missing CultureInfo namespace) is retained, corrected in the mapped store.
Corrected full build passed in124.08s, zero warnings; Core75/75 passed.
Integration17/18 passed, with the sole failure occurring during a duplicate
migration-seeded QDOS fixture insert before the Worker caller. Reusing
SeededPrincipals.QdosAsync fixed that setup without weakening assertions.
Incremental build21.64s and the actual restricted Worker case passed (35.396s).
All three distinct TRXs/hashes and failures are preserved in report
67f2c76040cf0b1c. Passing cohorts were not unnecessarily repeated.

The evidence covers actual timer replay, current identity and eligibility,
known-principal contradictions, staff intent, interrupted/reversed groups,
origin-version fingerprint conflicts and restricted SQL-role recovery.
The restricted fixture starts from registered state: it does not prove initial
registration, live incoming mail, provider behavior or deployment.

## GitHub and residual obligations

Gathered exact PR head/base, comments, reviews, checks and GraphQL review
threads. The re-gather now includes root's public review PRR_kwDOThBrk88AAAABMjUefw
on this exact head, repeating this disposition; no review threads exist and
threads_snapshot is truthfully empty. Informational Codex security-summary comment
IC_kwDOThBrk88AAAABTIpgiA now says completed at2026-09-08T04:27:27.909671Z
with mergeGateEnabled:false and contains no finding. It is ordinary evidence,
not an expected reviewer or a required CI check. Re-gather immediately before merge and disposition any new
substantive evidence.

statusCheckRollup is empty, not green CI. The dev protection read returned
Branch not protected (HTTP404); no protection or repository setting changed.
Publication uses the root-approved EPIC-014 single converged CI policy.
The final solution/release CI obligation is not discharged by this review.

Plan10543d55d4090f66, filesa2712472fca53cc9 and checklist5ff6eab776e8775e
were read; all six checklist entries describe completed author work/handoff,
not Done. Exact merged-SHA verification remains required before Done, using
the approved bounded cohorts and retained failures. Final release/live
acceptance remains with EPIC-014.

Final re-gather retained exact head/base, empty checks and no review threads.
The first final guard refused without a merge or source write because this
informational comment changed from running to completed. Root re-gathered all
comments, reviews and review threads at2026-09-08T04:36:38Z; there are no threads
or new findings. Root public review was posted at2026-09-08T04:26:37Z. Expected independent reviewer root is settled on this head.

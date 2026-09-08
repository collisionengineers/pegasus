---
kind: review-attestation
pr: "701"
head_sha: "bfa4e498f8fa8f9b3524f24748b5b89d8b324d67"
verdict: pass
reviewer: "pack_reconcile"
independent: true
plan_hash: "ea1fca1e3f50f1fb"
ticket_updated: "2026-09-08T07:10:27.921Z"
board_sha: "5f28dd2c61131269aa769a02b303268cedf28f43"
expected_reviewers: ["pack_reconcile"]
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Exact PR681 merge retained a Windows ORAS repair hint requiring Linux."
    disposition: fixed
---

# DELIV-048 independent follow-up review

## Decision and independence

PASS for PR701's bounded two-file correction at the exact head above.
Root authored this follow-up; pack_reconcile authored neither changed file
and is the sole assigned independent reviewer. The distinct agent role, not
a different GitHub account, establishes independence. Exact-head public
COMMENT review5138372200 was posted at2026-09-08T06:55:34Z and read back
before this attestation. No additional reviewer is expected or timeout-absent.

## Inputs and scope

Read the whole live ticket, gates, plan ea1fca1e3f50f1fb, file map
a6eace140ea512ca, report f380ddd091eb9ccb, checklist2d2f694653fb95bb,
research, execution/verify scratch, prior review ef0df9e8b673889c, and
whole proof FAIL2462b18082379607. No open-questions document or frozen
batch exists. Read EPIC-013 historical context and EPIC-014 current authority,
documentation index, ADR0007/0014/0039 and supported-platform runbook.
Current operator/ADR0039 Windows-and-Linux support supersedes EPIC-013's
historic Linux-only premise; original history is not current scope.

The full dev-relative PR diff is exactly PegasusPlatform.ps1 and
Test-PegasusPlatform.ps1, +6/-2. Read the whole existing script test,
changed hint owner and actual Doctor caller. Worktree is clean at the exact
head. Normal merge59c43f567e7bc9fbf40e5f152beb370d528e3e06 is tree-identical
to accepted dev96777888bfa7ee7f85d63979a4a09ae10cda7d13 (read-only diff
exit0), so the resolved ADR-index conflict introduced no separate source
change. No current application, schema, package, cloud, workflow or other
ticket change is in this PR.

## Finding and caller acceptance

F-001 was discovered by root's exact PR681-merge verification, not silently
erased by the old review PASS. Proof2462b18082379607 records actual Windows
Get-PegasusRepairHint oras returning the Linux-only sentence, command07e9d2,
exit1,1.512s. That contradicts the accepted portable workstation contract.

Fixed by bfa4e498f8fa8f9b3524f24748b5b89d8b324d67: Windows now returns
the same existing pinned ORAS1.3.4 official installation guidance as Linux.
Get-PegasusRepairHint selects the existing keyed table by the actual
Get-PegasusPlatform.Kind. Invoke-Doctor.ps1:547 calls that owner for the ORAS
check, whose existing version requirement is1.3.4. There is no parallel
installer, fallback, command convention or platform decision.

The existing Windows/Linux bundle-mapping loop now supplies Kind in its
platform fixture and calls the real hint owner on each host. Its exact
assertion catches the omitted Windows branch. All existing bundle identities,
manifest rejection/hash/OCI-stub checks and LocalDB classifications remain.
The original production builder/validator, Linux deployed packages, approved
artifact and migration boundaries are unchanged.

## Recorded evidence and limits

Root executed command3e756e in the author worktree: diff check, existing
Test-PegasusPlatform.ps1 and actual native Windows hint; PASS exit0,2.2845s.
This is root-executed evidence from the report/parent handoff, not a reviewer
test run or a new retained TRX. Reviewer ran no test, .NET build, artifact
packaging, provider call or cloud write.

The original PR681 author evidence remains59.58s Release build,17 focused
architecture passes, Local/script acceptance; original review root PASS at
b6ffdeda1f8eee7f71e86ca033260de20483cb60 and planf52800f76a333e02 remains
in review versionef0df9e8b673889c/board history. That earlier review had no
findings. PR681 squash merge1c1d7a0a45555604bafd3e732bd606bf083b804a and
the later native-hint FAIL remain distinct from this follow-up PASS.

Final clean integrated release-artifact build/Artifact validation is explicitly
still required before Done, once at the coordinated release head. No native
Linux execution, deployment or complete ticket acceptance is inferred from
host-mocked mappings or this script run.

A read-only documentation lookup first used an incorrect ADR0014 filename;
Get-Content reported missing path, then the actual linked
0014-local-to-production-deployment.md was read completely. This was a
discovery error, not a validation result. gh pr checks --required exited1
with no checks reported; current protection/rules establish an empty
required-check set, not a CI PASS.

## Current GitHub/board gather and merge boundary

At the post-publication gather PR701 is OPEN, MERGEABLE/CLEAN, same-repository
DELIV-048-portable-release to dev96777888bfa7ee7f85d63979a4a09ae10cda7d13,
head exact above. Effective dev rules[]; branch protectedfalse;
required_status_checks checks[]/contexts[] enforcementoff; rollup[].
All review threads[] with hasNextPagefalse, no unresolved finding/thread.
Only public review5138372200 is present on this head.

Issue comment IC_kwDOThBrk88AAAABTKFGEg is advisory status-only bot evidence,
completed at06:56:17.152001Z with mergeGateEnabledfalse and no actual findings.
It is not an expected reviewer or required check. If it adds findings or any
thread/head/plan/ticket/check changes, re-gather and replace this whole file.

Board tip 5f28dd2c61131269aa769a02b303268cedf28f43 was pushed with ahead0/behind0 at
the fresh pre-merge gather. Ticket timestamp now binds only the current
author lease heartbeat; plan/report/checklist/proof and source remain exact
unchanged versions above. The completed bot comment contains no finding;
reviews and all threads were read again and no late reviewer is outstanding.

Root explicitly released the previous PR700 ordering hold: this two-file
correction is disjoint from that PR's separately tracked CI failures. Fresh
head/base/diff/policy/check/thread facts still match the gather above. No
required check or protection is bypassed; PR701 rollup is empty, not CI PASS.
The prior hold and public review5138372200 remain in history. Ordinary
authorized merge may now proceed following one immediate final identity and
board check. Move only Review to Verifying after confirmed merge. Root owns
the exact-merge narrow proof and final integrated artifact obligation.
Original proof FAIL2462b18082379607 is untouched. No Done, cleanup,
deployment, lease transfer or release is authorized by this review.

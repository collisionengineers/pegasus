---
kind: review-attestation
pr: "682"
head_sha: "1bf9ac613a2b7610d2ddc23e8acfd9f4b462ef79"
verdict: pass
reviewer: "principal_delivery_audit"
independent: true
plan_hash: "b71b55e37264f8dc"
ticket_updated: "2026-09-07T21:40:35.885Z"
board_sha: "05fe78801d845e97e7e94688beceb6f112d87ad5"
expected_reviewers: ["principal_delivery_audit"]
threads_snapshot: []
findings: []
---

# Consolidated independent review — DOCS-020

Round 0, complete exact pushed head above: 15 files, +618/-20, based on
dev 2e50fde474ce35eb32eff8677eb2327cb6aad272. This separately assigned
reviewer authored none of this change. Root named this one reviewer; the set
settled with exact-head public COMMENT review 5135390914:
https://github.com/collisionengineers/pegasus/pull/682#pullrequestreview-5135390914.
Sharing the repository account does not make the author and reviewer agent
roles identical. Root owns the merge.

## Scope and findings

Read the live ticket, research/files/plan/checklist/execute/report documents,
FRD-11, EPIC-014 context, full committed diff and relevant direct callers.
Report 08e9663b3940a805 accurately records scope and verification history.
No findings.

The projection captures the workflow version before workspace/component
reads, compares the workspace version and rereads the final workflow
version. Freeze requires both the caller's guard/lease and that captured
input version. It re-resolves and compares the actual signatory identity,
printed name, qualifications, signature content type and signature digest
inside the serializable freeze transaction. Renderer/content-source work
remains outside the freeze lock.

Source add/remove and queued retained-source registration call the existing
same-context stale owner. Immediate and recovered artifact confirmation
commit source confirmation, stale state and workflow advancement together;
external custody writes occur before the new short confirmation transaction.
An exact GeneratedCaseArtifacts operation identity excludes report output
from both source assembly and self-invalidation, without excluding all other
Generated source documents. Pending confirmation remains pending on a lost
race, and replay does not duplicate the mutation.

Staff enabled-state, role, deletion and profile mutations compare effective
current tuples inside their existing transaction. They stale affected
current generations only; unchanged effective signatories and superseded
snapshots retain their identity/history. The existing preparation and staff
send readiness boundaries refuse a stale generation. The actual staff send
engine invokes readiness immediately before transport submission. Notes or
recipient edits do not require regeneration; previously prepared addressing
still requires a current preparation.

The additive SQL-Server-only migration and bootstrap census agree: Web gets
UPDATE on report generations/artifacts; Worker gets SELECT/UPDATE generations
and SELECT artifacts. Existing DELETE restrictions remain. Tests exercise
the actual store/helper under real runtime-role impersonation, not permission
strings alone. No schema column, model snapshot, package or new runtime.

LondonCalendar is the existing owner reused by preview/freeze dates and the
Razor generation-time value. No additional UI controls, layout or explanatory
copy are introduced.

## Verification and limitations

Root is the sole heavy verifier. Evidence was inspected and reused; this
reviewer ran no builds, tests, captures, cloud or email commands and made no
author-source edits.

- Locked restore passed. The initial build failed EF1002; the parameterized
  SQL correction then built successfully. Final incremental Release build:
  exit 0, zero warnings/errors, 45.74 seconds.
- Initial focused run: 143 PASS, 5 FAIL, total 148, 7m51s. All five failed
  because the existing test harness lacked document-content composition.
  The existing local artifact-root composition fixed that setup without
  weakening assertions. Only those five plus the missing Razor capture were
  rerun: 6/6 PASS, exit 0, 52 seconds.
- The initial snapshot update lacked the unavailable capture and failed;
  the corrected retained set passed update 2/2, verify 2/2, catalogue
  62 routes/69 prototypes/zero broken references, and migration-grant census
  101 migrations. Default/conflict/unavailable and index normalize to the
  existing tracked bytes, so there is no artificial snapshot diff.
- Static Razor semantics and retained snapshot structure were inspected.
  There is no manual visual/viewport pass or deployed correctness claim.
  The date boundary is also covered by focused BST-midnight assertions.
- Reviewer committed-diff whitespace check passed, exit 0.

The failed compiler, harness and capture attempts remain in the report.
Passing corrected evidence does not erase them. Final integrated release
packaging/CI and post-merge exact-SHA proof are not claimed here.

## Final gather and disposition

PR is OPEN, base dev, branch DOCS-020-report-consistency, exact head above.
Current dev protection API returned 404 Branch not protected; effective
branch rules are empty. Check runs/status rollup/status entries are empty,
not a green CI claim; aggregate pending with no status entries is not a
missing required check. Root-directed skip-ci does not waive any newly
required gate.

There are no review threads or requested changes. The automated security
status-only comment 5575879109 contains no finding, is informational, and is
not an expected reviewer or gate. Its disposition is no actionable finding.
The public independent PASS review is the settled reviewer evidence.
Board local/remote tips matched above with ahead=0 at gather.

## Next step

PASS. Root must re-gather exact head, ticket/plan, threads and live required
checks immediately before its authorized merge, replacing this record if
material inputs change. After confirmed merge, only Review to Verifying;
kanmer-verify owns integration-branch proof and the Done decision.

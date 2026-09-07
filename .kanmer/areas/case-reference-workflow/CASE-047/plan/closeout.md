# Approved v1 closeout implementation

## Authority and starting state

Operator approved the full closeout plan on 7 September 2026 with "Implement the plan".
Common source 1ba06bd01329d87c3aa4ce7c6b604def78c26809 already contains the preserved
B83e875022 and C5eb005802 owner histories. All remaining changes use A's recorded
worktree under the approved AGENTS closeout exception. Root owns all three
streams and is the sole heavy verifier; maximum two disjoint source editors.

Governing docs remain the current v1 PRD/FRD/ADR and original pack D01-D17,
with subsequent operator decisions taking precedence. No new architecture,
dependency, queue, compatibility layer, provider write or deployment.

## Remaining changes

1. C: replace obsolete QDOS Triage extraction fakes with existing real-classification
   fixtures and Core commands; retain ambiguity/replay/authority assertions. Correct
   six mappings to whole vehicle description and independently labelled make/model.
2. C/A: isolate supplemental notification dependency failures in RailCountsPageFilter,
   log and surface unavailable status, preserve cancellation/authorization failures
   and Search's own sanitized503. Reuse existing shell components/tests.
3. A/C: complete Message/Compose/Triage chaser attachment selection using current
   Case occurrences/versions and receipt assets. Extend existing StaffMailAttachment
   identity and logical reader branch; one authorized resolver. Re-resolve exact
   selections, wire existing holding retention, refuse stale/cross-context/missing
   or changed bytes before sends. No new schema/store/transport.
4. B: project accepted/superseded recorded raw and printed totals unchanged,
   without current-policy recalculation. Fail missing/unreadable stored evidence.
   Fix existing compare fixture and add superseded projection assertion. Verify
   confirmed report-image-source correction.
5. A: update remaining G28 migration expectation; verify committed OCR source
   mapping and recovery. Refresh manual-only EVA/current-profile documentation,
   snapshots, source/caller evidence and reports. Reconcile CASE-048 as implemented
   after proof; original210 ticket mappings keep individual residual gates.

## Verification and acceptance

Root runs locked restore, Release build, focused affected tests, then final
full non-Corpus and applicable Corpus tests on frozen clean head. Fresh Test UI
update/verify/catalogue, migration grants, documentation links, Markdown
placement, diff check and applicable existing script/infra checks are required.
No new generic test structure. Preserve all previous failures; latest543 result
was Core1811PASS, Architecture109PASS, Integration1968PASS24FAIL0SKIP.

Operator explicitly selected: integrate unique required work and record
contained/superseded histories, not literal obsolete branch merges. All95 remote
heads,51 local branches,62 worktrees and7 open PRs were inventoried. Eight
cherry-positive helper areas and four dirty composition/OCR handoffs were
semantically checked: required behavior is already incorporated or superseded.
Original dirty checkout and all refs remain preserved.

Operator explicitly selected: six scan-only corpus cases lacking genuine OCR
output remain INCONCLUSIVE release gates per C02, not code-integration gates.
Retain strict failed/inconclusive evidence; do not fabricate output or broaden
this disposition to another test. Live Glass's/Box/Microsoft365/provider proof
remains external release acceptance. DVLA/DVSA credentials are available;
Experian remains deferred. No real sends or provider operations in this work.

## Integration and stop condition

Fresh independent A/B/C scope reviews bind final common head, current reports,
plan versions, threads and green applicable CI. Strictly fast-forward B/C to
that finalH, preserving originaltips ancestry; push existing3PR branches.
Normally merge674 to dev once; record672/673 inclusion truthfully and link674
as integration evidence. Close639/646/670/671 only after required behavior is
independently proved preserved. Verify exact dev mergeSHA using bound receipts
or run missing checks locally, write per-owner integration proofs, move gates
individually. Open one dev-to-main PR, confirm history and applicable CI;
leave it open, unmerged, with auto-merge disabled. No deployment or cleanup.

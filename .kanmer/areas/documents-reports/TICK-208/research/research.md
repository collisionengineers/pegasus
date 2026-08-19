# Research — TICK-208: preserve final Sent evidence through report correction

## Question

Does the current report/Sent-evidence model preserve the original issued report and its final exact Sent evidence when a report is corrected or supplemented, and what boundary must change without inventing the unresolved CASE-23 post-report lifecycle?

## Findings

- The governing invariant is settled. An issued report has immutable artifact/version identity and hash; a correction or addendum creates a new reasoned version and retains every earlier artifact, accepted fact, actor, time, and source. The exact approved-mailbox Sent item is the report-sent business event, remains final even if Outlook later moves/deletes it, and its Outlook sentDateTime remains the business time. Source: docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md.
- Exact Sent evidence retains mailbox and Sent-folder scope, immutable item, internet-message, conversation/reply-chain, source-occurrence and content hashes, sent/discovery/link times, actor/matcher, case relationship, and reasoned reassociation history. It proves existence in the approved Sent scope only—not delivery, reading, content correctness, or completion. Source: docs/frd/frd-08-email-mailbox-and-background-processing.md.
- A report correction must not overwrite the issued report or its evidence. A closed case must be reasonedly reopened before report/evidence revision. Report sent moves work into PostReport; it does not itself close the case. The exact CASE-23 post-report query/dispute states and correction/reopen interaction remain unresolved and must not be invented here. Source: FRD-11, FRD-01, and docs/open-decisions.md.
- The current Core projection has only one ReportApproval and one ReportSentEvidence on CaseWorkflowRecord. The mutation API records one current approval, links one current evidence item, and unlinks that item before another can become current. There is no report-version aggregate or issued-version-to-Sent-evidence association. Source: src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs.
- Persistence mirrors the one-slot projection. CaseWorkflows has one ReportApprovalId and ReportSentEvidenceId. CaseReportApprovals can retain multiple rows per Case, and later approval changes the workflow pointer without deleting the older row. CaseReportSentEvidence has a nullable CaseId but no report artifact/version/approval foreign key. Source: CaseWorkflowEntities.cs and CaseWorkflowModelConfiguration.cs.
- Approval history is partially retained but not modeled as issued-version history. Old approval rows survive pointer replacement, yet the normal workflow projection returns only the current approval and there is no reason/supersession/addendum relationship. Source: EfCaseWorkflowStore.RecordReportApprovalAsync and Map.
- Sent history is weaker. UnlinkReportEvidenceAsync clears the evidence row CaseId, LinkedAtUtc, and linked-actor fields, clears the workflow pointer, and returns a PostReport workflow to ReportPreparation. The immutable item remains and the workflow event stores before/after IDs plus reason, but the row no longer carries its former association and becomes an unlinked candidate. Source: EfCaseWorkflowStore.cs.
- Current link policy prevents a second Sent item while one is current and does not bind the Sent item to the approved artifact it sent. It checks only that evidence follows a ReportPreparation transition and does not predate the current approval. The database cannot answer which immutable report version the final Sent item issued. Source: EvaluateReportEvidenceLinkAsync.
- Current tests prove the present unlink/relink model: unlink is rejected in PostReport; the test closes, reasonedly reopens to ReportPreparation, unlinks, and asserts the retained item appears in the unlinked list. It proves source retention, not immutable historical report-version/Sent association. Source: tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs.
- Existing polling/retention code captures the right exact external identity and hashes and can retain an item before linking. Auto-linking is fail-closed on ambiguous/non-authoritative Case identity and uses the same chronology guards. The defect is the case/report-version association model, not discovery. Source: ApprovedMailboxReportSentEvidence.cs, PollSentEvidence.cs, EfCaseReportSentEvidenceStore.cs, and their tests.
- TICK-055/CASE-23 owns query/dispute states, Engineer response, due/chaser and completion behavior. TICK-208 can preserve issued-version evidence without deciding those transitions.
- EPIC-004 supplies the renderer-side identity needed: generated reports have immutable version/reference identity and hash, retained provenance/custody, and correction/addendum versioning. Sent association should use that identity rather than a template name, current pointer, or mutable filename.

## Implications

- The durable owner should be an append-only issued-report version record (or equivalent Core aggregate) binding Case, immutable artifact/version identity and hash, approval, correction/addendum reason and predecessor where applicable, and zero/one exact final Sent evidence item for that version.
- Correcting or adding to a report creates a new version and approval. It must not unlink, clear, recycle, or repoint the earlier version’s final Sent evidence.
- A current-report projection may remain a convenience, but cannot be the only persisted relationship. History must expose every version and its evidence without implying an earlier send issued a correction.
- A new artifact, Box upload, queue result, or staff assertion must not inherit the previous version’s Sent status. It remains unsent until its own exact Sent item is linked.
- Existing exact-evidence retention, identity/hashes, chronology, idempotency, and auto-link policy should be reused. The link request must also identify the immutable report version and reject mismatches, ambiguity, duplicates, and evidence predating approval.
- Reasoned unlink/relink remains necessary for incorrect association, but must append association history rather than erase former metadata. A correction is not an unlink; it adds a version.
- Implementation must not settle CASE-23. It can add the version/evidence ledger and leave lifecycle activation separately governed.
- No mailbox/Graph or cloud write is required.

## Open questions

None for the preservation invariant. CASE-23’s exact post-report lifecycle remains separately unresolved and out of scope.

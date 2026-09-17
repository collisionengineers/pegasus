# Pegasus report and fee-note audit — review addendum

**Review date:** 16 September 2026  
**Basis:** the supplied `report-and-fee-note-audit.md`, independently checked against selected repository source paths.  
**Verified GitHub dev revision:** `9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396`.

## Verdict

Retain the audit, but revise several proposed fixes before converting it directly into implementation tickets. The principal weakness is treating related generation, invalidation, replay, custody, and delivery behaviours as isolated defects. A1, A2, B2, and B3 need one consistent freshness policy. A3 is potentially a wrong-document integrity defect, not merely an incorrect success message. A8 needs a complete recovery contract rather than either of two partial remedies.

Keep the existing freeze → render/retain → confirm structure, QuestPDF renderer, immutable snapshots, source/signatory rechecks, and staff-controlled delivery. The reviewed code does not justify a renderer replacement, microservice rewrite, or new approval workflow. [S1] [S2] [S5] [S13] [S16]

### Evidence boundary

GitHub reported the audited SHA as the dev head during this review. The registered MCP checkout instead reported clean local main at `32f8679d3695e0dcab8f310a1c20f8b129d20190`, while remote main was `8b9d358f71ac02363a3e9fa50549d4fef0fcdac8`. Revision-specific MCP reads returned `COMMAND_NOT_ADMITTED`; the implementation findings below therefore use GitHub reads pinned to the audited dev SHA, not the stale checkout.

This was a source review. No build, test suite, browser journey, rendered PDF comparison, Box operation, database mutation, deployment, or repository write was performed. “Source-verified” means the relevant branches and guards were read; it does not mean the scenario has been reproduced in a running application. The supplied audit's complete test-count and whole-repository absence claims were not independently re-counted.

The two operator decisions in the supplied audit remain the basis: attach a separate fee note to the Case's current generation, and do not stale a generation merely because workspace Save was pressed. Additional requirements proposed below are labelled as recommendations, not established operator decisions.

## 1. Amend A1/A2/B2/B3 together: distinguish three kinds of change

A Case version is a concurrency counter: it tells a command whether the record changed since the caller saw it. It is not automatically a measure of whether the report's content changed. A delivery preparation has a third concern: whether its exact recipient and attachment selection remains current.

The current material hash excludes operation key, generating actor, and generating timestamp, but retains CaseVersion, ReportDate, and the report packaging choice. Workspace Save marks the generation stale without checking a meaningful change. Delivery pins the Case version at preparation and refuses any subsequent difference. These are connected behaviours, not independent accidents. [S1: `MaterialOf`, `FreezeAsync`] [S8: `SaveAsync`] [S5: `RequireReady`]

### Recommended policy

| Concern | Preserve | Recommended use |
|---|---|---|
| Caller concurrency | Client-observed Case version, permission, lifecycle, lease | Refuse a command submitted from an outdated view. Do not silently substitute the freshly loaded server version. |
| Report freshness | Accepted report facts and mandatory source/signatory validity | Decide whether the existing generation may still be used. A note-only or no-op save must not invalidate it. |
| Delivery freshness | Exact generation/artifact set and reviewed addressing | Replace the delivery preparation when recipients or required attachments change, without regenerating otherwise unchanged PDF bytes. |

This is a separation of responsibilities; it does not require three new tables. An explicit Core-owned change policy and version/fingerprint fields where necessary are sufficient. A fingerprint is a stable digest of the selected inputs, not a new source of business facts.

### Replacement recommendation for A1

A separate fee-note request should identify the current generation it is extending. Inside a short transaction, verify that generation is still current and usable and that the command has valid edit authority. Render from its existing frozen snapshot, including its frozen report date and fee facts. Do not resolve today's default date again to decide whether to attach the second artifact.

Remove “at the same Case version” as the sole reuse criterion. That would still reject harmless version increments after A2 is fixed. Do not remove CaseVersion from every guard or rewrite historical snapshot hashes: retain the frozen version as provenance and retain client concurrency protection separately.

Define what happens when no current generation exists, when it is stale, or when a fee note is requested first. Preserve existing behaviour only where it is intentional; do not let the implementation invent the product rule.

For an embedded fee note, enforce the duplicate-packaging rule on the server as well as hiding the separate button. Whether a later standalone copy is permitted should be an explicit output decision, not a second silent billing artifact. [S1] [S2] [S3] [S16]

### Replacement recommendation for A2

Compare normalized effective values, not just whether fields or image edits were submitted. `imagesPrepared > 0` does not prove that the crop, rotation, role, or order changed. A serialized before/after line comparison is not a complete description of every accepted-estimate dependency. The workspace also assigns SignOffEngineerId directly, so the resolved sign-off change needs explicit coverage rather than an assumption that the generic assessment-field dictionary includes it. [S8]

Use a narrow, explicit change classifier shared by all applicable accepted-write routes. It should return both whether the report became stale and the established reason code. Commit the accepted change and its invalidation atomically. A blanket SaveChanges interceptor that stales all generations would reproduce A2 in another location.

The “printed facts” decision needs one documented qualification: FRD-11 separately requires invalidation for source confirmation/removal and relevant signatory changes. Preserve those safeguards unless the operator deliberately changes that contract. Do not quietly replace evidence integrity with a PDF-text-only comparison. [S16]

### B3 sequencing

The assessment save route increments the workflow version and commits changes without the report invalidation call seen in workspace Save. Case-data Save similarly lacks that wiring, although it is restricted to unassigned NotReady/Review cases; the report must not imply that this route is reachable in every lifecycle state. [S9] [S10]

Inventory eligible writers before narrowing the delivery version check. Otherwise a broad version mismatch that currently blocks a send could be removed while accepted changes still fail to mark the report stale.

## 2. Upgrade A3: wrong-kind replay can affect stored document identity

**Evidence level:** source-verified path; runtime reproduction required.  
**Suggested severity:** high; implement ahead of presentation cleanup.

`FreezeAsync` replays an artifact by OperationKey without checking the requested kind or packaging. `GenerateCaseReport` then selects that stored artifact but passes the new request's Kind to rendering and to the custody occurrence identity. Confirmation writes back to the stored artifact ID. [S1: `FreezeAsync`, replay block] [S2: `GenerateCaseReport.ExecuteAsync`, `ConfirmAsync`]

### Reproduction to add

1. Submit an AssessmentReport request and allow its generation/artifact row to freeze.
2. Interrupt the operation before custody has created the document/version identities.
3. Submit FeeNote using the same operation key.
4. Observe whether fee-note bytes are produced and attached to the originally pending AssessmentReport artifact row.

The pre-custody interruption matters. Once custody has a retained identity, its own replay checks may reject the mismatch instead. The audit should not claim every cross-kind replay corrupts an artifact. A confirmed replay also remains the original audit's wrong-result/wrong-message scenario. [S2] [S7]

### Required fix

Separate UI operation keys, but also bind backend replay to the command identity: Case, operation type, artifact kind, packaging choice, and any explicit target-generation identity. The same key with a different semantic request must return a typed conflict before rendering. Use the verified stored artifact kind after replay resolution.

A replay fingerprint must represent the original command, not a newly sampled date or current Case version. Otherwise a legitimate retry the next day becomes a different request accidentally. Include changed IncludeFeeNote under the same key in the tests as well as changed Kind.

## 3. New source findings to add

### N1 — The browser's expected Case version is not carried through generation/preparation

**Evidence:** the report/fee forms submit their operation key and edit lease, but not an expected Case version. Preparation submits the expected generation version, not the Case version. `GuardReportCommandAsync` loads the current Case and assigns `currentCaseVersion`; the handlers pass that freshly loaded value to the Core command. [S3: generation/preparation forms] [S4: `GenerateArtifactAsync`, `OnPostPrepareReportDeliveryAsync`, `GuardReportCommandAsync`]

**Impact:** the guard protects against changes after the server read, but it does not compare against the version displayed in the originating form. An older tab can therefore request output based on newer saved facts when the remaining authority checks still permit the action. This is not a claim that permission or lease protection is absent.

**Recommendation:** post the expected version from the displayed page and preserve it through the command. Reload the actual version for comparison, not as a replacement expectation. Test two tabs where an edit leaves valid edit authority available.

**Acceptance:** outdated form → typed conflict, no new generation/preparation; unchanged current form → existing normal path. Separately test unsaved form fields, without asserting that a JavaScript dirty-form defect has already been established.

### N2 — Adding a second artifact does not explicitly invalidate the existing ready/delivery state

**Evidence:** in `FreezeAsync`, adding a new artifact to a reused generation does not set that generation back to Pending or explicitly advance its version. The delivery read model filters its ConfirmedArtifacts list to confirmed rows. `RequireReady` compares the preparation with that list and the generation state/version. [S1: existing generation plus new artifact branch] [S6: `MapAsync`] [S5: `RequireReady`]

**Source-level failure sequence:** a report-only generation is confirmed and prepared; a separate fee note is added but remains Pending; the parent can still be labelled Confirmed and the filtered confirmed attachment list can still match the old report-only preparation. Once the fee note confirms, the attachment set changes and the old preparation fails equality instead. The current UI then supplies no re-prepare path. The source contract says a generation is Pending until every requested artifact is confirmed. [S2: generation state contract] [S3] [S5] [S6]

**Recommendation:** adding a required artifact atomically updates aggregate readiness and the generation/attachment-set version, and invalidates existing delivery preparation. Confirming it restores readiness only if all required artifacts are confirmed and the generation has not become stale. Replaying an existing artifact does not create a fresh invalidation.

**Acceptance:** prepare report → add pending fee → old preparation cannot send; confirm both → newly reviewed preparation pins both exact artifacts; finishing custody for a stale generation never restores it as current.

### N3 — Composition/render failures can leave an unexplained Pending artifact

**Evidence:** freeze persists Pending before content composition and rendering. The explicit outcome recording is after custody returns an outcome; exceptions from composition, snapshot validation, or rendering do not reach that recording call. The page catches several exceptions and reports failure, while the stored artifact can remain Pending. [S2: `ExecuteAsync`] [S4: `GenerateArtifactAsync`]

**Recommendation:** record stage-specific, recoverable outcomes, for example composition rejected, renderer rejected, render wait timed out, custody pending, external outcome unknown, and confirmation pending. These names are illustrative: use a small governed vocabulary, not arbitrary exception messages as user-facing reasons.

Do not label an ambiguous external upload or send as definitely Failed. Persist definitive pre-upload rejection separately from an operation whose remote result is unknown. A process crash also needs restart recovery; a catch block alone cannot cover process termination.

**Acceptance:** known invalid image/signature/template failures produce a stable diagnostic and next action, not an indefinitely unexplained Pending state. Cancellation and crash tests cover the boundaries between every durable stage.

### N4 — Frozen renderer provenance is recorded but not fully enforced on retry

**Evidence:** a generation freezes RendererVersion; a retry uses the currently injected renderer. The draft wrapper checks the returned PDF hash, but it does not compare the returned engine/template versions against the frozen generation. `GenerateCaseReport` does not add that comparison before retaining the bytes. [S1] [S2] [S11: `GenerateAssessmentReportDraft`] [S13: `EngineVersion`, `Artifact`]

**Impact:** after an engine upgrade, an unfinished generation can be rendered using a different engine while its frozen metadata still names the earlier one. A template change may instead fail the current payload-version check. These need deliberate, different recovery rules.

**Recommendation:** compare actual renderer/template identity with the frozen contract before new bytes are accepted. Prefer reopening already staged bytes for retries. Where rendering must resume after an upgrade, use an explicitly compatible versioned path or refuse with a recoverable version-mismatch outcome. Do not relabel old snapshots or overwrite previously confirmed bytes.

Separate snapshot schema compatibility from template/wording/assets and renderer identity conceptually. This need not become a large renderer registry for the initial fix.

## 4. Expand A8 into a custody recovery contract

`RetainCaseAsync` returns the existing Pending or Failed custody state without attempting a new upload. The generation orchestrator consults custody status only for Pending/Unknown artifacts already carrying both document and version IDs. Consequently “allow Failed upload retry” alone and “let the reconciler confirm the artifact row” alone solve different portions of the problem. [S7] [S2]

| Durable situation | Recommended next action |
|---|---|
| No custody identity exists and failure is known to precede upload | Resume from compatible frozen inputs, using the original artifact operation identity. |
| Document/version exists, but the generated artifact row lacks the IDs | Discover the existing object by stable operation key before creating anything new. |
| Exact bytes are staged | Reuse the staged bytes and their hash; do not rerender unnecessarily. |
| Upload definitely failed | Permit a controlled retry of the same identity and bytes after validating the failure/retry policy. |
| Remote upload outcome is unknown | Reconcile the exact remote operation/object before deciding whether a retry is safe. |
| Custody is confirmed, generated artifact is not | Confirm the existing generated artifact from verified custody evidence. |
| Generation became stale during any step | Retain history and bytes, but never revive it as current/deliverable. |

Changing the snapshot hash tomorrow is not recovery; it creates replacement work. A8 should specify how existing stuck rows are repaired or classified without manual history rewriting.

For B5, translate known database conflicts at the persistence boundary and use bounded retries only for operations whose retry safety is established. EF documents that insert uniqueness violations are provider-specific exceptions, not ordinary DbUpdateConcurrencyException. Explicit transactions must be retried as units, and uncertain commit outcomes require verification. Do not wrap Box upload or email send in a database retry delegate. [E1] [E2]

## 5. Promote B2 and define safe preparation replacement

The UI blocks preparing whenever a preparation exists, even though the store can create a new immutable intent. Keep the report, enable deliberate recipient re-review/re-preparation, and retain the previous intent as history. [S3] [S6]

However, merely deleting `preparation is null` is insufficient. The existing GetAsync/RequireReady path checks the named intent against the generation and artifacts, not whether a newer preparation superseded it. Add explicit preparation currentness/supersession validation so an older browser tab cannot send obsolete reviewed recipients. [S5] [S6]

Also account for transport already underway. The page derives the send operation key from the preparation ID; a new preparation therefore creates a different send identity. A new intent must not be a shortcut around a prior Sending, Submitted, or Unknown result. Reconcile those outcomes and make a genuine resend explicit. Preserve the current distinction between transport acceptance and observed Sent evidence. [S4: `OnPostSendPreparedReportAsync`] [S5: `SendPreparedCaseReport`] [S16]

**Further contract question:** the reviewed preparation policy accepts any nonempty, fully confirmed artifact list, including a fee-only list, and the send handoff uses CaseReport purpose. Specify whether standalone fee-only delivery is supported and verify downstream evidence handling. A fee-only email must not accidentally satisfy a business requirement for an assessment report to have been sent. This is an investigation/requirements addition, not a reproduced end-to-end lifecycle defect. [S5]

## 6. Tighten the remaining existing findings

| Audit item | Review recommendation |
|---|---|
| A4 — printed hours | Keep the finding. Store canonical priced quantities with the accepted calculation/version. Do not derive hours by dividing discounted money by a rate, and do not recalculate historical accepted totals with a newer policy. Include legacy snapshot compatibility. [S11] [S12] |
| A5 — fallback fee description | The fallback is present. Reject missing/blank required description before render, or use explicitly accepted default copy. Do not introduce a new wording default as an engineering convenience. Maintain the current FRD's shared fee requirements unless intentionally changed. [S14] [S16] |
| A6 — VAT footer | Keep the finding. Use fee-specific page sections/footer context rather than hardcoded page numbers. Test a multi-page fee note and ensure VAT does not appear on report-only pages. [S14] [S17] |
| A7 — culture | Keep the deterministic-formatting recommendation, but remove the unverified inference that Linux necessarily lacks a thousands separator. Test explicit en-GB output under several host cultures. The source-level formatting assertion originates in the supplied audit; this review did not run culture-specific renders. |
| A9 — unsupported total-loss wording | Add readiness feedback, but record the unsupported categories as an active release-scope/accepted-wording gap. A readiness message does not implement Category A/B/N/N/A wording. Never print Category S treatment as a fallback. [S11] [S17] |
| B1 — edit lease | Preserve legitimate editing authority across successful generate/prepare actions, or explicitly release it server-side if the intended journey ends editing. Do not leave a lost browser token with an unexplained live lease. Test the complete generate → fee → prepare journey. [S4] |
| B4 — preview/download failures | Return a meaningful refused/unavailable outcome and bounded diagnostic rather than an unhandled page error. Keep access checks and exact-byte verification. Use the injected clock for presentation history. [S4] |
| B5 — database races | Use typed conflict translation and safe transaction-scoped recovery, not a blanket catch-and-retry of every SqlException. [E1] [E2] |
| B6 — stale reasons | One governed reason vocabulary should serve invalidation, history, and UI. Distinguish superseded from materially stale conceptually; retain immutable bytes regardless. Source assertions beyond the reviewed supersession helper remain attributable to the supplied audit. [S1] |
| B7 — fee VAT literals | One fee-calculation policy should supply both amount and label, and be frozen/versioned with applicable billing facts. Duplicate 20% literals are a maintainability/reproducibility issue, not by themselves proof of an incorrect tax rate. [S11] [S14] |
| B8 — constants and versions | Pin mutable billing facts and version immutable template assets/wording. Record explicitly what happens to unfinished generations across deployment. The 89-day wording remains unverified against the actual reference PDF; do not call it either approved or wrong without that evidence. [S11] [S13] [S16] |
| B9 — dead/duplicate surfaces | Keep as later cleanup after current callers, DI, tests, and any documented API contracts are checked. This review did not independently prove every whole-repository absence claim in B9. |

### Two test-review corrections

The renderer tests configure a LocalDB connection through a broad infrastructure registration, but the inspected tests resolve the renderer and render PDFs without querying SQL. Adding a SqlServer trait is not automatically the right correction. Prefer a minimal renderer-only service registration; keep database-backed tests separately classified. [S15]

The renderer tests verify that a returned hash matches its returned PDF bytes. They do not establish that rendering the identical snapshot twice at different times produces identical bytes. Add that test and inspect metadata, document identifiers, font/assets, and engine compatibility. QuestPDF exposes creation/modification metadata settings, but documentation examples do not establish the defaults of the pinned Pegasus package. Metadata nondeterminism remains a verification target, not a confirmed new defect. [S15] [S13] [E3]

The renderer's timeout cancels the caller's wait, not the underlying render task; that task releases its gate when it eventually finishes. Treat this as an operational behaviour to measure: queue time, render duration, abandoned waits, memory and recovery. Do not introduce an isolated worker solely on suspicion, but do not describe the timeout as terminating native rendering either. [S13]

## 7. Required regression matrix

These are proposed tests, not executed results. They should become precise fixtures with explicit IDs, preserved timestamps, and controllable failure points.

| Test | Scenario | Required result |
|---|---|---|
| T01 | Report generated 16 September; separate fee requested 17 September with no material change | Same generation; original frozen report date/fee facts; existing report remains available. |
| T02 | No-op save, note-only edit, recipient-only edit | No report invalidation; any delivery re-review follows its own policy. |
| T03 | Printed fact, accepted estimate, selected image preparation, or effective signatory change | Required invalidation occurs atomically with the accepted change, with a governed reason. |
| T04 | Automation changes an applicable accepted report dependency | Same invalidation outcome as equivalent staff change; forbidden lifecycle changes remain refused. |
| T05 | Same key, different kind after confirmation and after freeze-before-custody | Typed conflict; no wrong-kind PDF, row mutation, duplicate artifact, or misleading success. |
| T06 | Same key, changed IncludeFeeNote | Typed conflict; prior packaging is not silently reused as success for another request. |
| T07 | Old browser form after a version-changing edit | Client-observed version conflict; no silent adoption of the new version. |
| T08 | Prepare report; add fee artifact held Pending; then confirm it | Old intent invalidated; no partial-generation send; new intent pins both confirmed artifacts. |
| T09 | Re-prepare recipients; submit an old tab's intent | Old intent refused; current reviewed addressing remains authoritative. |
| T10 | Re-prepare while prior transport is Unknown/Submitted/Sending | No unreviewed duplicate send; prior outcome reconciled or deliberately resolved. |
| T11 | Crash after freeze, after custody identity creation, after upload, before artifact confirmation | Same operation/object recovered; no duplicate generation/upload caused by lost local IDs. |
| T12 | Failed versus Unknown custody results | Definitive failure has a controlled retry; unknown outcome reconciles first. |
| T13 | Source/signatory changes while render/upload is in progress | Completion retains bytes but never revives a stale generation. |
| T14 | Pending generation retried after renderer/template upgrade | Correct staged bytes or explicit compatible renderer; otherwise a diagnostic version conflict. |
| T15 | Specialist hours, stray paint hours, blend/paint, discounts, rounding, older accepted totals | Printed quantities agree with the accepted calculation semantics without repricing history. |
| T16 | Repairable, supported total loss, cash in lieu, contract repair; separate and embedded fees | Real render succeeds for supported cases; missing approved wording fails at readiness. |
| T17 | Long fee text over multiple pages | Fee-only header/footer rules on all fee pages; continuous page numbering; no overflow. |
| T18 | Identical frozen input across time/cultures/process restarts within the supported build | Required byte reproducibility proven, or retries reuse staged bytes rather than assume it. |
| T19 | Concurrent same-kind and second-kind requests on real SQL Server | Unique/replay constraints produce a coherent aggregate and typed conflict, not a 500. |
| T20 | Fee-only generation offered to report delivery | Explicit product rule and correct transport/business-evidence classification; no accidental report-sent evidence. |

For visuals, inspect the actual PDFs rather than only extracted text: long names, line wrapping, images, rotation/crop, font embedding, signature placement, fees spanning pages, and page-local footers. This review did not perform that visual inspection.

## 8. Make the audit implementation-ready

Each finding should carry: stable ID; severity and affected user journey; exact SHA/path/symbol; evidence level; minimal reproduction; expected behaviour; actual source behaviour; recommended change; regression test IDs; and dependencies on other findings. Keep “source-verified,” “runtime-reproduced,” “requirements gap,” and “verification required” distinct.

Replace “no build/test run is needed” with “no build/test run was performed for this read-only findings deliverable.” Source inspection is useful evidence, but does not establish deployment behaviour, database contention behaviour, actual PDF appearance, or successful failure recovery.

Move issued-fee-note correction/finality and fee-preview gaps out of the test-only list where implementation is also absent. FRD-11 describes retained issued artifacts and reasoned corrections, and separately requires fee preview. Do not imply that renaming an ApprovalWebTests file establishes an approval or issue boundary. Conversely, do not introduce a new approval screen merely to make the terminology neat. [S16]

Document source precedence. Current FRD-11 is the behaviour owner; rendererref1 is accepted/historical design evidence with some superseded and unresolved statements. The reference's old outcome-count, VIN, and image rules should not silently replace later accepted requirements. [S16] [S17]

The audit's `a.`/`ap.` test expectations should identify the currently approved reference policy. A separate proposal to standardize audit prefixes must remain a separate decision until approved; do not bake an unresolved future convention into this report-route repair. The current FRD text still describes outcome-specific prefixes. [S16]

### Existing-data and deployment plan to add

Before rollout, produce a read-only inventory of stuck generations, failed/pending artifacts, custody-confirmed documents missing artifact confirmation, and preparations no longer compatible with their generation. Include counts and identifiers, not sensitive document content.

After the new policy is tested, reconcile only cases supported by exact identity/hash evidence. Do not mark generations fresh globally, recompute historical hashes in place, overwrite issued fees, or create replacements merely to clear a warning. Preserve confirmed downloads across template changes. Any migration needed for stored accepted quantities or new readiness/version fields must define defaults and compatibility for existing rows. These are proposed rollout requirements, not claims that such an inventory was performed.

## 9. Recommended implementation order

| Order | Work package | Why |
|---|---|---|
| 1 | Wrong-kind replay guards and client expected-version preservation: A3, N1 | Prevent wrong-command identity and stale-form acceptance. |
| 2 | Shared report freshness and writer parity: A1, A2, B3 | Make second-artifact reuse and material invalidation consistent before relaxing delivery guards. |
| 3 | Artifact aggregate state and safe re-preparation: N2, B2 | Eliminate partial-generation send paths and undeliverable prepared reports. |
| 4 | Durable recovery and version enforcement: A8, N3, N4, bounded B5 handling | Prevent stuck artifacts, duplicate external work, and misleading provenance. |
| 5 | Calculation/readiness/printed output: A4, A5, A9, A6, A7, B7/B8 | Restore correct quantities, accepted wording, page details, and reproducibility. |
| 6 | Lease/error presentation, reason vocabulary, observability, then dead-code cleanup | Finish the operator journey without conflating cleanup with safety fixes. |

Write the targeted failing tests with each work package, not after all implementation. A4 and unsupported/missing wording may be release blockers for affected cases even when the engineering dependency order puts foundational state work first. Separate implementation dependency order from release severity.

**Bottom line:** the existing architecture is suitable. The audit should lead to a smaller, more coherent correction of command identity, freshness, aggregate state, recovery and immutable delivery—not a new reporting subsystem.

## Source register

Repository references below are pinned to the audited dev commit. Paths and named methods are the primary evidence; line numbers in the original audit should be re-resolved when implementing on a newer revision.

- **[S1] Generation persistence:** `src/Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs).
- **[S2] Generation contract, readiness, and orchestrator:** `src/Pegasus.Core/Reports/CaseReportGeneration.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Core/Reports/CaseReportGeneration.cs).
- **[S3] Report section forms:** `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml).
- **[S4] Report page handlers and guards:** `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Web/Pages/Cases/Details.cshtml.cs).
- **[S5] Delivery policy, preparation, and staff-send handoff:** `src/Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs).
- **[S6] Delivery preparation persistence:** `src/Pegasus.Infrastructure/Persistence/EfCaseReportDeliveryPreparationStore.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Infrastructure/Persistence/EfCaseReportDeliveryPreparationStore.cs).
- **[S7] Case artifact custody:** `src/Pegasus.Infrastructure/Custody/EfCaseArtifactCustody.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Infrastructure/Custody/EfCaseArtifactCustody.cs).
- **[S8] Workspace persistence:** `src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs).
- **[S9] Assessment persistence:** `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs).
- **[S10] Case-data persistence:** `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs).
- **[S11] Report snapshot and rendering contract:** `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Core/Reports/AssessmentReportRendering.cs).
- **[S12] Canonical estimate calculations:** `src/Pegasus.Core/Assessment/Estimates.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Core/Assessment/Estimates.cs).
- **[S13] QuestPDF renderer:** `src/Pegasus.Infrastructure/Reports/QuestPdfAssessmentReportRenderer.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Infrastructure/Reports/QuestPdfAssessmentReportRenderer.cs).
- **[S14] Report and fee-note layout:** `src/Pegasus.Infrastructure/Reports/AssessmentReportLayout.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Infrastructure/Reports/AssessmentReportLayout.cs).
- **[S15] Real renderer tests:** `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs).
- **[S16] FRD-11:** `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md).
- **[S17] Retained renderer design evidence:** `reference/rendererref1/DESIGN_SPEC.md` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/reference/rendererref1/DESIGN_SPEC.md).
- **[S18] Renderer package and embedded assets:** `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` — [9c4c09eb7](https://github.com/collisionengineers/pegasus/blob/9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396/src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj).

### External implementation guidance

- **[E1] Microsoft — Connection Resiliency:** https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency (checked 16 September 2026).
- **[E2] Microsoft — Handling Concurrency Conflicts:** https://learn.microsoft.com/en-us/ef/core/saving/concurrency (checked 16 September 2026).
- **[E3] QuestPDF — Document metadata:** https://www.questpdf.com/concepts/document-metadata.html (checked 16 September 2026).

The external documents support implementation guidance, not claims about executed Pegasus behaviour. The original uploaded audit remains the source for findings explicitly described above as not independently re-proved.

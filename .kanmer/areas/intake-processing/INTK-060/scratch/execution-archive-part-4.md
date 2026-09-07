# Archived execution log — part 4 of 4

Original SHA-256: `e6b834160b41638637d9ffd1115c7ebc1cc502987578eb36c6711e474485bdc7`
Character range: 75000–95045 of 95046.

## Payload

C03 round 1 = the party-block cut + merge C head.

- 2026-09-06T19:28:31.145Z claim-transfer claude-fable-c → codex-stream-c-replacement (live; operator: user explicitly appointed this fresh Codex session as the replacement Stream C controller for usage-exhausted claude-fable-c; preserve the recorded task/pegasus-v1-intake branch, existing v1-intake worktree, helper worktrees, and all dirty work; lease 63073298-18b0-430d-a135-bd3a610d0f30 → 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 33; branch task/pegasus-v1-intake; worktree ../pegasus-worktrees/v1-intake; expires 2026-09-06T19:58:31.136Z; evidence: workspace clean (matches-claim), pr absent, commits 0, proof absent)

## 2026-09-06 replacement-controller takeover

User-authorized transfer from claude-fable-c completed without force. Preserved branch task/pegasus-v1-intake and worktree ../pegasus-worktrees/v1-intake; exact clean owner head 27004c0ea974057aa6363d4a61c6e2907a29c897 matches origin and open draft PR 673 against dev. New lease 40f03659-6845-4f20-b1cc-cfd09686ec00 revision 34, controller codex-stream-c-replacement, extended through 21:30Z. Native dispatch is disabled with zero dispatches. The remaining local claude.exe process and its Kanmer MCP child were stopped after transfer so the exhausted controller cannot auto-resume.

Preserved helper state: C07b clean 2c427c643667596e397b6e51672bc32595075646; C02 6482cca59a628019ffe781a9056cba7d85a04bc5 plus dirty src/Pegasus.Core/Intake/IntakeOcr.cs; C03 clean 0f1355108e2cb25c022a5e54454a97857b484794; C08 ef9d71655f76c69f88f04c0f8d52e133582a7d5c plus six dirty C-owned files. No reset, stash, rebase, clean, duplicate worktree or restart.

PR 673 takeover/status posted as comment 5561608677. C07b has no round-4 attestation: retained review file ends at round 3 needs-changes against 0a0e88975, while helper has advanced. Do not integrate until a fresh independent exact-head review exists. Next bounded executable lane is to inspect/complete the preserved C02 correction without replacing its work.

- 2026-09-06T19:51:21.907Z lease-phase running-command → implementing (lease 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 36; expires 2026-09-06T20:21:21.900Z)

Replacement controller integrated independently reviewed C07b custody/public-upload caller slice: helper 348cb07d9d7bc3e14a01f75f96e34882e2c10f71 (review PASS; focused Integration 36/36 PASS) merged with --no-ff into Stream C owner head 9e1565a30dd4492e35a00c8900b01a03668c14ac, build 0/0, pushed to PR #673. Architecture remains 98/100 due A-owned missing IReadLogicalDocumentVersion Worker composition; handed to PR #674 comment 5561776928. No merge/deploy/live writes.

- 2026-09-06T20:48:15.018Z lease-phase implementing → running-command (lease 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 42; expires 2026-09-06T21:33:15.012Z)

## Replacement-controller progress 2026-09-06 20:50Z

- C07/C-B01 reviewed helper published at bbda5b38acc52d4649b87853313408e998657b43. Boundary semantics match A; Recipient required <=500, Reason optional <=1000. A durability blob b1f463047 retained. Standalone/combined run awaits A-owned EfCaseArtifactCustody and A-owned durability conflict resolution requested on PR 674.
- C08 reviewed/tested helper published through 1c50286d3a71e872f95e1788f505945e49855a1e: palette idempotence 2 browser PASS, notification cap 2 Core PASS, mail round-trip 1 PASS after correcting the test anchor extraction, capability truth table 3 PASS. Reply modes still await A retained-message identity projection.
- C03 AX/FW reviewed/tested at d0937813e9621e4d62a012d9304cecee74e8cd95: Core 11 PASS including AX02; FW genuine five-source matrix 1 PASS/no skip. QCL is next bounded implementation.
- C06 zero-state delta remains reviewed and Release-build clean at refreshed head 49fabcb9da9667fdd9f4db8e8c425e7430dbc7fa. Required snapshot capture failed before generation (122/129) solely because the same A custody adapter is absent; no generated changes.
- PRs 672/673/674 remain open/unmerged. No deployment, mailbox/provider write, or mail send occurred.

- 2026-09-06T20:57:03.780Z lease-phase running-command → implementing (lease 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 43; expires 2026-09-06T21:27:03.775Z)

- 2026-09-06T22:40:29.355Z lease-phase implementing → running-command (lease 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 57; expires 2026-09-06T23:40:29.350Z)

- 2026-09-06T23:01:06.369Z lease-phase running-command → implementing (lease 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 58; expires 2026-09-06T23:31:06.363Z)

- 2026-09-06T23:20:18.752Z lease-phase implementing → running-command (lease 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 60; expires 2026-09-07T00:20:18.745Z)

- 2026-09-07T00:50:22.138Z lease-phase running-command → implementing (lease 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 66; expires 2026-09-07T01:20:22.121Z)

- 2026-09-07T01:02:51.552Z claim-transfer codex-stream-c-replacement → claude-fable-c2 (live; operator: replacement Stream C controller takes over from paused codex-stream-c-replacement per handoff-stream-c.md (2026-09-07); same worktree/branch, no reset; lease 40f03659-6845-4f20-b1cc-cfd09686ec00 → b03b3df8-dca2-44ae-b628-515528bdf02f rev 67; branch task/pegasus-v1-intake; worktree ../pegasus-worktrees/v1-intake; expires 2026-09-07T01:32:51.545Z; evidence: workspace clean (matches-claim), pr absent, commits 0, proof absent)

- 2026-09-07T01:04:24.992Z lease-phase implementing → running-command (lease b03b3df8-dca2-44ae-b628-515528bdf02f rev 68; expires 2026-09-07T03:04:24.985Z)

## 2026-09-07 01:15Z — controller resumption (claude-fable-c2)

Lease transferred from paused `codex-stream-c-replacement` under operator authority (lease `b03b3df8-dca2-44ae-b628-515528bdf02f`, revision 68, phase running-command). Same worktree `../pegasus-worktrees/v1-intake`, branch `task/pegasus-v1-intake`, no reset.

- Consumed A handoffs `012cbc0af` (C049 Worker-only lookup composition) and `c596f7570` (persisted OCR result projection) as exact objects via `--no-ff` merges `136b30a2d` and `aa5e669d7`; owner head `aa5e669d7` pushed. Solution Release build: all projects 0W/0E except `Pegasus.IntegrationTests` (A-owned `EfCaseArtifactCustody` at `DocumentCustodyDurabilityTests.cs:462`, unchanged hold).
- Combined probe C `aa5e669d7` into A `a243fd209`: 7 conflicts (corpus json, IntakeMcpTools, Program, AutomationIntakeParityIngressTests, CustodyOutboxIntegrationTests, ProductionCompositionTests, RetainedMailPersistenceTests); aborted clean. Reported on PR 673 with the request for a published combined tree or authorized resolutions, and for A's durability-test retarget.
- Dispatched: Opus source review of `df198034a` (scratch `review-c07c`); Opus C05 reconstruction seam in `v1-intake-c05` (`c05-third-party` at `aa5e669d7`); Sonnet C08 InvalidOperation correction in `v1-intake-c08` (`c08-shell` at `aa5e669d7`); Sonnet research for C07 promotion and C08 chaser identity. Session scratchpad `…/5adc2fb3-f15d-4145-84ed-948eb9fde4e4/scratchpad/takeover/`.
- No deployment, live provider write or mail send. PRs 672/673/674 open, unmerged.

## 2026-09-07 01:45Z — research results (controller)

- C08 chaser: originating retained-message identity is not queryable today; the join (receipt `SourceChannel`/`ExternalReceiptToken` ↔ `RetainedMailboxMessages.ExternalReceiptToken`) exists in `EfRetainedMailboxMessageStore`. Both `RetainedMail.cs` (`IRetainedMailQueries`) and that store are A-owned per the ownership register, so the member `GetByOriginReceiptAsync(Guid receiptId, ct)` was requested from A on PR 673 (comment 5563690152). C's handler `OnPostSendChaserAsync` on `Pages/Triage/Details` follows once published; Reply refused server-side for non-Mailbox origins, `New` only on explicit choice. `AllowStaffSend`/`Generation` mailbox mapping exists on A's branch only (combined dependency, same as Compose).
- C07 promotion: no B entrypoint needed — every acceptance path already funnels through C-owned `AcceptIntake.ExecuteAsync` into B's `ICaseAcceptanceStore`; `ILinkTriageCase` exists but is only called from the manual `/Triage/Details` link_case action. Slice to author: C-owned `AssociateOriginatingTriage` invoked after acceptance (advisory, replay-safe, deterministic key), linking (1) the Triage whose origin receipt is the accepted receipt, else (2) the single open unlinked Triage with the same principal code and normalized registration; zero/multiple candidates or an existing different link → no automatic link, staff path remains. DI registration is an A handoff (`DependencyInjection.cs`/`Program.cs` A-owned). Research files: scratchpad `takeover/c07-promotion-research.md`, `takeover/c08-chaser-research.md`.

## 2026-09-07 01:35Z — combined verification ref available

- A published `task/pegasus-v1-verification` exact `7441c89437e84cbc836475cf3ce86bbea1e8dc0f` (A `7786699f8` + B `0d0d5627c` + C `aa5e669d7` + C049/OCR handoffs + QDOS source metadata correction; PR 672 comment 5563660618). Rules: isolated worktree only, never merge into the owner branch, no extra PR; A forward-pushes it. Checked out in `../pegasus-worktrees/v1-intake-combined-verify` on local branch `verify/stream-c-combined`, with C slice commits layered locally for lane execution (C08 `4f0c04113`, C05 seam `35cc17c66`); locked restore + Release build running.
- Slice status: C05 seam `35cc17c66` READY (wave 36: Core ThirdPartyReport 36/0/2, Core full 1562/1/14 with the single failure the A-owned tracked-hash drift `TrackedPegasusSourceHashesHaveNotDrifted`; independent review running). C08 InvalidOperation correction `4f0c04113` READY (Web build 0W/0E; review running). C07 public-session slice `df198034a` review NEEDS CHANGES (4 blockers/7 majors/7 minors, `scratch/review-c07c`); correction round 1 running in `v1-intake-c07b`; round 2 (replacement as new occurrence) needs A's `ReplacesOccurrenceId` column, asked in PR 673 comment 5563714824. C01 all-15 Analyze proof authoring in `v1-intake-c01`.
- Lease renewed: revision 69, expires 03:28Z.

## 2026-09-07 02:05Z — C05 seam integrated; C01 proof authored; waves 37/38

- Owner head **`b386c9dd2`** pushed: `--no-ff` merge of reviewed C05 seam `35cc17c66` (review PASS `scratch/review-c05`; Core ThirdPartyReport 36/0/2; combined ThirdPartyReport corpus/provenance 17/0/5). Signature + verified consumer note published to A on PR 673 (the author's ordering guidance was corrected by the reviewer: order rows by `(Field, ReferenceRole, PartyRole, Id)`; printed order would need an A ordinal column).
- Wave 37 (combined `e31a58713` = verification ref + C08 `4f0c04113` + C05 seam): correspondence 37/1/0 (the one failure is the new C08 regression, wrongly asserting a thrown exception — review NEEDS CHANGES `scratch/review-c08`; correction round 6 running in `v1-intake-c08`); public-upload baseline 49/1/0 (the `df198034a` slice's own new test fails at `PublicUploadRetentionWebTests.cs:1331` Expected Found/Actual OK — handed to the C07c round-1 implementer as R-0); analysis-host 31/0/0; worker composition 45/0/0.
- C01 all-15 proof authored at `d505d6078` on `c01-retained-analysis` (`EveryGenuineOriginalReachesRetainedAnalysisWithoutAllocating`, `NoGenuineNonQdosOriginalIsAllocatedAutomaticallyThroughNormalIntake`; standalone compile check: only the A-owned CS0246). Layered onto the verification branch as `1d11ac1eb`; wave 38 running with `PEGASUS_REFERENCE_PACK_ROOT`; independent review running (`review-c01`).
- Open A items: combined-tree adoption acknowledged; still owed by A — durability-test retarget (moot in combined), receipt-keyed retained-mail query (5563690152), `ReplacesOccurrenceId` column (5563714824), C05 `IThirdPartyReportCandidateQueries` implementation now unblocked.

## 2026-09-07 02:40Z — findings from wave 38, C07c round 1, OCR research

- Wave 38 (combined `1d11ac1eb` = verification ref + C08 + C05 + C01 proof): Integration build 0W/0E; Top15 corpus + source manifest 9/0/0; `RetainedInstructionAnalysisTests` 5/1/0 — `EveryGenuineOriginalReachesRetainedAnalysisWithoutAllocating` fails with `ArgumentException` from `PchInstructionExtractionPolicy.Extract` ("accepts only fully readable, complete reader results") thrown through `AnalyzeRetainedInstruction.cs:417`. Production gap: Analyze checks `Status != Readable` but not `IsIncomplete` before `Extract`, so a partial read becomes an exception on the `/Received` re-evaluation path instead of a typed outcome; and the retained read of at least one PCH original is incomplete where the direct reader read (Top15 test) is complete — root cause (file name/media type via the A04 double, PDF limits, or reader) still to be found. Handed back to the C01 implementer when an editor slot frees.
- C07c round 1 at `64cc0e90e` (`324cf08f8` source, `64cc0e90e` tests): 16/18 findings fixed, standalone compile checks clean. R-0 root cause: in-place replacement re-presents the same occurrence identity to custody and violates the unique `(CaseId, SourceOccurrenceIdentity)` index — so the baseline slice's replacement could never work. Controller authorised R-1 half (a) now (new occurrence row, superseded row untouched, totals count all bytes); half (b) `ReplacesOccurrenceId` stays round 2 pending A. Test UI snapshots `upload-request--default/--validation` are stale pending combined capture.
- OCR path: research (`takeover/c05-ocr-replay-research.md`) confirms the Completed-replay gap (`IntakeOcr.cs:474-477`, `:752-753`) and that no reusable report-analysis command exists (inline in `ProcessIntake.RecordThirdPartyReportSourceAsync`). Bigger finding: `IProcessIntakeOcr`, `IIntakeOcrOperationStore`, the provider and the `intake_ocr` work kind are registered/routed NOWHERE on A, C or the verification ref — the C-F03 hunks drafted with C02 were never sent. Posted to A as the exact handoff (PR 673); C's enqueue method follows once the kind constant exists.

## 2026-09-07 03:10Z — C01 round 1, C07c round 1, reader gap

- C07c round 1 at `3a13a6e3d` (`324cf08f8`, `64cc0e90e`, `3a13a6e3d`): 17/18 findings fixed incl. R-1a (replacement = new occurrence row; old row untouched); wave 39 (combined `791b03e0c`): solution 0W/0E, Core upload policies 36/0/0, browser upload 7/0/0, public-upload Integration 56/1/0 — the one failure `FinishNamesTheFileItIsWaitingForAndProceedsPastARefusedOne` (:1443) because totals counted a terminal Failed occurrence. Rule set by controller: totals count Confirmed + in-flight (Pending/Arrived/Unknown), exclude terminal Failed. Round 1a fix underway; independent review of round 1 running (`review-c07c`).
- C01 round 1 at `d57383b2b`: `AnalyzeRetainedInstruction` now returns `SourceUnavailable` for `IsIncomplete` reads before selection (production fix, Core test added); corpus test survives per-sample throws, writes the matrix in `finally`, INCONCLUSIVE bucket. Independent review of d505d6078 was NEEDS CHANGES (F-001/F-002 = these fixes; F-003 standalone-only: A's `LocalLogicalDocumentVersionReader`/`CachedDocumentContentStore` are registered in A's DI, so the combined host resolves the real reader). Wave 40 running on combined `83df6c00b`.
- **Reader gap (C02, C-owned):** 31/81 genuine originals read `Readable+IsIncomplete` — 25 legacy `.DOC` (PCH, RJS, ALS, BC, MP-Word; the custom binary Word parser returns `Partial` for structures outside its text extraction, likely tables) and 6 low-text MP PDFs (OCR path, unwired — see C-F03 handoff). Consequence: five profiles have never reached extraction on genuine bytes through the reader; the Top15 corpus test had been skipping them as Inconclusive. Research dispatched (`c02-notes`) on the exact Partial triggers and the smallest no-new-package parser extension; an Opus reader slice follows. The all-15 proof cannot be called complete until this lands; the current honest ceiling is ~10/15 profiles, ~50/81 originals.

## 2026-09-07 03:40Z — checkpoint

- Owner head `68488bfa3` pushed (A origin-mail query `ce718db54` + G21 `47e36892f` consumed). PR 673 comment 5564043708 records consumption, the C-F03 split (A: router/kind/Worker composition; C: linked enqueue lifecycle), and the reader gap.
- Waves: 40 (combined `83df6c00b`, C01 r1) all green — Core Analyze 25/0, RetainedInstructionAnalysis 6/0, matrix 45 analysed / 36 inconclusive (ALS, BC, PCH, RJS, MP inconclusive; reader). 41 (combined `ef60a7f19`, C08 r6) all green — correspondence 38/0, mail workspace 79/0. 42 running (combined `d1359307b`, C07c r1a).
- Reviews: C07c r1 `PASS pending named corrections` bound to `ba8ccd79e` (majors: drop `Arrived` from the counted set; an R-10 test must execute; `IsUnresolved` (already removed in r2); replacement on a count-exhausted link must remain possible per item 6) → round 3 queued. C08 r6 re-review running; C01 r1 re-review running.
- C07c round 2 at `4476ed138` (R-1b lineage via G21, superseded rows rendered/ignored by Finish, IntakePersistence migration list) READY; B-owned `CaseWorkflowMigrationTests.cs:131` needs the same entry — told B on PR 672. Wave 43 after 42.
- Reader slice dispatched (Opus, `v1-intake-c02` at `68488bfa3`): reclassify binary-Word issue conditions so only genuine text loss sets `Partial`; emit table cells with locators; pack-gated proof for the 25 `.DOC` originals. Research recorded that the C03 PCH/ALS/BC suites were fed transcribed text, not reader output — profile corrections may follow the rerun of the all-15 proof.
- Envelope-limits slice (Opus, `v1-intake-c07` `c07-precase`) running. Lease revision 70, expires 03:57Z.

## 2026-09-07 ~02:45Z (server) — integrations and A findings

- Owner head `9c015ba56` = reviewed C01 round 1 `d57383b2b` (review PASS `review-c01`; matrix corrected to 50 analysed / 25 source-unavailable / 6 no-profile / 0 failed, replay 0, Cases 0 on all 81). Owner head **`d4496a838`** = reviewed C08 correction `729b284e1` (review PASS `review-c08`; wave 41 38/0 + 79/0); closes A finding 5563408956; F4 S12 note relayed to A (PR 673).
- Wave 42 (combined `d1359307b`, C07c r1a): solution 0W/0E, public-upload 57/0, Core documents 32/0. C07c r1 review `PASS pending named corrections` → round 3 running (drop `Arrived` from counted set; executing R-10 test; replacement allowed on count-exhausted link per item 6; ASSUMPTION 6/7 wording). Round 2 `4476ed138` (R-1b lineage) READY, unexecuted.
- Limits slice `4ae44e232` READY (100 MiB/pinned 200 MiB+64 KiB/20/Provider per-file = 30 MiB envelope); source review running; host items (Kestrel `MaxRequestBodySize` unset; "10 MB" literals in A-owned `StatusCode.cshtml.cs`, `IntakeMcpTools.cs`) reported to A (5564189655).
- A finding 5564202567 accepted: the all-15 corpus test will be split into the diagnostic inventory plus a mandatory acceptance gate that fails on any inconclusive and requires every profile analysed (expected RED until the reader/OCR work lands). C01 corrective queued for the next editor slot.
- Verification ref `97849cc73` now carries A's C05 query `028675839` (edits C-owned `RetainedInstructionAnalysisTests`/`ThirdPartyReportProvenanceWebTests`), which conflicts with C `9c015ba56`; A resolved it in its combined `025c60dd7` and C accepted that resolution and asked A to advance the ref (5564208395). Local verify worktree is clean at `97849cc73`; C08/C07r2/limits lanes wait for the advanced ref.
- Reader slice (C02 `.DOC` false-Partial + table cells) running in `v1-intake-c02`.

- 2026-09-07T03:09:08.211Z claim-transfer claude-fable-c2 → antigravity-stream-c (live; operator: resume Stream C under replacement controller antigravity; lease b03b3df8-dca2-44ae-b628-515528bdf02f → 9ae60d65-c40a-473e-a5ec-041f1ac5b5a5 rev 72; branch task/pegasus-v1-intake; worktree ../pegasus-worktrees/v1-intake; expires 2026-09-07T03:39:08.204Z; evidence: workspace clean (matches-claim), pr absent, commits 0, proof absent)

- 2026-09-07T03:16:49.251Z lease-phase running-command → implementing (lease 9ae60d65-c40a-473e-a5ec-041f1ac5b5a5 rev 73; expires 2026-09-07T03:46:49.245Z)

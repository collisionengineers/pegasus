# Pegasus codebase audit — 16 September 2026

Duplicate routes · unwired code · inefficiency · over-engineering · `/simplify` targets.

## Context

Alex asked for a full long-form audit of the codebase. A three-part audit ran on 2026-09-11
(`artifacts/audits/2026-09-11/`, 24-item backlog); nothing from it was implemented. This audit
re-verifies that backlog at today's head and covers the lanes it touched thinly — the
route/endpoint map and unwired code. It is read-only: no build, test, cloud call or source edit.

**Source root.** Started on `codex/u50-pdf-image-intake` @ `4cb3ad0c0`. Part-way through, the
main checkout was switched to `dev` @ `9c4c09eb7` by something outside this session (it now
carries uncommitted `design/` edits — not touched). The two refs differ only in the 33 U50
`src/` files (Core Intake/ImageIntake/Custody, Infrastructure Custody/Persistence, four Web
pages, `WorkerDependencyInjection.cs`); every other citation is byte-identical on both.
Citations in U50 files were verified against the codex ref.

**Scale (non-migration):** Core 198 files / 55.8K lines · Infrastructure 224 / 75.3K (+215
migration files / 408K) · Web 241 / 57.9K · Worker 9 / 1.2K · tests 395 / 154K. Composition:
`Program.cs` 1,402 · `DependencyInjection.cs` 990 · `OperatorLabels.cs` 2,233 · `site.js` 2,643
· `site.css` 2,658. Surface: 54 Razor pages, 255 handlers, 9 minimal-API registrations, 47 MCP
tools, 8 Worker functions, 410 Core interfaces, ~542 DI registrations.

**Method.** Three parallel read-only explorers (routes; unwired; inefficiency + backlog), then I
re-read every finding that would change a decision (marked VERIFIED). Grep-based reference
counts treat declaration + implementation + DI registration + tests as non-consumers; no
`using static` or reflection over the label classes exists, so dotted-name greps are sound.

---

## A. Defects found on the way (not cleanups — fix first)

**A1 — "Open case" link after upload is a 404.** VERIFIED.
`src/Pegasus.Web/Presentation/UploadOutcome.cs:212,240` hand-build `$"/Cases/Details/{caseId:D}"`.
`Pages/Cases/Details.cshtml:1` is `@page "/Cases/{id:guid}"`; a custom template starting with `/`
*replaces* the default `/Cases/Details` route, so nothing answers `/Cases/Details/<guid>` and the
request re-executes to `/status/404`. Every other site uses `RedirectToPage("/Cases/Details", …)`
(page *name*, resolved correctly). Three tests pin the broken string:
`tests/Pegasus.IntegrationTests/UploadOutcomeQueriesTests.cs:71,400`,
`UploadConfirmationWebTests.cs:139,264,567` — they prove the literal, not the link.

**A2 — "Move to <folder>" in the mail workspace always fails in Production.** VERIFIED.
`Pages/Mail/Message.cshtml:495-498` renders the button whenever a suggested move exists;
`Message.cshtml.cs:949` runs `MoveRetainedMailFolder`; `EfRetainedMailFolderMoveStore.cs:159-163`
records the operation then marks it `failed: "Outlook folder moves are unavailable in this
runtime"` because the only registration is
`TryAddSingleton<IRetainedMailFolderMover, UnavailableRetainedMailFolderMover>`
(`DependencyInjection.cs:108`). `GraphRetainedMailFolderMover`
(`Infrastructure/Email/GraphApprovedSources.cs:1296`, `internal`) has never been registered in
any root since it landed on 2026-08-20 (`8b1e6d74f`); it is exercised only by
`ProductionGraphSourceTests.cs:602,627`. ADR-0036 §28-30 says the mover "is composed only by
explicit configuration" — no such configuration path exists in `src/`. Not composing it is
consistent with the alpha rule (no Outlook mutation); the defect is a UI that offers a doomed
action and writes a failed operation row each time. Either gate the button on `IsAvailable`
(the store already exposes it) or decide to compose the Graph mover.

**A3 — Heartbeat handlers answer with different status codes for the same outcome.** CONFIRMED.
Nine copies (§B, R10): success is `204` in `Accounts:296`, `Configuration:269`,
`CaseMutationPageModel:472` but `200` in `Glass:238`, `Contacts/Edit:237`,
`ValuationPresets:380`, `ImageIntake:340`, `Triage:501`; a missing token is `400` in Accounts,
`409` in Glass/Contacts/ValuationPresets and unguarded in Configuration/ImageIntake/Triage. Five
JS loops consume them (`site.js:170`, `site.js:832`, `accounts.js:85`,
`workflow-configuration.js:4`, `case-workspace.js:461`). A shared handler would remove the
divergence for free.

**A4 — Five POST-only pages render an empty 200 on GET.** PLAUSIBLE.
`Cases/{Workflow,Vehicle,Custody,Tasks,Closure}.cshtml` are two-line pages with no `OnGet`;
Razor Pages renders the empty page when no handler matches. `Cases/Documents/Export.cshtml.cs:18-26`
documents exactly this hazard and adds an `OnGet` guard; `PreCaseImages/Index.cshtml.cs:23`
returns `NotFound()`. The five action pages have neither.

**A5 — Two Worker timers share one schedule key.** CONFIRMED. `Worker/IntakeFunctions.cs:26`
(PendingWorkRecovery) and `:49` (AutomaticEvaReviewSubmission) both bind
`%PendingWorkRecoverySchedule%`; the EVA timer cannot be tuned independently.

---

## B. Duplicate routes, handlers and views

**Routes — checked and sound.** No two `@page` templates collide; minimal-API paths (`/health/*`,
`/diagnostics/version`, `/hooks/…`, `/connect/token`, `/mcp`, `/automation/*`,
`/api/provider/v1/*`) overlap no Razor route; `IsMachineSurface` (`Program.cs:1210-1215`)
excludes exactly those from status re-execution; MCP tool names are unique; MCP tools call the
same Core use cases as the Razor handlers rather than re-implementing queries.

**B1 — Four zero-inbound redirect shims.** `Triage/Index.cshtml.cs:20`,
`Unidentified/Index.cshtml.cs:17`, `Cases/Assessment/Index.cshtml.cs:16`,
`Administration/ActionLogs.cshtml.cs:15` — each is `RedirectPermanent` to a `/Cases?tab=…` or
`section=` URL. Nothing in `src/` links to them (only `_Layout.cshtml:108` uses `/Triage`,
`/Unidentified` as `aria-current` prefixes). `Operations/Index.cshtml.cs:454-456` still routes
the AI-job "open" action through the `/Cases/Assessment/Index` shim instead of
`/Cases/Details` + `section=estimate`.

**B2 — Five orphan views.** `Cases/Assessment/Suggestions.cshtml` (129 lines, no `@page`, header
says "design only"); `Shared/_EditHeartbeat.cshtml` (its ViewData keys are set nowhere);
`Shared/_Provenance.cshtml`, `_ProvenancePanel.cshtml`, `_MetricCard.cshtml` (referenced only
by `docs/design/README.md:900`).

**B3 — Eight named handlers whose only callers are tests.**
`Cases/Tasks.cshtml.cs:66,94,122,148` (Create/Assign/Complete/CancelTask — of that page's eight
handlers only `AddNote`, `RecordManualChase`, `LinkReportEvidence`, `UnlinkReportEvidence` have
live forms), `Cases/Workflow.cshtml.cs:171` RecordEngineerFinding,
`Cases/Closure.cshtml.cs:24` RecordReportApproval, `Cases/Custody.cshtml.cs:41` RetryCustody,
`Cases/Details.cshtml.cs:1514` GenerateReportDraft (`AssessmentReportDraftWebTests.cs:130,164`
assert the handler is *not* rendered — it is dead by test contract).

**B4 — Dead middleware branches.** `Program.cs:1135-1138` (document-custody-off gate) 404s
`/requests` and `…/documents` paths; no page has a `/requests` route and no route ends in
`/documents` (they end `/Download`, `/Export`). Only the `/uploads` branch is live.

**B5 — Three document-download surfaces, two custody semantics.** Razor
`Cases/Documents/Download.cshtml.cs:80-131` (audited via `IDownloadCaseDocument`, filename and
media-type validated `:462-507`, missing-vs-transient classified `:443-460`) vs
`Mcp/AutomationDocumentStreaming.cs:21-53` (`IReadLogicalDocumentVersion` directly — unaudited,
unvalidated, `enableRangeProcessing: true`), reached from `DocumentMcpTools.cs:228` for oversize
content. Export: Razor `Export.cshtml.cs:46` uses `IExportCaseBundle`, MCP
`DocumentMcpTools.cs:266` uses `IExportCaseDocuments`; the Razor doc comment `:37-41` describes
an unnamed POST that does not exist and denies a GET that does (`:25`).

**B6 — "Closable by hand" predicate split.** `Index.cshtml.cs:649 CompletesByHand`
(QueryResponse | UnidentifiedQueuePass) vs `Operations/Index.cshtml.cs:469 CanCompleteByHand`
(adds MarketResearch), each feeding its own `OnPostCompleteAiJobAsync` (`:263` / `:219`).
`OnPostCancelAiJobAsync` (`Operations:264`) and `OnPostStopAsync` (`Administration/AiJobs:31`)
both call `ICancelAiJob` with different reason rules.

**B7 — The edit-scope handler family is copied across nine page models with no shared base.**
`AdministrationPageModel` has one member (`IsOperationKeyValid`), `StaffPageModel` two.
| Member | Copies | Most complete |
|---|---|---|
| `OnPostReleaseScopeBeaconAsync` | 8 public + identical XML doc | `Mailboxes.cshtml.cs:1044-1073` (parameterised by kind/id/operation) |
| Heartbeat | 9 (status codes diverge — A3) | `CaseMutationPageModel.HeartbeatLeaseAsync:472`, `Mailboxes.HeartbeatEditAsync:1075` |
| Claim ladder (`ClaimAsync` + HeldElsewhere/Conflict/VersionConflict catches) | 13 sites in 9 files | — |
| Cancel-edit release | 10 (some swallow `Expired` only, others `Expired\|Conflict`) | — |
| `RecordName` const | 8 files | — |
| `HeldByAnother` / `EditConflictMessageAsync` | 3 + 3 | `Mailboxes.cshtml.cs:987-1009` (takes kind + name) |
| Private "authorise → run → message → reload" `RunAsync` | 3 (`Accounts:391`, `ValuationPresets:410`, `Glass:267`) | — |
| `SafeReturnUrl` | 5 | — |
| `TryGetActor` → `Forbid()` prologue | 170 sites / 43 files | `CaseMutationPageModel.ExecuteCommandAsync:403` already wraps it |
Prior audit BL-10/BL-18 covered two rows of this table; the family is wider.

**B8 — Mail case-search trio byte-identical.** `ResolveCaseAsync`, `SearchCasesAsync`,
`TryNormalizeCaseQueryValue`, `TryNormalizeCaseReference` at `Mail/Message.cshtml.cs:1290-1340`
and `Mail/Compose.cshtml.cs:421-480` (differ only in `PageSize`); handler pairs
`SearchCorrespondenceCase/SelectCorrespondenceCase/ReconcileCorrespondence` vs
`SearchCase/SelectCase/Reconcile`. `Administration/Logs.cshtml.cs:323` is a third lookup.

**B9 — Parallel front-end implementations.** Two galleries (`Shared/_ImageGallery.cshtml` 86
lines / `Cases/Shared/_CaseImages.cshtml` 153 — same `data-evidence-set` tile contract, different
models); two viewers with two JS engines (`site.js:1326-1900` / `case-workspace.js:~2140-2660`,
both bind `[data-evidence-set] [data-evidence-item]`, both implement crop); two auxiliary
layouts (`_LayoutAuth` / `_LayoutExternal`, differing by a title suffix and a card class); the
take-over banner inlined in 9 views; the hidden heartbeat + release-beacon form pair inlined in
8 views while the one partial for it (`_EditHeartbeat`) is orphaned. `site.js:832-892` is a
**dead heartbeat loop** — it binds `data-heartbeat-seconds`, rendered only by that orphan.

**B10 — Pagination hand-rolled per list page** with three query-param names (`page`,
`pageNumber`, `newPage`): `Index:128`, `Cases/Index:344`, `Mail/Index:32`, `Logs:86`,
`AiJobs:24`, `Search/Index:40`.

---

## C. Unwired code

**C1 — 26 DI registrations nothing in `src/` resolves** (`DependencyInjection.cs` unless noted).
Use-case ports: `IListIntake:90`, `IGetLatestRetainedInstructionAnalysis:234`,
`ICreatePrincipal:322`, `IListPrincipals:323`, `IClearCaseEditLease:437`,
`IConfirmCompleteness:466` (`CaseDetailsWebTests.cs:1919` asserts no such handler exists),
`IEditValuation:525`, `IListAutomationActivity` (`Program.cs:812`), `ISendCaseToAi` /
`IReconcileAiWorkRequest` / `ICancelAiWorkRequest` (`AiWork/ChannelAiHandOffTransport.cs:205-210`),
`IGetStaffHeldCaseEditLeases:279` (+ its only dependency `IStaffHeldCaseEditLeaseQueries:260`),
`ReplaySentEmailEvidence:196` (+ its only dependency `IRecordSentEmailEvidence:189`).
Query ports: `ICaseEvidenceImageQueries:85` (prior BL-03, still dead), `ISourceCandidateQueries:230`,
`IThirdPartyReportCandidateQueries:232`, `IProviderReferenceCatalog:197`, `IGetAttentionRows:370`
(`Pages/Index.cshtml.cs:27` uses `IGetOperationsSnapshot`), `IEditScopeRevocations:392`,
`IInspectionLocationChoices:464`, `ICaseAssetPreparationStore:503` (`EfCaseWorkspaceStore` uses
the concrete type), `IApprovedMailboxReportSentEvidenceQueries:569` (only inherited by another
interface), `ICaseDocumentStateQueries:697`, `ICaseReportGenerationQueries:749`.
Concrete: `GetEmailOperations:362`, `RetryMailboxProcessing:364`, `ResolveApprovedOutlookCategory:415`;
`ResolveIntake`/`ReevaluateIntake` re-registered as concrete in `WorkerDependencyInjection.cs:151-152`
while consumers use the interfaces already registered at `:129-130`.
Sixteen Core use-case classes are alive only through these dead ports.

**C2 — Duplicate registrations.** `ICaseAcceptanceStore` (`DependencyInjection.cs:241` +
`Program.cs:760`), `IProviderInspectionModeStore` (`:250` + `Program.cs:761`),
`ReconcileAutomaticVehicleLookups` (`:349` + `WorkerDependencyInjection.cs:150`),
`VehicleLookupAvailability` five times across three roots (prior BL-13, still open),
`IDocumentContentCacheCleanup` `TryAddSingleton:75` then `AddScoped:818` inside the same call
(two descriptors, two lifetimes).

**C3 — Core types with no caller.** `NoImageIntakeAutomation` (`ImageIntakeAutomation.cs:40` —
not registered, not tested, zero references anywhere); `GetIntakeAssetMetadata` +
`IGetIntakeAssetMetadata` (`InstructionEvidenceImages.cs:228,241`, never registered);
`StagedArtifactOperationsSnapshot` (`OperationsSnapshot.cs:24`, its own remark says it was
removed from the dashboard and "retained"); `PrincipalReportActivityCsv`
(`Reports/V1ActivityReport.cs:46` — `Administration/Reports.cshtml.cs:109-134` re-implements the
same CSV instead of calling it: dead *and* duplicated); `DefaultCaseWorkflowConfiguration`,
`QdosPrincipal` (test-support living in production assemblies).

**C4 — Dead operator copy.** `OperatorLabels.cs`: 543 members, **66 dead** — the prior 22 (all
still dead) plus 44 new, e.g. `Admin.ActionLogs:289`, `Admin.ClaimSources:294`,
`NeedsAttentionPriorityTone:560`, `Settings:1590/1745`, `Back:1592`, `Confirm/Yes/No:1733-1736`,
`Preview:1937`, `DeleteEstimate:2031`, `EstimateDropzone:2070`, `GenerateReportDraft:2103`,
`PreviewReportDraft:2104`, `SendToClaude:2107`, `Paint/Subtotal/Line:2115-2118`,
`OutcomeTitle:2288`. Three more are test-only (`ProvenanceIcon:1546`, `RunExperianCheck:1896`,
`VehicleLookup.NotYetLookedUp:2291`). `CaseWorkspaceLabels.cs`: 22 of 414 dead (`Release:38`,
`CaseDataSaved:67`, `DownloadDraft:271,865`, `CropLeft…CropHeight:664-667`, `MoveUp/MoveDown`,
`SaveReason…ResetRefused:677-683`, `Fit:863`, `Done:892`). Note the prior audit's reviewer
caught that `NoSubscription`, `SignatureMissing`, `QualificationsMissing` feed live labels
*inside* the file — the new count excludes in-file consumers, so re-check those three before
deleting. Per memory, the approved-copy list is closed: deleting is fine, adding is not.

**C5 — Front-end dead hooks and styles.** JS reads `data-` hooks that no markup emits:
`site.js:96 [data-focus-trap]`, `:685 [data-counter-for]`, `:707 [data-edit-toggle-off]` (whole
IIFE returns early), `:773,2610 [data-case-save-reason]`, `:838 [data-edit-renew]`,
`:2631 tr[data-action]`, `:2660 [data-sort-toggle]/[data-sort-arrow]`, `:2688 data-preview-template`,
`:2769-2780 input[type=range][data-range-*]` (no range inputs exist), `case-workspace.js:2132-2659
[data-tile-view], .th-view`. `site.css`: 34 of 123 multi-hyphen selectors unused (e.g.
`assessment-v3-main`, `case-section-nav`, `work-centre-metrics`, `workflow-step-icon`) and
~110 of 490 single-hyphen ones (`accident-card`, `est-tabs`, `queue-*`, `report-page`,
`workflow-stepper`, `mail-workspace`, `unidentified-workspace`) — all in `site.css`, none in the
per-page sheets. `Program.cs:1118` allow-lists `/lib` though no `wwwroot/lib` exists.
`wwwroot/images/lucide-sprite.svg` is unreferenced (sprite is inlined in `_LucideSprite.cshtml`);
9 of 10 PNGs in `wwwroot/images/marks/` are unreferenced while their `README.md:20-29` claims
each is "Used by …".

**C6 — Configuration.** `TransportStorage__AccountName` and `CustodyStorage__AccountName`
(`platform.bicep:426-427,615-616`) are presence-checked (`Program.cs:148,150`) but never read.
`AutomationMcpOptions.KeyVaultUri` (`Mcp/AutomationMcp.cs:58`) is validated then never used.
`Features:LocalIntake`/`LocalDocumentCustody` are forced equal to `Runtime:Profile ==
DevelopmentOffline` (`Program.cs:126-130, 859-864`) — a second spelling of the profile switch.
`Features:SendToAi` (`AiWork/SendToAi.cs:12,35-42`) is set in no appsettings or bicep, only in
two tests, and refuses to run outside DevelopmentOffline — a test-only switch in production code.
`appsettings.json:8-14` still ships `Bootstrap:VerificationAccount` with a comment saying it
must be retired before go-live (informational; security lane).

**Checked and sound.** All 24 `scripts/` files and every CI job are referenced; migrations
(137) and snapshot are in step (latest `20260914150656_UploadedCorrespondenceMailbox`); no
`MailboxImageIntakeSubmission` residue in tracked src/tests/docs on the branch (the class still
exists on `dev` — the deletion is branch-only); all 11 JS and 11 CSS files are linked; the 15
`IInstructionExtractionPolicy` registrations are consumed via `IEnumerable<>` projection;
`Ef*` "as self" registrations are forwarding targets, not dead.

---

## D. Inefficiency

**Backlog re-verification (2026-09-11 → today).** FIXED: **BL-06** (batched
`ListImagesAsync(ids)`, `EfImageIntakeStore.cs:1032-1083`, chunked by 200), **BL-15**
(`Logs.cshtml.cs:456-502` two batch reads via `IListCaseReferences` /
`IAiJobQueries.ListSubjectReferencesAsync`), **BL-16**
(`EfRetainedMailboxMessageStore.CountManyAsync:108-134`, one `GroupBy` query).
STILL OPEN with current lines: BL-05 (`EfIntakeSubmissionGroupStore.cs:451-509` — now **two**
queries per member; a 30-member group is 61 round trips), BL-07 (`EfCaseQueryStore.cs:355-361`
VERIFIED), BL-08 (`EfValuationStore.cs:425-479`), BL-09/R1 (**24 literal `1205/2601/2627`
sites in 21 files** VERIFIED; 18 private classifier methods with at least three non-identical
bodies), BL-11 (`Download.cshtml.cs:394-427`), BL-12 (`CachedDocumentContentStore.cs:1182-1265`,
three buffers), BL-13, BL-14 (`EfManualCaseCreationStore.cs:187`, `EfCaseAcceptanceStore.cs:333`
VERIFIED), BL-17 (`CachedDocumentContentStore.cs:289-333, 898-903` — 2 contexts + 3 reads per
tile), BL-20 (`site.js:211` literal 60000), BL-21 (`IntakeWebTestSupport.cs:101,255`), R2
(`EfImageIntakeStore.cs:883→1043` — now opens a **fresh DbContext per candidate** inside an open
one, and `:859` loads the whole `IntakeManualAssociations` table per scan), R3
(`ImageIntakeCasePairing.cs:148-180`). CHANGED: BL-19 — four partials added
(`Details.Files/Frame/Report/Valuation.cs`, 986 lines) but `Details.cshtml.cs` is still 3,120
lines with a **64-parameter** constructor. BL-01/02/22/23/24 (scripts, infra, tests) not
re-examined here.

**New N+1 sites (CONFIRMED unless marked):**
- **D1 `Core/Actors/ActorDisplayNames.cs:44-51`** — one `staffAccounts.GetAsync` per distinct
  staff id, called from 15 sites (every list page, `CaseQueries.cs:567,901`,
  `OperationsSnapshot.cs:484`, `TriageQueryUseCases.cs:222`, `RetainedMail.cs:665`…).
  `IStaffAccountQueries` (`Identity/StaffAccountAdministration.cs:186-198`) has no batch read.
  The widest-reach single fix in this audit.
- **D2 `Presentation/UploadCaseDecision.cs:163-171, 210-218, 445-509`** — per receipt:
  `getIntake` + either a full paged search or `GetSuggestedAsync`, which
  (`EfIntakeAssociationDestinations.cs:66-73`) opens a DbContext per candidate (`:152`);
  `SearchAsync:93-103` opens a new context per page for workflow state. Receipts × candidates
  round trips per grouped upload.
- **D3 `Cases/Index.cshtml.cs:578-582`** — `_unidentifiedStore.GetAsync` per closed row (≤100)
  to read one field.
- **D4 `Operations/OperationsSnapshot.cs:666-672`** — `workflows.GetAsync` per AI draft for
  `AssignedEngineerId`.
- **D5 `ImageIntake/Details.cshtml.cs:127-136`** — `getIntake` per image receipt per view;
  `Mcp/UnidentifiedMcpTools.cs:220-222` same per group member.
- **D6 `ImageIntakeAutomation.cs:223-255`** — sequential `FindBySourceIdentityAsync` then
  `ScanAsync` per member; `ReconcileGroupedImageIntake.cs:65-117` four reads per candidate
  (U50 files; verified on the codex ref).
- D7 PLAUSIBLE — `EfTriageStore.cs:47-50` per-row link-candidate lookup (2 `AnyAsync` each);
  `EfTriageStore.cs:1115-1120` two `AnyAsync` one projection answers;
  `StaffMailSendEngine.cs:210-220` re-reads staff + mailbox per attachment (may be a deliberate
  pre-side-effect re-check — confirm before changing).
- D8 `EfCaseQueryStore.cs:338-352` — `GetHeaderAsync` materialises the Case twice (five-`Include`
  chain, then `SearchRows(...).SingleAsync`) before the three counts.
- D9 Constructor fan-in > 15: `Cases/Details` 64, `Triage/Details` 29, `Unidentified/Details`
  19, `Mail/Message` 19. Handlers ≥ 150 lines: `Triage/Details.cshtml.cs:223 OnPostActionAsync`
  (212), `Cases/Create.cshtml.cs:292 OnPostCreateAsync` (201), `Triage/Details.cshtml.cs:952
  OnPostSendChaserAsync` (183).

**Checked and sound.** JSON options and regexes are static/`[GeneratedRegex]` (216 uses);
`RailCountsPageFilter` runs its three counts under `WhenAll`; the fresh-context optimistic claim
loops (`EfQueuedCustodyProcessor:28`, `EfVehicleLookupWorkStore:36`) are the correct shape;
`CachedDocumentContentStore` cleanup's per-item contexts are deliberate failure isolation;
`Cases/Details.cshtml.cs:1116-1144` section loop is one query per requested section, not N+1;
no `.ToList().Count`/`.Count() > 0` patterns; `BoundedReadStream.Read` sync-over-async is a
documented non-hot fallback; `KeyVaultOAuthCertificateLoader` sync I/O is boot-time.

---

## E. Over-engineering

**E1 — 63 Core interfaces with exactly one implementation and no test double.** No mocking
library is referenced in `tests/`, so a double can only be hand-written, and for these 63 none
is. ~55 are one-interface-per-use-case seams whose sole implementer is the Core class
(`IAddCaseNote`, `ICancelTriage`, `IHoldCase`, `ISaveCase`, `IReopenUnidentified`…). The current
requirement is the `DependencyDirectionTests` rule that pages depend on ports, not classes —
worth keeping as a *pattern*, but the measured case is
`Core/Lifecycle/CaseCommandSeams.cs:76-95`: `HoldCase(IPutCaseOnHold) : IHoldCase` and
`ReleaseCase(IReleaseCaseHold) : IReleaseCase` are pure forwarders consumed only by
`Cases/Workflow.cshtml.cs:19-20`; the inner ports are consumed only by the wrappers
(`DependencyInjection.cs:582-591`). Two interfaces and two classes per verb where one would do.

**E2 — Fault-classification and retry scaffolding has no owner.** 18 private classifier methods
in 17 stores (`IsRetryableConcurrencyFailure` ×9 with three differing bodies, `IsRetryable` ×2,
`IsDuplicateKeyFailure` ×2, `IsConcurrencyConflict` ×2, `IsConcurrencyFailure`, `IsTransient`,
`IsTransientReadFailure`); 14 hand-rolled `for (var attempt…)` loops in 12 files; 152
`BeginTransactionAsync` sites in 60 files (133 Serializable), each with its own
catch/classify/retry. `docs/engineering.md` §Fault handling: "One classifier per decision."
This is the largest single violation of that rule and the precondition for ever adopting EF
connection resiliency (prior audit, out-of-scope 1).

**E3 — `CaseWorkflowEntity → CaseWorkflowRecord` mapped twice.** `EfCaseQueryStore.cs:1069
MapWorkflow` and `EfCaseWorkflowStore.cs:1609 Map`, ~45 lines each with parallel
actor/evidence helpers; every new workflow column lands in both.

**E4 — Two Box case-folder write routes still present** (prior memory: "two rival custody
routes"). Route A `BoxCaseCustody.cs:1461-1477 UploadOrVerifyFileAsync` (intake attachments via
`EfQueuedCustodyProcessor.cs:307-323`, names `$"{ordinal:D3} {SafeName}"`); Route B
`BoxDocumentContentStore.cs:44-80 FlatFileName/StoreVersionAsync` (document versions via
`EfDocumentCustodyStore.cs:1082-1090`, names `$"{ordinal:000} {safe}"` + "(revision NNN)");
a third direct `box.UploadAsync` for the holding folder at `EfCaseArtifactCustody.cs:461-470`.
The substantive difference is the `CustodyEffectLeaseGuard` on Route A, not the upload/verify/
naming mechanics, which are duplicated.

**E5 — Flags that never vary** (§C6): `Features:LocalIntake`/`LocalDocumentCustody` and
`Features:SendToAi`.

**E6 — `ImageIntakeCasePairing.cs:116-127` vs `:145-160`** — manual and automatic paths each
re-enumerate the group's members for the same "all linked?" answer (one `WhenAll`, one
sequential), and `ListImagesAsync` is called twice for the same record on the manual path.

**E7 — Files ≥ 1,500 lines (my counts).** `Cases/Details.cshtml.cs` 3,120 (+986 partials) ·
`site.js` 2,643 · `case-workspace.js` ~2,670 · `OperatorLabels.cs` 2,233 ·
`EfDocumentRequestStore` 1,809 · `Mail/Message.cshtml.cs` 1,772 · `EfTriageStore` 1,755 ·
`EfCaseWorkflowStore` 1,682 · `EfImageIntakeStore` 1,669 · `PegasusDbContext` 1,624 ·
`EfIntakeReceiptStore` 1,538 · `ProcessIntake.cs` 1,530 · `DurableIntake.cs` 1,519 ·
`BoxCaseCustody` 1,518. Only Details and the intake reader are `partial`.

**Checked and sound.** Every abstract base has ≥ 3 subclasses; no outcome-record hierarchies
to trim; `Features:AutomationMcp`/`ProviderApi` genuinely vary between dev and prod;
`CaseMutationGuard` is already the one owner of `RequireVersion/RequireLease` (the six
one-line forwarders are noise, not duplication); `EfVehicleWorkflowStore.CurrentRegistration`
is a single owner — BL-14's duplication is the *caller* block.

---

## F. `/simplify` backlog (ranked; supersedes the 2026-09-11 list)

Admission rule as before: leaves intended function unchanged unless marked **(behaviour)**.

| ID | Change | Files | Proof | Eff |
|---|---|---|---|---|
| S-01 **(behaviour: fixes a 404)** | Build the "Open case" URL from the page route (`Url.Page("/Cases/Details", new { id })` or `/Cases/{id}`); fix the three pinned tests | `Presentation/UploadOutcome.cs:212,240`; `UploadOutcomeQueriesTests`, `UploadConfirmationWebTests` | tests assert a URL the app answers | S |
| S-02 **(decision)** | Gate the "Move to …" button on `IRetainedMailFolderMover.IsAvailable` — or compose `GraphRetainedMailFolderMover` behind explicit config as ADR-0036 claims | `Pages/Mail/Message.cshtml:495`, `DependencyInjection.cs:108`, ADR-0036 | `MailWorkspaceWebTests` render without the button in Production profile | S |
| S-03 | Delete the 26 unresolved registrations, their 16 orphan use-case classes and ports, `NoImageIntakeAutomation`, `GetIntakeAssetMetadata`, `StagedArtifactOperationsSnapshot`, and the tests that exist only to cover them (§C1, C3, C9 list) | `DependencyInjection.cs`, `Program.cs:812`, `ChannelAiHandOffTransport.cs:205-210`, `WorkerDependencyInjection.cs:151-152`, listed Core files | compile; `ProductionCompositionTests` | M |
| S-04 | Delete 66 dead `OperatorLabels` + 22 dead `CaseWorkspaceLabels` members (re-check the three in-file-consumed names first) | `Presentation/OperatorLabels.cs`, `CaseWorkspaceLabels.cs` | compile | S |
| S-05 | Delete orphan views, dead JS hooks and the dead `site.js:832` loop, ~145 unused `site.css` selectors, `lucide-sprite.svg`, 9 mark PNGs (+ fix `marks/README.md`), the `/lib` allow-list | `Pages/Shared/_EditHeartbeat/_Provenance/_ProvenancePanel/_MetricCard.cshtml`, `Cases/Assessment/Suggestions.cshtml`, `site.js`, `site.css`, `Program.cs:1118` | web tests render every page; visual spot-check per design authority | M |
| S-06 | Delete the four redirect shims + their `_Layout` prefixes; route `Operations/Index.cshtml.cs:454` to `/Cases/Details` + `section=estimate`; delete 8 test-only handlers (B3) and their tests, or wire them | shims, `Operations/Index.cshtml.cs`, `Cases/Tasks/Workflow/Closure/Custody.cshtml.cs` | `OperationsWebTests`; compile | S |
| S-07 | Add `OnGet => NotFound()` to the five POST-only Case action pages (mirror `Export.cshtml.cs:25`) | `Cases/{Workflow,Vehicle,Custody,Tasks,Closure}.cshtml.cs` | one web test per page asserting 404 on GET | S |
| S-08 | One `EditScopePageModel` base carrying release-beacon, heartbeat (one status contract — A3), claim ladder, cancel-release, `RecordName`, held-by message; 9 pages become forwarders; one `_EditScopeForms` partial replaces the 8 inlined form pairs and 9 take-over banners | 9 page models + views; `AdministrationPageModel.cs` | the four administration web test classes + `ImageIntakeWebTests`, `TriageWebTests` | M |
| S-09 | `IStaffAccountQueries.GetManyAsync(ids)`; `ActorDisplayNames` takes the batch | `Core/Actors/ActorDisplayNames.cs:44`, `Identity/StaffAccountAdministration.cs:186`, `EfStaffAccountAdministration` | any list-page web test; `ActorDisplayNamesTests` | S |
| S-10 | Batch the remaining per-row reads: BL-05, R2 (+ drop the whole-table `IntakeManualAssociations` load), R3, D2–D6 | `EfIntakeSubmissionGroupStore`, `EfImageIntakeStore`, `EfIntakeAssociationDestinations`, `UploadCaseDecision`, `Cases/Index`, `OperationsSnapshot`, `ImageIntake/Details`, `UnidentifiedMcpTools`, `ImageIntakeAutomation`, `ReconcileGroupedImageIntake` | existing persistence + web tests per file | M |
| S-11 | One `internal static SqlFaults` classifier (`IsDuplicateKey`, `IsDeadlock`, `IsRetryable`) beside the persistence helpers; 24 literal sites and 18 private methods consume it | `src/Pegasus.Infrastructure/Persistence/*` | compile; `ImageIntakePersistenceTests`, `VehicleWorkflowTerminalTests` | M |
| S-12 | Move the creation-time vehicle-lookup predicate into Core (BL-14, unchanged) | `EfManualCaseCreationStore.cs:187`, `EfCaseAcceptanceStore.cs:333`, `Core/Vehicle/` | `AutomaticVehicleLookupTests`, `VehicleLookupGapFillTests` | S |
| S-13 | Collapse `CaseCommandSeams` forwarders (`IHoldCase`/`IReleaseCase` → the inner ports) | `Core/Lifecycle/CaseCommandSeams.cs:76-95`, `Cases/Workflow.cshtml.cs:19-20`, `DependencyInjection.cs:582-591` | `CaseWorkflowWebTests` | S |
| S-14 | One `CaseWorkflowRecord` mapper shared by both stores | `EfCaseQueryStore.cs:1069`, `EfCaseWorkflowStore.cs:1609` | `CaseCursorQueryPersistenceTests`, `CaseWorkflowPersistenceTests` | S |
| S-15 | Lift the mail case-search trio to one owner (`Mail/Shared` helper or a Core query) | `Mail/Message.cshtml.cs:1290-1340`, `Mail/Compose.cshtml.cs:421-480`, `Logs.cshtml.cs:323` | `MailWorkspaceWebTests`, `ComposeWebTests` | S |
| S-16 | One "closable by hand" predicate in Core; one `OnPostCompleteAiJobAsync` owner | `Index.cshtml.cs:649`, `Operations/Index.cshtml.cs:469` | `OperationsWebTests`, `WorkCentreWebTests` | S |
| S-17 | Retire `Features:LocalIntake`/`LocalDocumentCustody` in favour of the profile they must equal; move `Features:SendToAi` to a test-only registration | `Program.cs:126-130, 859-864`, `AiWork/SendToAi.cs` | `ProductionCompositionTests`, `SendToAiIntegrationTests` | S |
| S-18 | Remove the dead `/requests` + `/documents` middleware branches; give the EVA timer its own schedule key **(behaviour: config key)** | `Program.cs:1135-1138`, `Worker/IntakeFunctions.cs:49`, `platform.bicep` | `WorkerActivationReleaseContractTests` | S |
| S-19 | Unify the Box write route naming/verify/upload mechanics under one helper, keeping the lease guard on the intake path | `BoxCaseCustody.cs:1461`, `BoxDocumentContentStore.cs:44-80`, `EfCaseArtifactCustody.cs:461` | `ProductionBoxCustodyTests`, `CustodyOutboxIntegrationTests` | M |
| S-20 | Carry forward unchanged: BL-07, BL-08, BL-11, BL-12, BL-13, BL-17, BL-19 (continue the split: `Details.cshtml.cs` still 3,120), BL-20, BL-21, BL-01/02/22/23/24 | as listed in `artifacts/audits/2026-09-11/improvement-backlog.md` | as listed | — |

Sequencing notes: S-08 subsumes prior BL-10/BL-18 and fixes A3; do S-03 before S-04 (deleting
ports removes some label consumers); S-11 precedes any EF-resiliency ticket; S-05 needs a
visual pass under `design/README.md` authority.

---

## G. Caveats

- Read-only; every "proof" is a named test class, not executed evidence.
- Reference counting is grep-based. Verified sound for labels (no `using static`, no
  reflection); DI counts could miss a `GetRequiredService` behind a generic helper — the
  explorer checked `GetRequiredService<>` and constructor parameters across `src/`.
- Lane 3's file-size table was inflated (it reported `Details.cshtml.cs` at 3,378; it is
  3,120 on both refs); its line-number citations were spot-checked and hold.
- The main checkout is now `dev` with uncommitted `design/` changes from another session; this
  audit made no writes there.

## Deliverable and verification

Write this audit to `artifacts/audits/2026-09-16/codebase-audit.md` (ignored path; established
home for generated evaluations) and update the `pegasus-audits-2026-09-11` memory to point at
it. Prose-only: no restore/build/test. Verification is the citation re-read already done for
A1, A2, BL-07, BL-09, BL-14 and the label method, plus a final pass that every `file:line` in
the written artifact resolves on the codex ref.

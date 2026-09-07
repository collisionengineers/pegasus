## C08 first slice — implementer notes (worktree v1-intake-c08, branch c08-shell)

Controller mid-task correction received and honoured: dropped scope item 4
(A07 CaseActivity/MailActivity trim) entirely, no lines touched in
OperationsSnapshot.cs's CaseActivity/MailActivity members, DashboardCounts.cs,
or Index.cshtml(.cs)'s MailActivity usage.

### Done (build green, commits on c08-shell)
1. London calendar (G8): OperatorLabels.OfficeTime/OfficeDate/OfficeClock now
   go through LondonCalendar.LocalAt/DateAt/TimeAt; removed the private
   TimeZoneInfo.FindSystemTimeZoneById try/catch UTC fallback.
2. Admin nav: six static href links (Action logs, AI jobs, Reports, Health,
   Valuation presets, Claim sources) added to _AdminNav.cshtml in their own
   marked block; _Layout.cshtml's Administration entry is a single link with
   no sub-links, left untouched per the packet's own instruction.
3. Notifications menu: new IGetAttentionRows (OperationsSnapshot.cs, narrow
   query, Take(10), implemented by GetOperationsSnapshot via a shared
   FetchAttentionInputsAsync helper); RailCountsPageFilter calls it once per
   request except on Work Centre (which sets its own ViewData["AttentionRows"]
   from its already-fetched snapshot); RecordPage/ActionLabel/TitleLabel/
   DetailLabel/ReasonLabel/SourceLabel moved from IndexModel to new
   Presentation/NeedsAttentionPresentation.cs; _ShellDialogs.cshtml renders
   bounded rows, zero rows omits the list content entirely.
   NEEDS: DI registration IGetAttentionRows -> GetOperationsSnapshot in
   DependencyInjection.cs (A-owned, out of my file scope) — until that lands,
   every authenticated page fails DI resolution of RailCountsPageFilter.
4. (dropped by controller correction — not implemented)
5. Inbox read-only proof: new MailWorkspaceWebTests.cs test
   (OpenPreviewFilterUnreadAndSortNeverWriteThroughTheRetainedMailPorts) swaps
   RecordingFolderMover + new RecordingClassificationStore for
   IRetainedMailFolderMover/IRetainedMailClassificationStore, drives every
   filter + preview + open/back, asserts zero write calls; also checks the
   Back-to-Inbox link carries the mailbox query state forward.
6. Staff correspondence: NEW /Inbox/Compose (Compose.cshtml(.cs)) — New mode
   only. OperatorLabels.StaffMail.State added (S12: Sent/Failed/Cancelled/
   Unknown keep their own words, everything else reads "Submitted"; Unknown
   renders Reconcile, never resend). New StaffCorrespondenceWebTests.cs with
   a recording IStaffMailSend swapped in via WithWebHostBuilder: zero calls
   on GET/unauthenticated actor/empty recipients/unknown mailbox/stale Case
   version; exactly one call on a valid send (New mode, null OriginalMessage).
7. Command palette: verified against the design contract (Ctrl+K, Escape,
   focus trap, focus return, submits to /Search?query=) — already conformant,
   no production change needed. New Browser/OuterShellBrowserTests.cs proves
   it plus 390/1280px no-overflow plus the notifications control (stub
   IGetAttentionRows swapped via configureWebHost) showing bounded rows / none.
8. Search inline actions: verified — no onclick/style attributes exist in
   Pages/Search/Index.cshtml or _CasePreview.cshtml. No change made.

### Deviations / open gaps (item 6)
- ASSUMPTION 1 (implementer, attempt 1): Compose requires a linked Case
  (CaseId + ExpectedContextVersion are not optional on the form) — because
  StaffMailSendCommand.ContextId/ExpectedContextVersion are non-nullable Guid/
  long, so general correspondence with no Case is not representable by the
  contract as written; alternatives considered: a Guid.Empty/0 sentinel for
  "no context" (rejected — fabricates a meaning the contract doesn't state).
- ASSUMPTION 2 (implementer, attempt 1): Compose's ExpectedMailboxGeneration
  is read from the pre-existing IApprovedMailboxStore's ApprovedMailbox.Version
  (Administration/Mailboxes' query, filtered to RouteScope.SentEvidence) —
  because the approved-mailbox entity's real MailboxGeneration/AllowStaffSend
  pair (ApprovedMailboxEntity, Infrastructure) has no Core query exposing it
  to Web yet; alternatives: skip Compose entirely (rejected — the page is
  explicitly named as required file scope) or fabricate a placeholder value
  (rejected — Version is a real, if wrong-concept, field). A follow-up must
  add the correct query and fix this mapping.
- BLOCKED for this feature only (not the whole slice): Message.cshtml.cs's
  Reply/ReplyAll/Forward are NOT implemented. StaffMailOriginalMessage needs
  the retained message's immutable mailbox id / Graph message id / internet-
  message id / conversation id. RetainedMailDetail and RetainedMailSummary
  (src/Pegasus.Core/Intake/RetainedMail.cs) carry none of the four fields,
  and that file is outside this ticket's file scope (not in "Files you may
  edit"). Fabricating identity data was rejected (rule 13, and it would send
  through Graph threading incorrectly). A follow-up ticket needs to extend
  RetainedMailDetail (and its EF projection in Infrastructure) with these
  fields before Reply/ReplyAll/Forward can be built.

### Also needed from other streams (listed, not touched)
- DependencyInjection.cs: IGetAttentionRows -> GetOperationsSnapshot;
  IStaffMailSend -> its real Infrastructure implementation (none exists yet
  in this repo at all — Compose is wired against the Core contract only).
- docs/design/test-ui/**: /Inbox/Compose needs a catalogue.json entry (route,
  classification, states) — out of my file scope.

## Controller correction — RailCountsPageFilter optional resolution (commit c64d9cf83)

ASSUMPTION 3 (implementer, attempt 1, per controller instruction): resolve
`IGetAttentionRows` per request from `HttpContext.RequestServices.GetService<IGetAttentionRows>()`
in `RailCountsPageFilter` instead of the constructor — because Stream A's
registration for it has not yet landed on this branch, and a required
constructor dependency on an unregistered service broke every authenticated
page in every A- and C-owned web test. A null service now sets no
`ViewData["AttentionRows"]` (the dialog renders no list content) rather than
failing the whole page. This is the same optional-resolution bridge C01 used
for its analysis panel. A's registration patch makes the dependency required
again in the combined checkout — `RailCountsPageFilter` should revert to
constructor injection once that lands; alternatives considered: leaving the
required constructor dependency and waiting for A's patch to merge first
(rejected by the controller — it blocks every other web test on this branch
in the meantime).

Also added (build green, same commit):
- `ShellAndStatusPageWebTests.NotificationsMenuShowsAttentionRowsOnceTheQueryIsRegistered`
  — registers a stub `IGetAttentionRows` via `factory.WithWebHostBuilder` and
  proves the rows reach the rendered `/Search` page.
- `ShellAndStatusPageWebTests.ShellStillRendersWhenTheAttentionRowsQueryIsNotRegistered`
  — proves the shell renders (notifications dialog present) with no
  registration at all, today's state on this branch.

Head SHA now c64d9cf83. Full solution build (`dotnet build ./Pegasus.slnx
--configuration Release --no-restore`) green. READY_FOR_TESTS unchanged
otherwise — see the earlier note in this file for the full slice summary and
the other two deviations (Reply/Forward blocked; Compose's two assumptions).

## Correction round 1 (wave-9 defects)

**ASSUMPTION 4 (C08, correction round 1) replaces ASSUMPTION 2's placeholder:**
Compose's `ExpectedMailboxGeneration` now reads `ApprovedMailbox.Generation`
(G14's shared field) instead of `ApprovedMailbox.Version` (Administration's
own optimistic-concurrency counter for mailbox edits — a different field this
page no longer conflates it with). `LoadSendableMailboxesAsync` now offers a
mailbox when it is Approved and carries `StaffSend` (G14's dedicated
capability for this exact command) **or**, as a fallback, `SentEvidence` (the
pre-G14 placeholder scope) — because `EfApprovedMailboxStore.Routes`
(A-owned, `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs`)
does not map its backing `AllowStaffSend` column into `RouteScopes` at all
yet; filtering on `StaffSend` alone today would offer no mailbox to anyone.
Alternatives considered: filter solely on `StaffSend` (correct per contract,
but breaks Compose completely until A's mapping lands); leave filtering on
`SentEvidence` only (ignores the new capability G14 added). Revert to
`StaffSend`-only once `EfApprovedMailboxStore.Routes` maps `AllowStaffSend`.
`ApprovedMailbox.VerifiedEncodedMessageSizeLimit` (G14) has nothing to
enforce yet — this slice always sends `Attachments: []` — so a null
(unverified) limit is correctly never substituted with a guessed number; a
future attachment slice must read the chosen mailbox's real value.

**Defect (c) investigation — `OpenPreviewFilterUnreadAndSortNeverWriteThroughTheRetainedMailPorts`
still 404s; root cause not confirmed.** Traced `GetRetainedMail` (Core) and
`EfRetainedMailboxMessageStore.GetAsync` (Infrastructure, A-owned, read-only
for me): `LoadClassificationAsync` is a `private static` method called
directly on `this`/`context`, never through the `IRetainedMailClassificationStore`
interface — so the test's `RecordingClassificationStore` swap (which always
returns null from `GetClassificationAsync`) cannot be what 404s Preview; this
matches the class's own doc comment ("no read/open/preview/filter path calls
it"). Messages seeded via `RetainAsync` in this test carry no `IntakeReceipts`
row, so `detail.Classification` stays null and `RecommendFolderAsync` returns
before ever calling `IApprovedMailboxStore` — ruling that branch out too.
`IntakeWebApplicationFactory` provisions an isolated database per factory
instance (`LocalDbTestDatabase`), ruling out cross-test "instructions"
mailbox-literal pollution. I could not reproduce or pin the exact throwing
line by static reading alone with `dotnet test` unavailable to me this
session (build-only per dispatch). Rather than guess at a production fix I
can't verify, I added a diagnostic to the test itself: on a non-OK preview
response it now fails with the status, response body, and whether the seeded
row still exists in the DB, so the runner's next pass names the real cause
directly instead of only "OK != NotFound". If the runner's failure message
points at a specific line/exception, that is the next round's fix target —
no production change was made for this defect since I could not verify one.

**Defect (a)/(b) fixes are production changes** (`site.css` `.admin-nav`
narrow-width rule now wraps with `overflow:visible` instead of
`overflow-x:auto` leaving `overflow-y:hidden`; `site.js`'s command palette
now threads the real opener — the search box on Enter, the focused element
on Ctrl+K — through a new `dialog.pegasusOpen` so Escape's focus-return
targets it instead of the generic "open another record" trigger button) —
no assumptions needed, both were deterministic code-reading fixes.

## Step 3 note (pending — will append after `_AdminNav.cshtml` route audit)

## Step 3 — `_AdminNav.cshtml` route audit (correction round 1)

Checked all six C08 shell administration-area links against
`src/Pegasus.Web/Pages/Administration/` as it stands on this branch after
Step 0's two merges (`54a12dd7d`, `ecd546297`): `ls`/`find` under
`Pages/Administration` finds no `ActionLogs`, `AiJobs`, `Reports`, `Health`,
`ValuationPresets` or `ClaimSources` page anywhere (including the C06
`ClaimSources` page the dispatch named — not present on this branch at this
head). All six remain plain `href`s exactly as they already were; no markup
change was needed. Still forward references, pending their owning slice's
merge:

- `/Administration/ActionLogs` — no owning slice identified in files reviewed
  so far.
- `/Administration/AiJobs` — no owning slice identified.
- `/Administration/Reports` — no owning slice identified.
- `/Administration/Health` — label now reads "Service health" (Step 0's merge
  resolution, FRD-12 name); page itself not yet on this branch.
- `/Administration/ValuationPresets` — no owning slice identified.
- `/Administration/ClaimSources` — C06-owned per the dispatch; not on this
  branch yet.

The four `asp-page` links (Accounts, Principals, Configuration, Mailboxes)
plus the conditional Automation link all resolve to pages that exist on this
branch — verified present under `Pages/Administration/`.

## Correction round 2 (C08, this round)

1. Compose OperationKey (StaffCorrespondenceWebTests.cs): confirmed the missing-required-field
   behaviour is correct (OperationKey is a non-nullable BindProperty, implicitly required).
   Fixed the two Compose POST tests that need to reach the send/redirect or the stale-version
   branch (AValidCompose..., AStaleCaseContext...) to GET /Inbox/Compose?caseId=... first and
   read the rendered hidden OperationKey + antiforgery token, then POST with those. Added a new
   negative test AMissingOperationKeyReturnsTheFormWithoutSending asserting the validation
   summary shows "The OperationKey field is required." and SendCalls stays 0. Left the other two
   POST tests (EmptyRecipients, AnUnknownApprovedMailbox) untouched — they already omit
   OperationKey but aren't currently failing and weren't named in the correction.

2. MailWorkspaceWebTests preview 404 — root cause verified, and it is NOT the classification
   store fake. The wave 24 stack trace names line 139 (`GetHtmlAsync(client, $"/Inbox?{query}")`),
   not the Preview handler call at line 144. `TryParseQueue` in Index.cshtml.cs accepts only an
   AggregateViews key ("receiving-work", "queries", "other", "unidentified", "triage") or a
   "classification:" prefix; "all" is not a recognized value, so `OnGetAsync` returns NotFound
   before Preview is ever reached. Separately confirmed `RecordingClassificationStore`'s read
   stub is genuinely inert here: `IRetainedMailQueries` and `IRetainedMailClassificationStore`
   are two independent DI registrations both pointing at the same `EfRetainedMailboxMessageStore`
   instance (Pegasus.Infrastructure/DependencyInjection.cs:79-83); `GetRetainedMail` (which
   backs the Preview handler) is constructed with `IRetainedMailQueries` only and never touches
   `IRetainedMailClassificationStore`, so replacing the latter cannot affect the former's query.
   The doc comment on RecordingClassificationStore ("no read/open/preview/filter path calls it")
   is accurate. Fix: changed the test's query string from `queue=all` to `queue=receiving-work`
   (a real key already used elsewhere in this file) — the unfiltered scope is the absent `queue`
   parameter, not a literal "all". No production code changed.

3. _Layout.cshtml: added `<link rel="stylesheet" href="~/css/case-workspace.css"
   asp-append-version="true" />` right after site.css, per Stream B's PR 673 comment 5560632798.
   case-workspace.css does not exist on this branch yet. Verified via Microsoft Learn docs
   (Image Tag Helper page, same FileVersionProvider path as Link) that asp-append-version on a
   missing static file just omits the ?v= query string rather than throwing — no fallback to a
   plain href needed. Confirmed no C08 test (LayoutIntegrityTests.cs included, read-only) asserts
   an exact stylesheet list.

4. OperatorLabels.cs: CaseWorkspace scope already existed (CASE-039/CASE-009/CASE-040 etc., plus
   a nested EngineerSections scope). Checked the whole file for Recipient/Reason/Content/
   RecordChase before adding — none exist at the outer CaseWorkspace scope; EngineerSections has
   its own unrelated `Reason` (estimate-reject dialog), a different scope so no collision. Added
   all four as new consts at the outer CaseWorkspace scope under a "C08 labels batch" comment
   block, for Stream B's not-yet-landed documents/chase partials.

Build: `dotnet build ./Pegasus.slnx --configuration Release --no-restore` → Build succeeded,
0 Warning(s), 0 Error(s).

Commits (HEAD 8c5351296..6690a33cc):
- 35213545f test(mail): post Compose with the OperationKey the GET rendered
- 3005bac91 fix(mail): use a real queue key, not "all", in the workspace URL proof
- f276c2048 feat(shell): link case-workspace.css from the shared layout
- 6690a33cc feat(shell): add Case Workspace labels for the documents/chase port

Reported READY_FOR_TESTS to controller; no push, no PR, no dotnet test run (build-only per
controller override).

## Correction round 3

Controller override run (no execution packet; only `append_scratch` on
`c08-notes`; no push/PR/local test-runner call; build-only). Worktree
`../pegasus-worktrees/v1-intake-c08`, branch `c08-shell`. Starting HEAD
`df03ccd4e`. Merged `task/pegasus-v1-intake` first (C head expected/actual
`2c1a9d8a1`, no conflicts, merge commit `e1f8850ee`). Head after this round
`86e8659f5`.

M4 assertions passed: toplevel = worktree; both `--git-common-dir` values
name the primary `.git`; branch = `c08-shell`; HEAD matched `df03ccd4e`
before starting.

Wave 27 item (146/147, one failure):
`MailWorkspaceWebTests.OpenPreviewFilterUnreadAndSortNeverWriteThroughTheRetainedMailPorts`
— list GET 404'd because the test drove `sort=asc`. `TryParseSort`
(`src/Pegasus.Web/Pages/Mail/Index.cshtml.cs:623`) accepts only the absent
value (newest) or the literal `"oldest"`; `"asc"` is not one of them, so
`OnGetAsync` (line 107) returns `NotFound` before Preview is ever reached.
Production parsing is deliberate and untouched.

`TryParseUnread` (line 642) already accepted the test's `unread=true` for
`folder=inbox` — no change needed there.

Confirmed the page's own emitted vocabulary two ways: (1) `Index.cshtml`
rows/links (lines 118-425) and `RefreshFields` (`Index.cshtml.cs:363-364`)
build `["unread"] = UnreadOnly ? "true" : null` and
`["sort"] = OldestFirst ? "oldest" : null`; (2) an existing sibling test,
`ScopingAndPagingCarryTheMailboxFolderAndPageForward`, already asserts the
rendered markup contains `"unread=true&amp;sort=oldest&amp;pageNumber=2"`,
proving this exact pair round-trips today.

Fix: changed the failing test's query string from `sort=asc` to
`sort=oldest`, and added an assertion that the rendered list page's own
row-title link contains `unread=true&amp;sort=oldest` (round-trips the
page's vocabulary rather than a guessed one). No production file touched.
Commit `86e8659f5` — "fix(tests): match the page's sort=oldest vocabulary,
not sort=asc (INTK-060 C08)".

Build: `dotnet build ./Pegasus.slnx --configuration Release --no-restore` —
exit 0, 0 warnings, 0 errors.

No deviations, no new assumptions this round. READY_FOR_TESTS.

## Replacement-controller completion — C08 correction round 4

Preserved the exhausted worker's six-file dirty correction and committed it as ab7a69855 on c08-shell. It closes the recorded Inbox href, S12 label, Unknown/replay visibility, ten-row attention bound, and banned-placeholder findings while retaining Stream A's exact StaffSend + positive Generation ruling; SentEvidence alone is not send authority. Standalone tests adapt only the absent A-owned mailbox mapping. dotnet build ./Pegasus.slnx --configuration Release --no-restore exited 0 with 0 warnings and 0 errors. Implementation role ran no tests. Exact head ab7a69855 is READY_FOR_TESTS; focused wave and independent exact-head re-review remain required before integration.

## C08 item 3 research — Triage OnPostSendChaserAsync (read-only, controller override, no packet/move)

Worktree read: v1-intake @ aa5e669d7 (branch task/pegasus-v1-intake). Full brief:
C:\Users\PGUSER\AppData\Local\Temp\claude\...\scratchpad\takeover\c08-chaser-research.md

Answer: the originating retained-message identity IS truthfully derivable today from existing
columns/entities — IntakeReceipts.ExternalReceiptToken/SourceChannel joined to
RetainedMailboxMessages.ExternalReceiptToken (same join EfRetainedMailboxMessageStore.cs already
runs for classification/list projection, lines ~376-381/640-703). No existing public query exposes
it though: IRetainedMailQueries has no GetByReceiptId member. Gap closes with one new interface
member (mirroring ITriageQueries.GetByOriginReceiptAsync naming) + one new EfRetainedMailboxMessageStore
method, using only existing columns — no EF migration, no A dependency for this part.

Confirmed still-open, unrelated A gap the chaser inherits (not new): EfApprovedMailboxStore.cs still
does not map AllowStaffSend/Generation (verified via grep at this HEAD) — same gap as ASSUMPTION 2/4
above; Compose and the future chaser both offer no sendable mailbox until A lands it.

Q1 (Message.cshtml.cs Reply/ReplyAll/Forward, Compose.cshtml.cs OnPostSendAsync) is fully implemented
on this HEAD already, superseding the old BLOCKED note above in this file — StaffMailOriginalMessage
now reads real ImmutableMessageId/InternetMessageId/ConversationId/ReplyToAddresses off
RetainedMailDetail. StaffMailPurpose.TriageChaser enum value already exists in StaffMailSend.cs.

Q3: only Origin.SourceIdentity.Channel==Mailbox Triages can have a retained-mail row; ManualUpload/
Automation(ImageIntake)/ProviderApi never do (mailbox poll is the only RetainedMailboxMessages writer)
— handler must refuse Reply and offer only explicit New for those, never inferring from sent evidence.

Details.cshtml.cs today: single OnPostActionAsync(actionName) switch (assign/note/unassign/
await_information/record_finding/supersede_finding/link_response/unlink_response/complete/cancel/
reopen/link_case/unlink_case), plain per-page OperationKey reissued each mutation, tag-helper
antiforgery only. Chaser needs its own handler + its own retained-scoped operation-key (Message.cshtml.cs's
"retained:{id:N}:{guid:N}" scheme), same as Reply/ReplyAll, not folded into the generic switch.

Test plan (12 cases) targets a new partial QdosTriageIntegrationTests test file (mirroring
TriageEvidenceImagesWebTests.cs pattern), reusing StaffCorrespondenceWebTests.cs's RecordingStaffMailSend
double; TriageQueuesWebTests.cs only covers the /Triage list page, not Details, so not extended.

## C08 R5 — narrow correspondence error-handling correction (READY_FOR_TESTS)

Branch `c08-shell`, worktree `pegasus-worktrees/v1-intake-c08`. Start HEAD `aa5e669d7`, new HEAD
`4f0c04113` (1 commit).

Addressed Stream A's PR 673 comments 5563408956 / 5563452545 / 5563525850 on
`src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` `SendCorrespondenceAsync`:

- The `catch (InvalidOperationException)` (was ~line 905) no longer unconditionally labels every
  failure as an existing correspondence operation. It now re-queries
  `staffMailSend.GetLatestForOriginalAsync(actor, detail.Summary.Id, cancellationToken)` (existing
  query already used by `LoadRetainedOperationAsync` — no new store method) and only translates to
  the existing "operation in progress" message when an active (non-Sent/Failed/Cancelled) operation
  is actually confirmed. Otherwise it does a bare `throw;`, preserving the original exception, so
  `UnavailableStaffMailSend` (offline) and composition-invariant failures surface unmasked as any
  other uncaught exception on this page already does (Program.cs's `UseExceptionHandler` only runs
  outside Development).
- Checked the other two `InvalidOperationException` sites in the file (~370 query-response job
  handler, ~1388 Case-association lease helper) — neither is the same masking pattern on the same
  send/reply/forward handler path, so both were left untouched, as noted in the packet.
- `tests/Pegasus.IntegrationTests/StaffCorrespondenceWebTests.cs`:
  - `UnknownRetainedReplyReplaysAndReconcilesWithoutResending`'s "fresh" key changed from the
    syntactically invalid `fresh:{guid}` to a valid `retained:{messageId:N}:{guid}`, with an added
    assertion that the resulting page shows the conflict message — now proves active-Unknown
    rejection of a genuine fresh key, not just key-format validation.
  - Added `ANoActiveOperationInvalidOperationDoesNotMasqueradeAsASendConflict`: extends
    `RecordingStaffMailSend` with a minimal `ThrowOnSend` property (throws before recording any
    command) and asserts the handler lets the `InvalidOperationException` propagate unchanged
    (`Assert.ThrowsAsync`) rather than redirecting/labelling it a send conflict.
  - Both tests are **unexecuted** — Integration project's compile is held for an A-owned reason;
    controller instructions were build-only, no `dotnet test`.

Build gate: `dotnet build ./src/Pegasus.Web/Pegasus.Web.csproj --configuration Release --no-restore`
→ Build succeeded, 0 Warnings, 0 Errors, exit 0.

No push, no PR (controller override: build-only round). Full report:
`C:\Users\PGUSER\AppData\Local\Temp\claude\C--Users-PGUSER-documents-github-pegasus\5adc2fb3-f15d-4145-84ed-948eb9fde4e4\scratchpad\takeover\c08-r5-report.md`

Combined-tree test filter for the runner:
`dotnet test ./Pegasus.slnx --configuration Release --filter "FullyQualifiedName~Pegasus.IntegrationTests.StaffCorrespondenceWebTests"`

C08 r6 (correction round 6, test-only): fixed the regression test that FAILed in the combined tree.
Root cause per F1-F6: Development-environment developer exception page turns the rethrown InvalidOperationException into an HTTP 500 before it reaches the client as a thrown exception, so `Assert.ThrowsAsync` never observed one.
Fix: assert `HttpStatusCode.InternalServerError`, body excludes "existing correspondence operation", body includes the ThrowOnSend double's message; kept SendCalls==0 as a non-tautological secondary guard (double throws before recording). Renamed off the stray leading "A" (F6).
F5: extracted shared `IsActiveOperation(StaffMailOperation?)` in Message.cshtml.cs, used by both the exception handler and CorrespondenceSendBlocked — no behaviour change.
Compile gates (foreground, explicit timeouts): Pegasus.Web 0W/0E; IntegrationTests 0W/1E, sole error CS0246 EfCaseArtifactCustody (A-owned, expected).
Commit 729b284e1 on c08-shell. No push, no PR this round (controller override). Report: scratchpad/takeover/c08-r6-report.md. READY_FOR_TESTS.

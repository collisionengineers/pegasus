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

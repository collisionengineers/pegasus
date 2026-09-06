---
kind: review-attestation
slice: "INTK-060 C08 — v3 Inbox, Search, Work Centre, shell and shared Web assets"
pr: null
head_sha: "86e8659f57a920c1c7af1c3c37313f1739a6f037"
verdict: needs-changes
reviewer: "pegasus-reviewer (C08, wave 31)"
independent: true
implementer_head_reported: "86e8659f5"
plan_hash: "pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md @ 0840c33a5..86e8659f5 worktree copy"
ticket_updated: "INTK-060 scratch/c08-notes version c145621cbe467657"
worktree: "C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c08"
branch: c08-shell
ownership: PASS
lanes:
  - {lane: "1-build", result: PASS, summary: "0 Warning(s), 0 Error(s)"}
  - {lane: "2-core", result: PASS, summary: "59/59"}
  - {lane: "3-integration", result: FAIL, summary: "159/160 — MailWorkspaceWebTests.OpenPreviewFilterUnreadAndSortNeverWriteThroughTheRetainedMailPorts"}
  - {lane: "4-browser", result: PASS, summary: "82/82"}
  - {lane: "5-architecture", result: PASS, summary: "100/100"}
findings:
  - {id: C08-R-1, severity: blocker, disposition: open}
  - {id: C08-R-2, severity: major, disposition: open}
  - {id: C08-R-3, severity: major, disposition: open}
  - {id: C08-R-4, severity: major, disposition: open}
  - {id: C08-R-5, severity: major, disposition: open}
  - {id: C08-R-6, severity: major, disposition: open}
  - {id: C08-R-7, severity: minor, disposition: open}
  - {id: C08-R-8, severity: minor, disposition: open}
  - {id: C08-R-9, severity: minor, disposition: open}
  - {id: C08-R-10, severity: minor, disposition: open}
  - {id: C08-R-11, severity: minor, disposition: open}
  - {id: C08-R-12, severity: note, disposition: deferred-to-stream-A}
  - {id: C08-R-13, severity: note, disposition: accepted-risk}
---

# C08 review — head 86e8659f5

Verdict **needs-changes**: one lane red and six blocker/major findings. The
slice's own commits are the 17 first-parent non-merge commits in
`0840c33a5..86e8659f5`; merges from `task/pegasus-v1-intake` were read as
shared-branch context, not reviewed as C08 work.

## What the slice delivers, checked against the authority

| Review item | Result |
| --- | --- |
| 1 ownership and frozen contracts | PASS — see Ownership below |
| 2 `OperatorLabels` through `LondonCalendar`, UTC fallback removed | PASS |
| 3 admin nav: six static links, wraps ≤980px, labels one owner | PASS with C08-R-10 |
| 4 notifications: narrow read, `Take(10)`, one shared fetch, bounded rows, bridge honesty | PASS with C08-R-5, C08-R-7, C08-R-8 |
| 5 Inbox read-only proof | **FAIL** — C08-R-1, C08-R-2 |
| 6 `/Inbox/Compose` New mode, `Generation`, `StaffSend`/`SentEvidence`, size limit, OperationKey, ASSUMPTION 1/2 | PASS on the contract reads; **FAIL** on the ambiguous outcome — C08-R-3, C08-R-4, C08-R-9, C08-R-11, C08-R-12 |
| 7 command palette JS | PASS on the contract; C08-R-6 on re-entry |
| 8 Search inline actions | PASS — no `onclick`/`style=` in `Pages/Search/Index.cshtml` or `_CasePreview.cshtml` |
| 9 browser proofs real | PASS — `OuterShellBrowserTests` drives the real page through Playwright; lane 4 82/82 |
| 10 `_Layout.cshtml` stylesheet link for B | PASS |
| 11 one owner per rule, one doc comment per member, no dead code, no inline styles | PASS on owners/dead code/inline styles; C08-R-9 on field copy |

Item 2 verified end to end: `OfficeTime`/`OfficeDate`/`OfficeClock` now call
`LondonCalendar.LocalAt`/`DateAt`/`TimeAt`, the private `InOffice`
try/catch-to-UTC is gone, `LondonCalendar.GetTimeZone()` calls
`FindSystemTimeZoneById` unguarded (fails closed), and no second
`FindSystemTimeZoneById` or `TimeZoneInfo.ConvertTime` survives anywhere in
`src/Pegasus.Web`. One clock.

Item 4's shared fetch is real: `FetchAttentionInputsAsync`
(`OperationsSnapshot.cs`) is written once and called by both the full
snapshot and `IGetAttentionRows`, `ComposeNeedsAttentionAsync` resolves staff
names in one bulk call and then loops in memory (no N+1), Work Centre sets
`ViewData["AttentionRows"]` from its own snapshot and `RailCountsPageFilter`
skips that page for exactly that reason, and zero rows omits the
`_ShellDialogs` list content with no fabricated "No notifications" item.

ASSUMPTION 1 (Compose requires a Case) is **sound**:
`StaffMailSendCommand.ContextId`/`ExpectedContextVersion` are non-nullable
`Guid`/`long`, and the rejected `Guid.Empty` sentinel would have invented a
meaning the contract does not carry. ASSUMPTION 2 is properly **replaced** by
ASSUMPTION 4 — the page reads `ApprovedMailbox.Generation`, not `Version`.
The `StaffSend`-or-`SentEvidence` fallback is **justified and load-bearing**:
`EfApprovedMailboxStore.Routes` (line 407) maps only `InboundIntake` and
`SentEvidence`; `AllowStaffSend` exists on the entity and is never mapped, so
`StaffSend` alone would offer no mailbox to anyone.
`VerifiedEncodedMessageSizeLimit` is correctly not enforced and never guessed
— this slice always sends `Attachments: []` and the doc comment says a null
limit means unverified.

## Ownership — PASS

The 17 slice commits touch exactly 18 files, all C-owned or C-proposed:

`src/Pegasus.Core/Operations/OperationsSnapshot.cs`,
`Pages/Administration/Shared/_AdminNav.cshtml`, `Pages/Index.cshtml(.cs)`,
`Pages/Mail/Compose.cshtml(.cs)`, `Pages/Shared/_Layout.cshtml`,
`Pages/Shared/_ShellDialogs.cshtml`, `Presentation/NeedsAttentionPresentation.cs`,
`Presentation/OperatorLabels.cs`, `Presentation/RailCountsPageFilter.cs`,
`wwwroot/css/site.css`, `wwwroot/js/site.js`,
`tests/.../Browser/OuterShellBrowserTests.cs`, `MailWorkspaceWebTests.cs`,
`ShellAndStatusPageWebTests.cs`, `StaffCorrespondenceWebTests.cs`,
`WorkCentreLabelTests.cs`.

Untouched, as required: A-owned `DependencyInjection.cs`,
`Browser/LayoutIntegrityTests.cs`, `OperationsWebTests.cs`,
`docs/design/test-ui/*`; B-owned `Pages/Cases/Shared/*`. Nothing under
`.worktrees/kanmer`; no push, no merge, no code edit by this review.

Two files sit outside the plan's "### C08 files" lists —
`Presentation/RailCountsPageFilter.cs` (named C-owned by
`docs/design/README.md:858`) and the new
`Presentation/NeedsAttentionPresentation.cs` (C-owned area, but not in
"Proposed C files"). Recorded as C08-R-13, not an ownership failure.

## Findings

### C08-R-1 — blocker — the Inbox read-only proof fails

`tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs:156`

Wave 31 lane 3: `Assert.Contains("unread=true&amp;sort=oldest", listHtml)`
fails (159/160). The assertion added in correction round 3 asserts a
hand-built ordered substring the page never emits under this test's query:

- `search=vehicle` can only match through `IntakeReceipts.BodySearchText`
  (`EfRetainedMailboxMessageStore.cs:246`), and `SeedAsync` creates no
  `IntakeReceipts` row — the implementer's own round-1 note says so — so the
  filtered list renders **zero rows**;
- the three links that emit `unread=true` and `sort=oldest` adjacently are the
  row link (`Pages/Mail/Index.cshtml:246`) and Previous/Next
  (`:327`, `:339`), none of which renders with zero rows on one page;
- the sort **toggle** (`Index.cshtml:120`) deliberately emits the opposite
  value (`Model.OldestFirst ? null : "oldest"`).

The sibling test cited in round 3
(`ScopingAndPagingCarryTheMailboxFolderAndPageForward:823`) passes only
because it seeds 30 rows with no `search`/`queue` filter, i.e. rows and two
pages exist. The inference was drawn from a different query.

**Fix:** assert what the page emits for this query rather than a composed
string. Either (a) assert the list rendered at least one row (e.g. a
`data-mail-preview-trigger` anchor) and read `unread=true`/`sort=oldest` off
that row's own `href`, or (b) drop `search=vehicle` from the proof query (or
seed a matching `BodySearchText`) so rows exist, keeping every other filter
dimension. Production parsing needs no change.

### C08-R-2 — major — the Back-to-Inbox assertion looks for an attribute Razor strips

`tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs:182`

`Between(messageHtml, "<a class=\"btn\" asp-page", "Back to Inbox")` searches
the **rendered** HTML for the `asp-page` tag-helper attribute.
`Pages/Mail/Message.cshtml:19` writes `<a class="btn" asp-page="/Mail/Index"
asp-route-…>`, which the anchor tag helper renders as
`<a class="btn" href="/Inbox?…">` — `asp-page` never reaches the response, so
`Between`'s own `Assert.True(from >= 0)` will fail. It is latent only because
C08-R-1 fails four lines earlier; fixing R-1 exposes it immediately. The
following `Assert.Contains("href=\"/Inbox?mailbox={id}")` additionally depends
on `mailbox` being the first generated query parameter.

**Fix:** match the rendered form (`"<a class=\"btn\" href=\"/Inbox"`) and
assert `mailbox={FirstMailboxId}` as a substring of the extracted href rather
than as its first parameter.

### C08-R-3 — major — five S12 states are relabelled "Submitted"

`src/Pegasus.Web/Presentation/OperatorLabels.cs:1169-1181`

`StaffMail.State` keeps Sent/Failed/Cancelled/Unknown and sends everything
else through `_ => "Submitted"`. S12
(`SHARED-CONTRACTS.md:290-304`) gives those five states different permitted
next actions — `Prepared` waits for authorized staff to confirm the frozen
payload, `DraftReady` attaches then sends, `Submitted` may only observe Sent —
so a `Prepared` or `DraftCreating` operation is shown to the operator as
"Submitted", stating that the message reached the provider when nothing has
been submitted. The plan's item 3 says "Render A's canonical S12 state
projection; do not define a C send-state vocabulary". The catch-all also
silently absorbs any future enum member, against this file's own fail-closed
convention (`SourceCandidateDisposition` throws on an unknown value).

**Fix:** give each S12 state its own word (or one explicitly documented
in-flight grouping that never claims submission before `Sending`), and throw
`InvalidOperationException` on an unrecognised member as the rest of the file
does.

### C08-R-4 — major — the ambiguous-outcome path is unreachable

`src/Pegasus.Web/Pages/Mail/Compose.cshtml.cs:188-189`,
`src/Pegasus.Web/Pages/Mail/Compose.cshtml:31-49`

`OnPostSendAsync` always ends in `RedirectToPage(...)`, which discards
`Operation`. The view's "Send status" panel — and the Reconcile form inside
it, the only caller of `OnPostReconcileAsync` — renders only when
`Model.Operation is { }`, which after a send never happens. So:

- an `Unknown` outcome reaches the operator only as
  `SendNotice = "Correspondence unknown."` inside a `notice--success` banner;
- `OnPostReconcileAsync` and `OperatorLabels.StaffMail.Reconcile` are
  unreachable in practice;
- the plan's required "visible `Unknown` without resend after an ambiguous
  outcome" is not delivered, and no test covers it —
  `RecordingStaffMailSend.ReconcileAsync` throws `NotSupportedException`.

The plan's test list also names "replay on duplicate POST"; no test posts the
same key and payload twice.

**Fix:** carry the operation identity through the redirect (a `[TempData]`
operation id re-read on GET via `IStaffMailSend.GetAsync`, or return `Page()`
for a non-terminal state) so the state panel and Reconcile render; stop
presenting a non-`Sent` state in a success notice; add the `Unknown`
no-resend test and the same-key replay test.

### C08-R-5 — major — nothing proves the ten-row notification bound

`src/Pegasus.Core/Operations/OperationsSnapshot.cs:101-105, 168-192`;
`tests/Pegasus.IntegrationTests/Browser/OuterShellBrowserTests.cs:86-128`

`Take(MaximumAttentionRows)` is implemented, but the plan's test obligation
("Notification tests assert 0/1/10/over-10 attention rows") is unmet: the
browser tests cover 0 rows and 2 rows, `ShellAndStatusPageWebTests` covers 1
stub row, and no test at any level composes more than ten rows. The bound is
exactly the kind of constant that regresses unobserved, and it is the one
number the design contract fixes for this control.

**Fix:** a Core test over `IGetAttentionRows` with 11+ composable rows
asserting exactly 10, in the snapshot's own order; keep a web/browser case at
10 and over-10 for the rendered menu.

### C08-R-6 — major — a second Ctrl+K leaves the shell permanently inert

`src/Pegasus.Web/wwwroot/js/site.js:1395-1412` with `:948-961` and `:1738`

The palette's `open(seed, opener)` calls `dialog.pegasusOpen(source)` with no
"already open" guard, and Ctrl+K is deliberately live inside inputs
(`:1731-1742`, "Inside an input only Ctrl K acts"). Pressing Ctrl+K while the
palette is open therefore re-enters the dialog's `open`: `inertOutside`
(`:948`) skips every sibling that already carries `inert`, so it returns an
empty release closure that **overwrites** the first one. After Escape,
`close()` runs the empty release and the whole shell outside the dialog stays
`inert` until a page reload — keyboard and pointer both dead. The browser
lane passes because no test presses Ctrl+K twice.

**Fix:** return early from the palette's `open()` when `!dialog.hidden`
(reseed and refocus the input only), or make the dialog module's own `open`
idempotent (`if (!dialog.hidden) { return; }`) so `release` is never
overwritten.

### C08-R-7 — minor — the rail count and the attention query fetch the same two sources twice

`src/Pegasus.Web/Presentation/RailCountsPageFilter.cs:74-88` with
`src/Pegasus.Core/Operations/OperationsSnapshot.cs:200-246`

On every authenticated page except Work Centre the filter reads the
open-Triage page and the unidentified queue for the rail count, then
`IGetAttentionRows` reads both again inside `FetchAttentionInputsAsync` — two
extra round trips per request for figures already in hand, on every page.

**Fix:** either let the narrow query accept the inputs the filter already
holds, or record the duplication as accepted with the reason.

### C08-R-8 — minor — a Core doc comment crefs a Web type

`src/Pegasus.Core/Operations/OperationsSnapshot.cs:70`

`<see cref="RailCountsPageFilter"/>` names a `Pegasus.Web` type from Core. The
cref cannot resolve (the projects do not set `GenerateDocumentationFile`, so
the compiler never checks it) and it points Core's API documentation at Web.

**Fix:** describe it in prose ("Pegasus.Web's rail-counts page filter") with
no cref.

### C08-R-9 — minor — a field hint that also cannot bind

`src/Pegasus.Web/Pages/Mail/Compose.cshtml:65`

`placeholder="Case reference"` is a field hint, which
`docs/design/README.md:660-664` bans outright ("A field is a label and a
control, nothing more … no format guidance"). It is also wrong: `CaseId` binds
a `Guid?`, so a typed case reference cannot bind at all.

**Fix:** drop the placeholder. If staff are meant to choose a Case by
reference, that is a picker, not a Guid text box — and out of this slice.

### C08-R-10 — minor — the design authority's 980px statement is now false

`src/Pegasus.Web/wwwroot/css/site.css:792`

The ≤980px `.admin-nav` rule now wraps (`flex-wrap:wrap;overflow:visible`),
which is the controller-sanctioned fix for the 760px clip and the behaviour
this review was asked to confirm. It falsifies
`docs/design/README.md:435`, which still says `case-section-nav` and
`admin-nav` "become horizontal scrollers" at that breakpoint.

**Fix:** hand the one-line correction of `docs/design/README.md:435` to the
owner of that file; it is outside C08's file list, so this slice cannot make
it. Leaving the doc uncorrected leaves a false current-state claim.

### C08-R-11 — minor — one test name overstates, one recipient case is missing

`tests/Pegasus.IntegrationTests/StaffCorrespondenceWebTests.cs:48-69` and
`src/Pegasus.Web/Pages/Mail/Compose.cshtml.cs:216-222`

`AnUnauthenticatedActorIsForbiddenAndSendsNothing` sends `X-Test-Roleless` —
an authenticated staff member with no roles, not an unauthenticated caller.
Separately, `ParseRecipients` accepts any non-empty token as an address, so
the plan's "zero calls on … recipient" is proved only for the empty case.

**Fix:** rename the test to what it proves, and either add a
malformed-address case or state in the doc comment that address validity is
A's transport's to enforce.

### C08-R-12 — note (deferred to Stream A) — `ApprovedMailbox.Generation` is never populated

`src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs:394-404`

`Map(MailboxSnapshot)` passes neither `Generation` nor
`VerifiedEncodedMessageSizeLimit`, so both take their record defaults (`0`,
`null`) whatever `ApprovedMailboxes.MailboxGeneration` holds. Compose reads
the right field, but the value it sends is always
`ExpectedMailboxGeneration: 0` and G14's concurrency guard is inert until A
maps the two columns. A-owned Infrastructure, outside C08's file list —
recorded so the combined checkout does not mistake the read for a working
guard. No test can pin a non-zero generation until it is mapped.

### C08-R-13 — note (accepted risk) — two files outside the plan's C08 lists

`src/Pegasus.Web/Presentation/NeedsAttentionPresentation.cs` (new) and
`src/Pegasus.Web/Presentation/RailCountsPageFilter.cs` are edited but named in
neither "Existing C files" nor "Proposed C files". Both sit in the C-owned
shell `Presentation/` area (`docs/design/README.md:857-858`), the new file
exists to give Work Centre and the notifications menu one label list rather
than two, and no other stream's file was touched. Accepted; worth folding into
the C09 file-list reconciliation.

## Residuals (not findings — other streams owe these)

1. **`IGetAttentionRows` is still unregistered.** The only registration on
   this branch is `IGetOperationsSnapshot`
   (`src/Pegasus.Infrastructure/DependencyInjection.cs:295`). ASSUMPTION 3's
   per-request `GetService` bridge in `RailCountsPageFilter` therefore **must
   stay** this slice — it must not revert to constructor injection — and in
   the real composition the notifications menu is empty on every page except
   Work Centre. A owes
   `services.AddScoped<IGetAttentionRows>(…GetOperationsSnapshot)`. The bridge
   is honestly documented at both the filter and the notes, and both halves
   are tested.
2. **No `IStaffMailSend` implementation exists in the repository.**
   `/Inbox/Compose` cannot be activated in the production composition until A
   lands one; the failure is page-scoped (a required constructor dependency),
   which is the right shape here.
3. **`AllowStaffSend` unmapped** (`EfApprovedMailboxStore.Routes:407`). When A
   maps it, C must drop the `SentEvidence` fallback in
   `LoadSendableMailboxesAsync` — a sent-evidence-only mailbox must not be
   offerable for staff send — and `SeedSendableMailboxAsync` must switch from
   `AllowSentEvidence = 1` to the staff-send column.
4. **Reply/ReplyAll/Forward** remain blocked on `RetainedMailDetail` not
   carrying the retained message's immutable mailbox/message/internet-message/
   conversation identity (controller-recorded residual, not a finding).
5. **`/Inbox/Compose` has no `docs/design/test-ui/catalogue.json` entry**
   (A-owned path, correctly untouched).
6. **Route-name drift on the nav's forward references.** All six new
   `_AdminNav` links are forward references — nothing under
   `Pages/Administration/` matches any of them on this branch.
   `/Administration/Health` matches `A-platform.md:344`'s `Health` page (so
   the controller's note holds), while `C-intake.md:1130` still names
   `/Administration/ServiceHealth`; `/Administration/AiJobs` is named as a
   page by **no** stream plan and has no identified owner. Both need settling
   at C09 integration or the links stay dead.

## Lanes seen (bound to 86e8659f5)

`.../scratchpad/wave1/wave31-tests/`: `1-build` PASS (0/0), `2-core` PASS
(59/59), `3-integration` **FAIL** (159/160), `4-browser` PASS (82/82),
`5-architecture` PASS (100/100). The single failure is C08-R-1. `pass`
requires every lane green, so the verdict cannot be `pass` on this head even
before the findings.

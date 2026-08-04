# Defects and non-functional UI — register

Found during the whole-application UI review, 2026-08-04, on `dev` @ `b6d030b`. "Verified live"
means reproduced this session against a local DevelopmentOffline run (fresh `PegasusUiWork`
LocalDB) with genuine corpus material; other entries are code-verified with file:line evidence.
Severity: **Blocker** (feature does not work), **Major** (misleads the operator or breaks a design
contract), **Minor** (polish).

## Blockers

### B1. Manual upload is completely non-functional (verified live)
- Page: Intake `/Intake` (page-2).
- The upload form renders `action=""` — the `asp-page-handler="ReceiveIntake"` +
  `asp-route-page` combination on `src/Pegasus.Web/Pages/Intake/Index.cshtml:35` fails to generate
  a handler URL. The browser therefore POSTs to `/Intake` with no `handler` query, no handler
  matches, and Razor Pages silently re-renders the page: HTTP 200, no receipt, no work item, no
  staged artifact, **no error shown**.
- Proof: two live POSTs produced zero rows in `IntakeWorkItems` and no staged file; forcing the
  same form's action to `/Intake?handler=ReceiveIntake&page=1` via script made the identical
  submission succeed (receipt queued and processed). Every other page's forms generate correct
  `?handler=` URLs (verified on `/Intake/{id}` and `/Cases/{id}`), so the defect is specific to
  this form. Likely trigger: the page's custom `@page "/Intake"` route combined with
  `asp-route-page`.
- Consequence: the only manual submission path in the product is a dead button.

### B2. Triage records can never be created, in any deployment (code-verified)
- Pages: Triage queue `/Triage` (page-3) and Triage detail `/Triage/{id}` (page-11).
- The only implementation of `IIntakeTriageMatcher` in the entire source tree is
  `NoAcceptedIntakeTriageMatcher` (`src/Pegasus.Core/Intake/IntakeContracts.cs:399`), registered via
  `TryAddSingleton` at `src/Pegasus.Infrastructure/DependencyInjection.cs:107`. No composition
  (Web, Worker, tests aside) replaces it. `EfTriageStore.CreateAsync` correctly refuses evidence
  that is not retained on the receipt, so the create path can never fire.
- Consequence: a top-level nav item and two screens are backed by a pipeline that cannot produce a
  single record. The Triage tile on the dashboard will always read 0 and the queue will always be
  empty. (Live confirmation: a genuine triage-type corpus e-mail processed to `Needs sorting`;
  a direct Core `CreateTriageFromIntake` call was rejected fail-closed.)

### B3. Dashboard numbers are hardcoded placeholders (code-verified, operator-reported)
- Page: Operations `/` (page-1).
- `src/Pegasus.Web/Pages/Index.cshtml:7-11`: `const string Absent = "Unavailable"` — the tiles for
  Not ready, Held, New cases today, Sent to Engineer today/this week, Reports sent today/this week,
  and both workspace cards are static literals. `IndexModel` fetches only four values
  (intake counts, triage count, due work, staged artifacts) via `IGetOperationsSnapshot`; **no
  case-lifecycle count query or day/week activity query exists anywhere in Core**
  (`src/Pegasus.Core/Operations/OperationsSnapshot.cs:39-86`, `ICaseQueryStore` has only
  `SearchAsync`/`GetAsync`).
- Contradiction: `docs/capabilities.md` rows UI-02 and UI-04 claim these capabilities "Required and
  accepted before 0.1.0-alpha.1".

## Major

### M1. The "Review" tile shows an e-mail count, not the Review case state (code-verified; NOW.md already tracks the wording decision)
- `Pages/Index.cshtml:40-46` renders `Model.Counts.DraftReady` (an e-mail classification) under the
  label "Review" (a Case state) and links to `/Intake?decision=draft_ready`.
- Verified live: accepting the draft as a case did NOT decrement the count — accepted receipts keep
  their `draft_ready` decision, so the tile also over-counts (`IIntakeReceiptQueries.GetCountsAsync`
  does not exclude accepted receipts). The receipt row equally still shows "Instruction draft"
  with no accepted indication (`Pages/Intake/Index.cshtml` list).

### M2. Working screens advertised as "Unavailable" (verified live)
- The dashboard's Email and Requests cards are the ONLY navigation entry to
  `/Operations/Email` and `/Operations/Requests` — both fully working pages — yet each card carries
  an "Unavailable" chip and the copy "No dashboard aggregate exists for this route."
  (`Pages/Index.cshtml:131-156`).

### M3. Oversize upload returns a raw browser error page (verified live)
- A 25 MB file POSTed to the upload form returns a bare `HTTP ERROR 400` (Kestrel/antiforgery layer
  rejects before model binding), never the designed "The selected file must be 10 MB or smaller."
  message (`Pages/Intake/Index.cshtml.cs:82-85`). Files between the caps get the polite message;
  files above them get the raw 400.

### M4. Unknown-record URLs return raw browser 404s (verified live)
- `/Triage/{unknown-guid}` and `/Intake/{staged-id}` render the browser's default 404 page — no
  styled not-found page exists in the app. (`/Error` handles exceptions only, via
  `UseExceptionHandler`; there is no status-code page registration in `Program.cs`.)

### M5. Raw enum/database values as user-facing text (code-verified; operator-reported for the dropdown)
- Cases filter dropdown: `Pages/Cases/Index.cshtml:41-50` renders `CaseLifecycleState` values
  verbatim — `NotReady`, `ReportPreparation`, `PostReportComplete`, `ProviderCancelled`,
  `CollisionEngineersRejected`, `CreatedInError`. Correct human labels ALREADY exist in
  `_CaseWorkflow.cshtml:205,211` (close/reopen selects) but are not reused.
- `_StatusChip.cshtml` normalises only `_`/`-`, so `"NotReady"` misses every mapped key and falls
  to neutral grey — losing both wording and the amber/navy/green tone contract.
- Also raw: `Pages/Search/Index.cshtml:71` (`@item.State`), `_CaseSummary.cshtml:12,20,42`
  (`CaseType` "InspectionAndAudit", state, due-work state), `_CaseHistory.cshtml:22` (snake_case
  event codes like `case_created`), `_CaseDocuments.cshtml:34,37,128,161` (SemanticRole,
  CustodyStatus, BoxFileRequestStatus), `Administration/Mailboxes.cshtml:54` (RouteScopes join
  "InboundIntake, SentEvidence"), `Administration/Automation/Activity.cshtml:59-61`
  (EventKind/Outcome).

### M6. Freshness banner: five of its six states are dead code; UTC can be labelled "London" (code-verified; NOW.md tracks the timezone item)
- `Pages/Shared/_FreshnessBanner.cshtml:17` reads `ViewData["RefreshStatus"]` — never set by any
  page, so the chip is permanently "Current" and the loading/stale/partial/unavailable/failed
  states required by UI-06 cannot occur. Lines 22-31 fall back to `TimeZoneInfo.Utc` on
  `TimeZoneNotFoundException` while line 63 still prints "London".

### M7. Silent caps on dashboard lists (code-verified)
- `OperationsSnapshot.cs:46` `MaximumDueWork = 20` — the "Due case work" tile renders
  `DueWork.Count`, so with >20 due items it permanently reads "20" with no "+" indicator. Staged
  artifacts identically capped at 20.

### M8. "Staged intake artifacts" is a dead-end diagnostic on an operator dashboard (verified on prod screenshot + code)
- `Pages/Index.cshtml:193-237` lists the raw Azure staging container: blob `StorageKey`
  (`staging/f7d2...` + GUID chain), `ContentLength` in bytes (`10378983 bytes`), first-seen time —
  with no link to the receipt, work item, failure code, or any action. "Failed" here means the
  intake work item terminally failed in SQL and the blob is deliberately retained
  (`DurableIntake.cs:732-848`), none of which an operator can see or act on from this panel.
  The prod screenshot also shows the panel's text overlapping (layout defect at that width).

### M9. Upload succeeded but the queued item is invisible (verified live)
- After a (handler-fixed) upload, the page banner says "The instruction has been retained and
  queued for processing." while the list below still says "No intake receipts match this view." —
  queued-but-unprocessed items have no representation anywhere in the UI. With the Worker not
  running (or backlogged), items simply do not exist as far as the operator can tell.

### M9b. The external upload page shows internal staff navigation to anonymous visitors (code-verified)
- Page: `/Uploads/{token}` (page-13) — the product's ONLY external-facing screen.
- `src/Pegasus.Web/Pages/Uploads/` has no `_ViewStart` override, so the anonymous claimant receives
  the full staff `_Layout`: the Operations / Triage / Cases / Search nav, the disabled
  "Intake unavailable" span, a "Sign in" link, and a brand logo linking to the internal dashboard.
  The tab title and footer both leak the internal product name.
- Consequence: a third party outside Collision Engineers sees the internal application's structure
  and vocabulary. The same defect class affects `/Account/SignIn` (page-14), which also renders the
  authenticated nav to unauthenticated visitors.

### M9c. External dead-link outcomes return raw browser 404s (code-verified)
- Every failure path on `/Uploads/{token}` — expired, revoked, exhausted, superseded limits
  version, unknown token — returns `NotFound()`, so the external user gets Chrome's default 404
  rather than a worded "this link is no longer active" page. Presentation rule 6 violated on the
  one screen the company does not control the audience of.

### M9d. Sign-in rate limiting returns a bodyless HTTP 429 (code-verified)
- `Program.cs:248-251` — exceeding the sign-in attempt limit returns a raw 429 with `Retry-After: 60`
  and no body, so the operator sees a blank browser error page with no explanation and no stated
  wait. (`page-14` review.)

### M9e. Framework validation strings reach operators verbatim (code-verified)
- Change password renders the raw messages `"'ConfirmPassword' and 'NewPassword' do not match."`
  and `"The field NewPassword must be a string or array type with a minimum length of '8'."`
  Additionally `StaffPasswordChangeError` cannot distinguish a wrong current password from a weak
  new one, so the UI cannot tell the operator which failed. (`page-15` review.)

### M10. Sign-out page is unstyled (code-verified)
- `Pages/Account/SignOut.cshtml` renders a bare `<h1>` and an unclassed `<button>` — the only
  screen off the design system entirely.

### M11. Ambiguous three-cause error copy (verified live)
- Creating a public upload request when the store is not composed returns: "The upload request
  could not be created because the case changed, edit mode was lost, or requests are unavailable."
  — one message for three unrelated causes, none actionable. (`Pages/Cases/Details.cshtml.cs`,
  CreateRequestUploadLink handler path.)

## Minor

- **Principals table prints a sequence-lineage GUID** (`Administration/Principals/Index.cshtml`,
  "Sequence lineage" column) — internal identifier, meaningless to operators (verified live).
- **`/ImageIntake` list is orphaned** — not in any nav; the list is reachable only via
  ImageIntake detail → "All Image intakes", itself 4 clicks deep (`_Layout.cshtml:35-66`,
  `ImageIntake/Details.cshtml:23`).
- **Search/Cases disagree on failure semantics** — `Cases/Index.cshtml.cs:133` sets HTTP 503 on
  query failure; `Search/Index.cshtml.cs:59-63` sets nothing for the identical failure.
- **Dead shared partials** — `_MetricCard.cshtml`, `_ProvenancePanel.cshtml`, `_ReasonDialog.cshtml`,
  `_ErrorSummary.cshtml` are referenced by nothing (content-grep across Web + tests); the last two
  use inline `style=` attributes unlike the rest of the app.
- **Administration → Automation is permanently inert** — `Features:AutomationMcp` is set in no
  shipped configuration, so `Administration/Automation/Index.cshtml:27` always renders "The
  Automation ingress is not composed in this deployment…" (see also additions doc).
- **EVA/document surface degrades silently** — `_CaseWorkflow.cshtml:420` renders "EVA handoff
  preparation is unavailable for this runtime or case" whenever custody isn't composed; the
  operator cannot distinguish configuration absence from a case-specific problem.
- **"reasonedly"** — `Pages/Cases/Details.cshtml:56` ("association can be reasonedly reversed") is
  not a word.
- **`page1.png` "Not ready"/"Held" show "Unavailable" instead of 0** — operator-reported; root
  cause is B3 (they are hardcoded, not zero-suppressed).
- **Access review shows UTC** (`Administration/Access/Index.cshtml`, "Last reviewed (UTC)") while
  the rest of the app renders Europe/London.
- **`FormatBytes` can render "1024 KB"** on the upload page — a KB branch exists, so the size limit
  can display in kilobytes, violating the MB-only rule (`page-13` review).
- **Accepted file types are never shown to the uploader** — they exist only in the `accept`
  attribute, and the rejection message names no permitted type (`page-13` review).
- **Sign-out is dead markup** — `OnGet` redirects to `/Index` and the nav already POSTs sign-out
  directly (`_Layout.cshtml:57-59`), so the unstyled page is unreachable in normal use
  (`page-16` review).
- **Error page states one failure three times** (h1, panel, h2) and gives a 55-character trace ID
  hero prominence; its "Return to Operations" link still uses the old page name (`page-18` review).

## Performance and correctness findings from the per-page reviews

- **Per-principal correlated count subquery** — `EfOrganizationAdministration.cs:428-451` evaluates
  `principal.Cases.Count` inside the per-organization projection, so the Principals page can issue
  up to 25 organisations × 101 principals of `COUNT(*)` on a single page load (`page-25` review).
- **Automation activity renders server-local time** — `Administration/Automation/Activity.cshtml:57`
  uses `ToLocalTime()` (the server's clock) rather than Europe/London, unlike every other date
  surface in the product (`page-31` review).
- **Automation activity paging over-fetches** — offset paging requests `page * pageSize + 1` rows
  from both streams on every request (`page-31` review).
- **Failure reasons are exception strings** — `AutomationMcpAuditor` composes the Reason column as
  `"{ExceptionTypeName}: {message}"`, so the operator reads .NET type names. A stable reason code is
  required writer-side before the UI can label these (`page-31` review).
- **An over-length filter value returns `NotFound()`** on Automation activity — a raw browser 404
  for a typo (`page-31` review).
- **"Create principal" renders an upload-arrow icon** (`#icon-upload`) on a create action
  (`page-25` review).
- **Cells that narrate their own emptiness** — "No replacement action" as table-cell content
  (`page-25` review); the roles form's required 500-character Reason field states neither that it is
  required nor its limit (`page-24` review).
- **Duplicated labels** — page-24 renders "ORGANIZATION ROLES" twice (section label plus an
  identical fieldset legend) and a visible caption repeating its own H1.
- **Render-time-knowable rules enforced only after POST** — a Work Provider role cannot be removed
  while an active principal exists, but this is discoverable only by submitting and reading an error
  string (`page-24` review).

## Cross-references
- Hidden-but-shipped functionality (not defects, but invisible): `additions-hidden-features.md`.
- The durable rules preventing recurrence: `durable-rules-proposal.md`.
- Capability-tracking contradiction (UI-02/UI-04 "accepted" vs placeholder): raise via
  `docs/capabilities.md` truth-up — see durable-rules proposal §4.

## Plan

### 1. Core contracts (`UnidentifiedContracts.cs`, `IntakeDecisionPolicy.cs` area)
- Add `UnidentifiedMediaKind { Image, Email, Document }`.
- Add `UnidentifiedMediaKindPolicy.Classify(IntakeSourceChannel channel, string
  mediaType)`: `Mailbox` → `Email`; else `image/*` → `Image`; else `Document`.
  Pure function, unit-testable without EF.
- Add `UnidentifiedQueueRow(Guid Id, string Reference, UnidentifiedMediaKind
  MediaKind, string? FileName, string? EmailSubject, string? EmailSender,
  DateTimeOffset ReceivedAtUtc, UnidentifiedReasonCode ReasonCode)`.
- Add `IUnidentifiedStore.ListQueueAsync(UnidentifiedMediaKind? mediaKind,
  CancellationToken)` returning open items oldest-first (matches existing
  `ListAsync` ordering).

### 2. Infrastructure (`EfUnidentifiedStore.cs`, `EfIntakeReceiptStore.cs`)
- Bump `EfIntakeReceiptStore.ReadSubject` and `.ParseSourceChannel` from
  `private` to `internal` — reused, not duplicated.
- Implement `EfUnidentifiedStore.ListQueueAsync`: left-join open
  `UnidentifiedItemEntity` rows against `IntakeReceiptEntity` on `OriginId ==
  receipt.Id` (no FK modelled; a plain LINQ join). Classify each row via
  `UnidentifiedMediaKindPolicy.Classify` fed by
  `EfIntakeReceiptStore.ParseSourceChannel(receipt.SourceChannel)` and
  `receipt.MediaType`. A `SubmissionGroup`-origin row (no matching receipt)
  defaults to `Image` — INTK-007's grouped-VRM-conflict case is the only
  producer of that origin kind today, and it is image material; comment
  records this as the fallback, not a general rule. Filter in-memory after
  materializing (the queue is an exception list, same pattern the existing
  `ListAsync` already uses — no new pagination risk).
- Sender for an `Email` row: `receipt.MailRouteDecision?.EffectiveSenderAddress`.
  Subject: `EfIntakeReceiptStore.ReadSubject(receipt.EvidenceJson)`.

### 3. Presentation (`OperatorLabels.cs`)
- Add `UnidentifiedMediaKind(UnidentifiedMediaKind kind)` → "Image" / "E-mail"
  / "Document".
- Remove `UnidentifiedOriginKind(...)` — its one output, "Intake receipt", is
  exactly the banned wording the ticket names; nothing needs the origin kind
  once rows carry media kind + handle instead. Remove its two remaining call
  sites (Details rebuild, Index retirement) in the same change.

### 4. Web — Queues page (`Triage/Index.cshtml[.cs]`)
- `IndexModel`: inject `IUnidentifiedStore` and `IImageIntakeQueries`
  (constructor DI — both already registered for their existing pages).
- Extend `Queue` validation: `not_ready|review|held|triage|unidentified`.
- New `[BindProperty(SupportsGet=true, Name="kind")] string? KindFilter` for
  the Unidentified tab: `all|images|emails`, else `NotFound()`. Only consulted
  when `Queue == "unidentified"`.
- New `[BindProperty(SupportsGet=true, Name="origin")] string? OriginFilter`
  for the Not-ready tab: `all|instruction|image`, else `NotFound()`. Only
  consulted when `Queue == "not_ready"`.
- `OnGetAsync`: when `Queue == "unidentified"`, call
  `unidentifiedStore.ListQueueAsync(kind switch)`; skip the Cases/Triage
  queries entirely (a fifth queue, not a third case-stage branch). Keep
  `StageCounts` (unchanged shape) plus a new `UnidentifiedCount` from
  `unidentifiedStore.ListQueueAsync(null).Count` — one extra query, mirrors how
  `StageCounts` is always fetched regardless of the active tab so every tab
  shows every count.
- When `Queue == "not_ready"`: run `SearchCases` unless `origin == "image"`;
  run `imageIntakeQueries.ListAsync(associated: false, ct)` filtered to
  `State == AwaitingInstruction` unless `origin == "instruction"`. Both lists
  populate independently; `origin == "all"` renders both tables. Image-initiated
  rows are not paginated (small exception set, same trade-off as the existing
  Unidentified queue) — recorded here rather than silently accepted.
- Row-label static helpers on `IndexModel`, following the existing
  `ReasonLabel`/`OriginKindLabel` convention on `Unidentified.IndexModel`:
  `UnidentifiedHandle(UnidentifiedQueueRow row)` → filename, or "{subject} —
  {sender}" (subject falls back to "(no subject)" only if genuinely absent).

- `Index.cshtml`:
  - Add the `Unidentified` tab to `nav.tabs`, count = `Model.UnidentifiedCount`.
  - When `Queue == "unidentified"`: subtabs `All | Images | E-mails`
    (`kind=` param, same markup pattern as the Triage state subtabs). Table:
    U-reference (links to `/Unidentified/Details`), kind, handle, received
    (office time), reason — one row, one line. Empty state text, no lede.
  - When `Queue == "not_ready"`: subtabs `All | Instruction-initiated |
    Image-initiated` (`origin=` param). Render the existing Cases table
    (labelled "Instruction-initiated" only when both tables show, i.e.
    `origin == "all"`) and a new Image-initiated table (Reference, Registration,
    Status via `OperatorLabels.ImageIntakeLifecycleState`, Received) linking to
    `/ImageIntake/Details`, under the same "only label when both show" rule.
  - `Review`/`Held` unchanged.

### 5. Web — retire `/Unidentified` list, rebuild Details
- `Unidentified/Index.cshtml.cs`: replace the whole model with `OnGet() =>
  RedirectPermanent("/Triage?queue=unidentified")`. `Index.cshtml` keeps only
  `@page "/Unidentified"` (never rendered — redirect short-circuits before
  `Page()`).
- `Unidentified/Details.cshtml`: rebuild — `<h1>@Model.Item.Reference</h1>`,
  status chip beside it, no eyebrow/lede. One panel: kind, received, reason,
  "Concerns: {handle}" (reuse the same filename/subject+sender logic as the
  queue row — a static helper on `DetailsModel`, not a second copy), and (only
  when a receipt exists) one link "Open the received file" to
  `/Intake/Details`. History panel kept (it is the chronological record, not
  link-noise). Resolve form kept unchanged. Drops the "Retained source" panel's
  asset/evidence dump and the "custody detail" link text entirely.
- `Unidentified/Details.cshtml.cs`: keep `SourceReceipt` load; add the
  `Handle` computation (same shape as the Infrastructure mapping, reusing
  `UnidentifiedMediaKindPolicy.Classify` against
  `SourceReceipt.SourceIdentity.Channel`/`.MediaType` — one classification
  rule, called from two places, not two rules).

### 6. Nav and Dashboard
- `_Layout.cshtml`: delete the `/Unidentified/Index` rail link (lines 80-82).
- `Index.cshtml` (Dashboard): change the "Unidentified" metric's
  `asp-page="/Unidentified/Index"` to `asp-page="/Triage/Index"
  asp-route-queue="unidentified"`.

### 7. FRD-12
- Replace "### Unidentified queue and detail" with the tab structure: queue
  location, the two filter sets (kind on Unidentified, origin on Not ready),
  row content, and that resolution stays reachable from the Details page.
  Keep it behavioural — no mechanics, no route inventory beyond what the
  section already lists.

### 8. Tests
- `UnidentifiedPersistenceTests.cs`: `ListQueueAsync` — seed a mailbox-channel
  receipt (Email), a manual-upload image/jpeg receipt (Image), and a
  manual-upload application/pdf receipt (Document), register each as
  Unidentified, assert the kind filter returns exactly the matching row and
  the handle fields are populated correctly (subject+sender for Email,
  filename for Image/Document).
- A new `Pegasus.Core.Tests` case for `UnidentifiedMediaKindPolicy.Classify`
  (pure function, no EF) covering the three branches.
- Not-ready origin filter: seed one Instruction-initiated case (existing
  fixture) and one Image-initiated case in `AwaitingInstruction` state; assert
  `origin=instruction` returns only the case row, `origin=image` returns only
  the image-initiated row, `origin=all`/absent returns both.
- `/Unidentified` redirect: assert a 301/308 landing on
  `/Triage?queue=unidentified`.
- Rendered-markup assertion: fetch `/Triage?queue=unidentified`, assert the
  response body contains no `intake` (case-insensitive, operator-facing text)
  and no bare GUID pattern.
- `AccessibilityTests.AuthenticatedRoutes`: replace nothing (Unidentified was
  never listed); add `/Triage?queue=unidentified` alongside the existing
  `/Triage` entry so the new tab's shape gets the same axe/one-H1/no-inline-
  style coverage.
- Update any existing test that asserts the old `/Unidentified` page renders,
  the old nav link exists, or the old "Intake receipt — {guid}" row text —
  only where the old structure is literally what is being asserted.

### Simplification pass
Run after implementation, over the branch diff: reuse (did the Not-ready
origin filter reuse `ISearchCases`/`IImageIntakeQueries` untouched — yes, by
design), simplification (is the in-memory media-kind filter and unpaged
Image-initiated list the smallest change that satisfies the ticket, or does it
need real SQL-side filtering/pagination — recorded as a considered-and-
deferred trade-off unless corpus-scale testing shows otherwise), efficiency,
altitude. Findings and dispositions recorded here under a dated heading before
the PR opens.

### Governing docs
- `docs/design/README.md` (`:160-171`) — one H1, no lede, banned terms,
  `OperatorLabels` as the one label owner, office time everywhere.
- `docs/frd/frd-12-operator-experience.md` — queue-surface section owner,
  updated in this ticket.
- `docs/frd/frd-02-intake-and-source-identity.md` — Unidentified/receipt
  vocabulary; read, not changed (INTK-007's contracts are unchanged).

## Simplification pass — 2026-08-20

Ran `/simplify`: four parallel review agents (reuse, simplification,
efficiency, altitude) over `git diff HEAD` in the task worktree, followed by
applying the surviving findings.

### Applied

- **Reuse + simplification + altitude (3 agents, same finding): duplicated
  "subject — from sender" e-mail-handle formatting** between
  `Triage.IndexModel.UnidentifiedHandle` and `Unidentified.DetailsModel.Handle`.
  Extracted `OperatorLabels.EmailHandle(string? subject, string? sender)`;
  both call sites now call it instead of carrying their own copy of the
  switch expression.
- **Reuse: composed `OfficeDate` + `OfficeClock` instead of `OfficeTime`** in
  the Unidentified row's received-time cell
  (`Triage/Index.cshtml`) — a single-line surface reproducing the exact
  format `OperatorLabels.OfficeTime` already provides. Now calls
  `OperatorLabels.OfficeTime(row.ReceivedAtUtc)` directly.
- **Altitude: the "no receipt → treat as Image" fallback was duplicated** in
  `EfUnidentifiedStore.MapQueueRow` (Infrastructure) and
  `Unidentified.DetailsModel.MediaKind` (Web) — the same business judgement
  in two non-Core places. Moved the fallback into
  `UnidentifiedMediaKindPolicy` itself via a new nullable-channel overload
  (`Classify(IntakeSourceChannel? channel, string? mediaType)`); both callers
  now call the policy and carry no judgement of their own.
- **Simplification + efficiency (2 agents, same finding): `ListQueueAsync`
  was called twice on every Unidentified-tab load** — once unfiltered for
  `UnidentifiedCount` (every tab), once again filtered for `UnidentifiedRows`
  (Unidentified tab only) — identical join, run twice. `OnGetAsync` now
  fetches the unfiltered list once and filters it in memory for the tab's
  media-kind selection instead of re-querying.
- **Efficiency: the two counts every tab always fetches
  (`GetCaseStageCountsAsync`, the unfiltered `ListQueueAsync`) ran
  sequentially** despite using independent `DbContext` instances. Now started
  together and awaited via `Task.WhenAll`.
- **Altitude: `OnGetAsync`'s Not-ready branch inlined two independent
  origin-filter gates** (deciding whether to run `SearchCases` and/or
  `IImageIntakeQueries.ListAsync`), a level below its `ShowingTriage`/
  `ShowingUnidentified` siblings. Extracted `LoadNotReadyAsync`, which also
  runs its two queries concurrently via `Task.WhenAll` rather than skipping
  that opportunity now that they were made explicit.

### Skipped, with reason

- **Efficiency: `EfImageIntakeStore.ListAsync`/`EfUnidentifiedStore.ListQueueAsync`
  filter in memory after an unbounded fetch rather than pushing the
  predicate into SQL.** `ListAsync(bool? associated)` is a pre-existing
  method with other callers (`/VehicleImages`); widening its contract to add
  a state predicate is a change to a shared port outside this ticket's
  diff. `ListQueueAsync`'s in-memory media-kind filter is unavoidable without
  a persisted/computed column, since the kind is derived in C# from two
  joined columns — flagged by the agent itself as "a scale risk, not urgent"
  for what is an exception queue. Left as documented trade-offs (both already
  called out in the code comments and this plan's step 2/step 4); revisit if
  either queue's real volume proves the assumption wrong.

Full test suite re-run clean after every fix (`Pegasus.Core.Tests`: 690/690;
the Unidentified/Triage/Dashboard/ImageIntake/Cases/Shell integration filter:
47/48, 1 pre-existing unrelated skip; `Category=Browser`: 38/38;
`Pegasus.ArchitectureTests`: 97/97).

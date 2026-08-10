# Manual upload creates a case; the Inbox becomes a mail viewer

Task line: `NOW.md` → Doing → branch `task/upload-case-creation-and-inbox`.

Three things the operator asked for, in one task because they are the same
confusion seen from three sides: a manual upload does not produce a case, the
thing it produces instead is shown on a screen called "Inbox", and that screen
is not an inbox.

## What is actually wrong today

**A manual upload is only ever queued.** `src/Pegasus.Web/Program.cs:567`
binds `IIntakeSubmission` to `ReceiveIntake`, whose `IIntakeSubmission`
implementation stages the bytes and returns
`IntakeSubmissionDisposition.Queued` without reading them
(`src/Pegasus.Core/Intake/DurableIntake.cs:339`). So
`src/Pegasus.Web/Pages/Upload.cshtml.cs:132` always takes the `Queued` branch
and redirects to itself with "was received and is being processed". Its other
two branches — redirect to the created case, redirect to the receipt — are
unreachable code. Nothing is extracted while the operator is watching.

**Extraction then happens only if a Worker timer runs.** The staged item
waits for `DispatchPendingIntakeWork`, then `ProcessQueuedIntake`. On a
workstation with no Worker running, nothing happens at all.

**Even after processing, a case is rare.** `ProcessQueuedIntake`
`AllocateCaseIfDefinitiveAsync` (INT-25, landed in `9393c98`) allocates only
when `IntakeDecision.CaseCreated` *and* the draft carries a principal;
`IntakeDecision.CaseCreated` requires `QdosInstructionExtractionPolicy` to
return `Applicable`, which requires a "QDOS" marker plus at least two known
field labels in one content fragment. Every other upload lands in
`Needs sorting` and stops.

**The only way onward is a form nobody is sent to.** `IAcceptIntake` has one
staff caller, `Pages/Intake/Details.cshtml.cs:534`, reached by finding the row
in a list. Its principal check
(`Pages/Intake/Details.cshtml.cs:383`) and the Core policy behind it
(`QdosAlphaCaseActivationPolicy`, `src/Pegasus.Core/Cases/CaseContracts.cs:42`)
refuse every principal but `QDOS`.

**"Inbox" is the intake receipt list.** `Pages/Shared/_Layout.cshtml:42` points
the nav item at `/Intake/Index`, which lists `IntakeReceiptSummary` rows.
`docs/requirements.md:716-788` already specifies the real workspace (UI-10,
allocated `Next / 0.3.0` in `docs/capabilities.md:224`), and reads throughout
on *retained* messages — "shows the full retained message", "Thread display
includes only retained messages" — so it is a viewer over what polling has
already brought in, not a live Graph proxy from the Web app.

**Mailbox administration exists but drives nothing.**
`src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` already models an
approved mailbox with route scopes and an `Approved`/`Disabled` state, and
`Pages/Administration/Mailboxes.cshtml` already edits it under a reason and an
operation key. But intake polling reads its mailbox from configuration —
`Graph:MailboxId`, `Graph:InboxFolderId`
(`src/Pegasus.Worker/WorkerDependencyInjection.cs:113`,
`src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs:14`) — so an
administrator cannot add a mailbox or stop one being polled.

## What this task changes

### A. A manual upload is processed on the spot and lands on a create screen

1. Compose `ProcessIntakeSubmission` (already in Core,
   `DurableIntake.cs:101`) for the Upload page, leaving the queue-only
   `ReceiveIntake` composition for the MCP ingress
   (`src/Pegasus.Web/Mcp/IntakeMcpTools.cs:42`), which has no operator waiting
   on a screen. The registration becomes explicit per caller rather than one
   shared `IIntakeSubmission`.
2. `UploadModel` routes on the real outcome: an allocated case goes to
   `/Cases/Details`; a readable source that did not allocate goes to the new
   create screen; `Unsupported` and `TechnicalFailure` stay on Upload with the
   failure sentence; `ImageIntakeRegistered` goes to the Image intake surface.
   The `Queued` branch and its "is being processed" copy are deleted, not
   left as dead code.
3. Inline processing makes the POST as slow as reading the document. The page
   gains a submit-time progress state, and the existing 10 MB envelope limit
   (`IntakeEnvelopeLimits.MaximumContentLength`) is the bound on that cost.

### B. One create-case screen, one caller of `IAcceptIntake`

4. A new `Pages/Cases/Create.cshtml` ("New case") takes a receipt id and shows
   what extraction found — the `InstructionReviewField` set with its
   candidates and provenance, editable — plus principal, case type,
   completeness, inspection address resolution, and standalone Audit evidence
   where the type demands it. Confirming posts corrections through
   `IResolveIntake` with `IntakeResolutionKind.CorrectDraft` and then allocates
   through `IAcceptIntake`. Both are existing Core use cases; no acceptance or
   correction rule is reimplemented in Web.
5. The acceptance form **moves** off `Pages/Intake/Details.cshtml`, together
   with the address-resolution and standalone-Audit-evidence panels the
   acceptance gate depends on. `Intake/Details` keeps the evidence and
   decision record and links to the create screen. Duplicate business
   implementation is a stop condition, so there is exactly one acceptance
   caller in Web when this lands.
6. The create screen is also the manual route (INT-26): reachable from a
   received item, not only from an upload.

### C. Any registered principal may allocate

7. Retire `QdosAlphaCaseActivationPolicy`. Its four callers —
   `Core/Intake/AcceptIntake.cs:59`, `Core/Cases/CreateLinkedReplacement.cs:37`,
   `Infrastructure/Persistence/EfCaseAcceptanceStore.cs:100`,
   `Infrastructure/Persistence/EfRecordEngineerFinding.cs:64` — keep
   normalisation (trim, upper-case, 20-character bound) and fail closed on a
   principal that does not exist or is not active, which the acceptance
   transaction's principal lookup already establishes. Allocation stops being
   gated on which principal it is.
8. Automatic identification of a non-QDOS principal from a document is **not**
   in scope. `QdosInstructionExtractionPolicy` still recognises only QDOS, so a
   non-QDOS upload reaches the create screen with the principal blank for a
   person to key. That is the operator's stated position: nothing should block
   a non-QDOS principal, only the identification logic is absent.
9. Follow the removal through `docs/capabilities.md`, `docs/requirements.md`
   and the descriptive note in `docs/adr/0018-provider-inspection-mode-database-setting.md:94`,
   and through the tests that assert the refusal.

### D. The Inbox becomes a mail viewer; the receipt list gets an honest name

10. New workspace over retained mail: list and message detail. The list is
    paged (never infinite scroll), newest first, defaults across all approved
    mailboxes with per-mailbox refinement, shows sender, subject, received
    time and a body excerpt, distinguishes retained read and unread state
    without changing it, and carries an explicit manual refresh with a last
    successful update time and distinct stale and unavailable states. Detail
    shows the full retained message, its attachments, its retained thread, and
    the current processing outcome and case association before any action.
11. This needs message-level retention that polling does not persist today.
    `PollApprovedInbox` retains MIME bytes as an intake source and
    `EfIntakeReceiptStore.ListAsync:218-219` re-derives sender from the mail
    route decision and subject by reading evidence JSON. A new retained-message
    read model — mailbox, folder scope, immutable message id, conversation id,
    internet message id, sender, recipients, subject, received time, excerpt,
    attachment names and content types, read state — is written at poll time,
    with a Core port, an EF store and a migration.
12. The nav item "Inbox" points at the new workspace. The receipt list keeps
    its content and loses the borrowed name: it becomes "Received items" on a
    route that does not say "intake", since
    `docs/operator-notes.md:378` forbids that word in the interface.
13. **Not in this task, and recorded as still open on UI-10:** classification
    and folder-move actions from message detail, read-only Deleted Items
    search, and attachment-content search. The task delivers the read path and
    says so; UI-10 is not claimed as accepted.

### E. Administrator-managed mailboxes actually drive polling

14. `ApprovedMailbox` gains the Graph identity an administrator must supply for
    a mailbox to be pollable — mailbox id, inbox folder id, sent folder id —
    alongside the address, route scopes and `Approved`/`Disabled` state it
    already carries.
15. `PollApprovedInbox` and the Graph source iterate every `Approved` mailbox
    scoped to `InboundIntake` instead of the single configured one, holding a
    lease and cursor per mailbox — `IApprovedInboxPollStore` already keys on
    `MailboxId`. A `Disabled` mailbox is skipped; its already-retained messages
    stay visible in the viewer, labelled as coming from a mailbox that is no
    longer polled.
16. Moving mailbox identity from deployment configuration into an
    administrator-editable database setting is the same shape of decision as
    ADR-0018, so it gets an ADR rather than being folded in silently.
17. Tenant-side Graph permission for an added mailbox is an operator action
    outside this repository; the surface states the requirement rather than
    implying Pegasus can grant it.

## Verification

Canonical local run in the worktree: `dotnet restore`, `dotnet build
--configuration Release`, focused `dotnet test` per area then the full suite.

Automated, added with the work:

- `Pegasus.Core.Tests` — inline submission returns a processed disposition and
  a real receipt; allocation succeeds for a non-QDOS principal that exists and
  is active, and fails closed for one that does not; a draft correction
  followed by acceptance produces the corrected case; the poll iterates
  multiple approved mailboxes and skips a disabled one.
- `Pegasus.IntegrationTests` — the Upload POST reaches the create screen for a
  readable non-definitive source and the case for a definitive one, against
  the real page pipeline; the acceptance replay guard still holds through the
  new caller; the retained-message read model round-trips through a migration;
  the mail workspace list pages, scopes per mailbox, and reports its freshness.
- The existing suites that assert the QDOS-only refusal are updated to assert
  the principal-existence rule instead of being deleted.

Manual, classified under [engineering's evidence tiers](../engineering.md#required-evidence-tiers): run the Web
app locally, upload a QDOS instruction PDF and a non-QDOS document, and show
the create screen prefilled from extraction in one case and the case reached
directly in the other. Evidence claimed at the tier actually proven — a local
caller, not a deployment.

## Boundaries this task holds

- No new top-level directory, project, store, runtime or deployment unit.
  `Pegasus.Core` keeps business policy and ports; the retained-message read
  model and the Graph mailbox iteration are an Infrastructure adapter behind a
  Core port; Web stays a composition root.
- No Outlook mutation. The viewer reads retained material; it does not move,
  mark, or delete anything in a mailbox, and the read/unread state it shows is
  the retained one.
- No case deletion, no reference reuse, no reopening without a reason.
  Allocation stays fail-closed on incomplete or ambiguous principal identity,
  limits, or standalone Audit evidence.
- `corpus/` and `workspaces/` are untouched.

## Landed so far

Commit `de96d7b`, verified by a clean Release build and 441 passing
`Pegasus.Core.Tests`:

- `Program.cs` composes `ProcessIntakeSubmission` for the Upload page and
  leaves the queue-only `ReceiveIntake` for the automation ingress.
  `Upload.cshtml.cs` routes on the receipt's real decision and the dead
  "is being processed" branch is gone.
- `QdosAlphaCaseActivationPolicy` is deleted. Its four enforcement sites use
  `CasePrincipalCode.Normalize`; `QdosPrincipal.Code` remains for seeds and
  tests. Allocation still fails closed on the principal record inside the
  acceptance transaction (`EfCaseAcceptanceStore.cs:183`).

## Test state after `de96d7b`

`Pegasus.Core.Tests` is green (441). Eleven integration tests fail across
`QdosIntakeWebTests`, `IntakeWebNegativeTests`, `InstructionDraftWebTests`:

- Ten assert the old queued behaviour and now read `OK` where they expected
  `Found`, or `False` where they expected `True`. They are asserting a
  redirect to a staged receipt that the page no longer produces, through
  `IntakeWebDriver.QueuedReceiptId` and `ProcessQueuedAsync` in
  `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs:329-394`, which
  drive `DispatchPendingIntakeWork` by hand. That whole driver path is
  obsolete for the upload caller: processing now happens in the POST. The
  driver needs a second path that reads the processed receipt straight from
  the redirect, and the mailbox-caller tests keep the existing one.
- One is a real defect and not an expectation change:
  `QdosFilenameAndSenderWithoutConfirmingBodyNeedSortingThroughUploadCaller`
  returns `InternalServerError`. The receipt is `NeedsSorting`, so the page
  redirects to `/Cases/Create`, which does not exist yet. `RedirectToPage`
  only builds the result; URL generation happens after the handler returns,
  outside its `catch`, so the missing page surfaces as an unhandled 500 rather
  than the handler's error path. Creating the page closes it — but a
  redirect target that does not resolve should not be able to 500 the upload,
  so the handler needs a guard as well.

## Two findings that change the remaining work

**A hand-keyed case is still blocked by the inspection-address gate.**
`Ext18InspectionAddressPolicy.Evaluate` derives its suggestion from the
receipt's extracted `InstructionReviewField` candidates, and
`InspectionAddressResolutionStore.ResolveAsync` throws
`InvalidOperationException` when there is no suggestion. A non-QDOS upload with
no extracted address therefore has no route to a resolved address and cannot be
accepted; QDOS escapes only because it is image-based. Opening non-QDOS
principals is meaningless without a staff-supplied path — a `SupplyAddress`
decision and a `Supplied` state, recorded with staff provenance.
`docs/requirements.md:421` records the address as kept "when that location is
explicitly supplied or operator-confirmed", and the EXT-18 prohibition is on
inference, not on staff entry, so this is within the settled rule rather than a
change to it.

**The mail viewer needs no Graph credential in Web.**
`docs/requirements.md:716-788` describes UI-10 over *retained* messages
throughout — "shows the full retained message", "only retained messages within
approved mailbox/folder scope". It is a viewer over what polling already
brought in. What it does need is message-level retention that polling does not
persist: sender and subject are re-derived at read time from the mail-route
decision and evidence JSON (`EfIntakeReceiptStore.cs:218-219`), and nothing
retains recipients, an excerpt, attachment names, or read state.

## Known overlap

`NOW.md`'s Next queue still carries "Remove the manual case-acceptance gate and
the `DraftReady` decision, and implement INT-25/CAP-008". The code half of that
landed in `9393c98`: `IntakeDecision.DraftReady` is gone, the receipt counts
exclude receipts that produced a case, and `ProcessQueuedIntake` allocates
without a staff gate. This task takes over what remains of that line's surface
— the acceptance screen and its callers — and its PR removes the line along
with its own claim.

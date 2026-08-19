## Post-implementation report — PLAT-009

### What changed

- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml`:
  - "Current policies" table is now compact: Address / Route scope / State
    (`_StatusChip`, unchanged) / Polling / Version, no `Update` column, no
    embedded `<form>`. Normal row height regardless of estate size.
  - New "Update policies" section (rendered only when mailboxes exist): one
    `<div class="panel form-panel">` per mailbox, `<h3>` naming the address,
    `aria-labelledby` that heading. Field names, hidden `MailboxId`/
    `ExpectedVersion`/`OperationKey`, the `operationKey` fallback computation,
    and all validation are byte-identical to the pre-existing per-row form —
    only the container moved from `<td>` to a panel `<div>`.
  - Deleted the `<aside class="notice">` lede banner (design authority: no
    lede/subtitle). Collapsed the add-panel's two explanatory paragraphs to
    one sentence beside the identity fields it governs. Both moves lose no
    knowledge: `docs/runbook.md`'s "Approved mailbox estate" /
    "Runbook: admitting a new mailbox to the tenant" sections already carry
    the Exchange-tenant-permission and `mailbox_access_denied` material —
    verified present, not added.
  - Route-scope checkbox labels and the table's Route scope column both call
    `OperatorLabels.RouteScope(...)` (added to the shared label map, see
    below), reading "New instructions and Triage mail (Inbox)" instead of
    the banned-word "Inbound Intake (Inbox)".
- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml.cs`: one copy fix —
  `MissingMailboxIdentity`'s error string no longer reads "for inbound
  Intake". No handler, binding, or validation logic changed.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs`: added
  `RouteScope(ApprovedMailboxRouteScope)`, matching the file's existing
  per-enum switch-with-`Humanise`-fallback pattern (simplification-pass
  finding — see plan.md).
- `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs`:
  renamed/rewrote `ThePageStatesThatApprovalGrantsNoExchangeAccess` to
  `ThePageDoesNotDuplicateTheRunbooksTenantPermissionNarration`, asserting the
  narration and `mailbox_access_denied` text are now **absent** from the page
  (moved to the runbook) and the one-sentence hint is present. All other
  assertions in the file were already structure-agnostic and needed no change.

### Commands run and results

```
dotnet build ./Pegasus.slnx -c Release
  → Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test tests/Pegasus.IntegrationTests -c Release \
  --filter "FullyQualifiedName~Mailbox|FullyQualifiedName~Administration"
  → Passed! 56/56, ~2 min.

dotnet test tests/Pegasus.IntegrationTests -c Release --filter "Category=Browser"
  → Passed! 37/37, ~5 min. Includes Browser/AccessibilityTests.cs's theory case
    for "/Administration/Mailboxes" against real seeded data: 0 axe violations,
    exactly one <main> and one <h1>, no inline style attributes.

dotnet test tests/Pegasus.ArchitectureTests -c Release
  → Passed! 97/97 — confirms moving the label into OperatorLabels.cs (Web
    layer) introduced no layering violation.
```

Playwright chromium was already installed by
`scripts/Initialize-LocalDevelopment.ps1` (run for the local-host attempt
below); no separate install step was needed for the Browser suite.

### Visual proof

`pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start` failed on this
workstation with `Database instance '...' exists without completed run
ownership.` — traced to a pre-existing, environment-specific quirk unrelated
to this change: this workstation's `sqllocaldb.exe info <name>` returns exit
code 0 even for a nonexistent instance (only an stderr message says "doesn't
exist"), so `Get-PegasusDatabaseState` in `scripts/PegasusPlatform.ps1`
misreads a brand-new random-GUID run instance as already existing and
`Start-LocalRun` refuses to proceed. Confirmed independent of this branch
(the check runs before any Pegasus code loads). Not fixed here — out of scope
for a page-layout ticket; worth its own ticket if it recurs.

Substituted real, non-fabricated screenshots taken through the same
Playwright/Chromium harness the Browser test suite already uses
(`BrowserTestSupport.StartAsync`, `IntakeWebApplicationFactory` with
`initializeDevelopmentOffline: true`), driving the real rendered page (not a
copied DOM) at 1920×1080 and 1366×900, against a two-mailbox estate (the
seeded row plus one added live through the real Add form) so both the
compact table and multiple edit panels are visible together. Captured via a
temporary test file that was deleted immediately after capture and is not
part of this PR's diff. Both screenshots confirm: normal-height table rows,
data left-aligned and readable, one panel per mailbox below the table, no
lede banner, single-sentence field hint, corrected route-scope wording.

### Simplification pass

Recorded in plan.md under "Simplification pass — 2026-08-20": one applied
fix (route-scope label moved into `OperatorLabels.cs`, converged on by 3 of
4 review lenses) and two considered-and-skipped findings with reasons
(reverting to the in-cell form convention used by Roles/Access would undo
the fix this ticket exists to make; the double `foreach` over the mailbox
list is real but negligible at the estate's documented scale).

### Residual risk

- The `Invoke-LocalDevelopment.ps1` LocalDB-detection bug (above) blocks
  manual local QA on this workstation until fixed separately; Browser-suite
  evidence and the Playwright-harness screenshots substitute.
- Roles/Access Administration pages retain the same in-cell-form shape this
  ticket removed from Mailboxes. No defect is reported against them and
  their forms are materially shorter, so they were left untouched — flagged
  here only for board visibility, not filed as a follow-up ticket.

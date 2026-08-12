# Replace Received Items with Operations

## Summary

Replace the existing plan for a staff-wide `/Operations` workspace. The page
will combine useful operational information with the existing safe
request-management actions. It will not expose the general intake receipt
ledger, manual uploads, email receipts, or mailbox processing.

`User` remains the lowest application role; `/Operations` is available to
`User`, `Engineer`, and `Administrator`. Renaming the system-administration
role to `SuperUser` is outside this task.

## Implementation tickets

### OPS-PAGE-01 — Operations read model

- Add a Core-owned `OperationsWorkspaceProjection` under
  `src/Pegasus.Core/Operations/`.
- Include:
  - active, unexpired Pegasus upload links;
  - active Box file requests;
  - retryable external-work failures;
  - the 50 most recent `Automation`-channel intake receipts, including
    filename, received time, outcome, allocation state, and actual linked Case;
  - summary counts for each section.
- Extend the existing operations EF store in
  `src/Pegasus.Infrastructure/Persistence/EfOperationsStore.cs`.
- Filter API activity by persisted `IntakeSourceChannel.Automation`; exclude
  manual uploads and mailbox receipts at query level.
- Use existing tables and indexes. Do not add a migration, store, or deployment
  unit.

### OPS-PAGE-02 — Canonical `/Operations` page

- Add `src/Pegasus.Web/Pages/Operations/Index.cshtml` and `.cshtml.cs`.
- Authorize all existing staff roles: `User`, `Engineer`, and `Administrator`.
- Present four focused sections:
  - `Attention required` for retryable external-work failures;
  - `Active upload links` for usable Pegasus and Box requests;
  - `Received through API` for recent Automation-channel files and resulting
    Cases;
  - `AI operations` as an informational future-capability panel only.
- Move the existing `/Operations/Requests` handlers into the new PageModel
  unchanged in policy:
  - retry external work;
  - claim, renew, and release Case edit leases;
  - withdraw Pegasus upload links;
  - revoke Box file requests.
- Preserve antiforgery, idempotency keys, reasons, lease enforcement,
  attribution, PRG, and fail-closed error handling.
- API activity remains read-only and requires no approval action.
- The AI panel must state that job requesting and live work viewing are planned,
  not currently available from this screen.

### OPS-PAGE-03 — Retire obsolete list pages

- Delete only these list-page files:
  - `src/Pegasus.Web/Pages/Intake/Index.cshtml`
  - `src/Pegasus.Web/Pages/Intake/Index.cshtml.cs`
  - `src/Pegasus.Web/Pages/Operations/Requests.cshtml`
  - `src/Pegasus.Web/Pages/Operations/Requests.cshtml.cs`
  - `src/Pegasus.Web/Pages/Operations/Email.cshtml`
  - `src/Pegasus.Web/Pages/Operations/Email.cshtml.cs`
- `/Received`, `/Operations/Requests`, and `/Operations/Email` must return `404`;
  add no redirects.
- Preserve `/Received/{id}`, `/Received/{id}/Source`, EVA hand-off, and other
  receipt-specific workflow routes because existing intake and Case workflows
  call them.
- Remove the mailbox retry control with the retired `/Received` list. Inbox
  remains the email workspace.

### OPS-PAGE-04 — Navigation and dashboard

- Replace the `Received items` primary-navigation entry in
  `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` with `Operations` targeting
  `/Operations`.
- Keep `/` titled `Dashboard`; Operations becomes a separate primary
  destination.
- Remove dashboard links targeting `/Operations/Requests`, `/Operations/Email`,
  or `/Received`.
- Keep dashboard metrics, but render a metric without a link when no canonical
  non-receipt destination exists.
- Do not introduce links to hidden receipt filters or rebuild the receipt ledger
  elsewhere.

### OPS-PAGE-05 — Future AI operations contract

- Do not add AI request controls, an AI Viewer, transcript persistence, or
  connector changes in this delivery.
- Update `docs/capabilities.md` to record two future capabilities:
  - a named, extensible job catalogue beginning with the existing
    case-assessment job;
  - an AI Viewer for request lifecycle and, eventually, live work events.
- Update `docs/open-decisions.md` to state that job types, eligibility,
  transcript/event wire format, retention, redaction, and production transport
  remain unresolved before implementation.
- Preserve the current `Features:SendToAi` offline-only composition gate and
  existing Case Assessment controls.

### OPS-PAGE-06 — Documentation truth

- Update `docs/architecture.md` and `docs/design.md` so:
  - `/Operations` owns staff operational actions and focused API activity;
  - `/Received` is no longer a list/workspace;
  - receipt detail routes remain internal workflow callers;
  - Inbox alone owns email display;
  - Operations does not imply browser API access or production AI availability.
- Update affected route maps and implementation maps without changing
  historical evidence or operator statements.

## Test plan

- Core/persistence tests:
  - include only active, unexpired upload requests;
  - include only retryable external failures;
  - include only the latest 50 Automation-channel receipts;
  - exclude manual and mailbox receipts;
  - expose the actual linked Case and allocation outcome;
  - return honest empty states.
- Web tests in `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`:
  - all three staff roles can load `/Operations`;
  - anonymous access is challenged;
  - every inherited mutation retains antiforgery, lease, reason, idempotency,
    attribution, and PRG behaviour;
  - API rows have no approval or mutation controls;
  - AI content is clearly marked as planned;
  - obsolete list routes return `404`;
  - retained `/Received/{id}` workflow routes continue working.
- Update shell, mailbox, intake, image-intake, operator-journey, and
  accessibility tests that currently expect the `Received items` navigation
  entry or `/Received` list.
- Run:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OperationsWebTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
git diff --check
git status --short
```

## Assumptions

- “Admin workers” means ordinary administration staff, including the lowest
  `User` role—not only the `Administrator` system role.
- “API” means the existing Automation/MCP intake channel.
- Recent API activity is bounded to 50 newest receipts with no general receipt
  filters.
- Email and manual intake continue to exist operationally but receive no general
  ledger page.
- AI job requesting and live viewing are documentation-only future capabilities
  in this delivery.

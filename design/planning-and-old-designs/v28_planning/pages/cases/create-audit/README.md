# Create audit

- **Parent:** [Case record](../case-record/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Shared/_CaseDialogs.cshtml`, `src/Pegasus.Web/Pages/Cases/Details.Frame.cs`

Create audit is an Actions-menu item on a Case with a recorded outcome that permits an Audit, not a page of its own.

## Captured states

None. See below.

## Not captured

- The Create audit dialog. The fixture Case does not meet `CanCreateAudit`, so the live page does not render it.

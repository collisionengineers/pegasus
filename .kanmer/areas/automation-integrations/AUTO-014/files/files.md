# Files — AUTO-014

## Changed

| File | +/− | Why |
| --- | --- | --- |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | 83 / 1 | The rendered Create-query-response control and the Case-tab AI jobs panel |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | 115 / 3 | The `ListForSubjectAsync` call and the `CreateQueryResponse` handler |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | 14 / 0 | New nested static class only; nothing reordered |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | 106 / 0 | Two new tests; no existing test touched |

Total 314 insertions, 4 deletions across 4 files.

## Ownership check

`Pages/Mail/**` is MAIL-025's by `waves.md`. MAIL-025 is in `verifying`, **held by
the rule-14 reversal, with no branch in flight** — so no lane is editing it.
Verified against every remote `task/*` branch on 2026-08-29:

```
branches touching Mail/Message.cshtml(.cs):
  origin/task/auto-014-ai-job-callers   <- this lane only
```

This is D19 case 2 — a change in a file no in-flight lane owns — and it is
recorded here rather than done quietly. **MAIL-025's own re-prove must merge
`dev` forward before it re-audits**, or it will audit a stale page.

`src/Pegasus.Web/Presentation/OperatorLabels.cs` is shared by every UI lane. This
change appends one nested static class and reorders nothing, per
`decisions-2026-08-29.md` § Two shared files. PLAT-049 also appends to it, so an
ordinary textual merge conflict is expected and was already resolved once this
session between PLAT-025 and PLAT-049.

## Not touched

`Pages/Operations/**` (PLAT-049, in flight) · `Pages/Administration/**` (PLAT-026,
PLAT-027) · `Pages/Cases/Assessment/**` (ENG-028, in flight) ·
`Pages/Cases/Vehicle|Custody|Tasks|Documents` (CASE-027, in flight) · `Upload*`,
`Uploads/**` (INTK-047) · `Core/Intake` extraction (DELIV-036) · `Core/AiWork`
(**deliberately unchanged** — this ticket supplies callers for what AUTO-011
already built, and adds no port, command or query of its own).

No migration. No new package. No new top-level directory or project. No feature
flag touched.

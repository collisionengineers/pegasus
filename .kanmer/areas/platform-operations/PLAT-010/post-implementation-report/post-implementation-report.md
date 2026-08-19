# Post-implementation report — PLAT-010

## What changed
29 operator-facing Razor pages (plus 3 `.cshtml.cs` code-behind files carrying operator-visible
strings) were brought into compliance with `docs/design/README.md:160-161`: ledes/subtitles
deleted, multi-sentence guidance compressed to one sentence beside its control, `ViewData["Lede"]`
usage and the `_PageHeader` lede slot removed entirely, and banned terms (`intake`, `bounded`,
`projection`, `artifact`, `ingress`, `composed`, `correlation identifier`) fixed wherever found.
Full per-page disposition table is in the ticket's `plan` document.

## Scope changes received mid-task
The operator carved out `Pages/Unidentified/{Index,Details}.cshtml` and
`Pages/{Upload,UploadStatus}.cshtml` / `wwwroot/js/site.js` — separate structural-rebuild tickets
now own those files. Edits already made to `Unidentified/Details.cshtml` and `UploadStatus.cshtml`
were reverted with `git checkout --` before committing anything, and are not in the final diff.
`UploadGroupStatus.cshtml` does not exist in this checkout.

## Internal-identifier leaks (design :168) found and fixed
`ImageIntake/Details.cshtml`'s "Preserved origin" section rendered a raw receipt GUID as link
text, a raw source-receipt token, a raw source hash, and a raw evaluation-revision GUID — all
four removed; "Source channel" now goes through the existing `SourceChannelLabel` map instead of
printing the raw enum.

## Leaks identified but NOT fixed (reported per instruction, not invented)
Two raw-GUID leaks need a model/handler change this copy-only ticket does not make:
- `Administration/Automation/Activity.cshtml:65` — `@record.SubjectId` in the "Subject" column
  (a raw staff GUID; `AutomationActivityRecord` carries no display name).
- `Cases/Shared/_CaseSummary.cshtml:208` — `@approval.ApprovedBy.SubjectId` in "Actor" (same
  shape).
Both are legitimate follow-up findings for a ticket that can touch the query/handler layer.

## Deliberately unchanged
`custody` terminology (`Cases/Custody.cshtml`, "Document/Case custody" headings, "Custody Hash")
is not on the banned-word list and is established, correct domain language — left as-is. "AI" /
"Send to AI" / "Send to Claude" kept as the settled, already-approved control names.

## Tests
- `dotnet build ./Pegasus.slnx -c Release` — 0 warnings, 0 errors.
- `dotnet test tests/Pegasus.IntegrationTests --filter "Category=Browser"` — 37/37 passed
  (includes Playwright AccessibilityTests).
- `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName~WebTests"` —
  132/132 passed, 11 pre-existing skips. 3 failures on first run, all legitimate (asserted text
  this ticket removed/changed) — assertions updated, re-run green.
- `dotnet test tests/Pegasus.Core.Tests` — 684/684 passed.
Nothing could not be run.

## Simplification pass
Dated 2026-08-20: n/a — copy-only diff, no new abstractions, reused existing `_StatusChip`
partial and `SourceChannelLabel` map. Recorded in the plan doc.

## Verification checklist (from the ticket body)
- [x] No page renders a lede/subtitle; `_PageHeader`'s lede slot removed.
- [x] No multi-sentence guidance paragraph remains beside any control on the pages this ticket
      touched; consequence guidance is one sentence.
- [x] Banned-terms grep clean over the pages this ticket touched (two identifier leaks reported
      above are GUID leaks, not the literal banned-word list, and need a handler change out of
      this ticket's scope).
- [x] Browser/a11y suites green.

## PR
Branch `task/plat-010-copy-strip`, 5 commits, targets `dev`. Not merged.

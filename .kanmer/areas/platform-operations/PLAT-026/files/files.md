## Files owned by PLAT-026

Allocation source: `.kanmer/groups/EPIC-011/decisions-2026-08-29.md:31` —
`| PLAT-026 | Wave 2 · I3 | Pages/Administration/Mailboxes.*, MailCategories.* |`.

- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml`
- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml`
- `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml.cs`
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — append-only, inside a new
  nested `MailSettings` static class at the end of the file. Shared with
  PLAT-025/PLAT-027/PLAT-028/AUTO-006 and other wave-2 lanes this wave; never
  reorder existing members. Verified `101 added / 0 deleted` against
  `origin/dev`.
- `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs`
- `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryAdministrationWebTests.cs`

Seven files. `git diff --name-only origin/dev...HEAD` returns exactly this set.

## Explicitly not touched (other lanes' files, same wave)

- `Pages/Administration/Configuration.*` — PLAT-025.
- `Pages/Administration/Accounts/**`, `Access/**`, `Roles/**` — PLAT-027.
- `Pages/Administration/Principals/**`, `Organizations/**` — PLAT-028.
- `Pages/Administration/Automation/**` — AUTO-006.
- `Pages/Administration/Shared/_AdminNav.cshtml`, `Pages/Administration/Index.cshtml`,
  `wwwroot/css/site.css`, `wwwroot/js/site.js` — PLAT-029, read-only.
- `docs/design/test-ui/catalogue.json` — **PLAT-029**, per `waves.md:9`
  ("PLAT-029: … `docs/design/test-ui/catalogue.json` structural edits").

## Correction (2026-08-29, remediation round 2)

An earlier version of this document listed `docs/design/test-ui/catalogue.json`
as owned by PLAT-026. That was a self-issued grant, not the epic's allocation:
`waves.md:9` gives the file to PLAT-029, which is in flight. The lane's edit to
it has been reverted; the file is byte-identical to `origin/dev` again and no
longer appears in this branch's diff.

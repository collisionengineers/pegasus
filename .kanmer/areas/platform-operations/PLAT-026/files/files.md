## Files owned by PLAT-026

- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml`
- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml`
- `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml.cs`
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — append-only, inside a new
  nested `MailSettings` static class at the end of the file. Shared with
  PLAT-025/PLAT-027/PLAT-028/AUTO-006 and other wave-2 lanes this wave; never
  reorder existing members.
- `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs`
- `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryAdministrationWebTests.cs`
- `docs/design/test-ui/catalogue.json` — structural entries only for the two
  pages above; no snapshot regeneration (orchestrator-owned, once per merge).

## Explicitly not touched (other lanes' files, same wave)

- `Pages/Administration/Configuration.*` — PLAT-025.
- `Pages/Administration/Accounts/**`, `Access/**`, `Roles/**` — PLAT-027.
- `Pages/Administration/Principals/**`, `Organizations/**` — PLAT-028.
- `Pages/Administration/Automation/**` — AUTO-006.
- `Pages/Shared/_AdminNav.cshtml`, `wwwroot/css/site.css`, `wwwroot/js/site.js`
  — PLAT-029, read-only.

## Files this ticket owns (EPIC-011 D16, Wave 2 lane I2)

- `src/Pegasus.Web/Pages/Administration/Configuration.cshtml`
- `src/Pegasus.Web/Pages/Administration/Configuration.cshtml.cs`
- A new/updated web test file under `tests/Pegasus.IntegrationTests/` for this
  page's rendered markup, handler wiring, and non-administrator denial.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — append-only, inside a
  scoped location, never reordering existing members (shared file, several
  wave-2 lanes touch it this wave).
- `docs/design/test-ui/catalogue.json` — structural edit only, to the existing
  `administration-configuration--default` entry, if its description is now
  inaccurate. No new snapshot capture.

## Explicitly NOT touched (owned by parallel wave-2 lanes)

- `_AdminNav.cshtml`, `site.css`, `site.js`, any other `Pages/Shared/**` —
  PLAT-029.
- `Pages/Administration/Accounts/**`, `Access/**`, `Roles/**` — PLAT-027 (I1).
- `Pages/Administration/Mailboxes.*`, `MailCategories.*` — PLAT-026 (I3).
- `Pages/Administration/Principals/**` — PLAT-028 (I4).
- `Pages/Administration/Automation/**` — AUTO-006 (I5).
- `Pages/Administration/Index.cshtml`, `Pages/Administration/
  AdministrationPageModel.cs`, `Pages/Administration/Organizations/**` — not
  named as this ticket's files; left untouched.

## Out-of-scope backend gap (reported, not built here)

- A new Core port/config surface for administrator-configurable instruction/
  image completeness policy and a chase-interval setting, plus its migration —
  belongs to a new ticket (see plan's disposition section).

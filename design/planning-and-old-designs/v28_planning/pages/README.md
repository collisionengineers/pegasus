# v28 planning — pages

One folder per page of the application as mocked in [`../current/`](../current/README.md).
A page that is reached from another page is a subfolder of it (Create audit
and the EVA handoff under the Case record; the connector consent screen
under Sign in). Each page folder holds `dialogs/`, `states/` and `panels/`
for its own pieces where the page is complex enough to warrant them, plus
`how-it-works.md` — every one read from the live source on 18 September
2026, this round having proposed no changes.

- [Work Centre](work-centre/README.md) — [how it works](work-centre/how-it-works.md)
- [Cases](cases/index/README.md) — [how it works](cases/index/how-it-works.md)
  - [Case record](cases/case-record/README.md) — [dialogs](cases/case-record/dialogs/README.md) · [states](cases/case-record/states/README.md) · [panels](cases/case-record/panels/README.md) · [how it works](cases/case-record/how-it-works.md)
    - [EVA handoff](cases/case-record/eva-handoff/README.md) — [how it works](cases/case-record/eva-handoff/how-it-works.md)
    - [Create audit](cases/create-audit/README.md)
  - [Create Case](cases/create-case/README.md) — [how it works](cases/create-case/how-it-works.md)
  - [Triage](cases/triage/README.md) — [how it works](cases/triage/how-it-works.md)
  - [Unidentified](cases/unidentified/README.md) — [how it works](cases/unidentified/how-it-works.md)
- [Vehicle images](image-intake/README.md) — [how it works](image-intake/how-it-works.md)
- [Inbox](inbox/README.md) — [how it works](inbox/how-it-works.md)
- [Upload](upload/README.md) — [how it works](upload/how-it-works.md)
- [Search](search/README.md) — [how it works](search/how-it-works.md)
- [Operations](operations/README.md) — [how it works](operations/how-it-works.md)
- [Administration](administration/README.md) — [how it works](administration/how-it-works.md)
  - [Staff accounts & roles](administration/accounts/README.md) — [how it works](administration/accounts/how-it-works.md)
    - [Glass's credential](administration/glass/README.md) — [how it works](administration/glass/how-it-works.md)
  - [Contacts](administration/contacts/README.md) — [how it works](administration/contacts/how-it-works.md)
  - [Workflow configuration](administration/configuration/README.md) — [how it works](administration/configuration/how-it-works.md)
  - [Mail settings](administration/mailboxes/README.md) — [how it works](administration/mailboxes/how-it-works.md)
  - [Valuation presets](administration/valuation-presets/README.md) — [how it works](administration/valuation-presets/how-it-works.md)
  - [Service health](administration/health/README.md) — [how it works](administration/health/how-it-works.md)
  - [Logs](administration/logs/README.md) — [how it works](administration/logs/how-it-works.md)
    - [Action logs (legacy route)](administration/action-logs/README.md) — [how it works](administration/action-logs/how-it-works.md)
  - [Reports](administration/reports/README.md) — [how it works](administration/reports/how-it-works.md)
  - [AI jobs](administration/ai-jobs/README.md) — [how it works](administration/ai-jobs/how-it-works.md)
  - [Automation & AI](administration/automation/README.md) — [how it works](administration/automation/how-it-works.md)
- [Sign in](sign-in/README.md) — [how it works](sign-in/how-it-works.md)
  - [Connector consent](sign-in/connector-consent/README.md) — [how it works](sign-in/connector-consent/how-it-works.md)
- [Error and status-code family](errors/README.md) — [how it works](errors/how-it-works.md)

## Not given a mockup surface, and why

- **`Pages/Account/SignOut.cshtml`** — no markup; a handler only. Documented in [Sign in](sign-in/how-it-works.md).
- **`Pages/Notifications.cshtml`** — no markup; the bell's two handlers only. The dialog it drives is on every shell page.
- **`Pages/Triage/Index.cshtml`, `Unidentified/Index.cshtml`, `PreCaseImages/Index.cshtml`** — dead routes (`RedirectPermanent`/`NotFound`); the live lists are Cases-index tabs, captured in [Cases](cases/index/README.md) and cross-referenced from [Triage](cases/triage/README.md)/[Unidentified](cases/unidentified/README.md).
- **`Pages/Intake/Source.cshtml`, `Asset.cshtml`, `Image.cshtml`** — raw file-serving GET routes with no UI; documented in [Vehicle images](image-intake/how-it-works.md).
- **`Pages/Cases/Vehicle.cshtml`, `Workflow.cshtml`, `Tasks.cshtml`, `Closure.cshtml`, `Custody.cshtml`** — POST-only mutation endpoints with no markup, every `OnGet()` returning `NotFound()`; they are Case-record forms' own targets, not separate screens. Documented in [Case record](cases/case-record/how-it-works.md).
- **`Pages/Cases/Assessment/Suggestions.cshtml`** — deliberately unrouted (no `@page` directive), an inert design-only preview per `docs/design/README.md`'s deferred-surfaces carve-out. Not captured as a live flow.
- **`Pages/Cases/Documents/Download.cshtml`, `Documents/Export.cshtml`** — file-stream handlers with no rendered page; the Download/Export affordances they serve are captured as links within the Case record's Files/Report sections.
- **`Pages/Integrations/Glass/Callback.cshtml`** — no page of its own; every outcome hands the Case record's Estimate section back to the working set, or bounces through `_GlassBounce`/`_GlassReturn`. Documented in [Case record](cases/case-record/how-it-works.md).

## Genuine capture ambiguities

See [`../current/v28-notes.md`](../current/v28-notes.md) §6 for the full
lettered sign-off list — every item below concerns *how to capture* the live
application, not a design choice.

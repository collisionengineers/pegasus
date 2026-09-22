# v28 planning: pages

One folder per page of the application as captured in [`../current/`](../current/README.md).
A page that is reached from another page is a subfolder of it. Each folder's
`README.md` is generated from the capture by `current/v28-build/pagedocs.mjs`
and lists the page's captured states, their live routes, screenshots at three
widths, and what could not be captured. `how-it-works.md` is read from the
live source (`origin/dev` `904903fd1`, 18 September 2026); this round proposes
no changes.

- [Work Centre](work-centre/README.md) — [how it works](work-centre/how-it-works.md)
- [Cases](cases/index/README.md) — [how it works](cases/index/how-it-works.md)
  - [Case record](cases/case-record/README.md) — [dialogs](cases/case-record/dialogs/README.md) · [states](cases/case-record/states/README.md) · [panels](cases/case-record/panels/README.md) · [how it works](cases/case-record/how-it-works.md)
    - [EVA handoff](cases/case-record/eva-handoff/README.md) — [how it works](cases/case-record/eva-handoff/how-it-works.md)
    - [Create audit](cases/create-audit/README.md)
  - [Create Case](cases/create-case/README.md) — [how it works](cases/create-case/how-it-works.md)
  - [Triage](cases/triage/README.md) — [how it works](cases/triage/how-it-works.md)
  - [Unidentified](cases/unidentified/README.md) — [how it works](cases/unidentified/how-it-works.md)
- [Image intake](image-intake/README.md) — [how it works](image-intake/how-it-works.md)
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

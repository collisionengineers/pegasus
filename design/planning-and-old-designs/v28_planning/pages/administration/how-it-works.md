Read from the live source on 18 September 2026.

## What this page does not show

- No lede or subtitle under the "Administration" heading; the three group
  headings (People and access, Configuration, Operations and oversight) and
  the card grid name themselves.
- No card for Contacts' role types, no counts on any card — every card is a
  glyph, a title and one description sentence, nothing else
  (`docs/design/README.md`'s "a field is a label and a control, nothing
  more" economy rule applied to a link card).
- Automation & AI's card and rail entry are both absent, not disabled, when
  the deployment does not compose an automation client or AI channel
  connector (`Model.AutomationComposed` / `_AdminNav`'s `automationComposed`
  check spans `Mcp.AutomationClientRegistry` and
  `Core.AiWork.IAiChannelConnectorStore`).

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Index.cshtml` | The hub's three card groups |
| `Pages/Administration/Index.cshtml.cs` | `AutomationComposed` |
| `Pages/Administration/Shared/_AdminNav.cshtml` | The rail nav shared by all thirteen routes: three groups, area order, the Automation & AI composition gate |
| `Presentation/OperatorLabels.cs` (`Nav`, `Admin` classes) | Area names shared between the hub cards and the rail |

## Behaviours

### Nav area order (source of the `ad-area` switcher order)

`_AdminNav.cshtml` groups its ten links (the hub itself has no rail entry):

1. **People and access** — Staff accounts & roles, Contacts
2. **Configuration** — Workflow configuration, Mail settings, Valuation
   presets
3. **Operations and oversight** — Service health, Logs, Reports, AI jobs,
   Automation & AI (composed only)

This mockup's `ad-area` select and admin-nav panel use exactly this order;
the hub's own three card groups follow the identical grouping and order
(Contacts' card sits second in group one instead of first, matching the live
`Index.cshtml`).

### Thirteen live routes, eleven `ad-area` values

`ActionLogs.cshtml.cs` is `RedirectPermanent("/Administration/Logs" +
Request.QueryString.Value)` — a legacy address with no content of its own,
folded into the Logs area's "Action logs" tab. `Glass/Index.cshtml` sets
`ViewData["AdminArea"] = "accounts"` and is reached only as
`?staffId=<guid>` from an Accounts row or its settings dialog; it has no rail
entry and is captured as a dialog within the `accounts` area. Every other
live `.cshtml` maps one-to-one to an `ad-area` value.

### Access gate

Every Administration `PageModel` carries `[Authorize(Policy =
StaffRoleNames.Administrator)]`; the cookie scheme's `AccessDeniedPath` is
`/Account/AccessDenied`, which reads the eyebrow "Administration" and the
sentence "Administration is available to Administrators only."
(`OperatorLabels.Shell.AdministrationDenied`) for any `returnUrl` starting
`/Administration`. The rail itself hides the "Manage" label and the
Administration link for a non-Administrator (`_Layout.cshtml`'s
`isAdministrator` check). The mockup reproduces both: the whole
`role:Administrator` content block is absent for `role=Engineer|User`, and
that role instead sees this same eyebrow/heading/sentence.

## Things the FRD does not settle

- `docs/design/README.md`'s own marks table lists `accounts.png`,
  `configuration.png`, `mailboxes.png`, `automation.png` (and
  `principals.png`) as in use on "the Administration area panel heads", but
  no live `.cshtml` under `Pages/Administration/` references
  `images/marks/` — only the rail brand lockup does. Whether the marks are
  simply not yet wired into the hub cards, or the design document is stale,
  is not settled by anything under `Pages/`. Sign-off item A.

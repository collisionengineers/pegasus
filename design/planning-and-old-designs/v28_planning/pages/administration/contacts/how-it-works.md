Read from the live source on 18 September 2026.

## What this page does not show

- No lede under "Contacts" beyond the one meta sentence naming what the
  directory holds (Principals, claim sources, repairers, storage and third
  party engineers).
- The Principal-only sections on Edit (Principal details, Case guidance,
  Principal settings, Report generation, Default inspection location,
  Provider API, Replace principal code) render only when
  `Model.Principal is not null` — a non-Principal contact never sees them.
- An inactive Principal shows one line, "This principal is inactive. Change
  the settings on its successor.", instead of the settings panels.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Contacts/Index.cshtml` | Directory table, toolbar filters, Add-contact type picker, the create dialog and its "possible existing contacts" match step |
| `Pages/Administration/Contacts/Index.cshtml.cs` | `RoleLabel()`, `Contacts` query, `Create`/`FindMatches`/`ChooseExisting`/`Cancel` handlers |
| `Pages/Administration/Contacts/Edit.cshtml` | Organisation fields, contact-type checkboxes, Principal-only sections, Linked principals |
| `Pages/Administration/Contacts/Edit.cshtml.cs` | `Save`, report-generation/location/credential/`Replace` handlers |
| `Core/Cases/ContactDirectory.cs` | `ContactRole`, `ContactSort` enums |

## Behaviours

### Directory

Toolbar: Name or contact search, Type select (all five roles), Sort select
(Name/Type/Last case), auto-submitting `<form method="get">`. Table columns:
Name, Type (every role the contact holds, comma-joined), Contact person,
Email, Phone, Last case, Status chip, Open. "Add contact" opens a dialog of
five role cards; picking one opens the create dialog for that role, which
first offers "Check existing contacts" (name-match search) before creating a
new organisation outright — the same physical premises should not become two
directory rows.

### Edit — organisation fields

Name, Contact person, Email, Telephone, Address, Postcode, then the same
five contact-type checkboxes as the create dialog (an organisation may hold
several types at once), Active.

### Edit — Principal sections

Principal details (code, inspection mode); Case guidance (Guidance for new
Cases shown to staff opening a Case for this Principal or Claim source,
and a private Notes on every Case field, both read-only elsewhere);
Principal settings (accepted e-mail identities, read-only); Report
generation (Pegasus vs EVA route, and for EVA a further ZIP/manual-API/
automatic-API-at-Review choice, plus report recipients); Default inspection
location; Provider API (submission endpoint, credential state, Generate/
Reset/Pause/Resume/Revoke); Replace principal code, a danger action in its
own dialog that names the allocated-case count and keeps every existing
Case's own reference (FRD-01 — see `CLAUDE.md`'s "never reuse a Case
reference" rule).

### Linked principals

For every *non*-Principal role a contact holds, a role-scoped checklist of
every Principal on file lets the operator record which Principals that
Repairer/Claim Source/Storage/Third Party Engineer works with. Not shown for
a Principal contact itself ("Principal contacts cannot link to themselves").

## Things the FRD does not settle

- Nothing found while reading this page in isolation.

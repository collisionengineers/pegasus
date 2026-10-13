# Contact — how it works

Read from the live source on 13 October 2026 (`origin/dev` `5d2338708`).

## What this page does not show

- That two of its inputs do nothing. A changed **Principal code** is ignored by the save, because the code is written only when the Principal is created and **Replace code** is the real route. A changed **Inspection mode** is also ignored after creation.
- Where a Principal panel's refusal will appear. Every Principal panel's refusal lands in the **Principal settings** panel's summary, not in the panel that was saved.
- Where a save lands. **Save contact** returns to the Contacts list. Every Principal panel's save reloads the contact at the top of the page, with the notice there and the saved panel far below.
- A long Notes on every Case. The box is four rows, so a seeded 1,300-character note scrolls inside it.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-04 Contacts administration](../../../../../../docs/frd/frd-04-parties-accounts-and-access.md#contacts-administration) | The contact's fields; the Principal section (route domains, default inspection location, default fee, salvage matrix, report sending rules, Principal API); Notes on every Case for Principal and Claim Source only; linked Principals for the other types |
| [FRD-17 Contacts](../../../../../../docs/frd/frd-17-administration-workspace.md#contacts) | Report generation, then Report sending, then Salvage matrix, and what each panel holds |
| [FRD-01](../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity) | A code changes only by replacement; a Case reference is never reused |
| [v39 notes](../../../../v39_planning/current/v39-notes.md#6-sign-off-list) | Open items T and U on the Report sending panel |

## Source

| Layer | File | Owns |
| --- | --- | --- |
| Web | `Pages/Administration/Contacts/Edit.cshtml` | The contact form, the Contact types grid, Principal details, Case guidance, Linked principals, then for an active Principal: Principal settings, Report settings, Report sending, Salvage matrix, Default inspection location, Principal API, Replace code |
| Web | `Pages/Administration/Contacts/Edit.cshtml.cs` | `Save` (to the list), `UpdateReportSettings`, `UpdateReportSending`, `UpdateSalvageMatrix`, `UpdateLocation`, the credential handlers (each back to the contact), `Replace` (to the list), the refusal sentences |
| Web | `wwwroot/js/contacts-edit.js` | Shows and hides sections as type boxes are ticked; adds and removes rule rows, addresses and salvage bands; the location's physical fields |
| Web | `Presentation/OperatorLabels.cs` | `PrincipalAdministration` labels and hints, `ConditionKind` order |
| Core | `Cases/ContactDirectory.cs` | Record, roles, limits (notes 2,000, guidance 4,000) |
| Infrastructure | `Persistence/EfContactDirectoryAdministration.cs` | Save: roles replaced from the ticks; Principal created with code and inspection mode only when first ticked; a contact holding a Principal cannot lose the type |
| Infrastructure | `Persistence/EfCaseWorkspaceStore.cs` | Guidance for new Cases applied to a new Case of that Principal or Claim Source |

## Behaviours

### The contact form

The panel head shows the name, "One organisation may have several contact types." and the Active chip. The form has Organisation name, Contact person, Phone, Email, Address and Postcode. Below them are **Contact types**, a grid of five checkboxes, and **Principal details**: Principal code and Inspection mode, shown while Principal is ticked. **Case guidance** (Guidance for new Cases and Notes on every Case, four rows each) shows while Principal or Claim Source is ticked. **Linked principals** has one subheading per non-Principal type ticked, each with a checkbox for every active Principal. The form ends with Active, **Save contact** and Cancel.

### Principal panels

They show only for a contact with a Principal, in this order:

1. **Principal settings**: the code, the chip, Accepted e-mail identities, and the refusal summary for every panel.
2. **Report settings**: Default fee (£) and **Save report settings**.
3. **Report sending**, as v39 captured it.
4. **Salvage matrix**.
5. **Default inspection location**.
6. **Principal API**.
7. **Replace code**, a danger button opening a native dialog.

An inactive Principal shows "This principal is inactive. Change the settings on its successor." instead.

### Status lines

- "The contact was saved." (on the list)
- "The principal's report settings were updated."
- "The principal's report sending rules were updated."
- "The principal's salvage matrix was updated."
- "The principal's default inspection location was updated."
- "The principal credential was updated."

## Things the FRD does not settle

- Guidance for new Cases is live, but FRD-04 does not name it (item R).
- FRD-17 calls the fee panel "Report generation". The live heading has been "Report settings" since EVA was removed on 7 October 2026 (commit `a85f745e3`).
- Where a panel's save lands, and where its refusal shows (item N).

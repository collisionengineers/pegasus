# Contacts — how it works

Read from the live source on 13 October 2026 (`origin/dev` `5d2338708`).

## What this page does not show

- Which Principals a Repairer, Claim Source, Storage or Third Party Engineer works with. The links are on the record, but the list has no column for them.
- A Principal's code. The list shows only the organisation name.
- How many contacts there are of each type. A row's type can only be read from the comma-joined Type cell.
- A contact's address or postcode.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-04 Contacts administration](../../../../../docs/frd/frd-04-parties-accounts-and-access.md#contacts-administration) | The list's fields, filters and sorting; Add contact asks for the type first; matching names are suggestions only; no automatic merge; a duplicate name or code fails |
| [FRD-17 Contacts](../../../../../docs/frd/frd-17-administration-workspace.md#contacts) | One directory for the five types; the list filters 300 ms after the last keystroke, with Apply for no-script use; the phone rule |
| [Design authority](../../../../../docs/design/README.md#no-explanatory-copy-and-page-economy) | No lede; filters are dropdowns, not pill tabs |

## Source

| Layer | File | Owns |
| --- | --- | --- |
| Web | `Pages/Administration/Contacts/Index.cshtml` | Toolbar, the one table, the type tiles, the New {type} dialog and its match step |
| Web | `Pages/Administration/Contacts/Index.cshtml.cs` | `RoleLabel`, the list query, `FindMatches`, `ChooseExisting`, `Create`, the refusal sentences |
| Web | `wwwroot/js/site.js` | `data-auto-submit` (select change, search after 300 ms), backdrop dialogs |
| Core | `Cases/ContactDirectory.cs` | `ContactRole`, `ContactSort` (Name, Type, LastCase), `ContactDirectoryRecord`, length limits |
| Infrastructure | `Persistence/EfContactDirectoryAdministration.cs` | The list query (the type filter matches any type held), save, duplicate name and code |

## Behaviours

### The list

The page header is Administration / Contacts. Below it is a panel headed "Contacts", with the lede "Principals, claim sources, repairers, storage and third party engineers." and **Add contact**. The toolbar has "Name or contact" search, Type (All types and the five types), Sort (Name, Type, Last case) and Apply, and it filters as the operator types.

One table follows. Its columns are Name, Type (every type the contact holds, comma-joined), Contact person, Email, Phone, Last case, Status (Active or Disabled) and **Open**. When nothing matches, the page shows "No contacts found.".

### Add contact

**Add contact** opens a dialog of five type tiles. Choosing a tile reloads the page with `createType`, which opens "New {type}". That dialog has Organisation name, Contact person, Email, Telephone, Address and Postcode. A Principal also has Principal code and Default inspection. Any other type also has Linked principals: a four-row multi-select with "Optional. Select every Principal this {type} works with.". The sentence "Enter the details for this {type}. You can add more types later." sits above the fields.

**Check existing contacts** lists the name matches under "Possible existing contacts", each with **Use this contact**. That reopens the dialog as "Add {type}", with "Adding {type} to {name}. Existing types, linked principals and settings are retained.". Saving adds the type to that contact and returns to the list with "The contact was saved.".

### Refusals

The page refuses with one of these:

- "A contact with that name already exists. Choose it from the matches instead."
- "That principal code is already in use."
- "Check the selected linked principals."
- "This contact changed. Choose it again."
- "This form was already used for a different change."
- "This contact already has that type."
- "Telephone must be digits."

## Things the FRD does not settle

- Organisation names are unique across the whole directory. FRD-04 states this as a refusal, not as a rule about types. With one type per contact, a firm that is both a Repairer and Storage needs two contacts, and so two names (item G).
- Whether the list groups contacts by type or only filters them, and which columns each type has (items A and B).

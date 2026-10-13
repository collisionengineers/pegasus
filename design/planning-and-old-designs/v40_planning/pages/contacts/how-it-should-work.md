# Contacts — how it should work

Proposed on 13 October 2026 and settled the same day. Each line beginning "Open:" names its lettered item, whose outcome is in [v40-notes.md](../../current/v40-notes.md#6-sign-off-list).

The operator ruled on 13 October 2026 that every contact has exactly one type, chosen when it is created, and that the list is organised by type, with columns that fit each type and only fields that already exist.

- D1. The list shows one panel per type, in the order Principals, Claim Sources, Repairers, Storage, Third Party Engineers. Search runs across every panel, Type narrows the list to one panel, and a type with no matching contact is absent. Open: A (or one type at a time).
- D2. Each panel's columns:
  - Principals: Principal code, Name, Contact person, Email, Phone, Last case, Status.
  - Claim Sources and Third Party Engineers: Name, Contact person, Email, Phone, Linked principals, Last case, Status.
  - Repairers and Storage: the same, with Postcode before Linked principals.

  Every row ends with **Open**. Open: B.
- D3. Sort is Name or Last case, within each panel. Open: C.
- D4. The panel carries no lede. Open: D.
- D5. "No contacts found." shows when no panel has a match.

## Documentation impact when the FRD is written

- FRD-04 *Contacts administration*: "lists every external organisation with its type" becomes the grouped list with its per-type columns, and one type per contact.
- FRD-17 *Contacts*: how the list is organised.

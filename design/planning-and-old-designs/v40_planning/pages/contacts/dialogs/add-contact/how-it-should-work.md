# Add contact — how it should work

Proposed on 13 October 2026 and settled the same day ([v40-notes.md](../../../../current/v40-notes.md#6-sign-off-list)).

## The action

**Add contact** on the Contacts list. The type is chosen first and is fixed from then on (operator ruling, 13 October 2026).

## The dialog

- D1. The first dialog is unchanged: five type tiles, then Cancel.
- D2. New {type} keeps its fields: Principal code and Default inspection for a Principal, and Linked principals for the other types. The sentence "Enter the details for this {type}. You can add more types later." is removed, with no replacement. Open: E.
- D3. The existing-contact route is removed: no "Add {type}", no "Adding {type} to {name}. …" and no **Use this contact**. Open: E.
- D4. **Check existing contacts** lists each match as "{name} · {type}", with **Open** to go to that contact. Open: F (or remove the step).
- D5. The duplicate-name refusal needs a new sentence, and names may need to repeat across types. Open: G.

## Where this lands

| Page | Entry |
| --- | --- |
| Contacts | Add contact, New {type}, Check existing contacts |
| Contact | Opened from a possible match |

## Documentation impact when the FRD is written

FRD-04: "It then creates that role on a new identity or adds it to an existing identity the operator picks" becomes: it creates a contact of that type, and name matches can be opened but never merged or extended.

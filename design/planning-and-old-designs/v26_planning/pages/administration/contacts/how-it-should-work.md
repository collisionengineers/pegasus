# Contacts — how it should work

Decided with the operator on 13 September 2026.

## Notes on every Case

- The **Principal record** and the **Claim source record** each gain a text field, **Notes on every Case**, edited here by an Administrator (Principal settings dialog; Contact dialog for a Claim source). Saving records the change in the Action log like any other save.
- Every Case of that Principal, and every Case from that Claim source, shows the record's current notes read-only on its Overview: "Principal notes" and "Claim source notes", locked like the identity fields. The Case shows the record's current text; a change to the record shows on every Case at once and is not versioned per Case.
- A record with no notes shows nothing on the Case (absent, not empty).
- Beside each, the Case keeps its own editable "Principal notes · this Case" and "Claim source notes · this Case" (the v24 case-specific notes), saved with the Case.

## Where this lands

| Page | Entry |
| --- | --- |
| Case record | Overview › Principal column: Principal notes (record, locked), Principal notes · this Case, Claim source notes (record, locked, follows the chosen source), Claim source notes · this Case |
| Administration › Contacts | Notes on every Case on the Contact dialog for a Claim source, and on Principal settings |

## Open

- Whether Repairer, Storage and other contact types also carry notes shown on the Cases that use them. Default: only Principal and Claim source.

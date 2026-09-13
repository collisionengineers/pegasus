# Unidentified — how it should work

Decisions landing on Unidentified from other pages' planning. Unidentified's own planning is still to come.

| From | Decision | Where on this page |
| --- | --- | --- |
| Received file D2 | **Close with reason** absorbs the old Block: a readable item that must not become a Case is closed here with its reason. Blocked disappears as an operator term. | Close with reason |
| Received file D2 | Registration reading results with Dismiss, Register images and Open the Triage move here from the received-file page, where the item's material is | the record's panels |
| Received file D2 | "View received item" becomes "Open message" or "Open file" (the original in the viewer) | header actions |
| Received file D2 | Link to Case links the message or file, never a receipt | Link to Case |
| Work Centre D3 | an Unidentified item is due received + Unidentified target (default 0 days) | the Work Centre row; no change here |

**Definition change (Received file D5).** Unidentified is everything a person has to sort: material whose identity, meaning, ownership or destination cannot be established, *and* readable material that must not become a Case. Close with reason is the one refusal in the system. This is a glossary change for CONTEXT.md and FRD-02 when the FRD is written.

**Close with reason** is free text (decided 13 September). A closed item is a resolved Unidentified item: it keeps its U-reference, is never deleted, sits under the Closed filter indefinitely, and can be reopened (existing Core reopen, "Resolved to Open" in its history).

## An item that could not be read

Decided 13 September: an attachment or upload that processing could not read becomes an Unidentified item with the reason "Could not be read" and the file kind, so it lands in the work list and ages like any other. What the person can do with it:

| Action | When | What it does |
| --- | --- | --- |
| Open file | always | opens the original in the viewer; a scan the OCR could not read is often perfectly readable by a person |
| Open message | it came by e-mail | the Inbox message, with Reply |
| Request again | it came by e-mail or a public upload link | composes a reply to the sender, or issues a new upload link, asking for the file again; the item stays open until the replacement arrives |
| Link to Case | the person can tell which Case it belongs to | attaches the original to that Case's Files as evidence, even though nothing was extracted from it; resolves the item |
| Create case | the person can read an instruction in it | Create case seeded with whatever was read, the rest typed; resolves the item |
| Register images | it is a photograph set | as now |
| Close with reason | none of the above | resolves the item with the free-text reason; reopenable |

Retry processing is not offered here: it is a technical action on Operations and the Intake log. If a retry succeeds the item is resolved automatically with "Processed on retry" and the outcome it produced.

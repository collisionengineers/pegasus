# Contacts — how it works (notes so far)

Read from the live source on 13 September 2026 (`src/Pegasus.Web/Pages/Administration/Contacts/Index.cshtml`, `Edit.cshtml`; Principals are managed on the same Contacts area; `src/Pegasus.Core/ReferenceData/ReferenceDataModels.cs`). Only the part touched by the notes decision is written up.

- A Principal record holds its code, name, roles, sending domains, inspection default, EVA settings and API key. A Contact record (Repairer, Storage, Claim source, Salvage agent, Third Party Engineer) holds name, type, contact person, telephone, e-mail, address and VAT status.
- Neither record has a notes field. Nothing from a Principal or Claim source record is shown as guidance on a Case.
- The v24 Case mockup showed "Generic notes" from the Principal record and from the Claim source record (marked as applying to every Case) beside "Case-specific notes" for each; v25 and the first v26 dropped them.

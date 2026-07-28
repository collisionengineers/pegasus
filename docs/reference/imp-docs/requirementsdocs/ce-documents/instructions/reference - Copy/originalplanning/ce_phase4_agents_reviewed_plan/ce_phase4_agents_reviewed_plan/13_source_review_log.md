# Source Review Log

## Uploaded source files reviewed

### `phase_ai_agents.md`

Original Phase 4 plan. It proposed Inbox Triage, Missing Information, Valuation/Uplift, Engineer Support and Continuous Learning agents. This revised pack keeps the workflow intent but changes the implementation model to automation-first with controlled agents only where justified.

### `claudechat.md`

Prior CE Document Mapper development transcript. Key context used:

- CE Document Mapper is a Windows Python/Tkinter desktop app.
- It imports PDF, DOC/DOCX, MSG/EML and images.
- It uses provider presets stored in `providers.json`.
- It has batch mode and Engineer Report/Audit Mode.
- UX should remain simple; avoid major UI concepts where not required.
- Multiple later fixes reinforced reliability, deterministic parsing and avoidance of brittle Outlook COM behaviour.

### `handover.docx`

Operational handover. Key context used:

- Job sheet is Excel Desktop/VBA and stored in Microsoft/SharePoint context.
- Existing folder creator assumes a shared `Z:` drive.
- Job sheet columns include date, VRM, principal, client, vehicle, missing, due and notes.
- CE Document Mapper stores `app_settings.json` and `providers.json` in the user Documents folder.
- Audit Mode/Engineer Report overwrites fields from the instruction and highlights overwritten fields in red.
- Existing AI subscriptions include Copilot QRD and Claude projects.
- digital@ has delegated access to shared inboxes, but Outlook Classic delegated-access syncing issues have been observed.
- EVA setup and manual user guide exist.

### `Backup of CE Job Sheet 260309.xlsm` and `Backup of CE Job Sheet 260429.xlsm`

Reviewed workbook structure, not individual personal case details. Key context used:

- `Jobs` sheet has operational holding-pen columns including Date, VRM, Principal, Client, Vehicle, Missing, Due and Notes.
- `Principals` sheet contains provider metadata: Solicitor/Work Provider, EVA Code, Box Code, Inbox, Solicitors Instructions, Drag in to EVA?, Sent Mino, Images location, Image based or address, Sending Report.
- `Garages` sheet contains garage address/contact/figure metadata.
- VBA and formulas indicate this is a live operational control tool, not merely a static spreadsheet.

### `Mapped Principals.xlsx`

Principal/provider mapping and “lost causes” list. Key context used:

- Many provider/principal codes are in scope.
- CNX and EVA are mapped as engineer report sources.
- At least one noted lost cause is OCR quality.

### `Backup of Conditional Formatting 260202.txt`

Job sheet conditional-formatting rules. Key context used:

- Existing operational highlighting distinguishes overdue/ageing dates and repeated registrations.
- This supports maintaining visual risk/exception cues in any replacement dashboard.

### `EVA User Guide.pdf`

Manual EVA setup guide. Key context used:

- EVA setup requires offline copy of email, instruction document and damaged vehicle images.
- If any of those are missing, staff should pause/locate them.
- Manual fields include Reg No, Principal, Inspection Type, Case ID/PO, Insured, Claim No, Incident Date, Inspected On and Inspect At.
- Speedo/mileage may come from dashboard images or be estimated from Percayso using VRM and accident date.
- Previous-total-loss notes must distinguish current vs prior total loss.
- Photo upload has a special order/duplication requirement.

### `Sentry_API_Complete_Guide.md` and `evaapidocs.pdf`

Sentry/EVA API documentation. Key context used:

- API is JSON-based except token endpoint.
- JWT tokens expire quickly and need refresh logic.
- Endpoints include `Instruction/Inspection`, `Claim/LocationUpdate`, `Claim/AuthorityStatusUpdate`, `Note/SubmitNote`, `Claim/Update`, `Report/SubmitReport`, `Report/GetAvailableReports`, and `Report/GetReport`.
- `SubmitReport` contains inspection, vehicle, valuation, repair, salvage, parts, image and file fields.
- Case retrieval/search is limited; specific case targeting depends on identifier combinations or client-side filtering of reports.

### `CE Communication Style & Tone Profile.docx`

Tone profile. Key context used:

- Communication should be calm, professional, fact-driven, concise and evidence-based.
- Avoid emotive language, unnecessary apology and sales tone.
- Chasers should be direct but courteous.
- Disputes should be handled with technical rationale and measured language.

### `Standard Audatex Invoice.docx` and `Website Invoice Template.docx`

Invoice templates. Key context used:

- There is recurring Audatex Assessment billing context.
- Finance/Admin agent should be framed as billing-readiness support, not autonomous invoicing, unless integrated accounting controls are separately defined.

### `Final Format Example 02.json`

Example structured output. Key context used:

- Existing extraction contract includes Work Provider, VRM, Vehicle Model, Claimant Name, Reference, dates, Inspection Address, Accident Circumstances, VAT Status, Mileage and Mileage Unit.
- This is useful as an early extraction schema but is not a full case-management schema.

### `collision_project_context_pack.zip` and `collision-engineers-context-pack.zip`

Both context packs were extracted and reviewed. Key context used:

- Automation should sit around existing systems first.
- Recommended Phase 2 is Outlook intake, Box/file automation, PDF extraction, evidence matching, holding-pen dashboard, missing-info chasers, engineer pack generation, valuation lookup and cautious EVA pre-population/export.
- Avoid automatic final reports, automatic engineering conclusions, automatic fraud decisions, WhatsApp Desktop automation, screen scraping and direct EVA writeback before supported access is confirmed.
- Phase 3 should stabilise case database, file storage, permissions, audit logs and integrations before Phase 4 agents.

### `Collision Engineers Whiteboard.jam`

The Jam/FigJam package was inspected. Its thumbnail shows a workflow-map style whiteboard with Collision Engineers workflow, app/design and website form flow areas. The binary canvas itself was not reliably parseable as text in this environment, so this review only used the visible thumbnail-level context.

## Web sources checked

- Microsoft Graph mail API and shared mailbox access.
- Microsoft Graph Outlook change notifications.
- Power Automate Office 365 Outlook connector limitations.
- Box AI structured extraction and Box AI extraction guidance.
- DVLA Vehicle Enquiry Service API.
- DVSA MOT History API.
- Azure AI Document Intelligence custom extraction.
- Teams incoming webhooks / Workflows.
- WhatsApp Business template messaging.
- Audatex AudaConnect.
- Glass’s/Autovista, Cazana and cap hpi data/API pages.

## Limits of review

- I did not execute the live CE Document Mapper application because the current `app.py` was not part of this uploaded file set.
- The `.jam` canvas content could not be fully text-extracted.
- Some valuation and Audatex capabilities are commercial/licence-dependent and must be confirmed directly.
- Live mailbox/API credentials and Sentry/EVA sandbox access were not available.

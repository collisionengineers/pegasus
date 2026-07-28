# Phase 1 – Immediate Automation and Process Improvements

This document proposes actions to automate and streamline the existing Collision Engineers intake workflow without radically changing the business logic.  It focuses on improving the current document mapper, automating inbox processing, posting cases to EVA and organising files in Box.  Evidence from the provided materials is cited where relevant.

## 1.1 Upgrade the Document Mapper

The **CE Document Mapper** already performs basic extraction of fields from PDF/DOCX/EML files and can export a JSON in a defined order (Work Provider, VRM, Vehicle Model, Claimant Name, Reference, Incident Date, Instruction Date, Inspection Date, Inspection Address, Accident Circumstances, VAT Status, Mileage, Mileage Unit)【399092012268091†L47-L62】.  The **Final Format Example 02.json** demonstrates the output format【230218250735069†L0-L14】.  To improve reliability without altering scope:

1. **Add robust parsing for DOC/DOCX** – The existing importer struggles with Word documents (e.g., header/footer not parsed).  Implement a parser that extracts all text including headers/footers and tables.  Libraries like `python-docx` or `textract` can be used.  This addresses one of the top requests in the transcript【399092012268091†L114-L118】.

2. **Normalise provider presets** – Clean and consolidate the `providers.json` mapping rules.  Ensure that each preset has clear detection phrases and fall‑back rules.  For providers with similar patterns, generalise rules (e.g., a default template for solicitor letters).  Confirm that mileage and mileage unit are always placed as the last two fields in the JSON export【399092012268091†L63-L64】.

3. **Implement confidence scoring** – The `confidence_scoring` document (from the `collisionpdf` repository) outlines labels like `Strong`, `Check`, `Blocked`, `Missing`.  Integrate this scheme into the mapper’s output so that downstream automations know which fields require manual review【260811552025470†L2-L14】.

4. **File type detection and multi‑file batching** – Ensure the mapper correctly handles multi‑file import (batch mode) while enforcing the rules for engineer reports (e.g., only one instruction and one engineer report can be processed together【399092012268091†L87-L103】).  Add detection for image files (JPEG/PNG) and extract metadata (timestamp, orientation) for later use.

5. **MCP integration** – Where a vehicle registration is present, query the DVLA/DVSA MCP server to retrieve vehicle make/model and MOT mileage history.  Populate missing mileage fields automatically when possible.  Mark these values as estimated for transparency.

## 1.2 Automate the Outlook Inbox

Currently, three Outlook inboxes are monitored manually; staff copy data to a spreadsheet.  The goal is to automate email ingestion while leaving humans in control for exceptional cases.

1. **Use Microsoft Graph** – Create a service account with `Mail.ReadWrite` permission to read messages and attachments.  Poll the inboxes at configurable intervals (matching the automation plan’s `poll_interval`【722788243396246†L2-L28】).

2. **Parse attachments** – For each new email, download attachments and pass PDFs/DOCX/EML to the upgraded document mapper.  Extract any embedded images or attachments; if images are missing but a PDF contains them, extract using a PDF image parser.  Record the email’s metadata (sender, subject, received date) for case history.

3. **Categorise messages** – Only after successful parse, review, JSON export and Box upload should the email be marked with the `Parsed` category【353822838748330†L12-L22】.  Never delete or move emails; categorisation preserves auditability.

4. **Identify provider and create cases** – Using the extracted provider name and claim reference, check if a case already exists in EVA.  If not, assign a new internal case/po number by reading the highest existing number for that principal and incrementing it.  This counters the manual process of looking up the last reference.

5. **Handle missing items** – If images are missing:
   * Check if they are embedded within the PDF.
   * Look up the provider’s `Images location` rule in the Principals sheet (e.g., Accident Specialist group or customer WhatsApp)【173726810529521†screenshot】 and automatically send a templated request email/WhatsApp message to the garage or solicitor.  Use contact information from the **Garages** sheet【266162556730527†screenshot】.
   * Place the case in a “missing information” queue akin to the top section of the spreadsheet【500604108409406†screenshot】.  Provide a dashboard showing outstanding cases and the date the chase request was sent, similar to the existing spreadsheet’s “Sent Mino” column【173726810529521†screenshot】.

6. **Image preprocessing** – When images arrive via email or WhatsApp, automatically detect the first two images (vehicle overview and damage close‑up).  Ensure these are uploaded first when adding to EVA (the ordering rule in the current process) and then upload the rest.

## 1.3 Integrate with EVA via the Sentry API

Automation should interface with EVA only when all required data is available (instruction, images, valuations).  Key steps:

1. **Instruct Claim** – Use the Sentry API’s `POST /Instruction/Inspection` endpoint to create a new claim.  Provide fields such as `RequestFrom`, `ExternalRef` (internal case/po number), `VehReg`, `ClmNo` (client claim number), `InspType`, `Mileage` (estimated if necessary), `Value`, `VatStatus`, `InspectionDate`, `InspectionTime` and `Files` (JSON map of instruction document and images)【859874972426525†L180-L232】.  Use `inspectionType` values matching the current business categories (e.g., Vehicle Damage Inspection, Post Repair).  If the provider requests an uplifted valuation, include both original and uplifted values in `Files` or a note field.

2. **Update Claim Location** – Once the inspection address is known (garage address or image‑based assessment), call `Claim Location Update` with `LocationName`, `Address` lines, `LocationType` and `ApprovedRepairer` if needed【859874972426525†L275-L307】.  Use the garage list for accurate addresses【976881030568758†screenshot】.

3. **Authority Status and Notes** – For providers requiring pre‑authorisation, use `Authority Status Update` to change status to `Authorised` or `Not Authorised` and add comments【859874972426525†L343-L388】.  Use `Submit Note` to record missing information chases or interactions with garages【859874972426525†L402-L438】.

4. **Report Submission** – After an engineer completes the assessment, use `Submit Report` to upload the final report and images.  Include valuation fields (`Value`, `SalvageValue`), damage fields, parts lists and images in the correct order【859874972426525†L517-L576】.

5. **Error handling and retries** – Implement retries for API calls when tokens expire (the JWT token lasts five minutes).  Log all interactions for auditing.

## 1.4 Automate Box Uploads and Storage

1. **Folder creation** – Use the Box API to create a folder named after the case/po number.  Check for existing folders by searching for the name.  The Box integration plan suggests using a metadata query or search to detect duplicates before uploading【644536497277043†L14-L31】.

2. **File uploads** – Upload the original instruction (PDF/DOCX), the exported JSON, all images and the companion valuation report to the case folder.  Apply metadata (e.g., provider name, VRM, claim number) on upload.  After successful upload, delete the local copies to maintain data protection compliance.

3. **Network drive replacement** – For providers that currently store images in a network drive, create Box subfolders (e.g., by VRM) and store images directly there.  This eliminates the need for the spreadsheet’s button that creates network folders.

4. **Versioning and sharing** – Enable versioning in Box to keep track of updates.  Provide read‑only sharing links to engineers so they can access images and instructions without altering them.

## 1.5 Replace the Spreadsheet with a Structured Case Database

The existing Excel sheet acts as a task board.  To reduce manual copying and improve searchability:

1. **Create a database** – Use a lightweight relational database (e.g., SQLite or PostgreSQL) or a low‑code platform (e.g., Airtable) to store case metadata.  Fields should include everything from the current spreadsheet: dates, VRM, principal, client, vehicle details, missing items, chase dates and statuses.  Use foreign keys to link to provider and garage tables.

2. **Dashboard views** – Build a web dashboard that replicates the two‑section view: one for “missing information” and another for “ready for EVA”.  Provide filters by date, principal, missing item type, and allow exporting to CSV if needed.  When a case is created or updated via the automation pipeline, update the database accordingly.

3. **Alerts and reminders** – Automatically send reminders when a case has been in the missing information state for more than a configurable threshold (e.g., 48 hours).  Display these reminders in the dashboard and via email.

4. **Historical data migration** – Import existing rows from the job spreadsheet into the database.  Use the Principals sheet to populate the provider table and the Garages sheet to populate the garage table.  This will enable consistent auto‑completion and reduce typos.

## Expected Benefits

* **Reduced manual workload** – Admin staff no longer need to copy data into a spreadsheet or manually send chase emails; the system will handle routine tasks while surfacing exceptions.

* **Faster case intake** – Automatic parsing and EVA integration will shorten the time between receiving instructions and assigning a case to an engineer.

* **Improved data quality** – Structured extraction and validation rules will reduce errors.  Confidence scores will highlight fields needing review.

* **Auditability** – Using Graph API to label emails and Box to store all artifacts ensures a clear, tamper‑evident trail of communications and documents.

This phase lays the groundwork for more advanced automation and sets the stage for a fully bespoke system in later phases.
# Technical Architecture and Integration Notes

## Architecture pattern

Build an event-driven case workflow platform with controlled AI tooling layered on top.

```text
Outlook/WhatsApp/API input
      ↓
Intake service
      ↓
File/evidence storage + immutable originals
      ↓
Case database + workflow state
      ↓
Extraction/classification services
      ↓
Review dashboard + tasks
      ↓
EVA/Sentry bridge + engineer pack/report generator
      ↓
Audit log + management dashboard
```

## Core components

### 1. Case database

Stores cases, statuses, extracted fields, evidence links, tasks, notes, approvals and audit events.

### 2. Evidence storage

Stores original files, derivative files and metadata. If Box remains the source, use Box folders/metadata. If replacing Box later, preserve the same logical evidence model.

### 3. Intake service

Reads mailboxes and attachments using Microsoft Graph, Power Automate/Logic Apps, or another approved M365 method.

### 4. Extraction service

Uses deterministic parsers first:

- CE Document Mapper logic/presets where still relevant;
- PDF text extraction;
- DOC/DOCX/MSG/EML parsing;
- OCR only where text extraction fails or image evidence requires it.

AI/document intelligence can be layered after deterministic extraction.

### 5. Workflow state machine

Owns transitions such as `received`, `awaiting_images`, `needs_review`, `ready_for_eva`, `ready_for_engineer`, `submitted`, `failed`.

### 6. AI tool layer

Tools should be small, explicit and permissioned:

- read case;
- search cases;
- retrieve document;
- retrieve image metadata;
- draft email;
- create internal task;
- call DVLA/DVSA;
- call valuation source;
- create engineer pack;
- validate Sentry payload;
- log audit event.

### 7. Review UI

A lightweight dashboard is enough for the first production version. It must show status, missing items, suggested matches, draft communications, and audit history.

## Relevant services to evaluate

### Outlook / M365

- Microsoft Graph Mail API supports access to user and shared mailbox mail data.
- Shared/delegated mailbox access needs correct permissions; change notifications for shared/delegated folders require application-permission design rather than assuming delegated notification support.
- Power Automate can pilot shared-mailbox flows but has connector limitations and should be tested with the actual mailbox/delegated setup.

### Box

- Box metadata and webhooks can support case-folder metadata and file-event workflows.
- Box AI structured extraction may be relevant if licensing allows, but do not design around it until account availability and data-processing terms are confirmed.

### Document extraction

- Azure AI Document Intelligence custom extraction is suitable for repeated document templates and can start with small labelled datasets.
- Deterministic extraction should remain the baseline for known provider formats.

### Vehicle data

- DVLA VES for factual vehicle lookup by registration.
- DVSA MOT History API for authorised MOT/mileage history access.
- Glass’s/Autovista, Cazana, cap hpi or Percayso for valuation/specification evidence where licensed.

### EVA/Sentry

- Sentry API supports JSON workflows for instructions, notes, claim updates, report submission and report retrieval.
- Build token refresh, validation, retry and audit logging before any production writeback.
- Where no direct search endpoint exists, use report list retrieval then client-side filtering cautiously.

### Teams and notifications

- Teams Workflows incoming webhooks can post structured notifications/cards. Use for internal alerts; do not rely on deprecated connector patterns without checking current M365 setup.

### WhatsApp

- Use WhatsApp Business Platform/Cloud API only if the business wants official automated messaging. Otherwise provide copy/paste drafts and manual tasks.

### Audatex / AudaConnect

- AudaConnect is an encrypted API-based integration platform. Verify Collision Engineers’ actual Audatex access/licence before planning direct integration.

## Do not use

- Outlook Classic COM automation for central workflows.
- WhatsApp Desktop automation.
- Screen scraping EVA/Audatex/Glass’s if an API/export/manual-review approach exists.
- Model training on sensitive case data without DPIA/vendor review.
- Hidden prompts or tools that can take external actions without logs.

# 7.1 Integration with Insurance Platforms


## Purpose

Reduce dependency on email-based instructions and manual status updates by integrating directly with insurers, claims management companies and other work providers where they expose a usable API, EDI feed, portal export or structured inbox flow.

## Why this matters

Many current cases arrive through inconsistent email/PDF channels. Direct integrations can reduce manual attachment handling, improve status visibility and reduce duplicate/keying errors. However, these integrations should come after the internal work item model is stable.

## Step-by-step plan

### Step 1 — Segment work providers

1. Use the principal/provider lists from the Job Sheet and Mapped Principals workbook.
2. Group providers by volume, format stability, error rate and commercial priority.
3. Identify which providers are insurers, claims management companies, repair networks, solicitors, garages or private/direct sources.
4. Record whether each provider currently sends email, portal uploads, WhatsApp, API, networked Audatex/EVA material or mixed formats.

### Step 2 — Create a provider integration questionnaire

For each priority provider, ask:

1. Do you offer an API, EDI, SFTP, webhook, portal export or structured XML/JSON feed?
2. Can instructions, images and estimates be transmitted through that route?
3. Can Collision Engineers return statuses, notes or reports through the same route?
4. What authentication is required?
5. Are there rate limits, data-retention obligations or testing environments?
6. Are attachments transmitted as files, links or base64 fields?
7. Are duplicate prevention or external reference fields supported?

### Step 3 — Define canonical integration model

1. Keep Collision Engineers’ internal canonical work item model as the target.
2. Map each provider feed into that canonical model.
3. Do not let provider-specific schemas leak into extraction, review or EVA layers.
4. Store raw inbound payloads for audit and debugging.

### Step 4 — Build a sandbox spike for one provider

1. Choose one high-volume, stable provider.
2. Build a read-only/test ingestion adapter.
3. Create synthetic or redacted test payloads.
4. Validate field mapping against existing CE Document Mapper fields and EVA requirements.
5. Store source payloads and attachments in Box/source storage.
6. Route resulting work items through the same review and EVA adapter flow as email-originated cases.

### Step 5 — Add outbound status updates

1. Define safe statuses to send externally, for example received, awaiting images, under review, report issued.
2. Require human approval for any non-routine or technical status text.
3. Log every outbound update.
4. Confirm provider receipt/acknowledgement handling.

### Step 6 — Roll out by provider tier

1. Tier 1: highest volume and easiest API/EDI access.
2. Tier 2: medium volume but high manual-effort providers.
3. Tier 3: email-only providers, maintained through normal intake automation.

## Deliverables

- Provider integration inventory.
- Provider questionnaire.
- Canonical provider adapter interface.
- First provider sandbox adapter.
- Inbound/outbound payload logs.
- Integration runbook.

## Acceptance criteria

- One provider can create a work item from a structured feed without email parsing.
- Attachments are stored with checksums and Box/file IDs.
- Duplicate instructions do not create duplicate work items.
- The work item reaches the normal review/EVA flow.
- Outbound status updates are traceable and reversible if needed.

## Risks and controls

| Risk | Control |
|---|---|
| Provider schema changes | Version adapters and validate payloads. |
| Duplicate case creation | Use external reference + VRM + checksum + idempotency key. |
| Partner pushes bad data | Validate and route exceptions to review. |
| Over-customisation | Keep provider adapters thin; canonical model remains central. |
| Contract/data protection uncertainty | Confirm data-processing terms before live use. |

## Suggested priority

P3 unless a high-volume provider already has a mature API and business support. Build the internal pipeline first.

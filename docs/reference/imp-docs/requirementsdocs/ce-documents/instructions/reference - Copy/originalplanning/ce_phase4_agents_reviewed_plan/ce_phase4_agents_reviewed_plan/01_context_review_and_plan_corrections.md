# Context Review and Corrections to the Original Plan

## What the original plan gets right

The original `phase_ai_agents.md` correctly identifies the main operational pressure points:

- emails and attachments need triage;
- cases need association by VRM, claim number and subject/thread context;
- missing images, estimates and other items need chasing;
- valuation evidence needs to be gathered and reviewed;
- engineers need concise case briefings;
- management needs visibility of bottlenecks and provider behaviour.

These are real workflow needs. The correction is not to remove them. The correction is to implement most of them as **workflow automation with AI assistance**, not as free-running agents.

## Corrected assumption 1 — “Inbox Triage Agent” should start as Outlook intake automation

**Original assumption:** monitor Outlook inboxes, classify messages, create pending cases, process attachments.

**Correction:** build an Outlook intake service first. It should read selected inboxes/folders, persist mail metadata, extract attachments, classify file types, and write a case/evidence record. AI can assist classification and summarisation, but the durable process should be deterministic.

Recommended design:

- Use Microsoft Graph or a tested Power Automate/Logic Apps pilot for mailbox monitoring.
- Store internet message ID, mailbox, folder, subject, sender, recipients, received time, conversation/thread ID, attachment IDs, and original file hashes.
- Do not create a fully active case solely because a model says it is a new instruction. Create a `pending_case` or `needs_review` record if confidence is low.
- Use a rules + confidence model: exact VRM/reference/thread hits first, AI only for ambiguous or unstructured cases.

## Corrected assumption 2 — preserve original attachments before compression

**Original assumption:** compress and standardise large images to JPEG at 80% quality to save storage.

**Correction:** never replace original evidence. Store originals unchanged. Create derivative display images or thumbnails if storage/performance requires it.

Required implementation rule:

- `original_file` is immutable.
- `derived_file` may be compressed, resized, OCR’d or indexed.
- Reports and audit trails must be able to link back to the original evidence.

## Corrected assumption 3 — missing-info chasing is mostly a state machine, not an agent

**Original assumption:** a Missing Information Agent scans cases, generates and sends chasers.

**Correction:** implement a case-state scheduler with provider-specific rules and template-generated drafts. AI can draft wording where a template is not enough, but the logic for “what is missing”, “when due”, and “how many chases” should be rule-based.

Recommended states:

- `awaiting_instruction`
- `awaiting_images`
- `awaiting_estimate`
- `awaiting_mileage`
- `awaiting_location_confirmation`
- `awaiting_provider_response`
- `ready_for_eva`
- `ready_for_engineer`
- `needs_admin_review`
- `manual_escalation`

## Corrected assumption 4 — WhatsApp Desktop should not be automated

**Original assumption:** send chase messages by email or WhatsApp.

**Correction:** do not automate WhatsApp Desktop. If WhatsApp is in scope, use one of:

1. copy/paste draft generated inside the platform;
2. approved WhatsApp Business Platform/Cloud API integration;
3. a manual task reminding staff to send the message.

WhatsApp communications are external actions. They require an approved channel, templates where applicable, opt-in/consent handling, audit logging and role controls.

## Corrected assumption 5 — DVLA is not a valuation source

**Original assumption:** use DVLA data to fill make/model/year and obtain/assist valuations.

**Correction:** DVLA VES can support vehicle details from registration, but it is not a valuation source. Use it to enrich factual data. Use approved valuation/specification providers such as Glass’s/Autovista, cap hpi, Cazana, Percayso or existing business tools only where licensing and access permit. The output is valuation evidence, not a final value.

## Corrected assumption 6 — uplift should be a policy rule or recommendation, not an autonomous judgement

**Original assumption:** if the provider requests uplift, compute the recommended uplift.

**Correction:** any uplift rule must be documented, auditable, and explainable. The system can calculate a policy-derived suggested uplift, but the final valuation remains a human/engineer decision.

The valuation module should produce:

- source(s) used;
- lookup timestamp;
- mileage/spec adjustment inputs;
- market comparables where available;
- confidence/coverage notes;
- draft narrative;
- “requires engineer approval” status.

## Corrected assumption 7 — engineer support should split briefing, knowledge retrieval and report drafting

**Original assumption:** one Engineer Support Agent handles briefing, on-site Q&A and report drafting.

**Correction:** implement three separate capabilities:

1. **Engineer Pack Automation** — deterministic pack generation from case data and files.
2. **Engineer Copilot** — read-only, source-grounded Q&A over the case and internal knowledge base.
3. **Report Drafting Assistant** — draft-only report sections; engineer edits/signs off.

Safety-critical or vehicle-specific repair/ADAS guidance must be grounded in licensed technical sources and should be clearly separated from case summarisation.

## Corrected assumption 8 — Submit Report to EVA/Sentry should be gated

The Sentry/EVA material supports report submission and retrieval, but a structured `Submit Report` payload should not be sent automatically until field mapping, validation and engineer sign-off are stable. First build a draft payload validator and reviewed submission workflow.

## Corrected assumption 9 — “Continuous Learning Agent” should start as analytics

**Original assumption:** continuously analyse case data and propose process improvements.

**Correction:** begin as dashboards and scheduled management reports. Use AI only for narrative summaries and anomaly explanations. Avoid self-modifying prompts/models or live “learning” from case data until data protection, model governance and evaluation are agreed.

## New capabilities that should be added

The original plan omits several important modules:

- Evidence Matching Automation / Agent.
- Image Quality and Completeness Review.
- Communication Drafting Assistant.
- Compliance and Audit Checker.
- Operations Assistant for “what needs action today”.
- Finance/Admin Support for chargeable events and invoice readiness.
- Internal Knowledge Assistant for provider rules, email tone and process guidance.

# Delivery Roadmap and Acceptance Tests

## Recommended phased delivery

### Phase 4.0 — Foundations and decision lock

Deliverables:

- final case schema;
- evidence schema;
- status/state model;
- provider rules imported from job sheet/principals where appropriate;
- roles and permissions;
- audit event model;
- sample redacted test pack.

Exit criteria:

- all required case fields defined;
- current holding-pen statuses mapped;
- high-risk actions identified;
- API/service access confirmed.

### Phase 4.1 — Intake and evidence automation

Deliverables:

- Outlook intake service;
- immutable file capture;
- attachment classification;
- case/evidence record creation;
- exact-match case association;
- dashboard queue for ambiguous cases.

Exit criteria:

- emails and attachments are reliably captured;
- files are linked or queued;
- no evidence is lost;
- duplicates are detected by hash.

### Phase 4.2 — Missing information and chaser drafts

Deliverables:

- missing-item checklist;
- chase scheduler;
- provider-specific chase intervals;
- draft chaser emails/messages;
- escalation queue.

Exit criteria:

- cases can be filtered by missing item;
- chasers are generated only when due;
- receiving evidence closes missing items where confidence is high;
- all external messages are draft-only unless policy says otherwise.

### Phase 4.3 — Valuation and vehicle evidence assistant

Deliverables:

- DVLA lookup integration;
- MOT/Percayso/valuation provider integration where licensed;
- valuation evidence record;
- draft valuation summary;
- reviewer approval interface.

Exit criteria:

- factual vehicle enrichment is separate from valuation;
- valuation evidence is timestamped and source-linked;
- final value cannot be set by AI alone.

### Phase 4.4 — Engineer pack and report drafting

Deliverables:

- engineer pack generator;
- source-linked case summary;
- image ordering/checklist;
- draft report sections;
- Sentry/EVA payload validator.

Exit criteria:

- engineer pack is usable without AI hallucination;
- report drafts are clearly labelled;
- Submit Report cannot be called without sign-off.

### Phase 4.5 — Controlled internal agents

Deliverables:

- tool-permission layer;
- operations assistant queries;
- evidence matching suggestion agent;
- compliance checker;
- management summary generator.

Exit criteria:

- every tool call is logged;
- role restrictions are enforced;
- agents can create internal tasks but not external actions unless approved.

### Phase 4.6 — External action expansion

Deliverables:

- approved-send email workflows;
- WhatsApp Business API only if required and compliant;
- approved EVA/Sentry submission workflows;
- monitoring and rollback.

Exit criteria:

- external actions are restricted by policy;
- all actions are traceable;
- staff can pause automation quickly.

## System-level acceptance tests

### Intake

- Same-email instruction + images → one pending case, all evidence preserved.
- Separate image email → suggested link to correct case.
- Same VRM in multiple open cases → manual review.
- Unknown attachment → preserved and queued.

### Missing information

- Case missing images moves to `awaiting_images`.
- Chaser draft appears after interval.
- Receipt closes missing item only if evidence match confidence passes threshold.
- Repeated unanswered chases trigger manual escalation.

### Valuation

- DVLA enriches make/model facts only.
- MOT mileage is marked as historic/estimated if no dashboard image exists.
- Valuation source unavailable → manual review, not invented value.
- Human approval required for final value/uplift.

### Report/EVA

- Sentry payload validation catches missing required fields.
- Engineer sign-off required before report submission.
- Failed API call is logged and placed in retry/review queue.

### AI governance

- AI cannot send email without approval.
- AI cannot delete original files.
- AI cannot finalise report conclusions.
- AI answers cite source documents/evidence IDs.

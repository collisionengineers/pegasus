# Source Context Reviewed

The following project materials were reviewed and incorporated into the expanded plan.

## Plan file

- `phase_additional.md` — original Phase 7 plan with items 7.1 to 7.9.

## Core context packs

- `collision_project_context_pack.zip` — structured automation-centre context covering Outlook intake, Box storage, EVA integration, data extraction, workflow states, security, testing, roadmap, monitoring and open questions.
- `collision-engineers-context-pack.zip` — broader AI case-intake and engineer-pack context covering company/business positioning, current workflow, target workflow, UI, architecture, data model, AI modules, Audatex/ABP, compliance, roadmap, demo dataset and pitch guidance.

## EVA and Sentry materials

- `Sentry_API_Complete_Guide.md` — EVA/Sentry API integration guide, including authentication, instruction submission, claim updates, notes, report submission, report retrieval and batch patterns.
- `evaapidocs.pdf` — Sentry API documentation `v1.2`.
- `EVA User Guide.pdf` — manual EVA job setup guide. Key points: setup requires offline email copy, instruction PDF/DOCX and vehicle images; EVA fields include registration, principal, inspection type, case ID/PO, insured, claim number, incident date, inspected-on date, inspection address, speedo/mileage, valuation data and photo upload ordering.

## Current operational / tooling context

- `handover.docx` — job sheet, Z-drive, OneDrive/SharePoint, CE Document Mapper, EVA, Copilot agent, Base44, Claude and other internal tooling context. Sensitive access details were not reproduced in this pack.
- `claudechat.md` — CE Document Mapper development transcript, including provider presets, mapping methods, engineer-report/audit mode, batch mode, JSON export, date/mileage normalization and OneDrive path fixes.
- `Final Format Example 02.json` — current canonical JSON export example from the Document Mapper.
- `Mapped Principals.xlsx` — provider/principal mapping list with existing mapped/lost-cause providers.
- `Backup of CE Job Sheet 260429.xlsm` and `Backup of CE Job Sheet 260309.xlsm` — current job sheet structure and principal/garage data.
- `Backup of Conditional Formatting 260202.txt` — fragile Excel conditional formatting rules.
- `Collision Engineers Whiteboard.jam` — high-level whiteboard export/thumbnail reviewed as supporting process context.

## Communication and commercial context

- `CE Communication Style & Tone Profile.docx` — tone profile for routine chasers, status updates and technical correspondence.
- `Standard Audatex Invoice.docx` and `Website Invoice Template.docx` — invoice template context for possible payment/invoicing automation.

## Public/official sources checked

These sources informed the integration and governance recommendations:

- Microsoft Graph change notifications / Outlook resources: supports subscriptions for message resources and event-driven intake patterns.
- Box API reference: confirms metadata instances, metadata templates, webhooks, retention/hold and file operations are platform capabilities.
- ICO AI and data protection guidance: supports the recommendation for DPIA, data minimisation, transparency, security and human safeguards where AI processes personal data.
- ICO DPIA guidance: supports carrying out DPIAs where processing is likely to result in high risk, especially with new technologies or systematic/profiling-style evaluation.
- OWASP API Security Top 10 2023: supports partner-API security controls around authorisation, authentication, excessive data exposure and object-level/property-level access.

# Source URL Notes

This pack was written using the uploaded Phase 7 plan and the broader Collision Engineers project context. The following external references were checked to avoid planning against outdated assumptions.

## Microsoft Graph / Outlook intake

- Microsoft Graph supports subscriptions/change notifications for Outlook resources, including messages.
- Microsoft Graph webhook notifications allow an endpoint to be notified when subscribed resources change.
- Planning implication: Outlook intake should be event-driven where practical rather than relying only on polling.

## Box API / file storage

- Box developer documentation includes file upload, chunked upload, metadata, retention-policy and webhook resources.
- Planning implication: Box can support source-file storage, metadata tagging, folder/file events and retention-aware archival if configured correctly.

## ICO / AI and DPIA governance

- ICO guidance requires DPIAs where processing is likely to result in high risk to individuals.
- ICO AI guidance treats DPIAs as a key accountability mechanism for AI systems processing personal data.
- Planning implication: AI-assisted risk indicators, image processing, duplicate matching and portal/API workflows should have DPIA coverage before live deployment.

## OWASP / API security

- OWASP API Security Top 10 2023 highlights risks such as broken object-level authorisation, broken authentication and unrestricted resource consumption.
- Planning implication: any partner API or customer portal must be designed with object-level access checks, authentication, rate limiting, validation and monitoring from the start.

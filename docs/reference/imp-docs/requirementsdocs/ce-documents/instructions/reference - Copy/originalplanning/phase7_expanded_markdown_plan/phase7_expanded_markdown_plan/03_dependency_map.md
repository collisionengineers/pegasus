# Dependency Map

## Shared dependencies across most plans

| Dependency | Needed by |
|---|---|
| Work item ID | All integrations, portal, analytics, warehouse, audit. |
| Canonical schema | Extraction, EVA, portal, API, analytics. |
| Provider/principal library | Intake, routing, Box folder naming, EVA principal codes, chasers, scheduling. |
| Box file/folder IDs | Portal, engineer pack, audit, warehouse, retrieval. |
| Outlook/email metadata | Intake, duplicate detection, audit, chasers. |
| EVA/Sentry response log | Analytics, portal status, duplicate prevention, support. |
| Review events | Risk scoring, extraction improvement, audit, quality metrics. |
| Test corpus | Every parser, AI prompt, mapping change and integration. |
| Data governance baseline | AI modules, external APIs, portal, warehouse, risk indicators. |

## High-risk dependency clusters

### Risk indicators and fraud review

Requires:

- field-level evidence;
- duplicate detection;
- reviewer outcome data;
- DPIA/governance;
- careful wording policy;
- no automated adverse decisions.

### Partner API

Requires:

- authentication;
- tenant/partner-level data access rules;
- rate limiting;
- object-level authorisation;
- audit logs;
- API schema versioning;
- support process.

### Data warehouse

Requires:

- source system event logs;
- schema versions;
- retention policy;
- redaction/minimisation rules;
- durable identifiers;
- export/replay process.

### Predictive scheduling

Requires:

- engineer availability;
- region/postcode data;
- inspection type;
- case duration history;
- SLA data;
- cancellation/no-show data;
- manual override rules.

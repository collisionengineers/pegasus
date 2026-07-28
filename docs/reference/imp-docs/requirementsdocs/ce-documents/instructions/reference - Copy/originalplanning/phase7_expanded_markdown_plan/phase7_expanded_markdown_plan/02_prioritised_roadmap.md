# Prioritised Roadmap for Phase 7 Ideas

## Priority model

| Priority | Meaning |
|---|---|
| P0 | Foundation required before other Phase 7 ideas are safe. |
| P1 | High-value extension once foundation exists. |
| P2 | Strategic capability; build after operational data is mature. |
| P3 | Exploratory / partnership / longer-term option. |

## Recommended ordering

### P0 — Foundation

1. Work item state store and Job Sheet replacement path.
2. Provider/principal configuration library.
3. Document Mapper logic migration/reuse plan.
4. Human review queue and exception SLA.
5. Test corpus and regression harness.
6. Monitoring, audit events and runbooks.
7. Security, DPIA and vendor-governance baseline.

### P1 — Workflow acceleration

1. EVA/Sentry API adapter.
2. Customer self-service upload/status portal.
3. Engineer pack generator and template manager.
4. Image quality/required-photo assistant.
5. Estimate/Audatex/ABP review pack module.
6. Communications/chaser automation.
7. Advanced analytics dashboards.

### P2 — Data and intelligence

1. Data warehouse and archival.
2. Predictive scheduling.
3. Duplicate/historical case search.
4. Knowledge base / RAG for standard clauses and prior reports.
5. Risk indicator and anomaly review model.

### P3 — External ecosystem

1. Insurance platform integrations.
2. Audatex/AudaConnect partnership or licensed integration.
3. External partner API.
4. Invoice/payment automation where commercially useful.

## Practical sequencing

```text
Phase 7A — Foundation and controls
  State store, provider library, review queue, test corpus, monitoring, DPIA.

Phase 7B — Internal workflow enhancement
  EVA/Sentry adapter, engineer pack generator, image quality, estimate/ABP review.

Phase 7C — External access
  Customer portal, partner API, insurer integrations.

Phase 7D — Intelligence layer
  Analytics, predictive scheduling, warehouse, risk indicators, knowledge base.
```

## Dependency warning

Do not build portal/API/analytics as isolated projects. They must sit on the same work item model, otherwise each will create a separate version of case status and the business will lose the audit trail.

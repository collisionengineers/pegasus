# Decision Log

| Decision | Rationale | Status |
|---|---|---|
| Treat Phase 7 as a portfolio, not one build phase. | Items range from P0 foundations to high-governance future intelligence. | Recommended. |
| Build work item state store before external APIs. | External feeds need a stable internal destination. | Recommended. |
| Keep AI as assistive, not determinative. | Collision Engineers' work depends on expert judgement and auditability. | Required guardrail. |
| Reuse CE Document Mapper logic. | It already contains practical extraction and mapping work. | Recommended. |
| Make provider rules configurable and versioned. | Provider-specific formats are the main automation risk. | Recommended. |
| Use human review as the safety gate. | Prevents weak extraction from reaching EVA or external partners. | Required guardrail. |
| Treat fraud/risk scoring as review flags only. | Avoids automated adverse or unsupported conclusions. | Required guardrail. |
| Build a regression corpus early. | Protects current PDF/DOC/mapping improvements. | Recommended. |
| Keep source files immutable. | Supports audit, evidence traceability and rollback. | Required guardrail. |
| Do not expose a partner API until security/DPIA controls exist. | API/data-boundary risks are high. | Required guardrail. |

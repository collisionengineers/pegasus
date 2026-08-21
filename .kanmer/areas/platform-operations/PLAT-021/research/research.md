# Question

How can the application-exception alert retain incident detection without paging on recovered or duplicate telemetry?

# Findings

- `infra/modules/platform.bicep` currently counts every `AppExceptions` row in a five-minute window and triggers above zero at Sev1.
- Duplicate exception telemetry and successful Function operations inflate counts; the recovered SQL deadlock is the representative false page.
- The permission incidents remain actionable because they either correlate with failed operations or repeat across distinct scheduled operations.
- OPS-08 is owned by FRD-12; the action group and infrastructure-owned alert boundary already exist and should remain.
- A 15-minute evaluation window is required to express persistence, with an explicit five-minute filter for immediate failed-request correlation.

# Implication

Replace the count-all query with a single query that normalizes signatures, deduplicates operation-correlated rows, correlates failed requests, and buckets operationless exceptions by minute.

# 7.7 Advanced Analytics


## Purpose

Use operational data to improve throughput, quality, provider management, engineer workload balance and SLA visibility.

## Step-by-step plan

### Step 1 — Define KPI dictionary

Create agreed definitions for:

- intake volume;
- work items created;
- ready-for-engineer time;
- report turnaround time;
- review rate;
- correction rate;
- EVA import success rate;
- top exception reasons;
- unmatched evidence count;
- provider performance;
- engineer workload;
- duplicate prevention count.

### Step 2 — Build event logging

1. Log every work item state transition.
2. Log every extraction/review/EVA event.
3. Store duration between stages.
4. Use work item ID as the correlation ID.
5. Keep personal data out of generic operational logs where possible.

### Step 3 — Create MVP dashboards

1. Daily intake dashboard.
2. Needs-review backlog.
3. EVA failure dashboard.
4. Provider volume/failure dashboard.
5. Engineer workload dashboard.
6. Missing evidence dashboard.

### Step 4 — Add analysis slices

Analyse by:

- work provider/principal;
- document type;
- report type;
- engineer;
- region/postcode;
- month/week/day;
- source channel;
- exception reason;
- parser/template version.

### Step 5 — Add management reporting

1. Daily operations summary.
2. Weekly improvement report.
3. Monthly provider/performance report.
4. Extraction correction trend.
5. SLA breach analysis.

### Step 6 — Introduce BI tooling

1. Start with internal dashboard if data store is simple.
2. Move to Power BI, Metabase or equivalent once warehouse/event data is stable.
3. Publish role-restricted reports.
4. Automate refresh and quality checks.

## Deliverables

- KPI dictionary.
- Event schema.
- Dashboard `0.1.0-alpha.1`.
- Weekly report template.
- Analytics data mart or BI model.

## Acceptance criteria

- Managers can see current backlog and bottlenecks.
- Metrics are defined consistently.
- Every chart can be traced back to events/work items.
- Provider and engineer views do not leak unnecessary personal data.
- Reports identify actionable improvement areas.

## Risks and controls

| Risk | Control |
|---|---|
| Misleading metrics | Create KPI dictionary and data-quality checks. |
| Privacy overexposure | Aggregate where possible; role-based dashboard access. |
| Dashboard without action | Tie every KPI to an owner and review cadence. |
| Dirty historical data | Start from new clean events; treat old data as caveated. |

## Suggested priority

P1 for basic dashboards. P2 for advanced BI and trend analysis.

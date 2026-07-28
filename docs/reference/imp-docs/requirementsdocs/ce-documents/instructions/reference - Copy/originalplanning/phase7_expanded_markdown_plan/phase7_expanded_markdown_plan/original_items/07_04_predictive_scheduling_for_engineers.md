# 7.4 Predictive Scheduling for Engineers


## Purpose

Use historical case duration, region, inspection type and engineer availability to support scheduling decisions, reduce travel/time waste and improve turnaround.

## Step-by-step plan

### Step 1 — Capture scheduling fields

Add or confirm fields for:

1. inspection type: desktop, physical, roadworthy, valuation, forensic, other;
2. inspection location or image-based assessment;
3. postcode/region;
4. engineer assigned;
5. received date/time;
6. ready-for-engineer date/time;
7. inspection date;
8. report issued date;
9. cancellation/reschedule reason;
10. travel requirement.

### Step 2 — Standardise engineer profile data

Create engineer records with:

- name/code;
- region/base postcode;
- specialisms;
- availability;
- normal working hours;
- physical inspection capability;
- desktop capacity;
- holiday/leave calendar source.

### Step 3 — Build scheduling dashboard

1. Show upcoming inspections by region and engineer.
2. Show unassigned ready-for-engineer cases.
3. Highlight overdue/stale items.
4. Surface travel-heavy clusters.
5. Allow manual override.

### Step 4 — Add simple rules before prediction

1. Assign desktop cases by workload and skill.
2. Assign physical inspections by region and availability.
3. Avoid scheduling same engineer in incompatible locations.
4. Prioritise urgent/SLA cases.
5. Escalate cases missing images or address.

### Step 5 — Create prediction model only after data maturity

When at least several months of reliable data exists:

1. Predict expected case duration by provider, report type, evidence completeness and engineer.
2. Predict likelihood of missing evidence delaying the case.
3. Predict physical inspection lead time by region.
4. Forecast weekly regional capacity needs.

### Step 6 — Pilot recommendations

1. Show recommended engineer/time slot.
2. Ask scheduler to accept, reject or adjust.
3. Capture outcome to improve rules.
4. Do not auto-book until trusted.

## Deliverables

- Engineer profile schema.
- Scheduling data fields.
- Assignment dashboard.
- Rule-based assignment engine.
- Prediction prototype after data maturity.

## Acceptance criteria

- Every ready-for-engineer case has an assignment status.
- Scheduler can see why a recommendation was made.
- Manual overrides are captured.
- Forecasts are compared against actual workload.
- No system auto-books externally without approval.

## Risks and controls

| Risk | Control |
|---|---|
| Bad historical data | Start rule-based; clean data first. |
| Staff distrust | Show recommendation reasons and allow override. |
| Wrong address assumption | Require location confidence before physical scheduling. |
| Over-optimisation | Prioritise SLA and engineer judgement over route math. |

## Suggested priority

P2. Build data capture and dashboard earlier; predictive model later.

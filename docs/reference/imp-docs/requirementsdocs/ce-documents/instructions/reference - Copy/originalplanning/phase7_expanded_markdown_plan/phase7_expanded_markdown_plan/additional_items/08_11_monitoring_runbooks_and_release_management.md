# 8.11 Monitoring, Runbooks and Release Management

## Purpose

Ensure the automation platform is supportable by staff and developers when imports fail, APIs change, file storage is unavailable, provider formats change or a release introduces a regression.

## Why this matters

A partially automated workflow can create hidden failure modes. Monitoring and runbooks turn failures into visible, recoverable operational events.

## Step-by-step plan

### Step 1 — Define operational events

1. Intake received.
2. Files stored.
3. Extraction started/completed.
4. Provider detected.
5. Validation failed.
6. Review approved.
7. EVA payload generated/submitted.
8. External status sent.
9. Retry scheduled.
10. Terminal failure.

### Step 2 — Define metrics

1. Number of new cases per day.
2. Cases by state.
3. Average time in each state.
4. Extraction success rate by provider.
5. Missing-file rate by provider.
6. EVA submission success/failure rate.
7. Review queue ageing.
8. Duplicate detection rate.
9. Chaser volume and response time.

### Step 3 — Build dashboards and alerts

1. Daily operations dashboard for staff.
2. Technical dashboard for failures/retries.
3. Alerts for stuck cases.
4. Alerts for repeated provider extraction failures.
5. Alerts for failed integrations or token/auth issues.

### Step 4 — Write runbooks

1. What to do if Outlook intake stops.
2. What to do if Box upload fails.
3. What to do if extraction fails.
4. What to do if EVA rejects a payload.
5. What to do if a provider mapping breaks.
6. How to roll back a provider config.
7. How to disable an integration safely.

### Step 5 — Version releases

1. Version application code.
2. Version provider configs.
3. Version extraction prompts/rules.
4. Version EVA mapping definitions.
5. Version communication templates.
6. Record deployment date, author and summary.

### Step 6 — Establish rollback process

1. Keep previous stable app build.
2. Keep previous provider config version.
3. Keep previous prompt/template version.
4. Allow a provider to be switched to manual/review-only mode.
5. Log rollback reason and outcome.

## Deliverables

- Monitoring event taxonomy.
- Operations dashboard.
- Technical alert rules.
- Runbook library.
- Release/rollback process.

## Acceptance criteria

- Staff can see which cases are stuck and why.
- A failed integration produces a visible alert.
- There is a documented recovery path for each major failure type.
- Provider mapping changes can be rolled back.

## Risks and controls

| Risk | Control |
|---|---|
| Too many alerts | Alert only on actionable conditions and aggregate noisy events. |
| No one owns failures | Assign owner by failure category. |
| Rollbacks are untested | Test rollback during release rehearsal. |

## Suggested priority

P1. Build alongside first production automation releases.

# 7.9 Continuous Improvement Programme


## Purpose

Create a structured feedback and improvement loop so the automation improves from real operational use rather than becoming a static tool.

## Step-by-step plan

### Step 1 — Define improvement governance

1. Appoint business owner.
2. Appoint technical owner.
3. Appoint operations review owner.
4. Identify engineer representative.
5. Identify data/privacy owner.
6. Set meeting cadence.

### Step 2 — Create feedback channels

1. In-app feedback on work items.
2. Review correction reasons.
3. Weekly operations notes.
4. Engineer pack feedback.
5. Failed parser/template logs.
6. Portal/partner support tickets.

### Step 3 — Track improvement backlog

Classify suggestions as:

- bug;
- mapping/provider rule;
- template/parser improvement;
- workflow improvement;
- UI improvement;
- integration issue;
- data governance concern;
- analytics request;
- strategic feature.

### Step 4 — Run weekly pilot review

Review:

1. volume processed;
2. cases needing review;
3. top failure reasons;
4. extraction corrections;
5. missing evidence patterns;
6. EVA/API failures;
7. user feedback;
8. proposed priority changes.

### Step 5 — Run monthly steering review

Review:

1. KPI trends;
2. provider-specific issues;
3. governance/security concerns;
4. ROI indicators;
5. new integration opportunities;
6. roadmap reprioritisation;
7. staff training needs.

### Step 6 — Close the feedback loop

1. Convert repeated correction types into parser/validation improvements.
2. Add new document templates to the test corpus.
3. Update provider configuration.
4. Update training/support notes.
5. Publish release notes.

## Deliverables

- Steering group terms of reference.
- Improvement backlog template.
- Weekly/monthly agenda templates.
- Release note template.
- Correction-to-improvement workflow.

## Acceptance criteria

- Every recurring issue has an owner or explicit decision to ignore.
- Parser/prompt changes are tested before release.
- Staff see what changed and why.
- Monthly review produces a clear priority list.
- Improvement decisions are documented.

## Risks and controls

| Risk | Control |
|---|---|
| Feedback disappears informally | Central backlog and owner. |
| Overreacting to one-off issues | Require frequency/impact evidence. |
| Uncontrolled prompt/parser changes | Regression tests and versioning. |
| Staff disengagement | Show visible fixes from feedback. |

## Suggested priority

P0/P1. Start this as soon as the pilot begins.

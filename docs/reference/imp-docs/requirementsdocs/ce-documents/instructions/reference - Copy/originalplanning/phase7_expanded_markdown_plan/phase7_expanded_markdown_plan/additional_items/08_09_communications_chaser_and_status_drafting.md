# 8.9 Communications, Chaser and Status Drafting

## Purpose

Generate consistent draft communications for missing files, status updates, report delivery and routine clarification, using Collision Engineers’ established concise, neutral and technically professional tone.

## Why this matters

Much admin time is spent chasing missing instructions/images/estimates or sending routine status messages. Drafting support can reduce time while preserving human control and company tone.

## Step-by-step plan

### Step 1 — Define message categories

1. Missing instruction.
2. Missing images.
3. Missing estimate/Audatex document.
4. Clarification of inspection address.
5. Confirmation of receipt.
6. Report issued/attached.
7. Delay/status update.
8. Technical clarification request.
9. Partner portal/API notification text.

### Step 2 — Build template library

1. Use short templates for routine delivery.
2. Use slightly longer templates for clarification or dispute contexts.
3. Avoid emotional wording and sales language.
4. Use phrases consistent with existing CE style, such as “Please see attached…” and “Any issues let us know.”
5. Allow provider-specific wording where required.

### Step 3 — Connect templates to work item data

1. Pull claimant, VRM, provider, reference and missing-item list from the work item.
2. Insert only verified data.
3. Leave placeholders visible if required data is missing.
4. Include attachments/links only after staff confirm the correct files.
5. Log generated drafts and sent messages.

### Step 4 — Add approval controls

1. Draft only by default.
2. Staff review and send through email/portal/API.
3. Auto-send only for narrowly defined low-risk reminders after approval of that workflow.
4. Never auto-send technical disagreement or expert opinion.
5. Preserve final sent content for audit.

### Step 5 — Add chaser tracking

1. Record when each chaser was generated and sent.
2. Set next-chaser due dates.
3. Suppress duplicate chasers if the missing item arrives.
4. Escalate overdue missing files.
5. Show chaser status in the work item dashboard.

### Step 6 — Evaluate tone and accuracy

1. Review generated communications weekly during pilot.
2. Record edits staff make to drafts.
3. Update templates based on repeated edits.
4. Keep technical/dispute messaging under tighter review.

## Deliverables

- Communication template library.
- Draft generation service.
- Chaser tracking table.
- Send/approval workflow.
- Audit trail for communications.

## Acceptance criteria

- Staff can generate a missing-file chaser from a work item in one click.
- Drafts use verified case data only.
- Routine messages match Collision Engineers’ established style.
- Sent communications are logged against the case.

## Risks and controls

| Risk | Control |
|---|---|
| Incorrect or overconfident wording | Draft-only, approved templates and verified fields. |
| Duplicate chasers | Track chaser state and suppress when files arrive. |
| Sensitive technical comments sent too early | Require senior/engineer review for technical/dispute templates. |

## Suggested priority

P1/P2. High user-facing value once work item state and missing-information flags are available.

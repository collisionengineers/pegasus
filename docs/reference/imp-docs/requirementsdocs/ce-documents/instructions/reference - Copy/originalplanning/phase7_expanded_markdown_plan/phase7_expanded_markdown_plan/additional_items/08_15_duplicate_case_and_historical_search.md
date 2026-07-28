# 8.15 Duplicate Case and Historical Search

## Purpose

Detect possible duplicate cases, repeated VRMs, repeated references, repeated claimant names and related historical cases so staff can merge, link or investigate before duplicate work is created.

## Why this matters

The existing job sheet already highlights repeated registrations. A central system can extend that into controlled duplicate detection using VRM, provider reference, claimant, source email and file checksums.

## Step-by-step plan

### Step 1 — Define duplicate signals

1. Same provider + same reference.
2. Same VRM + close instruction date.
3. Same claimant + same VRM.
4. Same source email/message ID.
5. Same attached file checksum.
6. Same accident date + VRM.
7. Similar folder name or Box path.
8. Similar image set/checksum.

### Step 2 — Define confidence levels

1. Definite duplicate: same provider and reference or same source message ID.
2. Likely duplicate: same VRM, accident date and claimant.
3. Possible related case: same VRM but different date/provider.
4. Historical context only: old case with same VRM outside duplicate window.

### Step 3 — Build duplicate-check service

1. Run duplicate check when a new work item is created.
2. Run again when key fields are corrected in review.
3. Store duplicate candidates with reasons.
4. Show candidates to staff rather than merging automatically.
5. Allow staff to dismiss, link, merge or split.

### Step 4 — Add historical search UI

1. Search by VRM.
2. Search by claimant.
3. Search by reference.
4. Search by provider.
5. Search by date range.
6. Show prior outcomes and linked files where access permits.

### Step 5 — Add audit controls

1. Log merge/link/split decisions.
2. Preserve original source files and extracted data.
3. Do not delete duplicate records without clear policy.
4. Allow mistaken merges to be reversed where possible.
5. Keep a reason for each decision.

### Step 6 — Use insights carefully

1. Use duplicate signals to avoid admin duplication.
2. Use historical cases to provide context to reviewers/engineers.
3. Do not label fraud automatically.
4. Escalate suspicious repeated patterns only as review flags.
5. Keep risk-scoring governance separate.

## Deliverables

- Duplicate-signal taxonomy.
- Duplicate-check service.
- Historical search UI.
- Merge/link/split workflow.
- Audit log for duplicate decisions.

## Acceptance criteria

- Obvious duplicate instructions are flagged before EVA submission.
- Staff can see why a duplicate was suggested.
- Staff can merge, link, dismiss or split with an audit record.
- Historical same-VRM cases are visible without being treated as fraud by default.

## Risks and controls

| Risk | Control |
|---|---|
| False positives slow staff down | Confidence levels and dismiss option. |
| Wrong merges | Human decision, audit and reversal path. |
| Duplicate flags look like fraud accusations | Use neutral wording: duplicate/related/historical. |

## Suggested priority

P1/P2. Build after the state store exists and before advanced risk scoring.

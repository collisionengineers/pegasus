# 7.5 Customer Self-Service Portal


## Purpose

Provide clients, garages, solicitors and repairers with a controlled portal to upload documents/images, view limited case status and receive routine notifications.

## Step-by-step plan

### Step 1 — Define portal user types

1. Garage/repairer.
2. Solicitor/work provider.
3. Insurer/claims management company.
4. Private client, if applicable.
5. Internal staff/admin.

For each user type, define what they can upload, see, edit and download.

### Step 2 — Define portal scope `0.1.0-alpha.1`

Start narrow:

1. Upload instruction/estimate/images.
2. Link upload to VRM/reference.
3. Show received/awaiting images/under review/report issued statuses.
4. Display missing-information requests.
5. Allow safe download of submitted files or issued report if approved.

Do not include detailed engineer notes, risk flags, internal review comments or unrelated client cases.

### Step 3 — Design authentication and access control

1. Partner account or magic-link flow.
2. Per-partner case access rules.
3. Role-based permissions.
4. MFA for staff/admin.
5. Audit every access, upload and download.

### Step 4 — Build upload flow

1. Ask for work provider, VRM, reference and contact details.
2. Require file type/size validation.
3. Generate checksums.
4. Store files in Box/source storage.
5. Create or attach to work item.
6. Show upload receipt.

### Step 5 — Build status flow

1. Map internal work item statuses to external-safe statuses.
2. Hide internal exception language.
3. Show missing items as factual requests.
4. Do not expose risk flags or internal reasoning.

### Step 6 — Add notification preferences

1. Email notification on upload received.
2. Email notification when missing info is requested.
3. Email notification when report is issued or status changes.
4. Keep all outbound messages approved/template controlled at first.

### Step 7 — Pilot with one partner group

1. Choose a cooperative, repeat-volume partner.
2. Use test accounts and redacted data first.
3. Compare portal upload accuracy with email intake.
4. Measure reduction in chasers and unmatched files.

## Deliverables

- Portal permission matrix.
- Upload UI.
- Partner case-status API/view.
- File validation controls.
- External-safe status mapping.
- Portal audit log.

## Acceptance criteria

- Partner can only access their own cases/files.
- Uploaded files create or attach to a work item.
- Missing information is clear and factual.
- Portal actions appear in the audit trail.
- Staff can disable a partner/account quickly.

## Risks and controls

| Risk | Control |
|---|---|
| Data exposure across partners | Strict object-level authorisation tests. |
| Incorrect case attachment | Require identifiers and human review for ambiguous matches. |
| Unsupported/unsafe uploads | File type, size, malware and content checks. |
| Premature scope creep | Start upload/status only; no full case management. |

## Suggested priority

P1/P2 after work item state, Box storage and review queue are reliable.

# Work Package 07 - Outlook Intake and Communications

## Purpose

Turn mailbox activity into controlled case intake and case communications while preserving Collision Engineers' concise, professional style.

## Source files

- `handover.docx`
- `CE Communication Style & Tone Profile.docx`
- `collision_project_context_pack.zip` / `05_OUTLOOK_INTAKE.md`
- `collision-engineers-context-pack.zip` / `07_ui_dashboard_spec.md`, `10_ai_modules_and_prompts.md`
- `phase_new_system.md`
- `phase_bespoke_system.md`

## Scope

### Phase 2 scope

- Manual upload and/or basic mailbox ingestion.
- Email metadata capture.
- Attachment capture.
- Classification into instruction/images/estimate/engineer report/other.
- Chaser drafting with human send approval.
- Response tracking where practical.

### Phase 6 scope

- Full Microsoft Graph shared-mailbox ingestion.
- Webhook or polling architecture.
- Threading and response correlation.
- Notification service.
- Approved sending of chasers and status updates.
- Communication audit trail.

## Step-by-step implementation

### Step 1 - Confirm mailbox model

1. List all relevant mailboxes and shared inboxes.
2. Confirm whether the platform should monitor `desk@`, `engineers@`, `info@`, or other addresses.
3. Confirm whether `digital@collisionengineers.co.uk` delegated access should be used or avoided.
4. Confirm whether Graph app-only permissions or delegated user permissions are acceptable.
5. Record data-access decision in security decision log.

### Step 2 - Build intake connector

1. Register Microsoft Graph application.
2. Store credentials securely.
3. Implement Graph message listing for target folders.
4. Add fallback polling interval.
5. Capture message metadata:
   - Graph message ID.
   - Internet message ID.
   - Subject.
   - Sender.
   - Recipients.
   - Received date.
   - Conversation/thread ID.
   - Attachments.
6. Store the raw email body or safe extracted text subject to retention rules.

### Step 3 - Classify emails

Initial deterministic classification rules:

| Classification | Signals |
|---|---|
| Instruction | Provider phrases, attached PDF/DOC/DOCX, instruction/request wording. |
| Images | Multiple image attachments, subject/body containing images/photos. |
| Estimate | Audatex/estimate/repair estimate/parts/labour/VAT/gross terms. |
| Engineer report | Provider marked as engineer report, EVA/CNX engineer report phrases. |
| Chaser response | Reply to sent chaser, matching thread ID, missing-info item open. |
| Invoice | Invoice template/paid/VAT/total terms. |
| Unrelated | No case indicators. |

Create review flag when classification is ambiguous.

### Step 4 - Correlate related emails

Match late-arriving files using:

1. VRM normalized with spaces removed.
2. Claim/reference number.
3. Claimant name.
4. Provider/principal.
5. Email thread ID.
6. Sender domain/contact.
7. Temporal proximity.
8. Attachment filename.

For multiple possible matches, show candidate cases with confidence and require user selection.

### Step 5 - Add duplicate detection

Duplicate detection rules:

- Same Graph message ID.
- Same internet message ID.
- Same attachment checksum.
- Same provider + reference + VRM.
- Same VRM within a short time window with same claimant/provider.

Duplicate result options:

- Link to existing case.
- Mark as duplicate and archive.
- Treat as supplementary evidence.
- Create new case despite warning.

### Step 6 - Chaser drafting

Chasers should use the CE communication profile:

- Calm and professional.
- Direct request.
- No unnecessary apology.
- No emotive language.
- Concise.

Example missing-images template:

```text
Good morning,

Please can you send the vehicle images for [VRM] / [Reference].

Any issues let us know.

Kind regards,
[Name]
```

Example missing-estimate template:

```text
Good afternoon,

Please can you send the estimate for [VRM] / [Reference] when available.

Any issues let us know.

Kind regards,
[Name]
```

Example unclear-reference template:

```text
Good morning,

We have received the attached information but cannot match it confidently to an existing case.

Please can you confirm the vehicle registration and claim reference.

Kind regards,
[Name]
```

### Step 7 - Human approval gate

For Phase 2, every external message should require review and click-to-send. Phase 6 can consider controlled auto-send only for low-risk repetitive chasers after monitoring proves reliability.

Approval UI should show:

- Case details.
- Missing item.
- Suggested recipient.
- Draft subject/body.
- Source reason.
- Last chaser date.
- Edit field.
- Send/cancel.

### Step 8 - Communication audit

Record:

- Who sent or approved message.
- Recipient.
- Subject.
- Body hash and optionally body text subject to retention policy.
- Related missing item.
- Related case.
- Thread ID/message ID.
- Timestamp.

## Acceptance criteria

1. Email attachments can become case documents without manual re-saving.
2. Instructions, images, estimates, engineer reports, and unrelated emails are classified or flagged.
3. Late-arriving images/estimates can be matched to existing cases with human confirmation when ambiguous.
4. Chasers can be drafted in CE style and approved by a user.
5. Sent chasers and responses are visible on the case timeline.
6. Duplicate emails/attachments do not create duplicate active work unless a user overrides.

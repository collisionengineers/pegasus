# Outlook Intake, Case Association and Evidence Matching

## Objective

Replace ad hoc manual email/attachment handling with a reliable intake pipeline that captures instructions, estimates, images and correspondence, links them to cases, and queues ambiguous records for human review.

## Current-context constraints

- Work arrives by Outlook email, WhatsApp and occasionally API.
- Images and instructions may arrive at different times.
- The current holding pen is spreadsheet-like and tracks missing items, dates, VRM, provider, notes and chasing activity.
- CE Document Mapper already imports PDFs, DOC/DOCX, MSG/EML and images into structured fields, but it is a local desktop mapping tool, not a central case system.
- The existing Copilot setup has delegated access to multiple shared inboxes but has known friction with Outlook Classic delegated sync.

## Recommended implementation

### 1. Mailbox connection

Prefer Microsoft Graph for production because it can access Microsoft 365 mailboxes, including shared mailboxes, with properly configured delegated or application permissions. Power Automate can be used for a pilot, but shared mailbox behaviour and connector limitations should be tested early.

Capture:

- mailbox address;
- folder;
- internet message ID;
- Graph message ID;
- conversation/thread ID;
- sender and recipients;
- subject;
- received/sent timestamp;
- attachment metadata;
- raw email or export copy where legally/operationally appropriate.

### 2. Attachment handling

Do:

- save every original attachment unchanged;
- calculate file hash;
- assign evidence ID;
- create display/processing copies only as derivatives;
- tag files by type: instruction, estimate, image, invoice, report, note, unknown.

Do not:

- compress/replace original images;
- delete unmatched files;
- assume filename alone is enough to match a case;
- trust AI classification without review when confidence is low.

### 3. Case association hierarchy

Match in this order:

1. Exact existing internal case ID / EVA ref / claim reference if present.
2. Exact VRM + claim reference.
3. Exact email conversation/thread already linked to a case.
4. VRM + sender/provider + close received time.
5. VRM only where there is a single open case and no conflicting evidence.
6. AI-assisted semantic match for ambiguous correspondence.
7. Manual review.

### 4. Confidence thresholds

Suggested thresholds:

- `auto_link`: exact case ID or VRM + claim reference + thread match.
- `suggest_link`: VRM match with partial provider/thread/timestamp support.
- `needs_review`: multiple open cases for same VRM, reference mismatch, sender mismatch, or missing identifiers.
- `reject/unknown`: no VRM/reference and no meaningful case context.

### 5. AI role

AI should assist with:

- unstructured email classification;
- extracting missing candidate identifiers from messy text;
- explaining why evidence may belong to a case;
- summarising incoming correspondence;
- drafting internal notes.

AI should not autonomously:

- merge cases;
- discard files;
- assert fraud/reuse;
- create active work items without sufficient evidence;
- send external messages.

## Minimum data model

```yaml
inbound_message:
  id: string
  mailbox: string
  graph_message_id: string
  internet_message_id: string
  conversation_id: string
  sender: string
  subject: string
  received_at: datetime
  body_text_hash: string
  linked_case_id: string | null
  status: new | processed | needs_review | ignored

evidence_file:
  id: string
  case_id: string | null
  source_message_id: string | null
  original_filename: string
  file_hash: string
  mime_type: string
  storage_uri: string
  evidence_type: instruction | estimate | image | invoice | report | note | unknown
  classification_confidence: number
  original_preserved: true
```

## Acceptance tests

- Instruction and images in same email create one pending case with linked evidence.
- Instruction first, images later: images are suggested or linked based on VRM/reference/thread rules.
- Images first, instruction later: evidence stays in holding state until enough identifiers arrive.
- Two open cases for same VRM: system does not auto-link without a second strong identifier.
- Unknown PDF: file is preserved and queued for admin review.
- Duplicate email/forwarded email: system detects duplicate attachments by hash and avoids double-counting evidence.

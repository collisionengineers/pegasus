# Missing Information and Communication Workflow

## Objective

Turn the current “missing information” chase process into a controlled state machine with provider-specific rules, reviewed communication drafts and escalation queues.

## Correct framing

This should not start as an autonomous Missing Information Agent. It should start as:

- required-evidence checklist;
- case status rules;
- scheduled scans;
- template/draft generation;
- dashboard and task notifications;
- optional AI drafting for nuanced messages.

## Required evidence checklist

Store expected evidence by report type/provider:

- instruction document;
- email/offline instruction copy;
- vehicle images;
- image location / image-based assessment decision;
- estimate or Audatex evidence;
- VRM;
- claim/reference number;
- claimant/insured name;
- incident date;
- inspection date/current date where relevant;
- mileage/speedo evidence or MOT/Percayso estimate note;
- valuation evidence where required;
- engineer notes/questions.

## State machine

```yaml
missing_item:
  case_id: string
  item_type: instruction | images | estimate | mileage | location | valuation | other
  status: open | chased | received | waived | escalated
  first_missing_at: datetime
  last_chased_at: datetime | null
  chase_count: integer
  next_chase_due_at: datetime | null
  owner: user_id | team
  evidence_received_file_id: string | null
  resolution_note: string | null
```

## Chaser scheduling

Use rules rather than agent judgement:

- initial chase interval by provider/type;
- second chase interval;
- maximum chase count;
- escalation threshold;
- quiet hours/business days;
- channel preference: email, phone task, WhatsApp draft/manual.

Example:

```yaml
provider_rule:
  provider_code: QCL
  item_type: instruction
  first_chase_after_hours: 24
  repeat_every_hours: 72
  escalate_after_chases: 3
  preferred_channel: email
```

## Communication drafting

Start with templates in Collision Engineers’ tone:

- concise;
- professional;
- neutral;
- no unnecessary apology;
- clear request for action;
- “Any issues let us know” style closing.

AI can vary wording when the case context is unusual, but the output should be a draft until an approved-send policy exists.

## External send policy

### Stage 1

Draft only. Staff review/edit/send.

### Stage 2

Approved-send for narrow low-risk templates, such as first chaser for missing images where exact fields are populated.

### Stage 3

Auto-send only if:

- provider/channel allow-list exists;
- message template is locked or bounded;
- no dispute/sensitive content is present;
- audit log records sender, approver/policy, template, recipients and timestamp;
- easy rollback/correction path exists.

## WhatsApp handling

Avoid WhatsApp Desktop automation. Use:

- manual copy/paste drafts; or
- official WhatsApp Business Platform/Cloud API with approved templates and opt-in where required.

## Acceptance tests

- Case missing images appears in `awaiting_images` queue.
- First chase draft is generated only after the defined interval.
- Receipt of an image email closes or reduces the missing item automatically only when evidence matching is confident.
- After the configured number of unanswered chases, the case becomes `manual_escalation`.
- Drafts use the stored provider contact/channel and do not invent recipients.
- A disputed or complaint-related thread never auto-sends.

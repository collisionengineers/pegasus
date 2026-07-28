# Inbox Ingestion, Triage and Case Assembly

> **Pegasus custody boundary:** Outlook material may be used only as a bounded, separately approved
> external evaluation extract. The complete archive is not imported, repository inclusion is
> prohibited, and no live mailbox read or mutation is authorised by this historical research.

## Executive conclusion

The inbox may be more immediately valuable than additional model training. It contains the temporal and relational information needed to assemble complete cases: instructions, attachments, clarifications, missing-evidence requests, deadlines, amendments and final delivery.

The first system should be a permission-aware case-ingestion service. It should link messages and attachments to the correct matter, classify their role, extract actions and highlight missing information. It should not initially send messages or treat all email text as authoritative.

## Why email is valuable

Reports show the final assessment; email often explains how it came into being. It can reveal:

- the original instruction and questions asked;
- vehicle, claim and party identifiers;
- when each image or document was received;
- who supplied it;
- whether an attachment superseded an earlier one;
- evidence requested by the engineer;
- deadlines and service commitments;
- report delivery and acknowledgement;
- queries, challenges and responses;
- why an amendment occurred;
- operational delays and handoffs.

This makes email essential for reconstructing evidence cutoffs and workflow outcomes. It is also highly sensitive and noisy, requiring stricter filtering than a report-only corpus.

## Recommended ingestion pipeline

### 1. Preserve the source

Retain the original message or a defensible archive representation, including message identifier, sender, recipients, timestamps, subject, thread headers and attachment hashes.

### 2. Parse safely

Extract text and attachments from supported formats such as EML and MSG. Detect encrypted, corrupt or unsupported content and send it to a controlled exception queue.

### 3. Resolve the case

Use exact and fuzzy signals:

- Collision Engineers reference;
- client/claim reference;
- registration and VIN;
- party names;
- subject/thread identifiers;
- attachment content;
- message participants;
- temporal proximity.

Low-confidence matches should remain unassigned for review. Incorrectly merging two claims is more harmful than leaving a message temporarily unmatched.

### 4. Classify source role and message purpose

Suggested source roles:

- instructing client;
- claimant/vehicle keeper;
- repairer;
- insurer;
- solicitor;
- engineer;
- internal administrator;
- automated system;
- unknown.

Suggested purposes:

- new instruction;
- evidence supplied;
- clarification;
- evidence request;
- estimate or valuation material;
- query/challenge;
- response;
- amendment request;
- report delivery;
- invoice/fee;
- administrative/no action.

### 5. Extract structured facts and actions

Every extracted value should retain the source span and confidence. Contradictory values should coexist until resolved; the most recent message should not automatically win.

### 6. Build a case timeline

The timeline should show evidence arrival, review events, requests, report versions and deadlines in chronological order.

## Proposed output

```yaml
message_record:
  message_id:
  thread_id:
  case_candidates:
    - case_id:
      confidence:
      matching_signals:
  sender_role:
  purpose:
  received_at:
  extracted_facts:
    - field:
      value:
      source_span:
      confidence:
  attachments:
    - artifact_id:
      hash:
      document_type:
      case_match:
  actions:
    - action:
      owner:
      due_at:
      evidence:
  authority_status: instruction | evidence | third_party_position | ce_output | unknown
```

The `authority_status` is crucial. A confident statement in an email is not automatically a fact accepted by Collision Engineers.

## Model opportunities

Small pretrained language models or embedding systems can support:

- message-to-case matching;
- sender-role and intent classification;
- vehicle/reference extraction;
- deadline and action extraction;
- attachment-type classification;
- thread summarisation;
- duplicate and supersession detection;
- priority scoring.

Fine-tuning becomes useful when generic models repeatedly misread house references, specialist terminology or recurring client formats. A large generative model is not required for every step.

## Attachments and evidence lineage

Attachments should be content-addressed. If the same image is forwarded, embedded in a PDF and attached again:

- preserve each receipt event;
- link all receipts to one underlying artifact where appropriate;
- record transformations and compression;
- do not multiply it as independent training evidence.

When an attachment is replaced or a report amended, store a version edge rather than overwriting the earlier file.

## Inbox-wide scope control

“Access to all email” is not the same as permission to train on all email. Before ingestion:

- define relevant mailboxes, folders and date ranges;
- exclude personal and unrelated business correspondence;
- identify privileged or specially protected material;
- minimise recipient and signature data;
- set retention and deletion rules;
- document the lawful basis and purpose;
- restrict raw-message access.

The [ICO guidance on AI and data protection](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/artificial-intelligence/guidance-on-ai-and-data-protection/) should inform the design and DPIA.

## Human workflow

The interface should provide:

- a new-instruction queue;
- unassigned or ambiguous messages;
- missing-evidence prompts;
- conflicts in vehicle or claim facts;
- approaching deadlines;
- messages awaiting engineer response;
- report/amendment version links;
- evidence received after the current report cutoff.

Actions should be suggested with an owner and evidence. Users should be able to reject or reassign them, creating clean feedback labels.

## Evaluation

- case-link precision and recall;
- catastrophic cross-case merge count;
- source-role and purpose macro F1;
- attachment recall;
- field-extraction precision/recall;
- deadline and action accuracy;
- duplicate/supersession accuracy;
- percentage of cases with a complete reconstructed timeline;
- unassigned-message review time;
- reduction in missed evidence and overdue actions;
- personal-data leakage outside authorised views.

Evaluate by thread and case, not by randomly splitting individual messages from the same conversation.

## Recommended pilot

Use a defined historical mailbox period in read-only mode. Reconstruct case timelines and compare them with known reports and case folders. Initially expose only:

- suggested case match;
- message purpose;
- attachment inventory;
- extracted references;
- action/deadline candidates.

Do not move, delete, reply to or auto-forward messages. Once case-link precision and privacy controls are proven, connect the output to the wider case-data pipeline.

## Conclusion

Inbox data is highly useful because it supplies chronology, provenance and workflow labels that the report archive lacks. Its best first use is case assembly and operational control. Clean, role-aware email ingestion also makes every later vision, assessment and report model more trustworthy.

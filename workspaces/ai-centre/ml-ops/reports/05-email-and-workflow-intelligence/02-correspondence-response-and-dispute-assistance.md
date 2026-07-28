# Correspondence, Response and Dispute Assistance

## Executive conclusion

Historical correspondence can support a drafting assistant for evidence requests, status updates, technical queries and valuation or repair disputes. The system should retrieve approved prior reasoning and current case facts; it should not learn to mimic every email in the mailbox or automatically send a response.

The source distinction is fundamental: inbound allegations and commercial positions are evidence of what was said, while approved outbound Collision Engineers responses are potential drafting targets.

## Useful correspondence classes

- acknowledgement of instruction;
- request for missing photographs or documents;
- clarification of vehicle or incident facts;
- request for estimates, valuation data or repair information;
- status update;
- report delivery;
- query about damage relatedness;
- challenge to repair operations or labour;
- valuation/PAV challenge;
- total-loss or salvage query;
- request for amendment;
- explanation of remote-evidence limitations;
- fee or administrative response;
- escalation requiring engineer judgement.

Each class has different authority, urgency and approval requirements.

## Data preparation

Construct examples at the message-turn level:

```yaml
correspondence_example:
  case_state_at_draft:
  incoming_message:
    sender_role:
    purpose:
    claims_and_questions:
    source_spans:
  relevant_evidence:
  retrieved_approved_material:
  approved_response:
    author_role:
    sent_at:
    response_type:
  later_outcome:
```

Remove signatures and quoted thread duplication where it does not carry evidential value. Preserve the relationship between the response and the exact case state at that time.

Do not train on:

- unsent drafts without approval state;
- automated boilerplate as if it were technical reasoning;
- inbound wording as the firm's conclusion;
- privileged or unrelated exchanges outside the declared purpose;
- responses later corrected, unless the correction is explicitly modelled.

## Recommended architecture

### Query classification

Classify each incoming message into one or more approved categories and identify the questions that require an answer.

### Case-fact retrieval

Retrieve accepted facts, report sections, estimate lines, valuation evidence, report versions and prior messages from the same case.

### Knowledge retrieval

Retrieve approved query-response material, current policy, technical sources and controlled standard wording. Source role and effective date should be visible.

### Draft generation

Generate a structured response with:

- answer to each identified question;
- evidence or reference citation;
- explicit unknowns;
- request for missing evidence where needed;
- proposed attachment/version;
- required reviewer role.

### Pre-send validation

Check case identity, recipients, attachments, dates, monetary values, unsupported assertions and conflicts with the signed report.

No response should be sent without an authorised user's explicit action during the initial deployment stages.

## Replicating house style

Fine-tuning can improve:

- concise professional tone;
- recurring salutations and closing conventions;
- consistent terminology;
- organisation of multi-part answers;
- appropriate expression of uncertainty;
- separation of technical opinion from administrative content.

A style model should be trained only on approved outbound messages and reports. It should not reproduce individual signatures or personal mannerisms unless that use is specifically authorised.

For most deployments, retrieval plus a house-style guide should be tried before fine-tuning. Fine-tuning is justified where approved responses are numerous, consistent and difficult to reproduce reliably through prompts.

## Dispute support

The most valuable system does not merely draft defensive text. It should create a dispute map:

```yaml
dispute_map:
  issues:
    - issue_id:
      opposing_position:
      source_message:
      ce_current_position:
      supporting_evidence:
      unresolved_questions:
      possible_response:
      engineer_decision_required:
```

This makes it clear which points are factual, which concern methodology and which require an amended opinion.

Past outcomes can identify recurring query types and useful evidence, but they should not be used to predict which party “should win”. The product supports independent reasoning rather than adversarial optimisation.

## Failure modes

- replying about the wrong vehicle or report version;
- treating a third-party statement as an accepted fact;
- citing an obsolete method or value;
- inventing an attachment or promising an action;
- changing the professional opinion without engineer review;
- disclosing another case's information;
- sending a polished but non-responsive answer;
- escalating tone unnecessarily;
- copying privileged or personal content.

These failures require both technical validation and user-interface controls.

## Evaluation

Offline measures:

- issue and question extraction recall;
- retrieval relevance;
- factual consistency;
- source-citation accuracy;
- unsupported-assertion rate;
- correct report/version selection;
- PII and cross-case leakage;
- tone and policy compliance.

Workflow measures:

- response preparation time;
- engineer edit and rejection rate;
- questions missed;
- follow-up messages caused by an incomplete response;
- query resolution time;
- amendment accuracy;
- incident and near-miss count.

Reviewers should score whether the draft preserves independence, not merely whether it sounds persuasive.

## Recommended pilot

Begin with low-risk evidence requests and acknowledgements, followed by technical query-response drafts in shadow mode. Exclude automatic sending, recipient selection and autonomous opinion changes.

For each draft, show:

- the incoming questions;
- current accepted case facts;
- retrieved precedent/source;
- generated response;
- warnings and unresolved conflicts.

Move to live drafting only after cross-case leakage tests, recipient/attachment checks and engineer review metrics meet agreed gates.

## Conclusion

Email can teach Collision Engineers' response patterns and provide a searchable history of recurring disputes. The safe and useful product is an evidence-backed drafting workspace. It accelerates correspondence while keeping technical conclusions, amendments and the send action under human control.

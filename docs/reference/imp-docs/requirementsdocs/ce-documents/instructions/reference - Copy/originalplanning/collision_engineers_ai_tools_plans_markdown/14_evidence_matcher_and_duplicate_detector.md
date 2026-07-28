# Evidence Matcher and Duplicate Detector

Generated: 2026-05-22

**Type:** Workflow tool + model
**Priority:** High

## Objective

Match documents, images, estimates, emails, and notes into the correct work item while preventing duplicate cases and duplicate files.

## Why it matters for Collision Engineers

The core operational problem is asynchronous evidence and holding-pen reconciliation. Vehicle registration is strong but not sufficient; claim references, sender, batch, timestamps, and semantic similarity all matter.

## Proposed shape

A scoring engine generates match suggestions with reasons. A review UI approves/rejects medium-confidence matches and writes decisions back to the model/training set.

## Candidate tools / MCP methods / skill actions

- `score_evidence_match(evidence_id, candidate_work_item_id)`
- `suggest_matches(evidence_id)`
- `detect_duplicate_email(message_id)`
- `detect_duplicate_file(file_hash)`
- `detect_duplicate_work_item(canonical_fields)`
- `approve_match(evidence_id, work_item_id)`
- `split_or_merge_work_items(work_item_ids)`

## Inputs

- Extracted fields
- email metadata
- file checksum
- sender
- subject
- timestamps
- existing work items

## Outputs

- Match score
- reason list
- duplicate status
- review requirement
- audit event

## Guardrails

- Never auto-merge conflicting high-risk evidence.
- Reasons must be visible.
- Human approval for medium/ambiguous matches.
- No source file deletion.
- All merge/split decisions logged.

## MVP implementation path

1. Implement deterministic weights for VRM/ref/sender/batch/time.
2. Add queue for unmatched images/docs.
3. Add reviewer decisions.
4. Add learning/reporting later.

## Test / acceptance criteria

- Known batch links documents/images correctly.
- Conflicting VRM blocks auto-match.
- Duplicate email and duplicate file ignored safely.
- Reviewer decision is logged.

## Risks and open questions

- Weak references.
- Multiple cases from same sender/time.
- Forwarded emails.
- False matches are more damaging than false non-matches.

## Project source basis

- context_pack_1/05_target_workflow.md
- context_pack_2/10_WORKFLOW_STATES_AND_ORCHESTRATION.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools

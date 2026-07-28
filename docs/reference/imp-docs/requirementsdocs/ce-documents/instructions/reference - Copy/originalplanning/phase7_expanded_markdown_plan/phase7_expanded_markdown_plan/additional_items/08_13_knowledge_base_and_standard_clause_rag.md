# 8.13 Knowledge Base and Standard Clause Assistant

## Purpose

Build a controlled internal knowledge base for approved wording, process notes, provider rules, estimate/ABP references, report-delivery templates and technical guidance snippets.

## Why this matters

Staff and engineers rely on scattered knowledge. A retrieval-based assistant can make that knowledge easier to find, but it must cite sources and avoid inventing technical conclusions.

## Step-by-step plan

### Step 1 — Define knowledge sources

1. Approved CE email templates and tone guidance.
2. Provider/principal process notes.
3. EVA setup guidance.
4. Box/folder handling rules.
5. CE Document Mapper guidance.
6. Report-delivery wording.
7. ABP/rate notes approved for internal use.
8. Audatex/estimate handling notes.
9. Runbooks and release notes.

### Step 2 — Classify source trust levels

1. Official procedure.
2. Approved template.
3. Internal note.
4. Historical example.
5. Draft/unapproved material.
6. Retired/deprecated material.

### Step 3 — Build retrieval with citations

1. Store documents with source title, version and date.
2. Retrieve relevant passages for the user.
3. Display citations or source references.
4. Prefer current approved sources over old historical examples.
5. Warn when sources conflict or are outdated.

### Step 4 — Limit assistant scope

1. Can draft routine communications.
2. Can summarise internal procedure.
3. Can suggest where to find provider rules.
4. Can explain why a case is blocked.
5. Cannot produce final expert opinions without engineer review.
6. Cannot override approved templates or live provider config.

### Step 5 — Add feedback loop

1. Staff mark answers helpful/not helpful.
2. Staff flag outdated or wrong knowledge.
3. Admin updates the underlying source document.
4. Knowledge base refreshes and records version.
5. Track most common unanswered questions.

### Step 6 — Connect to workflows

1. In review queue: show provider-specific help.
2. In chaser drafting: retrieve tone/template guidance.
3. In estimate review: retrieve approved rate/ABP notes.
4. In runbooks: retrieve troubleshooting steps.
5. In onboarding: provide process walkthroughs.

## Deliverables

- Knowledge source register.
- Trust-level taxonomy.
- Retrieval/citation system.
- Feedback mechanism.
- Knowledge governance process.

## Acceptance criteria

- Answers cite source material or clearly say no source was found.
- Deprecated sources are not used silently.
- Staff can flag outdated guidance.
- Technical/expert conclusions remain gated by human review.

## Risks and controls

| Risk | Control |
|---|---|
| Assistant invents unsupported guidance | Require citation-backed answers and scope limits. |
| Old procedures resurface | Version and deprecate sources explicitly. |
| Sensitive examples leak | Redact examples and limit access by role. |

## Suggested priority

P2. Useful after core workflow data and approved source documents are structured.

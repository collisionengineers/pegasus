---
name: collisionspike-domain
description: Apply Collision Engineers case-management terminology, references, workflow, and source-of-truth rules. Use when designing schemas, UI states, APIs, intake, numbering, audits, case lifecycle, reminders, Box or EVA integration, permissions, or tests that encode business meaning.
---

# CollisionSpike domain

Read `AGENTS.md` and `docs/agent-guidance/source-of-truth.md`. Use operator notes as read-only authority. The predecessor and corpus describe historical reality but cannot override v2 decisions.

## Core rules

- Work provider and principal are synonyms. A principal code is a separate value from display name.
- A normal reference is `{principal code}{two-digit current year}{three-digit principal/year sequence}`. Example: `QDOS26001`.
- All work types share that principal/year sequence. A repairable audit is `a.{base reference}`; a total-loss audit is `ap.{base reference}`.
- A standalone Audit takes its prefix from the repairable/total-loss assessment in the original Engineer's report. Missing or ambiguous source evidence blocks case creation and reference allocation.
- Inspection + Audit starts as a normal inspection. After Collision Engineers' own assessment, the Engineer creates the applicable audit reference and Box subfolder.
- Image-led work uses vehicle registration as its identifier until a case/principal reference is assigned.
- Before report submission, correct a principal on the same case by allocating from the corrected principal's sequence for the correction year. Retain the old reference as a searchable alias and never reuse either number.
- Work can be re-opened; retain the original case identity, history, and audit events.
- Never delete a case. Initial terminal outcomes are post report, provider cancellation, and Collision Engineers rejection.
- The instruction carries an inspection date or similar `due by` value. Outstanding chasers recur every seven days.
- First-MVP chasers are manually sent copyable messages; Box File Requests may support collection.
- `Triage` means the reserved pre-case state. The operator inbox label is `Needs sorting`.
- `Blocked intake` is a manual inbox filter with a required reason. It retains the source but creates no case or reference until staff resolve and retry it.
- `Not ready` is incomplete work being chased; `Review` is complete work awaiting approval; `Held` is a reasoned manual pause that stops progression and chasers while due dates remain visible.
- Case data is in the new application. EVA remains an integration authority and Box remains the long-term file store.
- Accounts use application-managed usernames/passwords; do not assume each staff member uses a Microsoft account.

Read [domain-invariants.md](references/domain-invariants.md) before implementing business behavior. Read [open-decisions.md](references/open-decisions.md) before committing a schema or state machine; it routes to the canonical project register rather than duplicating it.

## Method

1. Cite the authoritative source for the requested behavior.
2. Express behavior as inputs, ordered decisions, output/state, audit event, and failure/unknown outcome.
3. Test literal examples plus contradictions.
4. Surface unresolved policy; do not fill it from predecessor code.

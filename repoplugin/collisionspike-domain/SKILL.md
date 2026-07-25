---
name: collisionspike-domain
description: Apply Collision Engineers case-management terminology, references, workflow, action-history, and source-of-truth rules. Use when designing schemas, UI states, APIs, intake, numbering, Audit work, case lifecycle, reminders, Box or EVA integration, permissions, or tests that encode business meaning.
---

# CollisionSpike domain

Read `AGENTS.md` and `docs/agent-guidance/source-of-truth.md`. Use operator notes as read-only authority. The predecessor and corpus describe historical reality but cannot override v2 decisions.

## Core rules

- Work provider and principal are synonyms. A principal code is a separate value from display name.
- A principal code is immutable after first use. A legitimate replacement is a linked new principal with atomic predecessor deactivation. Its cutover-year sequence continues the predecessor's next number; later years start at `001`.
- A normal reference is `{principal code}{two-digit current year}{three-digit principal/year sequence}`. Example: `QDOS26001`.
- All work types share that principal/year sequence. A repairable Audit reference is `a.{base reference}`; a total-loss Audit reference is `ap.{base reference}`.
- A standalone Audit takes its prefix from the repairable/total-loss assessment in the original Engineer's report. Missing or ambiguous source evidence blocks case creation and reference allocation.
- Inspection + Audit starts as a normal inspection. After Collision Engineers' own assessment, the Engineer creates the applicable Audit reference and Box subfolder.
- Image-led work uses vehicle registration as its identifier until a case/principal reference is assigned.
- A case principal/reference is immutable immediately on allocation. Wrong-principal allocation closes the original as `Created in error`, requires a reason and link to a new replacement case, and never reuses either reference.
- An authorised staff user may reopen with a reason to any otherwise-valid nonterminal state; normal gates still apply. `Held` is a separate action, and `Created in error` never reopens.
- Never delete a case. Initial terminal outcomes are post report, provider cancellation, Collision Engineers rejection, and wrong-principal `Created in error`.
- The instruction carries an inspection date or similar `due by` value. Outstanding chasers recur every seven days.
- The first chase is due at the same Europe/London local clock time seven calendar days after entering `Not ready`. `Held` preserves the prior state and remaining interval. Release offers the prior state or `Review`; return to `Not ready` resumes the interval, while `Review` ends the chase.
- First-MVP chasers are manually sent copyable messages; Box File Requests may support collection.
- `Triage` means the reserved pre-case state. The operator inbox label is `Needs sorting`.
- Business Triage is a separate pre-case record with an active-record vehicle-registration requirement, `Open`/`Awaiting information` -> `Finding recorded` -> `Completed` states, binary `Roadworthy`/`Unroadworthy` findings, and `Cancelled` as the only end without a finding. It has no due date or chasers.
- `Blocked intake` is a manual inbox filter with a required reason. It retains the source but creates no case or reference until staff resolve and retry it.
- `Not ready` is incomplete work being chased; `Review` is complete work awaiting approval; `Held` is a reasoned manual pause that stops progression and chasers while due dates remain visible.
- Case data is in the new application. EVA remains an integration authority and Box remains the long-term file store.
- Accounts use application-managed usernames/passwords; do not assume each staff member uses a Microsoft account.
- There is no pre-send report review gate. CollisionSpike detects reports but does not send them. Sent evidence is one exact Outlook Sent item from an Administrator-approved mailbox; Outlook `sentDateTime` is authoritative. Automatic matching remains deferred to the combined mailbox categorisation and email-matching research.
- Reserve `Audit` solely for Collision Engineers' actual business work type. Use permanent action history, action event, history, security log, or content-safe telemetry for technical and accountability concepts.

Read [domain-invariants.md](references/domain-invariants.md) before implementing business behavior. Read [open-decisions.md](references/open-decisions.md) before committing a schema or state machine; it routes to the canonical project register rather than duplicating it.

## Method

1. Cite the authoritative source for the requested behavior.
2. Express behavior as inputs, ordered decisions, output/state, permanent action event, invariants, examples, and failure/unknown outcome.
3. Test literal examples plus contradictions.
4. Surface unresolved policy; do not fill it from predecessor code.

# Page 3 — Triage queue → Queues: alteration plan

Source: `src/Pegasus.Web/Pages/Triage/Index.cshtml`. Operator notes: `../page3.md`.
Screenshots reviewed: `page3.png`, `cases.png`. Governing standards:
`../../ui-standards-and-review.md` (§2 vocabulary, §3.1 IA, §4 presentation rules).

## Review

### Aesthetics

A sparse page — eyebrow ("Triage"), H1 ("Triage queue"), lede, six filter buttons, and (in the
shipped build) a single empty panel reading *"No triage records match this view."* The empty
state is the page for most users. The heading stack is three deep for one word of information,
and the filter row carries six options for a screen that has no content behind any of them.

### Practicality

The operator's verdict: *"Page 3 seems to completely contravene repository rules and
guidelines. The page is called 'Triage Queue' — this is already a reserved term for another
business function."* Triage is a case **type** (technically a pre-case); this screen was meant
to be the viewer for **pre-engineer-assignment case queues**:

- **Not ready** — a case missing information (images, key details).
- **Review** — a case with all its details that needs one-click manual confirmation /
  assignment to an engineer.
- **Held** — a case placed on manual hold by staff.

None of those three states appears on the current page. Instead the filters are
Open / Awaiting information / Finding recorded / Completed / Cancelled — the lifecycle of the
internal triage **record**, a different entity (and one no composition can currently create;
see the standards' defects note). The lede — *"Intake-backed vehicle assessments requiring
review or follow-up."* — describes neither.

### Performance, design and good practice

- The empty state *"No triage records match this view."* is DB-speak: it names the storage
  row, not the operator's work.
- Row secondary text prints `Assigned: {AssigneeId}` — a raw identifier where a person's name
  belongs (standards §4.4).
- The reserved term "Triage" spent on this screen means the actual Triage concept (a pre-case
  assessment type) has no vocabulary left when it ships; the term must leave this page
  entirely (nav, title, headings, filters, empty states).
- Review confirmation today requires opening the record; the operator requires a one-click
  confirm from the queue row.

## Changes

1. **Rename the page.** Nav item, `<title>`, and H1: "Triage queue" → **"Queues"**. The word
   "Triage" does not appear anywhere on the redesigned screen.
2. **Remove the eyebrow and the lede** (*"Intake-backed vehicle assessments requiring review
   or follow-up."*). H1 + content only.
3. **Replace the filter row with three tabs carrying counts**: **Not ready (3) · Review (1) ·
   Held (0)** — the pre-engineer-assignment case queues. "Needs sorting" stays on the e-mail
   side (Inbox); it is not a case stage.
4. **Retire the current filters** (Open / Awaiting information / Finding recorded / Completed /
   Cancelled) from this page: they belong to the internal triage-record lifecycle, not to case
   queues. If triage-type records ever ship a UI, they surface as a record-type filter within
   Cases, under the reserved meaning.
5. **Row layout per tab**: Case reference · registration · claimant · principal · waiting
   context (what is missing / since when) · stage chip.
   - **Review rows carry a one-click "Confirm" button** directly in the row, which confirms
     the case and passes it to engineer assignment. One click; any reason/undo flow follows the
     existing confirmation patterns (single confirmation, standards §4.8).
   - Not ready rows show what is missing in business words ("Images missing",
     "Claim number missing").
   - Held rows show who placed the hold and when, with a "Release" action if policy allows.
6. **Empty states in business language**, per tab: "No cases are waiting." (Not ready),
   "No cases are ready to confirm." (Review), "No cases are held." (Held). The string
   "No triage records match this view." is deleted.
7. **Assignee display**: names, never IDs; unassigned shows "Unassigned".
8. **Metric links line up with the Dashboard**: the Dashboard's Active cases tiles link to
   these tabs one-to-one.

## Dependencies

- **Backing queries change entity**: today the page queries triage records
  (`Model.Results` of triage rows). The redesigned page needs case-queue queries: cases in
  Not ready / Review / Held with counts, plus the waiting-context fields (missing-material
  reason, held-by, held-at).
- **One-click confirm command**: a Core operation that confirms a Review case from the queue
  row (single action, recorded actor), and its authorisation rule.
- **Staff display names** for assignee/held-by rendering instead of raw IDs.
- Route: `/Triage` should redirect to the new Queues route so old links survive; the reserved
  term disappears from user-facing URLs going forward.

## Open questions

- Held-case "Release" from the row: allowed one-click like Confirm, or does policy require a
  reason? Mockups show the row action; the reason gate is a policy decision.
- Tab order Not ready → Review → Held is assumed from work priority; confirm with the
  operator if Review should lead.

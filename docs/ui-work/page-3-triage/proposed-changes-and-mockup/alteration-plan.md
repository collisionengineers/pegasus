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

1. **Rename the page.** Nav item, `<title>`, and H1: "Triage queue" → **"Queues"**. "Triage"
   never names the screen, the nav item, or the title again — it appears only as the one tab
   that genuinely holds Triage records, in its reserved meaning.
2. **Remove the eyebrow and the lede** (*"Intake-backed vehicle assessments requiring review
   or follow-up."*). H1 + content only.
3. **Replace the filter row with four tabs carrying counts**: **Not ready (3) · Review (1) ·
   Held (0) · Triage (0)** — the pre-engineer-assignment work queues. The first three are Case
   stages; **Triage is a separate entity, not a case stage** — a pre-case staff workflow for a
   recorded matter requiring a finding ([requirements](../../../requirements.md)), which is
   exactly why it needs its own tab rather than being folded into a case-stage filter.
   "Needs sorting" stays on the e-mail side (Inbox); it is neither a case stage nor Triage.
4. **The current filters become the Triage tab's sub-states, not the page's filters.**
   Open / Awaiting information / Finding recorded / Completed / Cancelled are the triage-record
   lifecycle (`TriageState`, TRI-03) and belong under the Triage tab, defaulting to open work
   (Open · Awaiting information · Finding recorded) with Completed and Cancelled reachable but
   not shown by default. They must never sit at page level, where they read as case stages.
5. **Row layout per tab.** The three case tabs: Case reference · registration · claimant ·
   principal · waiting context (what is missing / since when) · stage chip.
   - **Review rows carry a one-click "Confirm" button** directly in the row, which confirms
     the case and passes it to engineer assignment. One click; any reason/undo flow follows the
     existing confirmation patterns (single confirmation, standards §4.8).
   - Not ready rows show what is missing in business words ("Images missing",
     "Claim number missing").
   - Held rows show who placed the hold and when, with a "Release" action if policy allows.
   - **Triage rows carry no case reference** — a triage record is pre-case, so the row leads
     with registration · claimant · principal · state chip (Open / Awaiting information /
     Finding recorded) · waiting context · assignee. Where a triage record has been linked to a
     case, the reference renders as a trailing link; it is never a leading identifier, because
     most rows will not have one.
6. **Empty states in business language**, per tab: "No cases are waiting." (Not ready),
   "No cases are ready to confirm." (Review), "No cases are held." (Held), "No triage work is
   open." (Triage). The string "No triage records match this view." is deleted.
7. **Assignee display**: names, never IDs; unassigned shows "Unassigned".
8. **Metric links line up with the Dashboard**: the Dashboard's Active cases tiles link to
   these tabs one-to-one.

## Dependencies

- **Backing queries gain an entity**: today the page queries triage records only
  (`Model.Results` of triage rows). The redesigned page needs case-queue queries alongside
  them: cases in Not ready / Review / Held with counts, plus the waiting-context fields
  (missing-material reason, held-by, held-at). The existing triage query survives as the
  fourth tab's backing query — it is the one part of the current page that keeps its entity.
- **Triage counts and default sub-state filter**: a count of open triage work (Open ·
  Awaiting information · Finding recorded) for the tab badge, and the sub-state filter within
  the tab. Both are new; today's page filters one state at a time with no counts.
- **The Triage tab will read 0 in every deployment until B2 is fixed.** The only registered
  `IIntakeTriageMatcher` is `NoAcceptedIntakeTriageMatcher`, so no composition can create a
  triage record (`defects-and-non-functional.md` §B2). This is an honest 0 from a real query,
  not a placeholder, so the tab ships under standards §4.2 — but the tab is not evidence that
  the triage pipeline works, and it must not be cited as such.
- **One-click confirm command**: a Core operation that confirms a Review case from the queue
  row (single action, recorded actor), and its authorisation rule.
- **Staff display names** for assignee/held-by rendering instead of raw IDs.
- Route: `/Triage` should redirect to the new Queues route so old links survive; the reserved
  term disappears from user-facing URLs going forward, surviving only as a tab label.

## Open questions

- Held-case "Release" from the row: allowed one-click like Confirm, or does policy require a
  reason? Mockups show the row action; the reason gate is a policy decision.
- Tab order Not ready → Review → Held → Triage is assumed from work priority, with Triage last
  because it is a different entity rather than a lower priority; confirm with the operator if
  Review should lead or Triage should sit first.
- Triage tab default sub-state: assumed to be all open work (Open · Awaiting information ·
  Finding recorded) with Completed and Cancelled reachable through a sub-filter. Confirm
  whether Awaiting information should be split out as its own chip, since it is the state that
  waits on a third party rather than on Collision Engineers.

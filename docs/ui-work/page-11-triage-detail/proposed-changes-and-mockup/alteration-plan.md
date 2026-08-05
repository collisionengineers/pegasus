# Page 11 — Triage record — alteration plan

> Vocabulary note: the legacy term is written `in·take` where current identifiers must be named.

## Review summary

The current screen cannot be reached: the only `IIn·takeTriageMatcher` implementation is
`NoAcceptedIn·takeTriageMatcher`, so no Triage record can exist in any deployment and the page's
sole observable state is a raw browser 404. The source reveals a seven-form wall — every action
its own panel with its own Reason textarea — plus a GUID/hash identifier panel, lease-token
narration, a typed-GUID "Case ID" field, and dropdowns stuffed with message and evidence
identifiers. The label maps and the findings/history separation are good and are kept.

## Container restructure (operator decision, 2026-08-04)

This screen is one record, so it takes the shape every record screen now takes
(`../../ui-standards-and-review.md` §4 rule 13): **one container** holding a header band, an
action bar, and the tabs **Finding · Replies · History**.

The page-12 case detail is the reference implementation. What that changes here, over and
above the numbered changes below:

- The main-column/side-column split is retired. **Complete**, **Reassign** and **More** become
  the action bar, with **View e-mail** at its right-hand end.
- **Complete** stays visible and disabled with its condition named on the control — "Available
  once a finding is recorded and a reply is linked" — rather than being hidden until it works
  (rule 9: absence is for capabilities, disabled-with-a-condition is for conditions).
- Origin moves into the header band and, as one row, into the Finding tab; it no longer needs a
  card of its own.
- **Record finding** stays with the form it submits, inside the Finding tab.

Nothing in the change list is withdrawn — the copy, vocabulary, state and evidence decisions all
stand; they are re-housed.

## Changes

1. **Reachability and IA**: "Triage" is gone as a nav item; the word survives as the Triage tab
   inside **Queues**, which is the primary way these records are reached. This screen becomes
   **"Triage record"**, reached from a Queues Triage row, and also from a Triage-type case or
   pre-case under **Cases**; breadcrumb `Queues › Triage › AB12 CDE` from the queue, `Cases ›
   Triage › AB12 CDE` from a case. The `/Triage/{id}` route stays for links.
2. **Styled not-found page**: the raw 404 → a designed not-found state ("This record does not
   exist or you do not have access to it." + a link back to Cases), shared with every
   unknown-record URL. This ships **first**, since it is the only state users can currently
   reach.
3. **Header**: eyebrow + H1 + lede → H1 **registration** (AB12 CDE) with a **state chip**
   (Open / Awaiting information / Finding recorded / Completed / Cancelled) and the **assignee
   by name** ("alex", never a GUID), plus an "Assign to me" inline action when unassigned.
4. **Layout**: seven stacked form panels → **two columns**. Main column: the work — **Finding**
   (Roadworthy / Unroadworthy, Repairable / Total loss, reason, one Record button) and **Reply**
   (linking the insurer's reply in plain language). Side column: the context — **Origin e-mail**
   link and **History**.
5. **"In·take source mapping" panel** → a single origin line: "From an e-mail received
   4 Aug 2026 08:55 — **View e-mail**". Receipt GUID, revision GUID, and SHA-256 leave the
   markup.
6. **Reply linking copy**: "Select only retained approved-mailbox evidence whose exact
   In-Reply-To identity matches…" → **"Replies to the finding e-mail appear here
   automatically. Link the reply that answers this record."** Candidate rows show sender,
   subject, and received time — no message ids, no evidence GUIDs.
7. **Lease narration**: "Pegasus claims short-lived case edit authority for this operation;
   lease tokens are never shown or entered here." → deleted. No replacement; the operator
   never needed to know.
8. **Case association**: typed "Case ID" GUID input → a case picker by **reference or
   registration** with candidate rows; linked case shown as "Case 26001 →". One consequence
   line on the action only.
9. **Action grammar**: "Reopen to Open" → **"Reopen"**; "Complete Triage" → **"Complete"**
   (header action, enabled when a finding and reply are linked); Cancel and Await information
   move into a header **More** menu. Reasons stay mandatory where policy requires them
   (finding, correction, reopen, cancel, unlink) but "Assign to me" becomes one click.
10. **History**: "(version 3 to 4)" suffixes → removed; each line is event label, actor name,
    time, reason.

## Dependencies

- **Blocking**: an accepted matcher implementation must exist before any state other than
  not-found is reachable — this whole redesign is held behind that engineering decision.
  The not-found page (change 2) has no such dependency and can ship now.
- Actor names require resolving staff ids to display names (exists elsewhere in Admin).
- Case picker needs the same candidates query family as page 10.
- Nav/IA rework shared with all pages; "Queues" takes the old Triage list's URL.
- Whether the one-reason-per-action policy can relax for self-assignment is a Core policy
  question — flagged, not assumed.

## Open questions

- Does "Complete" require both a finding **and** a linked reply, or is a finding alone enough
  when no reply is expected? Source implies finding-then-reply; operator confirmation needed.
- Post-send correction: kept as a guarded action on the completed state ("Record a correction")
  — does the operator want it surfaced or buried in More?
- Should Triage records appear in the Cases list as rows of type "Triage", or only via their
  parent case? Proposed: a "Triage" case-type filter chip in Cases.

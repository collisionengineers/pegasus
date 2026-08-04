# Page 29 — Approved mailboxes: alteration plan

Source: `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml`.
Review: `../review.md`. Standards: `../../ui-standards-and-review.md`.

## Review summary

A policy table hidden inside permanently-open edit forms, with the banned term in both the
lede and a checkbox label, raw enum joins ("InboundIntake, SentEvidence") in the Route
scope column, and an internal version integer on display. The redesign separates reading
from editing: a clean four-column table with labelled scopes and status chips, one row at a
time expanding into an edit form, and a single add form below.

## Changes

1. **Replace the lede with one line of consequence guidance, relocated.** Old: *"This
   allowlist approves an address for one or both fixed read-only routes: inbound Intake
   from Inbox, or exact Sent evidence. It does not grant Exchange access and provides no
   mailbox browsing, message sending, credentials, rules, or folder controls."* New: no
   lede; one sentence sits with the add form, where the committing action is:
   **"Pegasus reads e-mail only from the addresses approved here."**
2. **Rename the route checkboxes** (both the per-row edit form and the add form):
   - Old **"Inbound Intake (Inbox)"** → **"Receiving (Inbox)"**
   - Old **"Exact report and Triage evidence (Sent Items)"** → **"Sent evidence
     (Sent Items)"** — "Sent evidence" is the settled business term and stays.
   - Fieldset legend: old *"Read-only route scope"* → **"What Pegasus reads"**.
3. **Label the table's scope values.** Old column "Route scope" rendering the raw enum join
   (*"InboundIntake, SentEvidence"*) → new column **"Reads"** rendering the labels:
   **"Receiving (Inbox)"**, **"Sent evidence"**, or **"Receiving (Inbox) · Sent
   evidence"**. No enum `ToString()` reaches markup (standards §4.3).
4. **Remove the "Version" column.** `ExpectedVersion` stays as a hidden field per edit form
   (standards §4.4).
5. **Close the edit forms.** The table shows Address · Reads · Status · Edit. Selecting
   **Edit** expands that row (single expansion at a time) into the five-field form;
   everything else stays a readable row. The permanently-open per-row forms are removed.
6. **Status chips**: Approved (green-bordered chip), Disabled (muted chip) — never
   colour-only, label always present.
7. **Redesign the empty state.** Old: *"No mailbox addresses are approved."* New: **"No
   addresses are approved — Pegasus is not reading any e-mail. Add an address below."**
8. **Add form kept below the table**, heading **"Add an approved address"**, same field
   set and labels as the row edit form (one form definition, two uses), with the change-1
   sentence above the button and button label **"Add address"** (old: "Add mailbox
   policy" — "policy" is internal vocabulary).
9. **Row save button**: old *"Save mailbox policy"* → **"Save"** (within the expanded row,
   context carries it). Reason field hint: **"Recorded permanently with the change."**
10. **Design the stale-save state** (currently a Razor comment): attention status card
    above the table — **"This address's approval changed while you had it open. Reload to
    see the current settings, then reapply your change."**
11. **One heading stack.** Eyebrow and back link replaced by breadcrumb "Administration /
    Approved mailboxes"; H1 stays "Approved mailboxes". Section label "Current policies"
    becomes unnecessary (the table is the page); "Add an approved address" remains the
    single H2.

## Dependencies

- Row expansion (change 5) needs a small progressive-disclosure mechanism: either a
  no-script fallback (Edit links to `?edit={id}` and the server renders that row expanded)
  or minimal script. The no-script query-string variant is the plan's default — it keeps
  the page fully server-rendered.
- Scope labels need a hand-labelled map in the page model (the pattern already used by
  `RecordTypeLabel` on the Automation activity page), replacing `string.Join(", ",
  mailbox.RouteScopes)`.
- Moving `OperationKey` generation out of the view into the page model fixes the
  fresh-key-per-render idempotency gap (review lens 3).
- Distinguishing stale-version rejection from validation errors (change 10) needs the same
  handler split as page 28.

## Open questions

- Should the table show "Last changed" (date + administrator) per row now that reasons are
  captured? The audit data exists; adding the column is one query. Mockups omit it —
  flagged for operator decision.
- Is disabling an address (rather than un-approving a scope) common enough to keep the
  State select, or should Disable be a row action with its own reason prompt? Mockups keep
  the select inside the edit form.

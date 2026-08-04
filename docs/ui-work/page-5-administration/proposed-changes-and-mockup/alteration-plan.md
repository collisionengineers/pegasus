# Page 5 — Administration: alteration plan

Source: `src/Pegasus.Web/Pages/Administration/Index.cshtml`. Operator notes:
`../administration.md`. Screenshot reviewed: `administration.png`. Governing standards:
`../../ui-standards-and-review.md` (§2 vocabulary, §4 presentation rules).

## Review

### Aesthetics

Eight identical cards in a flat four-column grid with no grouping — people-management sits
beside mailbox plumbing beside workflow gates, and nothing tells the eye which third of the
page it needs. The lede is architecture narrated as a subtitle: *"Manage staff access,
organizations, principals, workflow configuration, and approved mailbox routes through
authenticated, permanently recorded administration callers."* The operator's whole note:
*"Page overall too wordy and should be rearranged."*

### Practicality

Card descriptions are written from the implementation outward, not the job inward. Verbatim:

- *"Assign the Administrator, Engineer, and User roles without allowing the final enabled
  Administrator to be removed."* — a guard-rail detail as the headline.
- *"Create immutable principal identities and replace them through a linked,
  sequence-preserving successor."* — sequence lineage is internal; the job is "add and
  replace principals".
- *"Manage the versioned completeness and staff-review gates required before Engineer
  assignment."* — versioning is internal.
- *"Review the Automation actor's permanently recorded activity and enable or disable its
  authentication client registration."* — client registration is internal.
- *"Manage independently selectable Work Provider and Instruction Intermediary roles."* —
  data-model phrasing for "manage organisations".

An administrator scanning for "add a new staff member" or "approve a mailbox" has to parse
five lines of schema language per card to find the verb.

### Performance, design and good practice

- The page violates the one-sentence-job rule everywhere; descriptions restate invariants the
  destination pages already enforce (the final-Administrator guard, immutability, recording)
  instead of naming the task.
- No grouping means the page scales badly: a ninth card would join an undifferentiated grid.
- The current copy leans on internal vocabulary ("callers", "routes", the inbound pipeline
  name) that the vocabulary standard bans from user-facing surfaces.

## Changes

1. **Remove the lede** (*"…through authenticated, permanently recorded administration
   callers."*). Single H1 **"Administration"**.
2. **Group the eight cards under three H2 headings** (standards §3.1):
   - **People and access** — Staff accounts · Staff roles · Access review
   - **Organisations and principals** — Organizations · Principals
   - **System** — Workflow configuration · Approved mailboxes · Automation
3. **Rewrite every card description as one job-focused line** (old → new):
   - Staff accounts: *"Create staff accounts, permanently disable access, and require a
     password change at first sign-in."* → **"Add staff, disable access, and reset
     first-sign-in passwords."**
   - Staff roles: *"Assign the Administrator, Engineer, and User roles without allowing the
     final enabled Administrator to be removed."* → **"Set who is an Administrator, Engineer,
     or User."**
   - Access review: *"Review current staff access and record an attributable review
     decision."* → **"Check who has access and record the review."**
   - Organizations: *"Manage independently selectable Work Provider and Instruction
     Intermediary roles."* → **"Manage work providers and instruction intermediaries."**
   - Principals: *"Create immutable principal identities and replace them through a linked,
     sequence-preserving successor."* → **"Add principals and replace them when they
     change."**
   - Workflow configuration: *"Manage the versioned completeness and staff-review gates
     required before Engineer assignment."* → **"Set the checks a case must pass before it
     goes to an Engineer."**
   - Approved mailboxes: *"Approve mailbox addresses for the fixed inbound Intake and exact
     Sent-evidence routes."* → **"Choose the mailbox addresses Pegasus accepts e-mail from
     and sends from."**
   - Automation: *"Review the Automation actor's permanently recorded activity and enable or
     disable its authentication client registration."* → **"See what runs automatically and
     switch it on or off."**
4. **Restyle**: cards keep icon + linked title + one line; groups render as labelled sections
   with their own grids so a future ninth card lands in an obvious home. Card titles stay the
   whole-card link target.
5. **Guard-rail and invariant messaging moves to the destination pages** (where it already
   exists at the point of action) — e.g. the final-Administrator protection surfaces when an
   admin actually tries to remove that role, not on the index card.

## Dependencies

None beyond markup and copy — the eight destinations, their routes, and their behaviour are
unchanged. Grouping is presentation only.

## Open questions

- Spelling: nav and headings use "Organisations" (British) in the proposed grouping while the
  destination page is titled "Organizations" (American). One spelling should win
  application-wide; the mockups follow the existing page title "Organizations" for the card
  and the operator's heading term for the group. Needs a one-line operator decision.

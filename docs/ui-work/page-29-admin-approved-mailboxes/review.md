# Page 29 — Administration / Approved mailboxes: review

Source: `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml`.
Screenshot: `approved-mailboxes.png`. Governing standards: `../ui-standards-and-review.md`
(§2 vocabulary, §4 presentation rules).

## 1. Aesthetics

- The lede — *"This allowlist approves an address for one or both fixed read-only routes:
  inbound Intake from Inbox, or exact Sent evidence. It does not grant Exchange access and
  provides no mailbox browsing, message sending, credentials, rules, or folder controls."*
  — commits both cardinal copy sins at once: the banned term "Intake" in user-facing text
  (standards §2) and a second sentence that is pure self-negating narration, listing five
  things the page cannot do. "Allowlist", "fixed read-only routes" — engineering
  vocabulary throughout.
- The table's "Route scope" column renders the raw enum join — a mailbox approved for both
  routes prints **"InboundIntake, SentEvidence"** — PascalCase compound enum values on an
  operator screen (standards §4.3, banned).
- The "Version" column prints the internal concurrency integer (**"1"**) for every row —
  meaningless to an administrator (standards §4.4).
- The layout is dominated by the "Update" column: an entire five-field edit form
  (address, two checkboxes, state select, reason, button) is permanently rendered inside
  every table row. With one mailbox the form is already ~400px tall; with ten mailboxes
  the page becomes ten stacked forms pretending to be a table.

## 2. Practicality

- The operator's questions are "which addresses does Pegasus read, what does it read from
  each, and are they on?" The always-open inline forms bury those answers inside editing
  chrome; reading the current policy means visually filtering out input fields.
- The checkbox label **"Inbound Intake (Inbox)"** uses the banned term where the business
  word is simply receiving; **"Exact report and Triage evidence (Sent Items)"** buries the
  settled business term ("Sent evidence") under a compound qualifier.
- Editing any row and adding a new address use the same handler and nearly identical
  forms, but the add form's field order and fieldset copy are duplicated wholesale — two
  maintenance surfaces for one form.
- The empty state — *"No mailbox addresses are approved."* — states the fact but not the
  consequence (Pegasus is reading nothing) or the next step (add one below).
- A "Reason" is demanded for every save but the page never shows previous reasons or when
  a policy last changed — the operator writes history into a void.

## 3. Performance / Design / Good practice

- Per-row `OperationKey` regeneration (`Guid.NewGuid().ToString("N")` for non-posted rows)
  is done in the view — logic in markup, and a fresh key every render defeats idempotent
  retry for any row except the last-posted one.
- `ExpectedVersion` per row is correct optimistic concurrency; only its *display* is wrong
  (standards §4.4). The stale-save recovery text lives in a Razor comment (*"recovery is
  to reload and reapply against the current version"*) rather than in any operator-visible
  designed state.
- The status chip partial receives `mailbox.State.ToString()` — works today because the
  enum values read as words, but it is an enum-to-markup path of exactly the kind
  standards §4.3 bans.
- `inputmode="email"` and `maxlength` are good touches; the address input lacks
  `type="email"` validation.
- Accessibility: the table-with-forms structure makes row scanning with a screen reader
  painful (each row announces ~10 controls); separating read view from edit state fixes
  this for free.

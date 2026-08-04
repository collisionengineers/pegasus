# Page 11 — Triage record (today: the Triage detail screen) — review

> Vocabulary note. The legacy pipeline term is banned from every deliverable in this folder set,
> including this review. Wherever current copy or code identifiers must be quoted, the term is
> written `in·take` (with an interpunct) so the zero-occurrence check stays clean.

**Reachability finding (also a defect).** The capture in this folder is a raw browser 404 —
the only state this screen can present locally, or in **any** current deployment: the only
implementation of `IIn·takeTriageMatcher` in the product is `NoAcceptedIn·takeTriageMatcher`,
so no composition can ever create a Triage record, and every `/Triage/{id}` URL 404s. The raw
404 itself violates rule 6 of the standards file — unknown-record URLs must render a styled
not-found page. This review is therefore written from the source,
`src/Pegasus.Web/Pages/Triage/Details.cshtml`.

What the screen would be: the working record for one Triage-type assessment — registration in
the H1, state in the lede, then panels: "Triage record", "In·take source mapping", and action
panels "Assignment", "Workflow action", "Finding", "Exact response evidence", "Post-send
correction", "Reopen", "Case association", followed by "Recorded findings", "Response
evidence", and "Permanent history".

## 1. Aesthetics

- **Seven stacked action panels, each a full form with its own Reason textarea.** Assignment,
  Workflow action, Finding, Exact response evidence, (Post-send correction), Reopen, Case
  association — a wall of near-identical white forms with no visual priority. The screen's one
  real job (record a finding, link the reply) has the same weight as its rarest escape hatch.
- **A second panel of identifiers.** "In·take source mapping" prints "Receipt ID" (GUID),
  "Evaluation revision" (GUID), and "Source SHA-256" (64-hex in `<code>`) at full width — the
  same raw-identifier wall as page 10, violating rule 4.
- **GUIDs where people belong.** "Assignee" renders `AssigneeId?.ToString()` — a GUID or
  "Unassigned"; "Linked case" renders a case GUID; the finding-supersede dropdown labels
  options with finding GUIDs ("04 Aug 2026 16:41 — 3f2e…").
- **Dropdown options are data dumps.** The reply-evidence select renders "04 Aug 2026 16:41 —
  mailbox@… — message &lt;internet-message-id&gt; — Sent evidence &lt;GUID&gt;" as one option
  string — unreadable at select-box width.

## 2. Practicality

- **Concurrency plumbing narrated at the operator.** "Case association is retained evidence and
  advances both workflow versions. Pegasus claims short-lived case edit authority for this
  operation; lease tokens are never shown or entered here." Telling the operator about tokens
  they will never see is the purest form of the narration disease — copy that exists only to
  describe its own implementation.
- **Engineering constraints as instructions**: "Select only retained approved-mailbox evidence
  whose exact In-Reply-To identity matches this Triage's Sent evidence. Subject or registration
  similarity is never accepted." The system enforces this match itself — the candidate list
  already contains only exact replies — so the paragraph asks the operator to enforce a rule
  they cannot violate.
- **"Case ID" is a typed GUID field.** Linking a case requires pasting a raw GUID into a text
  input. No search, no candidate list, no reference entry.
- **Snake_case action grammar leaks**: buttons post `await_information`, `record_finding`,
  `link_response`, `supersede_finding` — invisible, but the button labels mirror it: "Reopen to
  Open" exposes the state machine ("to Open") instead of saying "Reopen".
- **Every action demands a reason, even assignment to self.** "Assign to me" with a mandatory
  500-character reason textarea is process theatre for the commonest action on the page.
- **Under the new IA this screen is orphaned twice over**: nav says "Triage" but the standards
  file reassigns that word — Triage-type work belongs under **Cases** as a case type, and this
  screen must be reached from a Triage-type case, not from a queue that no longer exists.

## 3. Performance / Design / Good practice

- **Dead code shipped as UI.** Because no matcher implementation can accept a record, every
  panel, handler, and label on this page is unreachable in production. Shipping an elaborate
  seven-form surface with zero possible visitors is exactly the "prototype" verdict of §1.2 of
  the standards file. Until a matcher is accepted, the honest deliverables are (a) a styled
  not-found page and (b) this redesign held ready.
- **The raw 404 is the only observable behaviour** — rule 6 violation, shared with the receipt
  detail page; one styled not-found layout fixes both.
- **Version numbers as content**: "Version" in the record panel and "(version 3 to 4)" in every
  history line — rule 4 bans version integers from operator screens; history needs the event,
  actor, time, and reason only.
- **Good bones worth keeping**: state, roadworthiness, assessment, and event labels already
  pass through hand-written label maps (`StateLabel`, `RoadworthinessLabel`, `AssessmentLabel`,
  `EventLabel`) — this page is the pattern rule 3 asks the rest of the app to follow. The
  findings/history separation (settled facts vs permanent log) is also right and survives the
  redesign.

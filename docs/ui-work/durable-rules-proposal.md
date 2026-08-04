# Durable rules proposal — preventing recurrence of the UI issues

This document specifies the exact edits to canonical repository documents that make the standards
in `ui-standards-and-review.md` durable. **It changes nothing itself.** The edits are to be applied
by a follow-up task through the normal workflow (NOW.md claim → worktree → temp plan → PR into
`dev`), because they touch `docs/operator-notes.md` (operator truth), `design/README.md` (design
authority), `CONTEXT.md` (glossary), and `docs/capabilities.md` (schedule) — all canonical files
whose changes need review.

Why these homes (authority analysis):

- `docs/index.md` already routes "What are the UI rules?" to `design/README.md`, and the authority
  chain ranks `operator-notes.md` (business fact) above everything. So: business-vocabulary
  decisions land in operator-notes; enforceable presentation rules land in design/README; term
  meanings propagate to `CONTEXT.md` (ADR-0010 makes it the sole glossary).
- No ADR is required: `docs/engineering.md` reserves ADRs for decisions that constrain
  architecture; these rules constrain presentation, which design/README owns within the chain.
- No new canonical markdown file is created (repo rule: new files are ADRs or temp-plans only) —
  every edit below amends an existing file.

---

## 1. `docs/operator-notes.md` § Interface language — record the new operator statements

The section currently reads (lines 367-373):

> - Do not include "dev copy" or similar internal or unusual wording.
> - Functions must be apparent from buttons and labels.
> - Do not scatter explanatory sentences throughout the application.
> - The application must not narrate its own functions.
> - Do not expose internal Azure function names, concepts, or wording in the interface.

**Append** the following operator statements (recorded from the operator's UI review and answers,
2026-08-04). These are additions; no existing statement changes meaning:

> - The word "intake" is an internal development term and must not appear anywhere in the
>   interface. Interface surfaces use business language: e-mail activity, Inbox, Upload,
>   received items, vehicle images.
> - "Blocked intake" is renamed to "Blocked" in the interface, without explanatory copy.
> - File sizes are never shown in bytes. Where a size is relevant (for example an e-mail
>   attachment or an upload limit) it is shown in megabytes; otherwise it is not shown.
> - A count of zero is shown as 0. Placeholder states such as "Unavailable" must not stand in
>   for numbers, and a metric whose query does not exist must not be shown at all.
> - The interface never displays raw internal identifiers: GUIDs, hashes, storage paths,
>   database or enum value names, event codes, or version integers.
> - "Needs sorting" refers to e-mail that cannot be matched; it is not a case stage.
> - Screens are screens in an application, not pages on a website. They are compact, and
>   scrolling is minimised: the identity, the state, the available actions and the main content
>   of a screen are visible without scrolling.
> - A screen about one record shows that record inside one container, with its actions as a bar
>   at the top and its sections as tabs — not as separate panels stacked down the page.
> - A case's material is called **Evidence**, and covers files, images and e-mail.
> - Where a value or a document came from is shown as an icon with a one-word explanation on
>   hover: Staff, Extracted, AI, E-mail, Lookup, Principal, Automatic.
> - An action that this record will offer once a condition is met stays visible and disabled,
>   with the condition named on it. Exporting a case is available when the case is in Review.

Note: `## Reserved terms` already states "the reserved list may be extended over time" — no change
needed there; "intake" is not reserved, it is banned from the interface layer only (it remains a
valid internal code identifier).

## 2. `design/README.md` § Design principles — sharpen into enforceable rules

Current lines 59-60:

> - Controls communicate purpose without narrating obvious actions.
> - Do not expose Azure, OCR, AI, queue mechanics, extraction engines, deployment or adapter terminology in operator copy.

**Replace with:**

> - Controls communicate purpose without narrating obvious actions. Pages carry no lede or
>   subtitle: one H1 and the content. Guidance appears only beside a control whose action has a
>   consequence the operator must understand, and is one sentence.
> - Do not expose Azure, OCR, AI, queue mechanics, extraction engines, deployment, adapter,
>   lease/version, projection, ingress, or artifact terminology in operator copy. The word
>   "intake" never appears in operator-facing text (operator decision 2026-08-04).
> - Every state value shown to an operator passes through an explicit operator-label map; raw
>   `ToString()` of enums, snake_case event codes, GUIDs, hashes, storage paths, version
>   integers and byte counts never reach markup. File sizes, where relevant, are megabytes.
> - A composed query that returns zero renders `0`. A capability that is not composed in a
>   deployment is absent from the interface — never a disabled item, inert card, or
>   "Unavailable" placeholder. Genuine runtime failure renders the designed failure state with
>   the last-good time.
> - Every screen defines its empty, loading, and failure states in business language, and
>   unknown-record URLs render the styled not-found page, never a raw browser error.
> - Screens are compact working surfaces, not marketing pages: 4px base rhythm with 8/12/16
>   steps, 32px table rows, 12–16px panel padding, 13.5–14px body text. A screen about a single
>   record is one container — header, action bar, tabs — and the operator reaches its identity,
>   its state, its available actions and its main content without scrolling.
> - Provenance is an icon with a one-word tooltip, shown on hover **and** on keyboard focus with
>   a matching accessible name: Staff · Extracted · AI · E-mail · Lookup · Principal ·
>   Automatic. Source labels, policy keys and provenance sentences do not appear in markup.

The "capability that is not composed is absent" rule above needs one qualification appended, or it
contradicts stage-gated actions:

> This applies to capabilities, not to conditions. An action that the record in front of the
> operator will genuinely offer once a condition is met stays visible and disabled with the
> condition named on the control ("Available in Review"); removing it would assert that the
> action is impossible, which is false.

Current line 62:

> Settled terms retain their exact meanings and casing, including `Audit`, `Triage`, `Needs sorting`, `Blocked intake`, `Not ready`, `Review` and `Held`. Never substitute a generic **Close** action for a named lifecycle outcome.

**Replace `Blocked intake` with `Blocked`** in the settled list, with a dated note:

> Settled terms retain their exact meanings and casing, including `Audit`, `Triage`,
> `Needs sorting`, `Blocked`, `Not ready`, `Review` and `Held` (`Blocked` supersedes the earlier
> interface wording `Blocked intake`, operator decision 2026-08-04; the pre-case failure boundary
> it names is unchanged). Never substitute a generic **Close** action for a named lifecycle
> outcome.

## 3. `design/README.md` § Voice, labels and necessary copy — update the approved specimen

Current (line 285):

> > Blocked intake — no case has been created. A reason is required.

**Replace with** (the operator asked for no narrating copy; the reason requirement is a
consequence, which is the one kind of guidance the voice section permits — keep it minimal):

> > Blocked — a reason is required.

Also **append** to this section:

> A banned-terms list is enforced at review time for `src/Pegasus.Web/Pages/**/*.cshtml` and
> PageModel label maps: `intake`, `bounded`, `projection`, `lease`, `opaque`, `ingress`,
> `composed`, `artifact`, `durable`, `aggregate`, `queue` (as user-facing copy), `caller`,
> `correlation identifier`, `bytes`. A PR that introduces one of these into user-facing copy does
> not merge. (Suggested check: `git grep -inE "intake|bounded|lease|ingress|artifact|bytes" --
> "src/Pegasus.Web/Pages/**/*.cshtml"` reviewed against user-facing strings; a scripted variant
> can live in `scripts/` if the team wants it automated.)

## 4. `design/README.md` § Product direction / Operations-first shell — new navigation and shell rules

- Update the approved route/nav order from
  `Operations → Intake → Triage → Cases → Administration (admin-only) → Search + user controls`
  to `Dashboard → Inbox → Upload → Queues → Cases → Administration (admin-only) + user controls`,
  recording: Search merged into Cases (identical backing query), page-2 split into Inbox and
  Upload, Queues as the pre-engineer-assignment work viewer (Not ready / Review / Held /
  Triage — the first three Case stages, the fourth a separate pre-case entity), and "Triage"
  no longer naming a screen, nav item, title or route.
- In the Operations-first shell rules, the existing sentence "`Blocked intake` is exact wording"
  updates to "`Blocked` is exact wording", and the existing "0 is a current result never a
  substitute for stale/unavailable" rule gains: "and no shipped tile may render a placeholder for
  a query that does not exist."
- Update the `DecisionLabel` mapping table: `BlockedIntake` → `Blocked`;
  `ImageIntakeRegistered` → `Vehicle images registered` (or the operator-settled wording).
  `DraftReady` gets **no** operator label: the decision is removed, not renamed. Definitive
  authorised intake creates the case directly (`requirements.md:251` — "the allocation decision adds
  no universal manual acceptance gate"); ambiguous or unidentified material is `Needs sorting`
  (`operator-notes.md:204`). An earlier draft of this file proposed `DraftReady` → `Ready to review`;
  that is withdrawn — it renamed a state that should not exist, and `Review`/`Ready to review` is
  the Case stage before the report is with an Engineer (`CaseWorkflowContracts.cs:15`,
  `requirements.md:295`). See `defects-and-non-functional.md` §B4.

## 5. `CONTEXT.md` (glossary) — record the interface-vocabulary layer

Add a short subsection stating: internal domain identifiers (`Intake*`, `ImageIntake*`, etc.)
remain unchanged in code; the interface layer maps them to operator vocabulary via label maps.
Record the pairs: intake receipt → received item; intake queues → e-mail activity; Blocked intake →
Blocked; Image intake / Image Intake Reference → vehicle image / image reference; State (case
filter) → Case stage. This keeps ADR-0010's single-glossary rule intact while making the two-layer
vocabulary explicit. `DraftReady` is deliberately absent from the pairs — it is not a business state
and is being removed, not mapped (`defects-and-non-functional.md` §B4). `Review` and `Ready to
review` denote the Case stage only.

## 6. `docs/capabilities.md` — wording and truth-up

- UI-03's row wording ("intake queues") updates to the e-mail activity naming.
- UI-02 ("Case queues for Not ready, Review, and Held") and UI-04 ("New cases today, Sent to
  Engineer, and Reports sent day/week activity") are recorded as **not implemented** (the dashboard
  ships hardcoded placeholders; no Core count queries exist) — their "Required and accepted before
  0.1.0-alpha.1" qualification is factually wrong and needs the capabilities truth-up treatment
  (this overlaps the in-flight `task/docs-truth-up`; coordinate rather than duplicate).
- UI-14 (categorised e-mail queues) gains a pointer that the classification decision is already
  persisted (MAIL-21/22) and the surfacing is UI-only work.

## 7. `design/product/ui-spec.md` — per-surface propagation

The presentation-contract file mirrors the §2/§4 changes where it names surfaces: the shell
section adopts the new nav; the UI-07 search section records the Cases/Search merge; the state
matrix adds the styled not-found page and the "queued item visible in Inbox" state (defect M9).

## 8. What is deliberately NOT proposed

- No ADR (presentation rules, not architecture).
- No code renames of `Intake*` identifiers — the ban is interface-layer only.
- No new canonical markdown file.
- No change to reserved-term machinery for `Audit`/`Triage` (their meanings are untouched; page 3
  simply stops misusing `Triage`).

## 9. Sequencing for the follow-up task

1. Operator-notes additions (§1) first — they are the authority everything else cites.
2. design/README edits (§2-§4) + CONTEXT.md (§5) + ui-spec (§7) in the same PR (the design
   change-and-verification rule requires authority, map, and implementation to move together —
   since this is a docs-only change ahead of implementation, record the affected surfaces as
   Planned in the evidence-discipline terms).
3. capabilities.md corrections (§6) coordinated with `task/docs-truth-up`.
4. Implementation tasks then consume the per-page `alteration-plan.md` files folder by folder.

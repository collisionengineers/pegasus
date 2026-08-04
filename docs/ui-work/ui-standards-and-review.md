# Pegasus UI standards and whole-application review

Date: 2026-08-04. Scope: every reachable screen of Pegasus.Web at `dev` @ `b6d030b`, reviewed from
the deployed screenshots in `page-1` … `page-5` and a full local capture pass
(`dotnet run`, DevelopmentOffline, fresh `PegasusUiWork` LocalDB, genuine corpus material only).
This file is the anchor for all 31 page folders: it defines the verdicts, the vocabulary, the
information architecture, and the two mockup systems every `proposed-changes-and-mockup/` folder uses.

---

## 1. The three questions, answered

### 1.1 "Show, don't tell" — does the UI narrate itself?

**Verdict: fails today.** The application repeatedly explains its own mechanics instead of showing
state. The worst offenders, verbatim from the current build:

| Where | Current copy | Why it fails |
|---|---|---|
| Dashboard lede | "Every current queue for the office, with the exact filter behind each count." | Narrates the page instead of being it |
| Dashboard workspace cards | "Mailbox outcomes and owned retries. No dashboard aggregate exists for this route." | A tile explaining why it has no number is a tile that should not exist |
| Dashboard | "Bounded inventory from the latest refresh: 0 pending · 1 failed · 0 orphaned · 0 unmatched" | Reconciliation diagnostics narrated at the operator |
| Cases lede | "Search the cases you are authorised to access. Filters remain in the URL when you move between result pages." | Browser mechanics narrated as guidance |
| Upload panel | "Original bytes are retained before durable processing." | Storage internals narrated at the operator |
| Case workspace | "An opaque edit lease is active for this response. A successful change consumes it." | Concurrency internals narrated as instructions |
| Administration lede | "…through authenticated, permanently recorded administration callers." | Architecture narrated as a subtitle |

The rule that replaces all of this: **a control's label states the job; a state chip states the
fact; guidance appears only where an action has a consequence the operator must understand before
committing** (this is already the `design/README.md` voice rule — it is not being enforced).
Ledes/subtitles are removed everywhere; a page's `<h1>` plus its content is the explanation.

### 1.2 Professional and release-ready — or prototype?

**Verdict: prototype.** Specific evidence, all reproduced this session:

- **Nine dashboard tiles and both workspace cards are hardcoded to the literal string
  "Unavailable"** (`Pages/Index.cshtml:7-11`, `const string Absent = "Unavailable"`). The backing
  queries do not exist in Core. A first-run user sees a wall of grey "Unavailable" pills.
- **The manual upload button does nothing.** The Intake page's upload form renders `action=""`
  (the `asp-page-handler` URL is never generated), so "Queue instruction" posts to a handler that
  doesn't exist and the page silently re-renders. No error, no receipt, nothing. Verified against
  the live local build; forcing the correct handler URL makes the same form work.
- **A 25 MB upload returns a raw browser "HTTP ERROR 400" page**, not the designed "must be 10 MB
  or smaller" message.
- **Unknown record URLs return raw browser 404s** (triage detail, receipt detail) — no styled
  not-found page exists.
- **Raw identifiers on operator screens**: 64-char hex source-receipt ids, `staging/…` blob paths,
  byte counts (`10378983 bytes`), sequence-lineage GUIDs in the Principals table, snake_case event
  codes (`case_created`) in case history.
- **Raw enum values as user-facing text**: the Cases "State" dropdown shows `NotReady`,
  `ReportPreparation`, `PostReportComplete`, `CollisionEngineersRejected`; the Search results table
  and case summary do the same.
- **The sign-out page is unstyled** — a bare `<h1>` and a default browser button, the only screen
  off the design system.
- **The freshness banner can only ever say "Current"** — the loading/stale/partial/unavailable/
  failed states required by the design contract are dead code; it can also label a UTC time
  "London" when timezone data is missing.
- **`docs/capabilities.md` claims UI-02/UI-04 "Required and accepted before 0.1.0-alpha.1"** while
  the page ships placeholders — the roadmap and the product disagree.

Release-ready means: every number is real or the tile is absent; every failure has a designed,
worded state; no raw identifier, enum, hash, path, or byte count is ever printed; every screen
including sign-out and errors is on the design system.

### 1.3 Would it make sense to someone who has never used it?

**Verdict: no.** The interface speaks the codebase's language, not the business's:

- "Intake", "Intake queue", "Blocked intake", "Staged intake artifacts", "Image intakes" — internal
  pipeline vocabulary. The operator's own statement: *"We don't have intake queues — intake is
  automatic. Nothing needs to queue."*
- "Triage queue" for a screen that is not about Triage-type work at all — a reserved business term
  spent on the wrong concept.
- "Queue instruction" as an upload button; "Record corrected draft"; "Enter case edit mode" with a
  typed "Expected case version" number; an "Artifact SHA-256" input a human is expected to fill.
- "Bounded view; more exist", "The principal projection is bounded" — projection jargon as
  pagination copy.
- Duplicated confirmation checkboxes ("Instruction evidence is complete" AND "I have confirmed the
  instruction evidence") that read like an audit schema, not a form.

---

## 2. Vocabulary: banned terms and replacements

**"intake" is banned from every user-facing surface** (nav, titles, headings, labels, buttons,
chips, empty states, error messages, `<title>` tags). It remains a perfectly good internal code
identifier — no C# renames are required or proposed.

| Current user-facing term | Replacement | Notes |
|---|---|---|
| Intake (nav item) | **Inbox** and **Upload** (two nav items) | Page 2 splits; see IA below |
| Intake queue (h1) | **Inbox** | The list of received e-mail and uploads |
| intake queues (dashboard section) | **E-mail activity** | Operator's naming |
| Case queues | **Active cases** | Operator's naming |
| Blocked intake | **Blocked** | Operator decision 2026-08-04; no narrating suffix copy |
| Intake receipt / intake resolution | **Received item** / plain verbs ("Sort", "Block") | Context carries it |
| Image intakes / Image Intake Reference | **Vehicle images** / **Image reference** | Pre-case image records keyed by registration |
| Staged intake artifacts | *(removed from dashboard)* | Relocates to a system-health surface if kept at all |
| Queue instruction (button) | **Upload** | |
| Receive intake (section) | **Upload a document** | |
| Instruction drafts (chip) | *(chip removed entirely)* | `DraftReady` is not a business state and gets no operator label — definitive intake is a case, ambiguous intake is **Needs sorting**. See `defects-and-non-functional.md` §B4. **"Review"/"Ready to review" is reserved for the Case stage** and must never label an intake filter. |
| Document text required (chip) | **Needs text extraction** — or keep operator label if settled | Decide with operator-notes update |
| Intake unavailable (nav span) | *(nav item simply absent)* | Never show a disabled nav item |
| State (Cases filter) | **Case stage** | With human labels, never enum names |
| Documents (case section) | **Evidence** — sub-tabs **Files · Images · E-mails** | Operator decision 2026-08-04. One home for everything a case carries; the separate "Image in·takes" section folds in, and linked e-mail gets a home it has never had |
| Triage queue (page 3 h1) | **Queues** (pre-engineer-assignment viewer) | "Triage" stops naming the screen, nav item, title and route; it survives as one tab inside Queues, in its reserved meaning |

Also banned in user-facing copy: *bounded, projection, lease, opaque, ingress, composed/composition,
artifact, durable, retained bytes, aggregate, route (as plumbing), caller, policy re-evaluation,
operation key, correlation identifier* (show "Reference" if operators genuinely need it), enum
`ToString()` of any kind, GUIDs, hashes, blob paths, and **byte counts — filesizes are shown in MB
(one decimal) where relevant (e-mail attachments, uploads) and otherwise not at all**.

Renames that need canonical-doc propagation (operator statement → CONTEXT.md → design/README.md →
capabilities.md) are specified exactly in `durable-rules-proposal.md`.

---

## 3. Information architecture

### 3.1 New navigation

```
Dashboard · Inbox · Upload · Queues · Cases · Administration          alex · Change password · Sign out
```

- **Dashboard** (was Operations, `/`): counts that are real, three sections — *Active cases*
  (Not ready / Review / Held), *E-mail activity* (Received today / Queries outstanding / Needs
  sorting), *Today and this week* (New cases, Sent to Engineer day/week, Reports sent day/week);
  an Engineer's own **To do** section (assigned reports, e-mail queries) visible to Engineer
  accounts only. Requires new Core count queries — flagged as implementation prerequisites, not
  assumed.
- **Inbox** (was Intake, `/Intake`): the e-mail activity viewer. Rows show sender, subject,
  received time, and state chip (Case ⟨reference⟩ / Needs sorting / Blocked / Vehicle images) —
  never a filename hash. Upload moves out entirely. There is no "Ready to review" chip: definitive
  intake is already a case and the row links to it; only ambiguous or unidentified material sits
  in a pending state, as Needs sorting (`defects-and-non-functional.md` §B4).
- **Upload** (new surface, was the "Receive intake" panel): a proper drop-zone page for manual
  submission, 10 MB limit stated as "up to 10 MB", designed success/duplicate/failure states.
- **Queues** (was Triage, `/Triage`): the pre-engineer-assignment case queues — Not ready /
  Review / Held tabs with counts; Review rows carry the one-click confirm action. "Needs sorting"
  stays on the e-mail side (it means unmatched e-mail, not a case stage). Triage-type records
  become a filter within Cases and the reserved term is never used for this screen again.
- **Cases** (`/Cases`): absorbs Search. Compact top-anchored filter bar (Case/PO or keyword box +
  Case stage dropdown + Principal dropdown + date range behind a "More filters" disclosure), results
  table with proper stage chips. `/Search` retires and redirects.
- **Administration**: unchanged scope; grouped cards (People and access / Organisations and
  principals / System), one-line job-focused descriptions.

Screens that gain a navigation path (today orphaned): Email operations and Request operations
(reachable only via dashboard cards that claim "Unavailable"), Vehicle images list (today reachable
only 4 clicks deep). **Email operations stops being a separate screen**: operator decision
2026-08-04 merges it into Inbox as the Received/Sent direction tabs plus a Failed filter, which
solves its discoverability by removing the orphan rather than labelling it (page 2 plan). Request
operations becomes a Dashboard drill-down with an honest label; Vehicle images becomes an Inbox
filter chip.

### 3.2 Page merge/retire map

| Today | Becomes |
|---|---|
| Operations `/` | Dashboard (rebuilt sections, no Unavailable pills, no staged-artifacts panel, compact corner refresh) |
| Intake `/Intake` | Inbox (viewer only), absorbing Operations Email — Received/Sent tabs, Failed filter, retry in the row |
| — | Upload (new page, extracted) |
| Triage `/Triage` | Queues (Not ready / Review / Held / Triage) |
| Cases `/Cases` | Cases (compact filters + keyword search absorbed) |
| Search `/Search` | **Retired** — redirect to `/Cases?query=…` (verified: both run the identical Core query) |
| Operations Email `/Operations/Email` | **Merged into Inbox** — redirects to the Received tab with the Failed filter (operator decision 2026-08-04) |
| Operations Requests | Dashboard drill-down, honest entry card |
| Image intakes list/detail | Vehicle images (Inbox filter + detail) |

---

## 4. Presentation rules (apply to every mockup and every future screen)

1. **No page ledes/subtitles.** H1 + content. Consequence-guidance sits inline next to the specific
   control it concerns, one sentence maximum.
2. **Zero is zero.** A composed query that returns 0 renders `0`. A tile whose query does not exist
   is not shipped. "Unavailable" chips are banned as placeholders; genuine runtime failure of a real
   query renders the designed failure state with the last-good timestamp.
3. **Every state string passes through an operator-label map.** Raw `enum.ToString()`, snake_case
   event codes, and PascalCase compounds never reach markup. The existing hand-labelled maps
   (`Triage`, `Intake`, `Operations`, close/reopen selects in `_CaseWorkflow.cshtml:205,211`) are
   the pattern; Cases is the gap.
4. **No raw identifiers.** GUIDs, hashes, storage keys, correlation ids, sequence-lineage ids and
   version integers are internal. Where operators genuinely need a stable handle, show the business
   reference (Case/PO, Image reference, registration).
5. **Filesizes in MB** (one decimal) and only where the size matters to the operator; never bytes.
6. **Every screen has designed empty, loading, and failure states** written in business language
   ("No cases match these filters", not "No triage records match this view"). Unknown-record URLs
   render a styled not-found page, never a raw browser 404.
7. **One heading stack.** Kicker/eyebrow labels are dropped; pages get exactly one H1 and section
   H2s. Sibling pages use consistent heading grammar.
8. **Forms confirm once.** One checkbox per confirmation; a reason field where policy requires a
   reason; no duplicated "X is complete" + "I have confirmed X" pairs.
9. **Disabled ≠ visible — for capabilities, not for conditions.** A capability that is not
   composed in a deployment is **absent**: no inert card, no disabled nav span, no
   permanently-"Unavailable" tile. An action that *this record will genuinely offer once a
   condition is met* is the opposite case: it stays in place, **disabled, with the condition
   named on the control** ("Available in Review"). Removing it would say "this cannot be done",
   which is false. Test: *will this control ever enable itself for the thing I am looking at?*
   Yes → show it disabled with the condition. No → it does not exist here.
10. **Every metric is a link to the exact filtered list behind it** (kept from the current design
    contract — it is right).
11. **Refresh/last-updated is a compact corner element** (timestamp + refresh icon-button), not a
    full-width banner, and the redundant "Current" badge is dropped; non-current states use the
    designed stale/failed chips.
12. **Accessibility floor**: labelled controls, visible focus, chips never colour-only (kept
    from design/README; the mockups honour it).
13. **Application density, not landing-page density.** These are screens in an application that
    an operator works in all day, not marketing pages. Minimise scrolling: the identity, the
    state, the available actions and the primary content of a screen fit above the fold at
    1280×800. This is a *density* rule, not a cramming rule — hairlines, alignment and
    whitespace still do the separating; there is simply less of it. Working figures, which the
    page-12 mockups implement and every other mockup is measured against:

    | | Value |
    |---|---|
    | Base rhythm | 4px, steps of 8/12/16 (was 8/16/24/32) |
    | Body text | 13.5–14px |
    | Table row height | 32px (was 40px) |
    | Fact/detail row height | 28–30px |
    | Card/panel padding | 12–16px (was 24px) |
    | Gap between blocks | 12–16px (was 24px) |
    | Page h1 | 19–20px (was 22–24px) |
    | Action/tab bar height | 38–44px |

    Structural consequence, not just spacing: **a screen about one record is one container** —
    header, action bar, tabs — not a vertical stack of sibling panels the operator scrolls
    through. Tabs beat stacked sections whenever the sections are alternatives rather than a
    reading order.

    The container has three parts and only three: a **header band** carrying the reference, the
    state chip and the identity; an **action bar** carrying every action valid for the current
    state, with the record-level commitment right-aligned behind a divider; and either **tabs**
    or a plain body. Tabs appear when the sections are alternatives (Overview / Evidence /
    History); a record with one section, or with sections that form a reading order, gets a body
    and no tab row. An action opens a **dialog** carrying its own fields; a form's own submit
    stays with the form, because a submit is not a lifecycle action.

    Status: applied. Page 12 is the reference implementation; the shape is also built on page 8
    (received item), page 10 (image reference detail), page 11 (triage detail) and pages
    20/24/26/27 (the admin record screens), in both the hardened and refreshed systems. Every
    other mockup has had the spacing figures applied. List and dashboard screens are not record
    screens and keep their own shape.
14. **Provenance is an icon with a one-word tooltip.** Where a value or a piece of evidence came
    from is never a sentence, a source label, a policy key or a third table column. It is a
    small icon at the end of the row whose tooltip — on hover **and** on keyboard focus, with a
    matching accessible name — is exactly one word: **Staff · Extracted · AI · E-mail · Lookup ·
    Principal · Automatic** (operator decision 2026-08-04; the mapping onto persisted
    `CaseDataSourceKind` values is in the page-12 plan). The row must still make sense with the
    icon ignored.

---

## 5. The two mockup systems

Every folder's `proposed-changes-and-mockup/` contains `mockup-hardened.html` and
`mockup-refreshed.html` — self-contained (inline CSS, no external requests), openable by
double-click, desktop-first at 1280px+.

### 5.1 Hardened (current identity, fixed)

Uses today's tokens exactly (`src/Pegasus.Web/wwwroot/css/site.css` / `design/README.md §Tokens`):

- Colours: red `#DB0816` (sparse: primary action + active nav only), charcoal `#2C2A27`,
  ink `#16191D`, paper `#F5F4F2`, panel `#FFFFFF`, hairline `#E6E4E1`, muted `#6B6B6B`,
  green `#16833B` (confirmed completion only), amber trio (pending/incomplete), navy trio (Review).
- Shape: 2px radius, 1px hairlines, 3px red focus ring at 38% alpha.
- Type: system UI stack, 14–16px body, existing scale; uppercase section labels retained but
  reduced to one per card cluster.
- Spacing: 4px rhythm, 24px gutters.
- What changes is **content and structure only**: copy per §2/§4, layout per the page's wireframe,
  states designed, density corrected.

### 5.2 Refreshed (design pass; divergences declared)

A visibly upgraded system that stays on-brand. Divergences from the recorded design authority are
listed in a comment block at the top of every refreshed mockup and summarised here:

1. **Typography**: keeps the system stack but adds a real scale — 22/17/15/13px with 600-weight
   headings and tabular numerals for metrics (feature-setting `font-variant-numeric: tabular-nums`).
2. **Surface model**: paper `#F7F6F4`, cards `#FFFFFF` with 6px radius and a 1px hairline plus a
   very low shadow (`0 1px 2px rgb(22 25 29 / 5%)`) — replaces the flat 2px/hairline-only card.
3. **State colour roles unchanged** (amber/navy/green/red semantics preserved exactly) but chips
   gain a 10% tinted background with the 1px border, improving scanability without colour-only
   signalling.
4. **Metric tiles**: 28px tabular numerals, label under number, whole tile is the link, hover
   raises the hairline to charcoal — replaces label-over-number cards with a separate link.
5. **Density**: 4px base rhythm with 8/12/16 steps; tables at 32px rows; filter bars single-line
   with disclosure for advanced fields. *(Superseded figures: the first pass used an 8px rhythm
   with 16/24/32 steps and 40px rows. The operator's verdict on that pass — "these are huge, they
   seem like a homepage" — is now §4 rule 13; the page-12 mockups are the reference
   implementation and the remaining mockups are being brought down to match.)*
6. **Left-rail option is NOT taken** — top nav is kept so the refresh remains an evolutionary step.
7. **One container per record.** A screen about a single record (case, received item, image
   reference) is one card with a header band, an action bar and tabs — see the page-12 mockups.
   The refreshed system adds a dark header band (`#1B1E23`) and a stage-coloured 3px accent to
   that container; it is the only filled dark surface in the system, and it does not spend the
   brand red.

Both systems use **schematic placeholder content only** (e.g. `AB12 CDE`, `Principal A`,
`Case 26001`, `Sample Claimant`) — mockups never contain corpus-derived e-mail content, real
claimant names, or fabricated realistic documents.

---

## 6. Folder conventions

- One folder per screen. The original five keep their names; the two `page-5-*` folders are a
  known numbering collision, tolerated rather than renamed (screenshots and review text reference
  them heavily). New folders run `page-6` … `page-31`; their slugs use the new vocabulary
  (`page-8-receipt-review`, `page-9-image-references-list`, …).
- Each folder: screenshot(s) (`*.png`), a review markdown (operator's original file, or `review.md`
  in new folders, using the same three lenses: aesthetics / practicality / performance-design-good
  practice), and `proposed-changes-and-mockup/` containing `alteration-plan.md`, `wireframe.md`,
  `mockup-hardened.html`, `mockup-refreshed.html`.
- Screens that cannot be reached in the local build carry their reachable state's screenshot plus
  an explicit note naming the blocking mechanism (all four cases are also defects/findings):
  - `page-13-public-upload` — upload-request creation is not composed locally; raw 404 captured.
  - `page-14-sign-in`, `page-16-sign-out` — DevelopmentOffline auto-authenticates every request;
    both screens redirect. Reviews are written from source (`Pages/Account/*.cshtml`).
  - `page-11-triage-detail` — no `IIntakeTriageMatcher` implementation exists in the product other
    than `NoAcceptedIntakeTriageMatcher`, so no composition can ever create a Triage record; raw
    404 captured. See `defects-and-non-functional.md`.

Companion documents in this folder:

- `additions-hidden-features.md` — shipped capabilities with no UI visibility, and what to surface.
- `defects-and-non-functional.md` — everything found broken or inert, with evidence.
- `durable-rules-proposal.md` — exact edits to canonical docs so these standards outlive this review.

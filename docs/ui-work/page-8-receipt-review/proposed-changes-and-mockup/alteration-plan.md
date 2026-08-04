# Alteration plan — Received item review (was the receipt drill-down)

Exact current copy strings are quoted with file references in `../review.md`; this plan
masks the banned vocabulary word as `[banned]` where an old string contains it.

> **Premise correction — the accept surface should not exist.**
> This plan, the wireframe, and both mockups were written around "Accept as case" as the
> screen's primary action. That premise is wrong. `requirements.md:251`: *"Definitive
> authorised intake creates exactly one instructed Case idempotently… The allocation
> decision adds no universal manual acceptance gate."* `operator-notes.md:204` sends only
> ambiguous or unidentified material to `Needs sorting`. So a definitive instruction is a
> case at processing time, and this screen never sees it in a pending state.
> What survives: this is the **Needs sorting / Blocked** working surface — sort, correct,
> block, re-evaluate, register vehicle images, and link to the case where one exists. What
> goes: the Accept rail, the accept checklist, the "to accept" requirements panel, the
> `DraftReady`/"Ready to review" chip, and every accept-gated enablement rule. The layout,
> vocabulary, hierarchy, and identifier fixes below all still stand on the surviving
> surface. See `defects-and-non-functional.md` §B4.

## Review summary

Today's screen is a 6,000px single column headlined by its own state, narrated by
architecture essays, and operated through developer controls: a typed "Expected case
version" number, four checkboxes for two facts, three competing red buttons, a raw hex
receipt token, PascalCase evidence keys, and an accept surface buried mid-page below a
section that duplicates the form above it. The redesign renames it **Received item
review** (reached from Inbox rows) and restructures it as: a header that states source,
received time, and state; a left column of extracted/suggested fields to confirm; a
sticky right action rail (Create case / Save corrections / Block / Re-evaluate); and
evidence and assets collapsed below. Per the premise correction above, the screen's
subject is a `Needs sorting` or `Blocked` item, not a case awaiting acceptance.

## Container restructure (operator decision, 2026-08-04)

This screen is one record, so it takes the shape every record screen now takes
(`../../ui-standards-and-review.md` §4 rule 13): **one container** holding a header band, an
action bar, and the tabs **Details · Files and images · How this was read**.

The page-12 case detail is the reference implementation. What that changes here, over and
above the numbered changes below:

- The two-column content + sticky action-rail layout is retired. **Create case**, **Block**,
  **Link to a case** and **More** become the action bar; the readiness checklist ("Principal
  identified / Case type chosen") becomes the right-hand end of that bar, so an operator sees
  what is outstanding without leaving the field they are filling.
- **Create case** opens a dialog carrying principal, case type, reason and the readiness
  summary, rather than expanding a panel inside a rail. **Save corrections** stays with the
  Details form it submits — a form submit is not a lifecycle action and does not belong in the
  bar.
- The two `<details>` disclosures become tabs, so the default view is the work, not two
  collapsed headings under it.
- The details form runs in two columns; the whole of it now fits without scrolling.

Nothing in the change list is withdrawn — the copy, vocabulary, state and evidence decisions all
stand; they are re-housed.

## Changes

1. **Rename and re-anchor.** Page `<title>` and eyebrow "[banned] review" → page name
   **"Received item review"**; nav highlights **Inbox**. Header action "Receive another"
   → **"Back to Inbox"** (that is where it goes).
2. **Header states facts, not essays.** h1 becomes **"Received item"** with a meta line —
   source name, channel, received time ("sample-instruction.pdf · Uploaded ·
   04 Aug 2026 16:30") — and one state chip: **Needs sorting** (amber), **Blocked** (red),
   **Vehicle images** (neutral), **Case 26001** (green, linked). The decision-reason lede is
   deleted; where the reason matters (Needs sorting) it renders as one sentence under the
   chip. There is no "Ready to review" chip — `Review` is a Case stage, and the state it
   would have named is being removed (see the premise correction above).
3. **Case state is visible.** Where the receipt produced or was linked to a case, the header
   chip is green "Case 26001" (linked), the action rail collapses to **"Open case 26001"**,
   and all editing surfaces render read-only. (Today the chip still says the draft label
   with no case indication.)
4. **Raw identifiers removed.** The "Source receipt" hex row in Result is deleted. The
   header meta carries everything an operator needs; no hex, no version integers.
5. **Two-column working layout.**
   - **Left — Details to confirm:** one field list (Principal, Claimant name, Claim
     number, Vehicle registration, Make, Model, Mileage, Accident circumstances, Date of
     incident, Instruction date, Inspection address, Inspection date). Each field shows
     the suggested/extracted value as the editable input value, with a muted one-line
     source hint ("From page 1 of the instruction") where a suggestion exists and a
     **Missing** amber tag where empty. This single list replaces the correction form,
     the "Missing fields" panel, the "Typed review draft" duplicate, and the
     "Suggested fields" card grid — the same data painted once instead of three times.
   - **Right — sticky action rail:** requirements checklist + actions (change 6).
6. **Action rail.**
   - **Create case** (red primary) sits at the top of a sticky rail with a compact
     requirements checklist above it (Principal identified · Case type chosen). This is the
     sorting resolution for an ambiguous item — the operator supplies the identity the route
     could not establish (`INT-26`, manual creation through the same business rules) — not a
     gate every intake passes. The button is enabled only when requirements are met; unmet
     items are listed, not narrated. It expands to: Reason, Principal, Case type, and the
     Audit sub-fields when relevant. No evidence-completeness checkboxes (change 7).
   - **Save corrections** (secondary) — replaces "Record corrected draft"; reason field
     inside the disclosure.
   - **Block** (secondary) — replaces "Block [banned]"; the reason field appears on
     disclosure, not permanently expanded.
   - **Re-evaluate** (text link under "More") — replaces "Re-evaluate with current
     policy"; reason on disclosure. Rare action, minimal weight.
7. **The evidence-completeness checkboxes are deleted, not consolidated.** Incomplete
   ordinary detail is not a bar to allocation: the new case enters `Not ready` and its detail
   is chased there (`requirements.md:251`). Genuine fail-closed conditions — limits, principal
   identity, standalone Audit evidence — refuse with a reason via **Block**, never via an
   unticked box. (The superseded proposal here was to merge each "complete" + "I have
   confirmed" pair into one checkbox; that kept a gate that should not exist.)
8. **Case linking is one button.** The typed "Case identifier" + typed "Expected case
   version" + "Enter case edit mode" flow → a single **"Link to a case"** action: pick
   the case (search or candidate list), one click, and the claim happens automatically
   server-side against the current version. Conflict is a designed *result*
   ("This case was updated by someone else — try again."), never a precondition typed by
   hand. "Reverse current association" → **"Unlink from this case"** with reason.
9. **Address panel becomes one sentence.** "Missing or conflicting physical-address
   evidence remains unresolved. Pegasus will not infer an address from a spreadsheet,
   geocoder or model." → **"No inspection address was found. Enter or confirm one."**
   next to the Inspection address field in the left column (with Accept-suggestion /
   corrected-value controls where a suggestion exists). The separate full-width panel
   disappears.
10. **Narration deleted.**
    - "Every correction, block and re-evaluation is versioned and retained in permanent
      history." — removed (history is implicit and visible in case history later).
    - "Each attachment, inline image and discrete embedded image remains a separate
      review occurrence. Matching hashes are grouped, not removed." — removed; duplicate
      copies simply show a "Duplicate of X" tag.
    - "Each retained image was read automatically. A suggestion never registers, links
      or identifies anything on its own; …" — removed entirely; reading outcomes render
      as plain rows ("No readable registration · 04 Aug 2026 16:40").
11. **Vehicle-images state.** "Register Image [banned]" → **"Register vehicle images"**:
    registration input (prefilled when read), reason, one primary button, and a single
    consequence line: **"Registering keeps these images filed under the registration
    until a case claims them."** The raw lowercase disposition ("pending") and engine
    version strings go; outcome rows use the existing human outcome labels only.
12. **Evidence and assets collapse below.** Two collapsible sections under the working
    area: **"Documents and images (3)"** (source download, attachments, duplicate tags,
    sizes in MB one decimal) and **"How this was read (5)"** (decision evidence with a
    key label map: "EmailBody" → "E-mail body", "PdfContent" → "Document text",
    "SystemDefault" → "Default applied", "Sender" → "Sender"). Collapsed by default;
    counts visible.
13. **Duplicate banner.** "This source receipt was already processed. The existing
      [banned] record is shown." → **"Already received — this is the existing record."**
14. **Timestamps and sizes.** All times "04 Aug 2026 16:30" local; all sizes MB one
    decimal; nothing in bytes; no engine/version tokens in operator copy.

## Dependencies (backend needs, plan only)

- **Automatic edit claim** for case linking: a Web handler that resolves the case's
  current version server-side, claims, links, and releases in one post — retrying once
  on version conflict before surfacing the designed conflict message. Core operations
  exist; the composition and retry policy are new (application layer, no policy change).
- **Create-case requirements evaluation** exposed to the view (which requirements are met)
  so the rail checklist and button enablement are real, not client-side guesses. The
  handler's fail-closed validation is unchanged and remains authoritative.
- **Removal of the acceptance gate is the governing prerequisite** for this whole page
  (`defects-and-non-functional.md` §B4). The four evidence-completeness booleans on the
  current accept command retire with it; an earlier version of this list proposed mapping
  them onto one checkbox each, which preserved a gate that should not exist.
- Label maps: decision-evidence keys, reading-outcome dispositions, missing-field names.
- Case-state rendering needs the case reference in the receipt view model (already present
  as `AcceptedCaseId`/reference lookup — the field survives the gate's removal; it is how a
  receipt points at the case its processing created).
- Inbox rows must link here (`page-2`/Inbox plan); route stays, old links redirect.

## Open questions

1. Should **Block** require a distinct follow-up destination (who un-blocks, from
   where?) — the Blocked list lives in Inbox filters, but the recovery path needs an
   owner before copy is final.
2. Is **Re-evaluate** an operator action at all, or administrator-only tooling that
   should leave this screen?
3. When suggestions conflict (two candidate registrations), does the left column show
   both as choices inline, or does the field open a small chooser? Mockups assume inline
   choice chips; confirm with the operator.
4. Does "Save corrections" need its reason once per save, or should the reason be
   optional when only prefilled suggestions were confirmed unchanged?
5. Sticky-rail behaviour below 1280px (rail drops under the fields?) — desktop-first per
   the standards doc, but the fallback needs a decision.

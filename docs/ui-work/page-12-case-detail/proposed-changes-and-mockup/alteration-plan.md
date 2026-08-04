# Page 12 — Case detail — alteration plan

## Review summary

The case workspace is a five-screen vertical stack: a width-wasting identity list, lease
narration in four variants, an 18-row provenance table of em-dashes, ten stacked lifecycle
forms with fifteen-plus visible Reason fields, a prefilled internal key
(`case-completeness-projection`) in an editable input, a typed SHA-256 field, raw enums and
event codes, byte counts, and self-negating empty states. The good bones — evidence always
readable, one versioned change at a time, immutable history, custody table — survive; the
presentation is rebuilt as **one case container: header, action bar, tabs**.

## Shape of the screen (operator decisions, 2026-08-04)

The case is a *record being worked*, not a page being read. It gets an application shape:

```
Case container ─┬─ header strip      reference · stage · identity · Edit
                ├─ action bar        every valid action, left; Export, right
                └─ tabs              Overview · Evidence · History
                    └─ Evidence      sub-tabs: Files · Images · E-mails
```

Five decisions drive the rebuild, all operator-stated:

1. **Actions are a bar at the top**, not a panel in the body. The operator arrives to *do*
   something; the actions are the first thing under the reference, permanently in reach
   (sticky), and never scrolled to.
2. **The case is inside one container.** The whole record sits in a single bordered shell with
   its own header band, so the screen reads as *this case* rather than as loose cards drifting
   on the page background.
3. **Compact.** This is a screen in an application, not a landing page. Density targets are in
   `../../ui-standards-and-review.md` §4 rule 13 — the rule is written there because it applies
   to every page, and this page is the reference implementation of it.
4. **Tabs, not stacked sections.** Overview, Evidence and History are one-at-a-time panels, not
   four containers piled up. This settles the plan's former "tabs vs sections" open question in
   favour of tabs.
5. **Documents becomes Evidence**, with Files / Images / E-mails sub-tabs — the three kinds of
   material a case actually carries, which the current build scatters across a documents table,
   a separate "Image in·takes" section, and nothing at all for e-mail.

## Changes

1. **Case container.** One shell (hairline border, own header band) holds header, action bar and
   tabs. Page chrome outside it: breadcrumb only.
2. **Header strip**: eyebrow + "Case QDOS26001" + lede → reference, **stage chip** (label map:
   Not ready / Review / Report preparation / Held / Post-report complete / Provider cancelled /
   Collision Engineers rejected — never enum names), then identity inline (principal ·
   registration · claimant), then **one Edit toggle** at the right. The lede is deleted.
3. **Action bar** (new; replaces change 5 of the previous plan): ten stacked forms → a single
   horizontal bar directly under the header, showing **only the actions valid for the current
   stage**, ordered progress-first, with rare actions (Reopen, Archive, Create linked
   replacement) behind **More ▾**. Each action opens a **dialog** carrying exactly its own
   fields: reason, and — where policy requires — the completeness confirmation shown **once** as
   a summary of what is recorded rather than four re-editable checkboxes. The bar is sticky
   under the header when the panel scrolls. The former "Actions" tab does not exist.
4. **Export lives in the action bar, right-aligned and visually distinct** (bordered,
   separated from the stage actions by a rule). It is **disabled outside the Review stage**,
   with the condition stated on the control ("Available in Review") rather than the button
   disappearing — see the rule-9 clarification in `../../ui-standards-and-review.md` §4. In
   Review it opens the export dialog; if rows are ticked in Evidence, the dialog opens with that
   selection, otherwise with every confirmed-custody item. The stage gate must be enforced in
   Core, not only greyed in the UI (see Dependencies).
5. **Edit-mode copy**: the four lease narrations → three designed states with no mechanics:
   read-only (default, no message at all — the Edit button is the state); editing (button becomes
   "Finish editing", an **Editing** chip joins the header, quiet microcopy "Changes save one at a
   time"); contended ("Sample Colleague is editing this case", Edit disabled). Recovery
   ("protected browser state must be recovered") happens silently on click.
6. **Tabs**: Overview · Evidence (n) · History (n), one panel visible at a time, counts on the
   tab. Every partial's copy is rewritten in business language as it moves.
7. **Overview**: the 13-row vertical identity list → a **three-column fact grid** (Case ·
   Instruction · Assignment) that fits above the fold; the 18-row provenance table → a
   **typed-data list showing only populated rows by default** ("Show all 18 fields" expands),
   with suggestions inline per field: "Suggested 27 Feb 2025 — **Accept**", replacing the
   Fact/Suggestion/Confirmed three-column dump. Policy keys, reader versions and source hashes
   leave the markup.
8. **Provenance is an icon, not a sentence.** Where a value came from stops being prose in a
   third column and becomes a small icon at the end of the row, whose tooltip is **one word**
   (operator decision 2026-08-04). The vocabulary — this is the whole set, and it is a label map
   like every other:

   | Icon | Tooltip | Persisted source (`CaseDataSourceKind`) |
   |---|---|---|
   | person | **Staff** | `StaffCorrection` |
   | document | **Extracted** | `IntakeEvidence`, document reader |
   | spark | **AI** | `IntakeEvidence`, AI reader |
   | envelope | **E-mail** | `MailRoute` |
   | magnifier | **Lookup** | `VehicleLookup` |
   | building | **Principal** | `ProviderSetting` |
   | bolt | **Automatic** | `CaseAcceptance` |

   The icon carries an accessible name equal to its tooltip word, the tooltip appears on hover
   **and on keyboard focus**, and no row depends on the icon alone: a value is still a value
   without it, and an unconfirmed suggestion still says "Suggested". Same icon set on Evidence
   rows, so "where did this file come from" reads identically to "where did this date come
   from". Nothing longer than the one word is shown anywhere — the old "— from the instruction
   PDF" trailing clause goes, and so does the `PolicyKey`/`PolicyVersion` pair that the current
   page prints beside it.
9. **Evidence — Files**: Role/enums/hash/bytes columns → **File · Type (human label) · Custody
   (chip) · Size (MB, one decimal) · Added · provenance icon**; hash column dropped; per-row
   actions behind a row menu (Download / Remove… / Mark third-party vehicle…). Selection
   checkboxes feed Export.
10. **Evidence — Images**: the "Image in·takes" section (banned term) moves here as **Images**,
    with the image reference, registration, custody chip and provenance icon, chips matching
    page 9. The "reasonedly" sentence is deleted; reversal is a row action.
11. **Evidence — E-mails**: new — the received and sent messages linked to this case (sender,
    subject, direction, received time, link to the message in Inbox). Today the case↔message
    links exist but the case surfaces none of them (see Dependencies).
12. **Internal keys out of forms**: the "Evidence reference" input prefilled with
    `case-completeness-projection` → removed from the UI; the handler supplies it. The
    "Artifact SHA-256" typed input → replaced by a report-file picker that computes the digest
    on upload; approval shows "Report approved 4 Aug 2026 by alex" only.
13. **History**: `case_created` event codes and "0 → 1" version arrows → **labelled events**
    ("Case created", "Sent to Review") with actor name, time and reason; automation actor keeps
    its chip. Version integers dropped.
14. **Empty states**: every self-negating string → plain business language: "No files yet." /
    "No vehicle images yet." / "No e-mail is linked to this case."; panels for capabilities not
    composed in the deployment are **absent**, not explained (rule 9 — the EVA panel disappears
    where EVA is not composed; blocking reasons become a single "Not ready to send: 3 items
    outstanding" disclosure with human wording).
15. **Language fixes**: "reasonedly" removed; "Archive terminal case" → "Archive" under More
    (kept per current policy, noting operationally cases are never archived); "Retain document" →
    "Add file"; snake_case origin values mapped ("Manual upload").
16. **One-time secret**: inline panel → modal with a Copy button, shown at creation only.

## What the container earns

The old page put ~7,900px of stacked panels in front of one case. The rebuilt shape shows
identity, stage, every available action, Export, and the whole Overview **without scrolling**;
Evidence and History are one click, not one scroll. That is the point of the change, not the
styling.

## Dependencies

- Stage/event/enum **label maps** for case stage, case type, file type, custody, history events,
  origin channel — the single biggest prerequisite; shared with the Cases list.
- Dialog component (accessible modal) — new shared UI primitive; also serves pages 10/11.
- Valid-actions-for-stage helper in the page model (Core already enforces; the UI must ask
  before rendering) so the action bar can filter.
- **A Core stage gate on export.** `IExportCaseDocuments` has no stage condition today
  (`Pages/Cases/Documents/Export.cshtml.cs:59`); a disabled button is presentation, not policy.
  The Review-only rule must be a Core precondition with the UI reflecting it, or the rule is not
  real.
- **A case→messages query for the E-mails sub-tab.** The links are persisted
  (`CaseIntakeLinks`, written by `EfCaseAcceptanceStore.cs:276-288`) but nothing reads them back
  case-first; this is a new Core query, not a projection of something the page already has.
- **A provenance label map for `CaseDataSourceKind`** (`src/Pegasus.Core/Cases/CaseDataContracts.cs:14`).
  Six of the seven words map one-to-one onto persisted values. **"AI" does not**: an AI-read value
  and a plain document-read value are both `IntakeEvidence` today. The distinction has to come from
  the reader identity already carried on `CaseDataSource.Label`/`PolicyKey` (line 30-35) — no schema
  change, but the mapping must be written down explicitly, not inferred per screen.
- File-picker digest computation for report approval (client hash or server-side on upload) —
  engineering decision required.
- Engineer and staff GUIDs → display names (Admin lookup exists).
- MB formatting helper (rule 5) shared app-wide.
- The nav/IA rework (Cases active section) shared with all pages.

## Open questions

- ~~Tabs vs sections~~ — settled 2026-08-04: **tabs**. Consequence to accept: Ctrl-F no longer
  finds text in a hidden tab. Mitigation if it bites: the browser find falls back to the tab
  counts, and Evidence/History are one click away.
- Should "Archive" be offered at all while operational practice never archives? Proposal: keep,
  behind More.
- Completeness confirmation: is one summary line with an "Amend…" link acceptable to policy, or
  must the four assertions be re-affirmed per action? Core policy question to settle before the
  dialogs are final.
- Where EVA **is** composed, does the send action belong in the action bar or in Evidence?
  Proposed: the action bar, "Send to Engineer".
- Provenance words: is **AI** worth separating from **Extracted** at all, or does the operator only
  care that a machine produced it rather than a person? Separating them is free in the UI but only
  meaningful if the reader identity is reliably recorded (see Dependencies).
- Export's Review-only rule: does a **closed** case (Post-report complete) also permit export —
  the report has been issued, and a later request to re-send the bundle is plausible? The plan
  currently disables it outside Review exactly as stated; confirm the closed-case case.

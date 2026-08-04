# Page 12 — Case detail — alteration plan

## Review summary

The case workspace is a five-screen vertical stack: a width-wasting identity list, lease
narration in four variants, an 18-row provenance table of em-dashes, ten stacked lifecycle
forms with fifteen-plus visible Reason fields, a prefilled internal key
(`case-completeness-projection`) in an editable input, a typed SHA-256 field, raw enums and
event codes, byte counts, and self-negating empty states. The good bones — evidence always
readable, one versioned change at a time, immutable history, custody table — survive; the
presentation is rebuilt around a header, sections, and a single Actions panel with per-action
dialogs.

## Changes

1. **Header**: eyebrow + "Case QDOS26001" + lede → one case header: reference, **stage chip**
   (label map: Not ready / Review / Report preparation / Held / Post-report complete / Provider
   cancelled / Collision Engineers rejected — never enum names), principal, registration,
   claimant, and **one Edit toggle**. The lede is deleted.
2. **Edit-mode copy**: the four lease narrations → three designed states with no mechanics:
   read-only (default, no message at all — the Edit button is the state); editing (button
   becomes "Editing — finish" with a quiet "changes save one at a time" microcopy);
   contended ("Sample Colleague is editing this case" with the Edit button disabled).
   Recovery ("protected browser state must be recovered") happens silently on click.
3. **Body structure**: one endless column → **sectioned body with in-page navigation**
   (sticky section tabs): **Overview · Actions · Documents · History**. Every partial's copy is
   rewritten in business language as it moves.
4. **Overview**: the 13-row vertical identity list → a compact **two-column fact grid**; the
   18-row provenance table → a **typed-data list showing only populated rows by default**
   ("Show all 18 fields" expands), with suggestions inline per field: "Suggested: 27 Feb 2025 —
   from the instruction PDF — **Accept**", replacing the Fact/Suggestion/Confirmed
   three-column dump. Policy keys, reader versions, and source hashes leave the markup.
5. **Actions**: ten stacked forms → **one grouped Actions panel** listing each action as a
   single labelled button, grouped (Progress / Hold / Engineer / Closure), showing **only the
   actions valid for the current state**; each opens a **dialog** carrying exactly its own
   fields: reason, and — where policy requires — the completeness confirmation shown **once**
   as a summary of the recorded checkboxes rather than four re-editable ones.
6. **Internal keys out of forms**: the "Evidence reference" input prefilled with
   `case-completeness-projection` → removed from the UI; the handler supplies it. The
   "Artifact SHA-256" typed input → replaced by a report-file picker that computes the digest
   on upload; approval shows "Report approved 4 Aug 2026 by alex" only.
7. **Documents**: Role/enums/hash/bytes columns → **File · Type (human label) · Custody (chip)
   · Size (MB, one decimal) · Added**; hash column dropped; per-row actions behind a row menu
   (Download / Remove… / Mark third-party vehicle…). Export keeps checkbox selection with one
   toolbar button.
8. **History**: `case_created` event codes and "0 → 1" version arrows → **labelled events**
   ("Case created", "Sent to Review") with actor name, time, reason; automation actor keeps its
   chip. Version integers dropped.
9. **Empty states**: every self-negating string → plain business language: "No file requests
   yet." / "No upload requests yet." / "No vehicle lookup has been run."; panels for
   capabilities not composed in the deployment are **absent**, not explained (rule 9 — the EVA
   panel disappears where EVA is not composed; blocking reasons become a single "Not ready to
   send: 3 items outstanding" disclosure with human wording).
10. **Language fixes**: "reasonedly" removed; "Archive terminal case" → "Archive" under
    Closure (kept per current policy, noting operationally cases are never archived);
    "Retain document" → "Add document"; snake_case origin values mapped ("Manual upload").
11. **One-time secret**: inline panel → modal with a Copy button, shown at creation only.
12. **Image material section**: heading "Image in·takes" (banned term) → **"Vehicle images"**,
    rows matching page 9's chips.

## Dependencies

- Stage/event/enum **label maps** for case state, case type, document role, custody, history
  events, origin channel — the single biggest prerequisite; shared with the Cases list.
- Dialog component (accessible modal) — new shared UI primitive; also serves pages 10/11.
- Valid-actions-for-state helper in the page model (Core already enforces; the UI must ask
  before rendering) so the Actions panel can filter.
- File-picker digest computation for report approval (client hash or server-side on upload) —
  engineering decision required.
- Engineer and staff GUIDs → display names (Admin lookup exists).
- MB formatting helper (rule 5) shared app-wide.
- The nav/IA rework (Cases active section) shared with all pages.

## Open questions

- Tabs vs sections: the plan proposes sticky in-page section navigation (evidence stays
  find-on-page searchable); true tabs would hide Documents/History from Ctrl-F. Operator
  preference needed.
- Should "Archive" be offered at all while operational practice never archives? Proposal:
  keep, Closure group, behind "More".
- Completeness confirmation: is one summary line with an "Amend…" link acceptable to policy,
  or must the four assertions be re-affirmed per action? Core policy question to settle before
  the dialogs are final.
- Where EVA **is** composed, does the send action belong in Actions/Progress or in Documents?
  Proposed: Actions, "Send to Engineer".

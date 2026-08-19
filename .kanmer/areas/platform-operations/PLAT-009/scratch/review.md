# Independent review — PLAT-009 / PR #430

Reviewed by claude-code (release owner; did not implement this change).

**Plan vs ticket:** covered. The ticket asked for the form out of the table cell, the operator's copy direction applied to this page, and unchanged semantics — the diff delivers exactly that surface: 4 files, +94/−85, `Mailboxes.cshtml(.cs)`, one shared-label addition in `OperatorLabels`, one test rewritten.

**Implementation vs plan:** the compact 5-column table + one edit panel per mailbox (h3 = address, `aria-labelledby`) matches the pattern the ticket named — the page's own "Add an approved address" panel. Field names, handler, antiforgery, version/operation-key semantics byte-identical, which is what keeps this a layout fix. Copy: the banner and multi-sentence paragraphs are gone from the added markup (checked by grep over the `+` lines — no receipt/intake/custody/tenant narration), and the surviving route-scope labels come from `OperatorLabels`, not inline strings.

**A judgement call I endorse:** the reuse lens suggested matching the in-cell-form convention still used by `Roles/Index` and `Access/Index`; the lane refused, because that convention *is* the defect on this page. I checked the two cited pages myself: their in-cell forms are 3–4 compact controls (11–21 lines) — the acceptable inline-action scale — so no follow-up restructure ticket is warranted for them; PLAT-010 sweeps their copy. Recorded here so the "existing convention wins" rule is answered with a reason, not a preference.

**Evidence:** build 0/0; Mailbox/Administration filter 56/56; Browser 37/37 with 0 axe violations; Architecture 97/97 (the `OperatorLabels` move stays in layer). Screenshots at 1920 and 1366 via the same Playwright harness the Browser suite uses, against a two-mailbox estate — normal row height, one panel per mailbox, no banner. The `Invoke-LocalDevelopment` failure was diagnosed as a pre-existing LocalDB-detection bug on this workstation, not papered over, and the temporary capture test was deleted before commit.

**Simplification pass:** four lenses, one applied (shared label), one skipped with the right reason. Honest.

**Verdict: PASS.** Merge on green CI.

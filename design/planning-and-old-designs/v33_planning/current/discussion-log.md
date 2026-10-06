# v33 discussion log

Chronological. The operator's words are quoted; the rest records what was explored, built and raised. This log is not design authority.

## Round 1 — 6 October 2026

**The brief** (operator, with two screenshots of the section while editing): "Examine the valuation section on the case page. The calculation section is very seperated from the engineers value box. Additionally, there is a seperate box showing "guide retail" and "proposed engineers value", and an "applied engineers value" that appears always empty. Check where and how these are connected. Additionally create 5 seperate proposals for a redesign/improvements on this section for my perusal. It must account for the above issues."

**The workflow the operator set.** Mockup proposals first, presented for approval; the operator selects one or proposes alterations; the work is done in a worktree and ends in a pull request once a design is approved.

**What was explored.** The section at `origin/dev` f5bc6e5f1, traced from the partial to the store ([how it works](../pages/case-record/valuation/how-it-works.md)).

- The Engineer's Value box is a Case field. The report, the salvage matrix, repair spec scaling and Send to AI read it.
- "Proposed Engineer's Value" is a preview of Core's arithmetic. Script copies it into the box, so the two are the same number twice.
- "Applied Engineer's Value" is a separate record of a calculation recorded against a source. A save writes it only when Use this value was pressed or the calculator changed, and only when the box holds the calculated figure.
- A save does not redraw the Valuation section, so a newly written record and the section head figure stay as they were until the page is reloaded. Saves are made in place since 29 September, which is why the block is rarely seen filled.
- The calculation is the right-hand column of a two-column grid placed after the five source cards.

**Beyond the brief.** While editing, the first source with a retail is drawn as chosen before anyone chose it. Four of the five source cards are a notice and empty boxes and take most of the height. "Get valuation" stands as a loose label beside AI market research. On the report sits between the sources and the calculation.

**What was built.** Five designs (A Calculation under the boxes, B Worksheet, C Three columns, D Chosen source opens, E Side by side) and today's section, all inside the Case page as the application renders it; the comparison page; the self-check and screenshot script; this folder. Design E began as three panes across and was redrawn as two after its first render: at the section's real width (1003px beside the aside) the source boxes could not hold a five-figure value.

**No question was put to the operator before building.** The brief named the problems and the count; the choices it leaves open are the lettered items.

**Items raised.** A to O in [v33-notes.md](v33-notes.md).

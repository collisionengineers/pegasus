# Review record — DELIV-041 (PR #647)

Reviewed 2026-09-02. Independent read by gpt-5.6-sol (xhigh) over the PR head
`632ec0c436e301023f3aa6a5e1f4e0e149a192b5` in `.worktrees/deliv-041`
(prompt and output under the controller's scratch `review/deliv-041`);
dispositions by the controller (Claude Fable, not the implementer).

Verdict of the independent read: REQUEST CHANGES (3 blockers, 5 should-fix,
1 nit). CI at that head: documentation, changes, local-development-scripts,
reference-data green; code lanes skipped (docs-only).

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | D43 contradicted: design README still says prototype fixture data is never copied; engineering.md adds a sign-off gate D43 does not carry | Fix — scope the README sentence; engineering.md states the permission, the PII fact and the supersession plainly |
| 2 | D36 contradicts FRD-07 (export only in Review; no API re-submission); FRD-07 outside the ticket's refs | Fix as a reviewed scope amendment — frd-07 added to refs; FRD-07 records the With Engineer re-send |
| 3 | Duplicate design-contract summaries retain Download EVA package, Operations Service health, Triage-only Pre-case, Assessment-only-from-With-Engineer, 7-item ribbon | Fix |
| 4 | D31 signer roster (Andy, Neil, Ed; Andy default; Neil's qualifications later) not stated | Fix — FRD-04 and FRD-11; tuple complete with name and signature, qualification line optional |
| 5 | D35 omits polling the ledger through the Automation Actor; adds "Case reference only" | Fix |
| 6 | Guide month introduced and Date/Time removed without a decision | Fix — keep Date, Time, Mileage; Guide month attributed to CASE-029 (EPIC-012 context), capabilities made consistent |
| 7 | D41 permission turned into a required Core ratio calculation with fixed operands | Fix — permission only |
| 8 | D39 "clickable" diagram called unauthorised | Reject — the operator asked for a clickable vehicle SVG on 2026-09-02 and ENG-036's title records it |
| 9 | Prose lines over 78 columns; `|---|` delimiters in open-decisions | Fix |

Implementer's own flags: FRD-07 conflict (→ finding 2); FRD-11 fail-closed
tuple vs. Neil's missing qualification (→ finding 4); ten new capability ids
(accepted); route keys are the kebab-case of the decision names, owned by the
implementing tickets (accepted).

Merge condition: fixes pushed, documentation CI green, controller re-reads
the diff for findings 1–7 and 9.

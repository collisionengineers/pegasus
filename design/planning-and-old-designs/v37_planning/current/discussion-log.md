# v37 discussion log

Chronological. The operator's words are quoted; the rest records what was explored, built and raised. This log is not design authority.

## Round 1: 9 October 2026

**The brief** (operator, through `/razor-html-mockup-creation`): "Management Reports Page. Additionally consider the features and functionality behind this, your mockup can include proposals for this too. Generate 3 alternate designs with a widget control"

**Two questions answered by the operator before building.**

1. What "a widget control" means: a configurable widget board, a design switcher in the mockup, or both. Answer: "Design switcher in mockup".
2. Who uses the page: Administrators only as today, all staff with figures by role, or left to the proposal. Answer: "Administrators only (as today)".

**What was explored.**

- The live page at `origin/dev` 970ef9f10: four sections (MI01 Engineer activity, MI02 Reports by Principal with By month, MI03 Turnaround, MI04 Case list), their Core reads (`EngineerActivityReport`, `PrincipalReportActivity`, `MonthlyReportActivity`, `CaseListReport`), downloads and tests. Thirteen usability findings are in the [page's how-it-works](../pages/administration-reports/how-it-works.md).
- Provenance. MI-01 to MI-04 trace to operator decisions recorded in FRD-17, FRD-15 and the commits, not to `reference/`. The `reference/` greps for management information, MI, KPI, turnaround, monthly, per engineer, invoice and statistics found nothing relevant. Binary workbooks there are raw case logs, not definitions.
- What else Pegasus already records that a report could read without new recording: Triage and Unidentified queues, Cases by stage, outcomes and Audit agreement, previous periods. Also what it does not record: invoices and payment, actual inspection date, region, Principal groups, costs, structured reasons ([notes](v37-notes.md), section 9).
- The running page, captured from the local visual host. Its structure matches the mockup's Today file. It confirmed the doubled sort arrow.

**What was built.** Three designs (A Tidied sections, B Overview first, C Month ledger), Today from the same fixtures, the comparison page, the self-check and 50 screenshots. Ten strip switches cover the open choices and proposals.

**Items raised.** A to S in [v37-notes.md](v37-notes.md), section 6. Five Stage 2 defect fixes are listed there unlettered.

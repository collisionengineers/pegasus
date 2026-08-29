## 2026-08-29 — proof written; Done withheld

`proof` is written against merged `dev` `b92cb9a7` (PR #591, merge
`33b99547`). `get_doc_gates` now reports `enter-done` passable.

**Not moved to Done.** The Service health half is wired and rendered
(`Pages/Operations/Index.cshtml.cs:76`, `Index.cshtml:46`–`:95`), but the
Engineer activity report half has no production caller anywhere on `dev`:
`GetEngineerActivityReport` appears only at its declaration and
`Infrastructure/DependencyInjection.cs:259`; `EngineerActivityReportCsv` and
`StaffAccessRight.ViewOperationalReports` are reachable from nothing outside
tests; there is no `Pages/Administration/Reports*`. AGENTS.md rule 14 —
"registered-but-unreachable or test-only code is not done" — therefore bites.

Unblocks when **PLAT-051** ships the Administration → Reports page. At that
point the move is a one-liner; the proof already carries the evidence.

Also note: the Service health composition rides the `Features:AutomationMcp`
gate (`Program.cs:682`), so there is no tier-3 evidence for it either — `main`
is not promoted until wave 5.

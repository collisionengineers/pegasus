2026-09-02 kanmer-research (Claude wrapper over gpt-5.6-terra high): Codex launched read-only in `.worktrees/research` (origin/dev cad00be9); prompt/output in session scratchpad `prep/CASE-040/`. MCP tool replies in this session return only the project stub; write landing verified on disk.

2026-09-02 kanmer-research result: research, files and open-questions written (gpt-5.6-terra high, wrapper spot-checked ~10 VERIFIED claims in the main checkout at cad00be9; two wrapper corrections: labels home is the existing `OperatorLabels.CaseWorkspace`/`EvaHandoffs`, and the EVA dialog markup + "Download EVA package" label sit in CASE-038-owned `Details.cshtml` lines 561-620/251). Research checkout `.worktrees/research` clean after Codex. Ticket moved backlog → preparing (one move). Leaving preparing still needs plan and checklist, and the open question on how the A Patterson default account is identified must be ticked or parked. Plan must also settle: who hosts the Sign-off select in the script dialog (CASE-038 vs a handoff of the dialog block), API re-send semantics against the one-delivery-per-case rule, and the FRD-07 reconciliation follow-up (outside DELIV-041's file list).

2026-09-02 kanmer-plan (re-entry check): plan/plan.md (25.5 KB, gpt-5.6-terra xhigh) and checklist/checklist.md already exist and post-date research; byte-identical to the session scratchpad finals, so Codex was not re-run. Leave-preparing gate (research, files, plan, checklist) is satisfied on disk; the ticket stays in Preparing until the operator question on the fallback A Patterson account identity in open-questions is ticked or parked. Research checkout `.worktrees/research` clean at 897db953.

## Interim state you must close (recorded 2026-09-03 by the controller)

[[DOCS-017]] merged to `dev` at `86ce276d`. It replaced the fixed D18
signatory tuple with a supplied `ReportSignatory`, and — deliberately, and
declared as an accepted risk in its post-implementation report — left the one
production input source, `EfAssessmentReportProjectionSource.cs`, passing
`Signatory: null`. `AssessmentReportProjection.Prepare` requires
`signatory?.IsComplete == true`, so **from that merge until CASE-040 ships,
no report draft can be generated on `dev`**: every attempt returns the
"Sign-off Engineer" readiness item instead of a PDF. Only tests supply a
signatory today.

CASE-040 already names `EfAssessmentReportProjectionSource.cs` in its
`files/files.md`, so it owns closing this. Two things follow:

1. Wiring the case's sign-off Engineer through that source into both
   readiness and the production projection is not optional polish — it is
   what makes DOCS-017 reachable. A registered-but-uncalled signatory is
   test-only code ("done means wired").
2. CASE-040's proof must show a report draft generating end to end again from
   the production path, not only that the tuple renders when one is handed in.
   That is the evidence that the interim state is closed before any release.

No release may go to `main` while this is open; EPIC-012 ships one production
release after all its PRs, which is what keeps the interim state off any
deployed environment.

# Plan — AUTO-009

Docs-only. Estimated diff: ~+190 lines across four Markdown files (one new ADR
~90 lines, FRD-11 +~60, FRD-10 +~35, ADR index +1 row).

## Steps

1. `docs/adr/0035-ai-job-ledger.md` — new. Frontmatter per AGENTS.md; one
   decision: a durable pull-based `AiJobs` ledger claimed through the
   Automation Actor under `automation.jobs`; distinct from the
   `ExternalWorkItems` outbox (`ExternalWorkProcessing.cs` throws
   `UnknownExternalWorkKindException` for an unknown kind) and from the AI-09
   `AiWorkRequests` pointer hand-off (`AiWorkContracts.cs`: push, one request
   per case, DevelopmentOffline-only), whose operation-key / version /
   kill-switch / actor-attribution patterns it reuses. Supersedes the
   `boundaries.md` "shared AI usage ledger" exclusion (UIIMP-007 edits that
   file). Reuses ADR-0031/0026/0027 style; no behaviour.
2. `docs/adr/README.md` — one row in the accepted table.
3. FRD-11 — new "AI Job List (AI-10 catalogue)" section under Targeted
   sending: kinds (D5), states, started-by, lease expiry, staff actions on
   Operations (§1.11) and Automation & AI counts/kill switch (§1.12); and
   "Estimate VAT on the rendered report" (D9) replacing the built-in
   `AssessmentReportRendering` repairer-VAT rule for the Current estimate.
4. FRD-10 — new "Automation tool inventory additions" section: the seven
   `pegasus_ai_job_*` tools under `automation.jobs` (D6, consent line
   required), `pegasus_estimate_save`/`pegasus_estimate_list` under
   `automation.assessment` (AI drafts only, cite the job id), the missing
   `automation.mail` consent description (`Authorize.cshtml.cs` has none),
   kill switch and attribution unchanged from ADR-0031.
5. `pwsh ./scripts/Test-DocumentationLinks.ps1`; commit; PR to `dev`.

## Verified premises (read-only)

- `Authorize.cshtml.cs` `ScopeDescriptions` lists cases/intake/documents/
  assessment only — `automation.mail` has no consent description.
- `AssessmentReportRendering.cs` computes VAT as 20% of Subtotal when the
  repairer is VAT-registered, else of Parts + Paint materials.
- `docs/capabilities.md` IDs used: AI-10, MCP-06, AI-09, MCP-01, RPT-02.

## Out of scope

`boundaries.md`, `capabilities.md`, `open-decisions.md` (UIIMP-007), code
(wave 3 AI job ledger ticket), FRD-12 (UIIMP-007).

## Simplification pass — 2026-08-28

n/a — docs-only.

---
kind: review-attestation
pr: "573"
head_sha: "a7b44e327b7e7780874b9c0250c1fad5145f424c"
verdict: pass
reviewer: "claude-code-independent-reviewer (Fable 5, 2026-08-27)"
independent: true
plan_hash: "00f795083c3fb834"
ticket_updated: "2026-08-27T14:45:22.633Z"
findings:
  - id: R1
    severity: note
    summary: "Local canonical restore/build/test not run; diff touches no .NET file and the CI `changes` job skipped the .NET shards by the repo's own path filter."
    disposition: accepted-risk
    reason: "Script/docs-only diff; the mocked Architecture tests covering -WorkerOnly ran locally 14/14 and the script parses. No .NET behaviour can have changed."
  - id: R2
    severity: minor
    summary: "Plan said the liveness block goes after the Web checks; it sits before the redirect/anonymous-denial checks (after the Worker readback)."
    disposition: rejected-with-reason
    reason: "Order of independent throw-on-fail gates does not change the verdict; every gate still runs on the full path and -WorkerOnly returns before the block."
  - id: R3
    severity: note
    summary: "`dev` has no GitHub branch protection, so no check is formally required; all nine rollup checks are green or path-skipped."
    disposition: accepted-risk
    reason: "Repository convention; recorded as evidence, not claimed as a required gate."
---

# MAIL-019 review — PR #573 at a7b44e32

## Changes reviewed

- `scripts/Invoke-ProductionSmoke.ps1` (+49): after the `-WorkerOnly` early
  return, one read-only `Invoke-Sqlcmd` (SET NOCOUNT + single SELECT of
  counts/MAX/MIN/SYSDATETIMEOFFSET) against the prod database using the
  bootstrap access-token pattern; throws on unactivated Approved+inbound
  mailbox, no unexpired Active subscription, NULL poll, or poll age > 15 min
  on the database clock.
- `docs/runbook.md` (+8/-2), `.agents/skills/pegasus-release/SKILL.md`
  (+5/-2): name the gate and the `SqlServer` module / Azure CLI identity
  prerequisite.

## Acceptance checks

- SQL is read-only: SELECT only, no DML/DDL. PASS.
- Thresholds match the ticket body and EPIC-010 context (Active subscription,
  recent poll within the recovery grace, release-33 unactivated shape). PASS.
- `SqlServer` module is not a new dependency: `Invoke-AzureDatabaseBootstrap.ps1:360-363`
  uses the identical guard and token call; runbook line 64 lists the module
  in the live-work profile and line 230 pins 22.4.5.1. PASS.
- `-WorkerOnly` path and the mocked `WorkerActivationReleaseContractTests`
  untouched (block placed after the `return`). PASS.
- Markdown: no new headings, no added line over 80 columns, H1 unchanged. PASS.
- Secrets: token held in a variable, never echoed; no credentials in diff. PASS.
- Script parses (`[scriptblock]::Create`) in the worktree at a7b44e32. PASS.
- Worktree clean, one commit ahead of `dev`, matches PR head. PASS.
- Review threads: none. Comments: one Codex quota notice, no content.
- Checks: changes, documentation, local-development-scripts, reference-data,
  infrastructure green; unit/sql-integration/browser/coverage skipped by path
  filter. No red or pending check.

## Residual risk

The full smoke now requires an Azure CLI identity with read access to
`pegasus` in prod; a release terminal lacking it fails closed (throw), which
is the intended behaviour. Live run recorded in the plan passed at 14:30Z.

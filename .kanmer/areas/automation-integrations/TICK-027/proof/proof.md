# Proof — TICK-027 (MCP-06)

Type: command-log. Test-evidence PR #445 released in **release 14** (`d91fd7d7…`), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: all five assessment tools (`pegasus_assessment_get`/`update`, `pegasus_case_update_details`, `pegasus_eva_bundle_generate`, `pegasus_eva_handoff_status`) registered and per-tool scope-guarded (`automation.assessment` / `automation.cases`); writes go through the same Core `ISaveCase` path as staff with lease/version/reason and logging parity via the action auditor; the PR closed the test gap with three functional `pegasus_case_update_details` tests (scope denial, lease-guarded HTTP mutation with logging parity, missing-lease refusal without token disclosure).
- Live: `Features__AutomationMcp = true` in the production bicep — the `/mcp` surface is composed in the deployed release.
- Full transcript: DELIV-013 scratch.

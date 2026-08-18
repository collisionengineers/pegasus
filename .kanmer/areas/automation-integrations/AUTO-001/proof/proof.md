# Proof — AUTO-001 (verified on the deployed estate at merged `main` `f1e116c6`, 2026-08-18)

Activation shipped by release 9 ([[DELIV-008]]): revision `pegasus-prod-web-252ow37gij--f1e116c6eb93` renders `Features__AutomationMcp=true`, `AutomationMcp__ClientId=pegasus-automation`, `AutomationMcp__ClientSecret` ← Key Vault secret reference `automation-mcp-client-secret` (`pegasusprodkv252ow37g`, version `68ff4a6b…`), `AutomationMcp__PublicOrigin` = the app's https origin — all from `infra/modules/platform.bicep`, not from a manual edit.

## Live evidence (production `/connect/token` and `/mcp`, 11:59–12:03 UTC; secret read into process memory only)

| Check | Result | Permanent history |
| --- | --- | --- |
| Token with wrong secret | 401 `invalid_client` | SecurityEvents `automation_token_rejected` |
| Client credentials `pegasus-automation`, scope `automation.cases` | 200, Bearer, `expires_in` 600 | — |
| `POST /mcp` without token | 401, `WWW-Authenticate: Bearer resource_metadata="…"` | SecurityEvents `automation_access_denied` |
| `initialize` (protocol 2025-06-18) | 200 | — |
| `tools/list` | 15 tools: assessment_get/update, case_edit_begin/end/renew, case_get, case_search, case_update_details, document_add/download/export, eva_bundle_generate, eva_handoff_status, intake_queue_list, intake_submit | — |
| `pegasus_case_search {pageSize:1}` | success; structured result (correlationId, page, pageSize, items…, existing case QDOS26001) | ActionHistory `Succeeded`, ActorKind Automation, ActorSubjectId pegasus-automation |
| `pegasus_intake_queue_list` with cases-only token | isError "The 'automation.intake' scope is required for this tool." | SecurityEvents `automation_scope_denied` |
| `pegasus_case_get` empty id | isError "A non-empty case identifier is required." | ActionHistory `Failed` |
| Kill switch: Administration → Automation → Disable (as `claudeuiverification`) | token endpoint 400 `unauthorized_client`; in-flight token refused within 12 s: "The Automation client registration is disabled." | ActionHistory (registration change) |
| Kill switch: Enable | new token issued; `pegasus_case_search` succeeds again; registration left **enabled** | — |

Success evidence used read tools only; no write tool touched production data. Rate limit and per-area scopes are the existing implementation (unchanged). Not proved here: an external MCP client session (Claude Desktop/Code) — the operator's connector configuration is outside the repository; the endpoint, credentials location and scopes are recorded in `docs/operations.md`.

## Merged-code evidence (`f1e116c6`)

Release build 0/0; `Pegasus.ArchitectureTests` 96/96; integration `AutomationMcpIngressTests|AutomationDocumentIngressTests|AutomationAssessmentIngressTests` 15/15; `Test-AzureDeploymentPlan.ps1 -Mode Local` pass; PR #403 CI 10/10; independent review PASS after fixes (`3f836469`).

PR #403 merged 2026-08-18 (`f1e116c6`). Docs refreshed by PR #404.

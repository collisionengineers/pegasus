# Proof — TICK-026 MCP-04 (verified on merged `main` `f1e116c6`, 2026-08-18)

Replaces the earlier "Operator confirmed" stub.

Local caller-proof tier (HTTP `/mcp` against the DevelopmentOffline-gated host, LocalDB, Release, at `f1e116c6`):

```
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~AutomationMcpIngressTests|FullyQualifiedName~AutomationDocumentIngressTests|FullyQualifiedName~AutomationAssessmentIngressTests"
→ 15 passed, 0 failed (ingress + document + assessment; run together with the MAIL-21/22 filters: Passed! - Failed: 0, Passed: 19, Skipped: 2, Total: 21)
```

Per FRD-10, every document tool now has success, validation-failure, authorization-failure and action-history evidence: add (success, replay, bad role, missing lease, scope denial), download (inline, oversize notice, empty version id, scope denial, `Succeeded`/`Failed` history), export (after return-to-Review, refused when not in Review, empty selections, scope denial).

Deployed tier: the same code is live in release 9 with the Automation MCP gate enabled (see [[AUTO-001]] proof — `tools/list` on production returns the three document tools among the 15). No document write tool was exercised against production data.

PR #393 merged 2026-08-18T11:12Z (`6cf9b166`); reviewer fix `e108ec87`.

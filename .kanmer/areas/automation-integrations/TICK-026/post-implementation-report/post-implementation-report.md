# Post-implementation report — TICK-026

## Summary

MCP-04 was already implemented as `pegasus_document_add`, `pegasus_document_download`, and `pegasus_document_export` on the gated `/mcp` host. This ticket did not add a server. It added the missing FRD-10 HTTP caller evidence: success (add, replay, inline download, oversize notice, Review-only export), validation refusals, documents-scope denial, and ActionHistory attribution. Production code is unchanged.

## Changes

| File | Change | Why |
| --- | --- | --- |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Added three tests plus seed/lease helpers | FRD-10 requires an exercised `/mcp` caller for each document tool, not just tool registration and a lease-conflict add |

## Governing docs

- **FRD-10**: meets the document-action inventory (add / download / export; no delete) and the per-tool evidence bar on the existing DevelopmentOffline-gated host. Does not claim live activation or an external Claude client.
- **ADR-0011 / ADR-0021**: unchanged. Tools still wrap the staff Core ports behind `Features:AutomationMcp`.

## Risks / follow-ups

- Export success depends on Review stage and a *new* edit lease after add (`CaseMutationGuard.Complete` clears the lease). That is Core behaviour, not an MCP quirk.
- Tier-5 external-client evidence remains on [[TICK-023]].
- Sibling Now tickets [[TICK-027]] [[TICK-023]] [[TICK-024]] [[TICK-025]] still need the same as-shipped closeout; do not take them until this PR lands if they would also edit this test file.

## Verification hand-off

On merged `main`:

```
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter FullyQualifiedName~AutomationMcpIngressTests --configuration Release
```

Expect 9 passed. Rewrite `proof.md` from that command output. Do not treat the previous “Operator confirmed” stub as evidence. Not proved: deployed `/mcp`, `Features:AutomationMcp` outside DevelopmentOffline, or a real Claude Desktop/Code caller.

## Review follow-up

Review asked not to grow `AutomationMcpIngressTests` toward 1k lines or copy the assessment seed/HTTP helpers again.

- Shared harness: `AutomationMcpTestSupport.cs` (token, HTTP, seed, lease, `EnsureInReviewAsync` via `IReturnCaseToReview`).
- MCP-04 facts: `AutomationDocumentIngressTests.cs` (add/download, export refused when Not ready, export after Core return-to-Review, validation, scope).
- Ingress file back to gate/token/inventory/kill-switch/guard tests (~496 lines). Assessment tests use the same harness.
- No `UPDATE CaseWorkflows`. Focused Release run after the split: 15 passed (ingress + document + assessment).

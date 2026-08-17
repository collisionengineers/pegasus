# Research — TICK-026: MCP-04 document actions already exist

## Question

Are Automation Actor document actions already implemented through the same Core use cases as the staff app, and if so what evidence still fails FRD-10?

## Findings

- Capabilities, FRD-10, ADR-0021 and `docs/operations.md` already mark MCP-04 as implemented (lease-guarded add, download, export) behind `Features:AutomationMcp`, DevelopmentOffline only; non-blocking for `0.1.0-alpha.1`.
- `src/Pegasus.Web/Mcp/DocumentMcpTools.cs` registers three tools on the existing streamable-HTTP `/mcp` host: `pegasus_document_add`, `pegasus_document_download`, `pegasus_document_export`. They wrap `IAddCaseDocument`, `IDownloadCaseDocument`, and `IExportCaseDocuments` — the same ports as `Pages/Cases/Details.cshtml.cs`, `Documents/Download.cshtml.cs`, and `Documents/Export.cshtml.cs`.
- Scope is `automation.documents`. Add and export require `pegasus_case_edit_begin` lease + expected case version and an `mcp:` operation key. Download is read-only and synthesizes its own operation key. Add labels `DocumentSource.Automation` and requires `automation:` source-occurrence identity (default derived from the operation key).
- Inventory is not a fourth document tool: `pegasus_case_get` (MCP-02) returns a 200-entry document inventory and tells the caller to use `pegasus_document_download` for content. `ILogicallyRemoveDocument` is staff-only; ADR-0011 forbids Actor deletion authority.
- Core export is Review-only (`CaseNotInReviewException` in `EfDocumentCustodyStore`). Add writes `CustodyStatus.Confirmed` immediately. Inline MCP results cap at 64 KiB by default (raise via `maxInlineBytes`, add/download hard-cap 10 MiB, export archive 20 MiB).
- FRD-10 requires an exercised caller plus success, authorization failure, validation failure, and action-history proof per tool. `AutomationMcpIngressTests` lists the three tools, shares bearer-only and scope-denial coverage with the ingress, and has a lease-conflict failure + `ActionHistory` row for `pegasus_document_add` only. There is no HTTP success path for add, download, or export, and no documents-scope denial or document-tool validation test.
- The sibling assessment tranche already shows the pattern to copy: `AutomationAssessmentIngressTests` seeds a real case, claims `pegasus_case_edit_begin`, calls the tool over `/mcp`, and asserts structured content plus `ActionHistory`. `SeedAcceptedCaseAsync` does not put the case in Review; export success must move `CaseWorkflows.State` first (same SQL pattern as `QdosTriageIntegrationTests`).
- Ticket `proof.md` is the stub “Operator confirmed”. Tier-5 external-client evidence belongs to [[TICK-023]], not this ticket. Live activation stays gated off.

## Implications

- Do not scaffold a new MCP server, workspace, or tool. The product decision (remote HTTP in `Pegasus.Web`, one tool per action, official C# SDK, client-credentials) is already shipped.
- Remaining work is FRD-10 caller evidence on the existing factory, plus walking this feature ticket’s pipeline. Proof is rewritten at verify from the test run, not from the stub.
- Export success is not “call add then export”: the case must be in Review and the added version must still be Confirmed (add already does that).

## Open questions

None that block planning. Tier-5 Claude Desktop/Code evidence is explicitly parked on [[TICK-023]].

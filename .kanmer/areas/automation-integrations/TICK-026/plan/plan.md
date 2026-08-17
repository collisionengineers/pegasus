# Plan — TICK-026: Close MCP-04 as shipped with caller evidence

## Approach

Do not reimplement document tools. Add the missing FRD-10 HTTP caller tests on the existing gated `/mcp` host, then walk this feature ticket. The alternative — scaffolding a second MCP server — was rejected because `DocumentMcpTools` already wraps the staff Core ports behind `Features:AutomationMcp`. The alternative — closing on the stub “Operator confirmed” proof — was rejected because FRD-10 says a tool schema is not proof.

## Governing docs

- **FRD-10** (`docs/frd/frd-10-mcp-automation-and-actor-boundary.md`, linked): meets the document-action inventory (add / download / export only; no delete, no staff MCP access) and the per-tool evidence bar (exercised `/mcp` caller, success, authorization/scope failure, validation failure, action-history). Does not modify the FRD. Does not claim live activation or a Claude Desktop caller.
- **ADR-0011 / ADR-0021** (cited, not newly linked): the plan stays inside the existing gated Automation Actor contract; no new architectural decision.

## Steps

1. Create `../pegasus-worktrees/tick-026-mcp-04-document-evidence` on `task/tick-026-mcp-04-document-evidence` from `origin/dev`. `take_ticket` with those exact paths.
2. In `AutomationMcpIngressTests`, add a success test that seeds an accepted case (copy `SeedAcceptedCaseAsync` from `AutomationAssessmentIngressTests`), claims `pegasus_case_edit_begin`, then:
   - `pegasus_document_add` with a small PDF/bytes payload, `semanticRole` Other, `mcp:` key — assert structured occurrence/version/sha256, `IsReplay` false, `ActionHistory` Succeeded for `pegasus_document_add`.
   - Replay the same operation key — assert `IsReplay` true and the same occurrence id.
   - `pegasus_document_download` of that occurrence/version — assert `contentIncluded` true and decoded bytes match.
   - Download with `maxInlineBytes` below content length — assert `contentIncluded` false and a notice, no silent overflow.
3. Put `CaseWorkflows.State` to `Review` for that case (SQL update used by other integration tests). Call `pegasus_document_export` with the same lease (renew or re-begin if the add bumped version) and the occurrence/version pair. Assert manifest + inline or notice path; `ActionHistory` Succeeded for `pegasus_document_export`.
4. Add validation refusals: unrecognized semantic role; missing lease token; empty export selections. Each writes `ActionHistory` Failed and a content-safe error (no lease token in the body).
5. Add `automation.documents` scope denial: cases-only token calling `pegasus_document_add` writes `automation_scope_denied`.
6. Run focused `dotnet test` on `AutomationMcpIngressTests` (SqlServer / LocalDB). Record the command log for the post-implementation report.
7. Write `post-implementation-report`, push, open PR to `dev` with `Kanmer: TICK-026`. Move the ticket to Review.

## Verification

- Focused: `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter FullyQualifiedName~AutomationMcpIngressTests --configuration Release`
- Proof (after merge, on `main`): replace the stub with the test command output. Name the tier: HTTP `/mcp` caller against LocalDB (engineering tiers 4–5 local). Explicitly not: live activation, deployed `/mcp`, or an external Claude client.

## Risks / open questions

- Export fails unless the case is in Review — seed then SQL-set state; do not weaken Core.
- Add bumps case version — export must use the post-add version and a still-valid lease (renew if needed).
- LocalDB / SqlServer trait — same lane as existing ingress tests; do not add a new CI job.
- File collision with later MCP tickets — this PR owns `AutomationMcpIngressTests.cs` until merge.
- No open questions for the user. Tier-5 stays on [[TICK-023]].

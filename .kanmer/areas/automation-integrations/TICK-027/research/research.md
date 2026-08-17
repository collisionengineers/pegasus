# Research — TICK-027: MCP-06 assessment actions already exist

## Question

Are Automation Actor assessment / case-detail / EVA tools already implemented through the same Core use cases as the staff app, and what FRD-11 / ADR-0021 evidence is still missing?

## Findings

- Capabilities, FRD-11 and ADR-0021 already mark MCP-06 implemented behind `Features:AutomationMcp`. Automation writes land unconfirmed; finding confirmation, report approval, and outward dispatch tools are structurally absent.
- `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` hosts five tools: `pegasus_assessment_get` and `pegasus_assessment_update` (`automation.assessment`); `pegasus_case_update_details`, `pegasus_eva_bundle_generate`, and `pegasus_eva_handoff_status` (`automation.cases` for EVA status/generate; details uses the cases lease path).
- `AutomationAssessmentIngressTests` already exercises: assessment-scope denial on get; lease-guarded assessment update with unconfirmed provenance, work-request correlation, replay, and ActionHistory; EVA status (blocked reasons) and EVA generate (Blocked outcome + history) on a fixture with no custody-confirmed images.
- `pegasus_case_update_details` is registered and listed in `ExpectedTools` but has **no** HTTP success, validation, or ActionHistory test. `pegasus_assessment_get` is only hit as a scope-denial target against a random Guid, not as a successful read of a written assessment.
- No confirm-finding, approve-report, or dispatch tool exists in the 14-tool inventory — matches ADR-0021.
- [[TICK-026]] is still Implementing (document caller tests committed locally, PR not pushed). This ticket’s execute should land in `AutomationAssessmentIngressTests.cs`, not the MCP-04 file.

## Implications

- Do not reimplement tools. Remaining work is caller evidence for `pegasus_case_update_details` and a successful `pegasus_assessment_get` after an update.
- EVA generate success-with-images is a heavier fixture; the existing Blocked path already proves the same Core gate as staff. Do not expand that unless a reviewer asks.
- No confirmation/dispatch tools to add.

## Open questions

None that block planning.

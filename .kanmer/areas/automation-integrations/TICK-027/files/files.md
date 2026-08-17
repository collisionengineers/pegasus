# Files — TICK-027

## Where the change lands

| Path | Why |
| --- | --- |
| `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` | Add HTTP success for `pegasus_case_update_details` and a successful `pegasus_assessment_get` after the existing update, plus any missing validation/history assertions. |

No production code change expected.

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` | Tool contracts; case-owned fields must go through `pegasus_case_update_details`, not assessment update. |
| `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` | Unconfirmed writes; no confirm / approve / dispatch tools. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Behaviour this ticket implements. |
| `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` | Existing seed, lease, update, and EVA fixtures to extend. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Owned by [[TICK-026]] until that PR lands — do not edit it here. |

## Ripple effects

Callers and Core ports unchanged. Docs unchanged unless a current-state sentence is wrong.

## Out of scope

- New MCP host or live activation.
- Finding-confirmation, report-approval, or outward-dispatch tools.
- Estimate derivation (EXT-09).
- EVA generate happy-path with real images unless the existing Blocked proof is judged insufficient at review.
- [[TICK-023]] tier-5 client evidence.

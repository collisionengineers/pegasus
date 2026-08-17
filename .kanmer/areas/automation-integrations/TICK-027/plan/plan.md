# Plan — TICK-027: Close MCP-06 as shipped with remaining caller evidence

## Approach

Do not reimplement the assessment tranche. Extend `AutomationAssessmentIngressTests` so `pegasus_case_update_details` and `pegasus_assessment_get` have the same HTTP caller bar FRD-11 / ADR-0021 already have for assessment update and EVA. The alternative — treating the existing three tests as enough — leaves the case-detail tool unexercised.

## Governing docs

- **FRD-11** (linked): meets the direct-write assessment contract (unconfirmed automation values, staff-Engineer confirmation only, no dispatch). Does not modify the FRD.
- **ADR-0021** (cited): inventory stays fourteen tools; no new confirm/approve/dispatch tool.

## Steps

1. After [[TICK-026]]’s PR is open, create `../pegasus-worktrees/tick-027-mcp-06-assessment-evidence` on `task/tick-027-mcp-06-assessment-evidence` from `origin/dev` and take this ticket.
2. In `AutomationAssessmentIngressTests`, after a successful assessment update (or in that test), call `pegasus_assessment_get` and assert the unconfirmed fields are readable.
3. Add a lease-guarded `pegasus_case_update_details` success (one ordinary field, e.g. claimant or claim number), ActionHistory Succeeded, and an `mcp:` replay.
4. Add a validation refusal (unknown/invalid field or missing lease) with Failed history and no leaked token.
5. Run focused `AutomationAssessmentIngressTests` Release.
6. Write the post-implementation report, push, PR to `dev`, move to Review.

## Verification

```
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter FullyQualifiedName~AutomationAssessmentIngressTests --configuration Release
```

Proof after merge names the HTTP `/mcp` + LocalDB tier. Not: live activation or finding confirmation.

## Risks / open questions

- Wait for [[TICK-026]] to leave Implementing so two agents are not mid-flight on the same MCP surface, even though the files differ.
- Case-detail save re-opens completeness review — assert history, not a completeness policy rewrite.

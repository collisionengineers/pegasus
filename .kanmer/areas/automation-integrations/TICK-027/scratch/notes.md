## PROOFS-lane verification (2026-08-20)

Checked the ticket's own plan (`plan.md`) against what exists on `dev`, per this run's instructions ("compare the ticket's own plan/checklist scope against what exists; if fully covered, backfill PIR+proof and walk to done; if partially, name the gap").

**Verified covered:**
- `pegasus_assessment_get` after an update IS exercised with real assertions in `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` (`AssessmentUpdateOverHttpMutatesUnderLeaseWithCorrelatedAttribution`, around line 220): asserts `readiness` array populated and `caseOwned.registration` correct. This satisfies plan step 2.

**Gap found — NOT covered:**
- `pegasus_case_update_details` has **no functional integration test**. The only hit for this tool name anywhere under `tests/` is `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs:25`, where it appears solely in a static `ExpectedTools` inventory list (a tool-name/discovery assertion), not an invocation. There is no test exercising a lease-guarded successful case-detail write, its ActionHistory `Succeeded` record, an `mcp:` idempotent replay, or a validation/lease refusal that doesn't leak the token — i.e. plan steps 3 and 4 (which the plan itself specifies as the acceptance bar for closing this ticket) are not done.

**Verdict:** the ticket is genuinely partially covered, not just under-documented. This PROOFS-lane agent makes no code changes and cannot add the missing tests. Leaving TICK-027 in `preparing` (research/files/plan/checklist docs already existed and are adequate) rather than fabricating a post-implementation-report or advancing it. The next implementation lane should follow the existing plan.md steps 3-4 exactly (extend `AutomationAssessmentIngressTests` with a `pegasus_case_update_details` success + a validation/lease refusal case) before this ticket can honestly enter review/done.

No files were changed by this review.

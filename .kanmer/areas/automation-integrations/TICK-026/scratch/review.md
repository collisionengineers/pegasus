## Review — 2026-08-18 (claude-code; did not implement the PR)

PR #393 `task/tick-026-mcp-04-document-evidence` → `dev`, reviewed at `7c0387cc`, fix committed as `e108ec87`.

### Changes (reviewer's reading)
- `tests/Pegasus.IntegrationTests/AutomationMcpTestSupport.cs` (+263): shared harness — token request, `/mcp` POST + JSON-RPC readers, `SeedAcceptedCaseAsync`/`SeedPrincipalAsync`, `BeginEditAsync`, `EnsureInReviewAsync` via Core `IReturnCaseToReview`.
- `tests/Pegasus.IntegrationTests/AutomationDocumentIngressTests.cs` (+341): add→replay→download inline→oversize notice; export refused when not in Review; export after Core return-to-Review; validation refusals without leaking the lease token; documents-scope denial.
- `AutomationMcpIngressTests.cs` (−95) and `AutomationAssessmentIngressTests.cs` (−162): moved onto the harness; no behavioural change.
- No `src/` change. Tests only.

### Comments
1. **blocking → fixed-in-PR (`e108ec87`)** — FRD-10 §22-24 requires success, authorization failure, validation failure and action-history proof *per tool*. On `7c0387cc` download had no history assertion, no validation refusal and no scope denial; export had no scope denial. Codex P1 "Add caller evidence for every document tool" was still open. Fixed: success test asserts 2× `pegasus_document_download` Succeeded history; validation test adds an empty-version download refusal (`Failed` history now 4 across add/download/export); scope test posts all three tools with a cases-only token and expects 3 `automation_scope_denied` security events.
2. non-blocking — Codex P1 "consolidate duplicated seed fixture": addressed on `7c0387cc` by the shared harness (assessment tests use it too).
3. non-blocking — Codex P1 "fabricated domain instruction fixture": the seed line is the same minimal synthetic placeholder already used by `AutomationAssessmentIngressTests` and `ProcessIntakeTests` on `dev` (`Claimant Name: <label> / Claim Number: <id> / Vehicle Registration: AB12 CDE`); it is a test-only marker string, not fabricated domain evidence. Won't-do; existing convention wins.
4. Governing docs: FRD-10 unchanged; plan's "Governing docs" section matches the diff (no server added, no live activation claimed). Post-implementation report matches the diff, including its review follow-up section.

### Verdict
PASS after fix. Local focused run (Release, LocalDB): ingress + document + assessment = 15 passed, 0 failed. Merge once CI is green on `e108ec87`.

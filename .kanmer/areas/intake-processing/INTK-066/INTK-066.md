---
id: INTK-066
type: ticket
title: Confirm viable Case destinations directly from manual upload
status: implementing
area: intake-processing
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-09-08T17:32:00.155Z'
taken_at: '2026-09-08T17:48:42.856Z'
branch: INTK-066-manual-upload-confirmation
worktree: .worktrees/INTK-066
claim_expires_at: '2026-09-08T20:22:38.397Z'
claim_controller: codex-mcp-client
lease_id: eef8e3c2-cb07-4588-a7f9-5cb09a3433fc
lease_revision: 15
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-066'
lease_phase: running-command
lease_heartbeat_at: '2026-09-08T19:22:38.397Z'
labels: []
links:
  - DELIV-056
  - INTK-010
  - INTK-016
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - da6ff8e16815100e42da65e60df3e45a3cced2d2
  - 4a17becc09584696531611b4dc39d19521f489b3
  - 26bf5d00206014f58adf8149abe39084fd52b3f4
  - c57d8487cd343321a07abb68c161bd7d9a00aa27
  - 4b3329675f48faede428a9c97212d1a610584136
capture_evidence:
  - 'https://github.com/collisionengineers/pegasus/actions/runs/34240260482'
  - docs/frd/frd-02-intake-and-source-identity.md
  - tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs
  - tests/Pegasus.IntegrationTests/Browser/UploadCaseSearchBrowserTests.cs
capture_actor: codex-mcp-client
capture_disposition: promoted
capture_decided_at: '2026-09-08T17:31:49.444Z'
capture_decided_by: codex-mcp-client
archived: false
created: '2026-09-08T15:28:34.215Z'
updated: '2026-09-08T19:22:38.397Z'
---

## Observation

Corrective-release investigation found contradictory current FRD-02 rules: Matching conflicts and reversible association (line184) sends competing candidate Cases to Unidentified; Manual upload confirmation rows4/5 and Add to an existing case (lines382–408) offer direct attach for ambiguous/eligible unmatched uploads.

The two existing manual-instruction tests use generic body-only QDOS email. Current processing truthfully returns NeedsSorting and registers Unidentified; UploadOutcome prioritizes an open Unidentified item before PossibleMatch. The expected attach card/no-match message is therefore absent. A formal unmatched instruction auto-allocates; substituting one would erase the intended manual decision. Static investigation also found definitive ambiguous input is converted to NeedsSorting/Unidentified before the possible-match presentation branch. Do not fabricate a query result or repurpose the tests to image intake merely to get a pass.

Failures: UploadConfirmationWebTests.AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely and Browser.UploadCaseSearchBrowserTests.CaseSearchComboboxIsKeyboardOperableAndCompletesTheAttachDecision. Same browser failure existed on prior dev run34235294697 at6509746913eda16d2c4440add20e7f6793500f0b. D56 scratch/investigation contains the retained evidence and source routing details. No D56 production-path fix or browser edit is authorized.

## Operator decision — 8 September 2026

Implement the upload-screen workflow: staff choose the destination directly after uploading, where a viable Case exists, and may search for a Case. Surface only viable choices; if only one is available, show only that option. Explicit confirmation is still mandatory even for a single match because this is manual upload, not direct attachment from a Case. Include permissions, locking, reasons, error handling and safe retries. Update the conflicting FRD clauses and affected production callers/tests coherently. This authorizes bounded repository implementation, not deployment or live data mutations.

## Scope boundary

[[INTK-010]] and [[INTK-016]] are completed historical upload owners, not active correction tickets. Current board search found no active exact owner. Preserve their history. The whole root-cause class belongs together; it is not part of [[DELIV-056]]'s formal-evidence fixture-only correction. Use the feature profile for the missing confirmation route and permission/concurrency consequences. Preserve keyboard accessibility, search, destination, lease/version/reason and replay assertions. Do not expand into unrelated email allocation, upload caps or public sessions.

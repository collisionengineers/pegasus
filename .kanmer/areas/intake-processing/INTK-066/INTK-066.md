---
id: INTK-066
type: ticket
title: Resolve conflicting ambiguous-upload decision contract
status: backlog
area: intake-processing
assignee: ''
profile: capture
labels: []
links:
  - DELIV-056
  - INTK-010
  - INTK-016
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
capture_evidence:
  - 'https://github.com/collisionengineers/pegasus/actions/runs/34240260482'
  - docs/frd/frd-02-intake-and-source-identity.md
  - tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs
  - tests/Pegasus.IntegrationTests/Browser/UploadCaseSearchBrowserTests.cs
capture_actor: codex-mcp-client
archived: false
created: '2026-09-08T15:28:34.215Z'
updated: '2026-09-08T15:28:34.215Z'
---

## Observation

Corrective-release investigation found contradictory current FRD-02 rules: Matching conflicts and reversible association (line184) sends competing candidate Cases to Unidentified; Manual upload confirmation rows4/5 and Add to an existing case (lines382–408) offer direct attach for ambiguous/eligible unmatched uploads.

The two existing manual-instruction tests use generic body-only QDOS email. Current processing truthfully returns NeedsSorting and registers Unidentified; UploadOutcome prioritizes an open Unidentified item before PossibleMatch. The expected attach card/no-match message is therefore absent. A formal unmatched instruction auto-allocates; substituting one would erase the intended manual decision. Static investigation also found definitive ambiguous input is converted to NeedsSorting/Unidentified before the possible-match presentation branch. Do not fabricate a query result or repurpose the tests to image intake merely to get a pass.

Failures: UploadConfirmationWebTests.AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely and Browser.UploadCaseSearchBrowserTests.CaseSearchComboboxIsKeyboardOperableAndCompletesTheAttachDecision. Same browser failure existed on prior dev run34235294697 at6509746913eda16d2c4440add20e7f6793500f0b. D56 scratch/investigation contains the retained evidence and source routing details. No D56 production-path fix or browser edit is authorized.

## Decision required

Operator must select the intended workflow: resolve competing matches through Unidentified and align obsolete upload clauses/tests, or offer direct Case selection in upload confirmation and implement the currently missing route with its authority/replay guards. Root asked this as a nonblocking question on8September2026. No answer yet. This capture does not choose the outcome or authorize implementation.

## Scope boundary

[[INTK-010]] and [[INTK-016]] are completed historical upload owners, not active correction tickets. Current board search found no active exact owner. Preserve their history. The whole root-cause class belongs together; it is not part of [[DELIV-056]]'s formal-evidence fixture-only correction. After decision, promote/plan this record using the consequence-appropriate profile, preserving keyboard accessibility, search, destination, lease/version/reason and replay assertions. No source change, live write, branch or worktree has been created.

# Plan — UIIMP-017

## Objective and starting state

Fix one office-time display inconsistency and one ambiguous Health snapshot
state. Baseline accepted dev19e6f523bf6760cab39104b4dca3674b0ac8a512;
read research/files and the complete ignored diagnosis. This is a bounded
follow-up, not takeover of the historical UIIMP005 claim.

## Governing documents and reuse

Meet FRD12 and design/README's one-clock rule. Reuse OperatorLabels.OfficeTime,
existing Health Razor, StateMatch and populated mailbox Web test. No new
policy, component, test framework, global culture or broad normalization.

## Changes

1. Render five metrics instants through OfficeTime with the same empty value
   convention as existing service rows. Preserve actual values/counts.
2. Require the exact graph_unavailable table cell for the existing Health
   default scenario; update catalogue scenario prose. Add a small direct
   predicate test rejecting empty/automation-only HTML and accepting the
   declared populated state; keep business timestamps in snapshots.
3. Extend the existing populated route test to assert the same London instant
   in the metrics and service row. Reuse its UTC input/fixed clock, not a
   newly fabricated mailbox or global clock change. Check non-UK culture with
   existing test conventions only if it genuinely reaches the rendered request;
   do not claim a culture test from a calling-thread change alone.
4. Root performs one focused build, actual route capture and selector test;
   update/verify administration-health only, then catalogue. Preserve failures.

## Files and boundaries

Only the six paths in files/files.md. No shared business formatter redesign,
production clock/culture mutation, page layout or JS changes. Wait until
TICK035 generated-index ownership clears before take. Do not mutate old
UIIMP005 worktree/claim, shared checkout, cloud, mailbox or corpus.

## Acceptance and commands

Use existing locked restore/Release build once, root only. Actual capture:
FullyQualifiedName=Pegasus.IntegrationTests.ApprovedMailboxAdministrationWebTests.ThePageShowsActivationAndSubscriptionHealthPerMailbox
plus the new same-owner Health selector regression. Set
PEGASUS_TEST_UI_CAPTURE_DIR to the exact worktree artifacts/test-ui-capture,
PEGASUS_TEST_UI_SCOPE=administration-health, UI mode unset. Do not use the
script's broad capture default or rerun unrelated browser cohorts.

Run Update-TestUiSnapshots.ps1 -SkipCapture -Scope administration-health, then
-Verify -SkipCapture -Scope administration-health and Test-UiCatalogue.ps1.
All must exit0 and changed files stay bounded. The full caller test proves
London display; the state predicate proves unrelated successful responses
cannot redefine the default. No manual visual PASS from a build alone.

## Stop condition

Preparation stops with plan/files/checklist; ticket stays Preparing/unclaimed
until root explicitly assigns a fresh packet after ownership clearance.
Execution stops for root focused verification, then independent review of the
pushed dev-targeting PR. No self-review/merge/deploy.

# Files — MAIL-036

## Where the change lands

| Path | Why |
| --- | --- |
| `scripts/Invoke-IntakeDataWipe.ps1` | Require a stopped Worker, capture one cutoff, execute the reset with deletion, check CLI failures. |
| `scripts/Reset-IntakeMailBoundary.sql` | One reusable SQL body advances/seeds existing poll boundaries without changing approval or onboarding. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedInboxPollStore.cs` | Scope refresh must never move a later wipe boundary backwards. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Reject old notifications before downloading MIME. |
| `tests/Pegasus.IntegrationTests/ApprovedMailboxEstateIntegrationTests.cs` | Exercise the exact reset SQL and real poll claim for existing/missing state and scope changes. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Prove old/new notification and delta-reset filtering. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Own the later wipe cutoff contract. |
| `docs/runbook.md` | Document maintenance/reset operation, not a new current-state claim. |
| `AGENTS.md` | Record changed wipe execution prerequisite. |
| `.agents/skills/pegasus-wipe-intake-data/SKILL.md` | Correct obsolete cursor-preservation and queue-storage claims. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Both notification and polling already filter against the same lease effective boundary; no new business rule needed. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` | Existing StartBoundaryUtc is sufficient; no schema change. |
| `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` | A new unbound poll state can use the existing empty scope fingerprint and nonnegative generation. |
| `docs/adr/0024-stable-approved-mailbox-identity-and-explicit-baseline.md` | Stable mailbox identity, approval untouched; Worker read-only on mailbox policy. |

## Findings and ripple effects

At dev 2e50fde474ce35eb32eff8677eb2327cb6aad272, wipe removes occurrence
identities but preserves onboarding-era boundaries. Graph resets enumerate old
mail again. Core already rejects old notifications, but only after MIME fetch.
Claim currently overwrites a later boundary on scope/generation refresh.
Seed missing poll state at wipe cutoff; normal Claim binds its scope without
lowering that boundary. Future re-enablement's newer activation still wins.

## Out of scope

No actual wipe, Azure mutation, mailbox/Box write, new migration or policy
port. Existing data/identity/sequence preservation set stays unchanged.

## Notification clarification

Existing `MailboxIntake.cs` ExecuteNotificationAsync scans up to 50 unrelated
messages after each direct fetch. The operator rejects this duplication;
existing recovery timer/lifecycle paths remain. Extend that existing port with
notification lease completion, update its sole fake in PollApprovedInboxTests,
and prove no scan and unchanged recovery cursor. No second policy owner.

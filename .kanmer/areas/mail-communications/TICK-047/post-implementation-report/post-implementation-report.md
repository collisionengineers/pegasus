# Post-implementation report — TICK-047

## Summary

MAIL-05 now provides an authenticated, read-only folder recommendation on one retained message. `GetRetainedMail` reuses the merged MAIL-23 policy and approved-mailbox store, matches the retained message's exact mailbox identity, verifies the current typed binding, and returns the canonical logical folder or an honest unavailable reason. The staff page displays that result without exposing opaque Outlook identifiers or adding any confirmation, persistence, Graph call, or mailbox mutation.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Added `RetainedMailFolderRecommendation` to detail and derived it in `GetRetainedMail` through `MailLogicalFolderPolicy` plus `IApprovedMailboxStore`. | Keeps one authorized exact-message Core owner and re-derives current classification/configuration on every read. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Added recommended logical folder/policy or labelled unavailable text to Classification evidence. | Proves the real staff caller and remains accessible without exposing internal folder identities or adding a form. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` | Added configured, ambiguous, wrong/disabled/unconfigured mailbox, No action, and re-derivation coverage; extended the existing fake boundary. | Proves exact scope, fail-closed behavior, no duplicate mapping, and live derivation. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Added current configured recommendation and unavailable caller evidence, including non-disclosure and no move control. | Proves the authenticated Razor caller against the real SQL persistence boundary. |
| `docs/design/README.md` | Narrowed the MAIL-23 exception to include locally activated read-only MAIL-05 while keeping opaque identifiers and MAIL-06/07 deferred. | Removes a now-false deferred-surface statement without broadening write authority. |
| `docs/capabilities.md` | Recorded the exact local Core/Web caller and evidence tier for MAIL-05. | Keeps the capability registry honest: local/test-backed, not deployed or live verified. |

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: the implementation keeps classification, application destination, folder recommendation, and later move separate; accepts no destination; returns no recommendation for ambiguous/unclassified or unconfigured state; and treats configured `No action` as a real folder.
- The user's explicit implementation instruction authorized the narrow `docs/design/README.md` and `docs/capabilities.md` activation updates. No operator truth or FRD behavior changed.
- No ADR was added because the existing Core policy/port and composition direction carry the feature.

## Simplification pass — 2026-08-20

- **Reuse:** reused `MailLogicalFolderPolicy.Map`, `MailLogicalFolders`, `IApprovedMailboxStore.ListAsync`, `RetainedMailDetail`, and `GetRetainedMail`; no second taxonomy, mapping, store, or use case.
- **Simplification:** removed an initial future-facing projection of opaque folder identity and classification/binding versions. MAIL-07 must re-read current state rather than trust read-page state.
- **Efficiency:** skips the approved-mailbox list when classification is absent/ambiguous; uses the existing small estate read otherwise. No new I/O boundary or cache.
- **Altitude:** no Infrastructure change, persistence, migration, transaction, operation key, Graph adapter/call, confirmation, retry, MCP schema, or generic action framework.
- **Unapplied findings:** none.

## Risks / follow-ups

- [[TICK-049]]/MAIL-07 remains the separate confirmed move owner and must independently re-read current classification and binding before any external write.
- Deployment and the already-approved authenticated read-only live viewer check remain separate evidence; this PR performs no external write and claims neither.
- The exact Outlook folder identity remains internal. The page intentionally shows the canonical logical label only.

## Verification hand-off

Completed locally on Windows / SQL Server LocalDB:

- `dotnet restore ./Pegasus.slnx --locked-mode` — passed.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- Focused final Core: `RetainedMailTests` — 27/27 passed.
- Focused final-markup Web/SQL: `MailWorkspaceWebTests` — 16/16 passed.
- Canonical non-corpus selection: Core 834/834, Architecture 98/98, Integration 800/800 — 1,732 passed. The explicit re-derivation unit was added afterward; the final Release build and 27/27 focused Core rerun passed.

After merge/promotion, `kanmer-verify` should rerun the locked restore, Release build, canonical non-corpus suite, and capture authenticated `/Inbox/{id}` evidence for a configured and unavailable classification. The permitted live step is read-only; do not confirm/move mail or alter mailbox/folder/cloud configuration.

## PR-032 blocker resolution — 2026-08-20

PR-032 corrected the null-dossier caller omission on the same branch. Message.cshtml now renders the existing Core unavailable recommendation in a semantic Folder recommendation section when Classification is null; classified/ambiguous rendering is unchanged. MailWorkspaceWebTests now has an exact authenticated null-classification case asserting the unavailable reason and no move/new POST control.

Verification after the fix: Release solution build passed with 0 warnings/errors; focused MailWorkspaceWebTests passed 17/17. Four-lens disposition: reuse the existing Core result; no new mapping/query/partial; Razor-only constant work; no Core, Infrastructure, persistence, DI, MCP, Graph, operation key, or mutation change.

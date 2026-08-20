# Plan — TICK-047: MAIL-05 folder recommendation

## Approach

Extend the existing authorized `GetRetainedMail` read with one nullable, Core-owned derived recommendation. It maps the current classification through the merged MAIL-23 policy, matches the retained message's exact mailbox identity to the current approved mailbox row, and resolves that logical type through the existing typed binding. This is smaller and safer than a new use case, store, persistence model, or UI mapping: it naturally re-derives after classification/configuration changes and leaves MAIL-06/07 mutation concerns out.

## Governing docs

- **Meets** `docs/frd/frd-08-email-mailbox-and-background-processing.md`: exact-message detail exposes only the designated folder from the current classification policy and current mailbox-scoped binding; ambiguous/unclassified/unconfigured states fail closed; classification, application destination, recommendation, and later move remain separate; no arbitrary destination is accepted.
- **Modifies under the user's explicit implementation instruction** `docs/design/README.md`: narrow the existing MAIL-23 activation exception to acknowledge this authenticated, read-only MAIL-05 message-detail caller, while leaving confirmation, moves, deployment, and Outlook writes deferred.
- **Modifies under the same instruction** `docs/capabilities.md`: replace MAIL-05's allocation-only note with the exact local Core/Web caller and test evidence, qualified as not deployed or live-mailbox verified.
- No ADR is needed: the existing Core port and Core/Infrastructure/Web dependency direction already carry the change.

## Steps

1. Extend `RetainedMailDetail` with the smallest recommendation projection and compose it inside `GetRetainedMail` using `MailLogicalFolderPolicy.Map`, `IApprovedMailboxStore.ListAsync`, exact `ApprovedMailbox.MailboxIdentity`, and the current typed binding. Preserve all current actor-resolution behavior and return an honest unavailable reason without any write machinery.
2. Render the Core result in the existing Classification evidence definition list on `/Inbox/{id}`, including logical label, exact configured folder identity, policy/version and binding version when available, and a labelled unavailable state otherwise. Add no form, folder input, or MCP contract.
3. Add focused Core tests for exact configured resolution, no recommendation for ambiguous/unclassified or missing/disabled/wrong-mailbox binding, and `NoAction` as a valid recommendation; extend existing fakes only.
4. Add focused Web integration evidence for the authenticated exact-message caller with a configured binding and an unavailable state, and reconcile the design/capability records to the local evidence tier.
5. Run locked restore and Release build, the focused Core/Web tests and proportional relevant suite; inspect the branch diff through reuse, simplification, efficiency and altitude lenses, apply behavior-preserving findings, and record dispositions before the post-implementation report and PR.

## Verification

Run `dotnet restore --locked-mode` if the runbook requires it, `dotnet build --configuration Release --no-restore`, focused `Pegasus.Core.Tests` and SQL Server-gated `MailWorkspaceWebTests` using the runbook profile, then the relevant non-external suite. The post-implementation report records commands/results, real caller evidence, no-write qualification, diff/file list, and four-lens dispositions. Proof and the approved live read-only mailbox check remain post-merge/release work, not this PR.

## Risks / open questions

- Exact identity confusion is mitigated by ordinal `Summary.MailboxId` → `ApprovedMailbox.MailboxIdentity` matching; aggregate id/address are not substitutes.
- A missing or disabled approved row, absent folder outcome, or missing typed binding returns unavailable rather than guessing.
- Constructor changes affect the existing MCP composition caller only through DI; no MCP schema changes.
- No open question remains. MAIL-06/07, external mutation, deployment, and live verification stay deferred.

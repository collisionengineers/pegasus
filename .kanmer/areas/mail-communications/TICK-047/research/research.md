# Research — MAIL-05

## Question

How should Pegasus recommend exactly the policy-designated Outlook folder for one classified message without accepting an arbitrary destination?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: FRD-08 separates classification, operational queue and Outlook destination; no Core folder-recommendation policy exists, while ApprovedMailbox holds exact mailbox/folder transport scopes.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

## Current-state refresh — 2026-08-20 (supersedes the earlier generic mutation assumptions)

### Question

What exact current policy and caller should MAIL-05 extend after MAIL-23, and what is the smallest read-only recommendation slice that does not duplicate taxonomy, mailbox binding, or the later confirmed move?

### Verified findings

- Current sources inspected were `origin/dev` `b36c6666` and its `origin/main` ancestor `2325ed4a`; the local `dev` checkout is 103 commits behind and is not an implementation base.
- Completed foundations are already present on both remote branches:
  - MAIL-22's canonical detailed taxonomy is `MailCategory`/`MailTaxonomy` in `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`.
  - MAIL-02's operational-queue owner is `MailOperationalDestinationPolicy`; it has no Outlook-folder result and must remain separate.
  - MAIL-04's current, corrected classification is `MailClassificationDossier.Current`, returned with permanent history.
  - The real authorized read use case is `GetRetainedMail` in `src/Pegasus.Core/Intake/RetainedMail.cs`. It loads one exact retained-message detail after `PerformCasework` authorization.
  - The real staff caller is `MessageModel.OnGetAsync` and `/Inbox/{id}` in `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)`. It already renders `MailOperationalDestinationPolicy.Map(dossier.Current)` live rather than persisting a duplicate.
- `RetainedMailDetail` already carries all message-side inputs a recommendation needs: `Summary.MailboxId`/`MailboxAddress`, exact retained message identity, folder scope, and the current classification dossier. Recommendation should therefore extend this existing exact-message read path rather than introduce a second message query or a UI-owned mapping.
- No executable logical-folder policy or mailbox-approved logical-folder binding exists on current `origin/dev`. `ApprovedMailboxAdministration.cs` and `EfApprovedMailboxStore.cs` currently know only mailbox, Inbox and Sent identities; `GraphApprovedMailboxResolver` resolves only those well-known folders. A retained message's `FolderIdentity` is its source location, not a destination catalogue.
- [[TICK-064]]/MAIL-23 has refreshed research proving and scoping the missing prerequisite: one typed classification-to-logical-folder/no-recommendation Core policy plus mailbox-scoped administrator-approved logical-folder-to-exact-identity binding. Its refreshed file map places those contracts beside `MailOperationalDestinationPolicy.cs` and in the approved-mailbox administration/store boundary. MAIL-05 cannot honestly recommend an exact designated folder until those contracts land.
- The dependency is now recorded structurally: [[TICK-064]] blocks [[TICK-047]]. MAIL-05 must be replanned from the merged MAIL-23 symbols rather than guessing their names now.
- MAIL-05 is a read-only derived projection. It writes no classification, mailbox binding, message, history, or Outlook state. The old plan's actor reason, operation key, idempotency transaction, optimistic-concurrency write, and arbitrary-destination rejection are mutation ceremony and do not belong here.
- The recommendation must be re-derived from the current dossier and current approved mailbox binding every time detail is read. It must be visibly unavailable where classification is absent/ambiguous, MAIL-23 yields no logical folder, the mailbox binding is absent/stale/unavailable, or identities do not belong to the exact approved mailbox. It must never invent a fallback folder or accept client input.
- FRD-08 keeps three facts separate: classification, application destination, and Outlook folder. A Triage application destination does not itself choose a folder; only the detailed classification's MAIL-23 folder outcome may do so. Unidentified has no automatic recommendation. A logical folder named `No action` is a real designated folder type and must not be confused with “no recommendation.”
- The output must retain enough provenance for the later MAIL-07 confirmation to prove which current classification and approved binding produced it, but MAIL-05 adds no confirm/move control. MAIL-07 owns confirmation, stale-state enforcement, operation identity, Graph mutation and retry.
- Exact overlap audit from current ticket maps:
  - `RetainedMail.cs`: [[TICK-049]], [[TICK-050]], [[TICK-053]], and [[TICK-056]].
  - `Mail/Message.cshtml.cs`: [[TICK-049]], [[TICK-050]], [[TICK-051]], [[TICK-052]], [[TICK-054]], [[TICK-057]], and [[TICK-088]].
  - `MailWorkspaceWebTests.cs`: [[TICK-053]], [[TICK-056]], and [[TICK-057]].
  - MAIL-23 owns the approved-mailbox Core/persistence/admin/migration/Graph-resolution files. MAIL-05 should consume those landed ports and policy rather than edit them again.
  - [[AUTO-003]] is downstream and later exposes landed Core use cases; MAIL-05 adds no MCP tool.

### Implications

Wait for MAIL-23, then refresh exact symbol names from merged `origin/dev`. Extend the existing `GetRetainedMail` exact-message read model/use case (or the smallest adjacent Core read use case only if MAIL-23's landed interface requires it) to combine the current classification with the mailbox-approved binding. Render the resulting recommendation/unavailable reason on message detail. Reuse the current policy/binding ports and Web labels; do not add recommendation persistence, a new store, Graph access, or a generic mail-action framework.

Tests should prove the current classified message resolves to its configured exact folder; reclassification or binding change re-derives; unclassified/ambiguous/Unidentified/unconfigured/wrong-mailbox states fail closed; `No action` remains a legitimate folder; the authenticated Web caller renders provenance; and reading causes no history, mailbox, or Graph mutation.

### Open questions

None. MAIL-23 landing is a hard dependency, not an unresolved product choice. The plan must refresh against its actual merged contracts and may not invent substitute folder vocabulary or identities.

# Plan — TICK-064: MAIL-23 logical-folder policy and approved mailbox bindings

## Approach

Add one pure Core `MailLogicalFolderPolicy` beside the existing `MailOperationalDestinationPolicy`. It maps the current `MailClassificationResult` to one of FRD-08's 13 typed logical folder values or an explicit no-recommendation result; it never recomputes MAIL-02's operational queue. Extend the existing approved-mailbox aggregate with a normalized collection of logical-type → exact Outlook folder-identity bindings, preserving its administrator authorization, optimistic version, idempotent operation and `ActionHistory`. A read-only resolver discovers exact folder identities inside the already resolved mailbox, and the existing mailbox administration page provides the real approval/refresh caller. No retained-message or per-message recommendation state is added.

A combined queue+folder result was rejected because MAIL-02 already owns queues. Thirteen nullable mailbox columns were rejected because they duplicate one concept structurally. Persisting a recommendation on each retained message was rejected because corrections must re-derive and MAIL-05 owns the message-level consumer.

## Governing docs

- **Meets `docs/frd/frd-08-email-mailbox-and-background-processing.md`:** implement its exhaustive classification → logical folder catalogue, the 13 approved folder types, explicit no-automatic-folder result for ambiguous/unclassified material, and administrator-approved exact mailbox binding. The Core projection remains separate from operational destination, Triage routing, MAIL-05 recommendation and MAIL-07 confirmation/move. Correct the one stale `Needs sorting` phrase in the catalogue to the already binding `Unidentified` term; this restates the deployed INTK-007 decision and does not alter operator truth.
- **No ADR:** the existing Core policy, Infrastructure external-identity adapter, EF child collection and Web composition boundaries carry the change. No new top-level boundary, store, runtime or deployment unit is introduced.
- **No deployment claim:** `docs/current-architecture.md` and `docs/operations.md` remain unchanged because this task performs no deployment or production resolution check.

## Estimated diff

About 12–15 source/test files plus one EF migration/designer/snapshot: one small Core policy file, focused additions to approved-mailbox contracts/store/model, a read-only folder-resolution extension, one administration handler/view section, and focused Core/integration tests. Expected handwritten diff is roughly 500–800 lines; generated migration metadata is mechanical. If implementation requires retained-message persistence, queue changes, message detail recommendation, or Graph writes, stop and replan because that is MAIL-05/07 or broader scope.

## Steps

1. Add `MailLogicalFolderType`, a stable key/label catalogue, and `MailLogicalFolderPolicy.Map(MailClassificationResult)`; reuse `MailCategory` and keep `MailOperationalDestinationPolicy` untouched. Add exhaustive Core tests for every registered Received/Sent/Other classification, reply-context invariance, and explicit no recommendation for Ambiguous/Unclassified.
2. Extend `ApprovedMailbox` and `UpdateApprovedMailboxRequest` with typed folder bindings. Validate unique defined types and exact bounded identities in Core, preserve existing bindings when an ordinary update omits them, and include bindings in the store's replay fingerprint/history snapshot.
3. Persist bindings in one normalized `ApprovedMailboxFolderBindings` child table keyed by mailbox+logical type, with exact identity length/uniqueness and cascade ownership. Add one migration and update the model snapshot; do not touch retained-message entities/store.
4. Extend the existing mailbox identity resolver with a read-only exact-folder discovery result, and add a focused administrator “resolve folder bindings” action to the current Mailboxes page. It may accept only server-resolved typed bindings, reports missing/ambiguous types as unconfigured, and performs no create/rename/move. Reuse the existing Graph token/host/safe-error conventions and Web authorization/operation handling.
5. Add focused EF, fake-Graph and Web tests for persistence/replay/conflict/versioning, mailbox scoping, unavailable/ambiguous resolution, administrator-only caller, honest unconfigured display, and absence of Graph mutation.
6. Run locked restore/build and focused Core/integration tests, then the full test suite proportionate to the shared mailbox model/migration. Run the required four-lens simplification pass over the branch diff (reuse, simplification, efficiency, altitude), apply behavior-preserving findings, and record each disposition here.
7. Commit, push, open the PR to `dev`, write the post-implementation report with exact command results and deployment qualification, record traceability, and move TICK-064 to Review without reviewing or merging it.

## Verification

Run from the ticket worktree:

- `dotnet restore --locked-mode`
- `dotnet build --configuration Release --no-restore`
- focused `dotnet test` filters for `MailLogicalFolderPolicyTests`, `ApprovedMailboxEstateIntegrationTests`, `ApprovedMailboxAdministrationWebTests`, and `ProductionGraphSourceTests`
- `dotnet test --configuration Release --no-build`

Review the generated SQL/model diff for normalized keys, constraints, ownership and runtime grants. Fake HTTP tests must prove GET-only folder discovery and exact mailbox confinement. Web tests must prove administrator authorization and no client-supplied folder identity. No live Outlook/Azure check is run; the parked post-deployment read-only check remains for later verification with separately appropriate runtime access.

## Risks / open questions

- Graph may expose duplicate display names or nested folders. Resolve only one exact, unambiguous server result per canonical label; duplicates/missing values stay unconfigured. Do not guess a path.
- Existing mailbox updates must not erase bindings simply because the ordinary policy form omitted them. The update contract distinguishes omitted bindings (preserve) from a resolver refresh (replace with the exact resolved set).
- Migration changes touch a shared administration table. Keep the schema additive and validate committed-migration fixtures/full tests.
- No open product question remains; all `open-questions` entries are resolved or explicitly parked.

## Simplification pass

To be completed during execution with dated findings and dispositions.

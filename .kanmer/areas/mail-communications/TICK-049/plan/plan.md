# Plan — MAIL-07

## Chosen approach

Implement one concrete Core-owned confirmed-folder-move use case after PR #469 lands. Preserve MAIL-05's read-only contract: Razor receives a logical folder recommendation and freshness values, never Graph transport identities or a selectable destination. On POST, Core re-loads the current classification, policy and typed approved-mailbox binding; Infrastructure supplies exact persisted coordinates to the existing Graph client.

Use one dedicated durable folder-move operation/current-location record because the SQL claim and Graph move cannot be atomic. Do not generalize it into a mail-command framework: no second concrete mutation caller exists. Keep retained arrival evidence write-once.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: deliver separate reasoned confirmation, exact-message/current-approved-destination validation, no automatic or arbitrary move, durable visible failure, staff-only explicit retry, preserved classification, and removal from Inbox after success.
- `docs/design/README.md`: reuse the existing reason dialog, anti-forgery, Confirm/Cancel, focus return, stale/conflict, replay and dependency-unavailable presentation.
- `docs/capabilities.md`: record only local implementation and fake/local evidence. Allocation and merged code are not deployment or live Outlook proof.
- No ADR: Core/Infrastructure/Web and the existing Graph client already carry the change.

## Preconditions

TICK-064 and TICK-047 are merged into `origin/dev` at or before `a1775841`. TICK-053 / PR #469 currently owns overlapping files, so execution must wait until #469 merges and the claim releases. Then fetch the new `origin/dev`, verify it contains both predecessors and #469, and re-read the landed overlap before creating/taking TICK-049.

## Ordered implementation

1. **Refresh and isolate.** From post-#469 `origin/dev`, re-read retained-mail search/detail, Graph client/composition, Razor and overlapping tests. Create `task/tick-049-mail-07-confirmed-folder-move` in `../pegasus-worktrees/tick-049` and take TICK-049. Reuse PR #469's landed query overloads and fake Graph handler shapes.
2. **Add the one Core move owner.** Create a focused `RetainedMailFolderMove` contract/use case. Reuse `StaffAuthorization.PerformCasework`, `MailLogicalFolderPolicy`, `MailClassificationDossier.Version`, `ApprovedMailbox.Version` and typed bindings. Validate internal message id, expected classification/policy/mailbox versions, GUID operation key and 1–500 character reason. Accept no mailbox/source/destination transport value.
3. **Persist one exact operation/current location.** Add the focused entity, configuration, store and migration. Reserve a unique operation key plus request fingerprint before Graph; matching completed replay returns its result and changed inputs conflict. Preserve the retained row, append action history, and overlay only the latest successful location onto list/detail. Reuse existing EF concurrency and ActionHistory conventions.
4. **Add exact Graph move/probe.** Extend `GraphMailClient` with only the folder-scoped move POST and immutable-item parent-folder probe. Reuse its authorization, host/URI confinement, safe HTTP mapping and `Prefer: IdType="ImmutableId"`. On uncertain response, probe: destination means recovered success, source means recorded failure eligible for a deliberate new-key retry, and unresolved location remains uncertain/replay-probe-only so a blind second move is impossible.
5. **Wire the authenticated message caller.** Extend MAIL-05 recommendation with only approved-mailbox version freshness. Reuse the message page, anti-forgery, list-context route values and shared reason dialog. Render the current server-derived label, Confirm/Cancel, durable result and explicit retry; preserve saved classification on every provider failure.
6. **Prove locally without external writes.** Add focused Core tests, SQL integration for claim/concurrency/history/projection, fake-handler Graph request/recovery tests, and exact authenticated Web tests. Run locked restore and Release build, focused tests, then the proportional full Core and Integration suites. No live Graph/mailbox/cloud action is permitted.
7. **Simplify and report.** Review the branch diff through reuse, simplification, efficiency and altitude lenses. Apply behaviour-preserving findings; record dispositions under a dated Simplification pass. Update capabilities/current architecture only to the evidence reached, write the post-implementation report, push, open a PR to `dev`, and leave TICK-049 in Review.

## Acceptance evidence

- Core: authorization and all freshness/state checks fail before the provider port; replay/conflict/recovery/new-key retry are deterministic.
- Persistence: concurrent claims produce one operation; arrival identity/classification remain unchanged; failed/uncertain attempts stay visible; success alone changes effective location and removes the item from Inbox.
- Graph fake: exact folder-scoped POST body/header plus exact immutable-id parent probe; no network outside the fake handler.
- Web: authenticated staff sees the current recommendation, submits no destination, must confirm with reason, gets stale/failure/retry/success semantics, and classification survives failure.
- Commands and results are captured in the post-implementation report. Live mutation, permissions, deployment and production activation are explicitly not claimed.

## Risks and mitigations

- **Overlapping search work:** wait for #469, branch from its merge, and preserve its focused tests.
- **Silent binding drift:** expose only `ApprovedMailbox.Version` as freshness; re-resolve the exact binding server-side.
- **SQL/Graph split:** durable reservation plus exact destination/source probe; never blind-retry an uncertain operation.
- **Scope growth:** keep one folder-move operation, one adapter extension and one Web caller; defer every other mail action and Automation exposure.

## Simplification pass — 2026-08-20

- **Reuse:** Kept one Core move owner, the existing `MailLogicalFolderPolicy`, typed approved-mailbox binding/version, retained-mail query, `GraphMailClient`, shared reason dialog, EF concurrency and ActionHistory shapes. No duplicate taxonomy, authorization table, Graph client or destination list was introduced.
- **Simplification:** Removed an unused move-reason bound property; the shared dialog's existing `Reason` field binds directly to the handler. Kept the operation/current-location owner concrete to MAIL-07 and did not introduce a generic mail-command/result framework.
- **Efficiency:** Inbox exclusion is a SQL `NOT EXISTS` against successful operations before count/paging. The command performs one exact current-location probe, one folder-scoped move, and probes again only after an uncertain response; replay does not issue a second move.
- **Altitude:** Core owns authorization/input/freshness policy, Infrastructure owns exact coordinates/durable recovery, and Razor only confirms the server-derived recommendation. The default provider remains unavailable and the control stays absent until an explicitly composed writer exists.
- **Applied findings:** preserve actor roles in durable history; suppress duplicate history when an uncertain replay remains unchanged; render the durable result reason; remove the accidental duplicate `ApprovedMailboxFolderBindings` creation from the generated migration after the post-merge snapshot divergence.
- **Unapplied findings:** none.

## Review-blocker simplification pass — 2026-08-20

- **Reuse:** The corrections continue to use the existing dedicated MAIL-07 operation, MAIL-05 recommendation, MAIL-11 retained search, typed approved-mailbox bindings, Graph mover/probe, EF store and shared reason dialog. No second category list, search implementation, destination policy or command framework was added.
- **Simplification:** One filtered active-operation index and one latest-success current-location rule replace the earlier process-local assumptions. Same-key recovery carries only the original safe freshness material required to recreate the existing request; transport identities remain server-side.
- **Efficiency:** Serialization is enforced at the SQL claim boundary. Current-location verification occurs after reservation, search inclusion remains in the canonical SQL query before count/paging, and recovery probes without issuing another move.
- **Altitude:** Core still owns authorization/freshness and the read-only recommendation; Infrastructure owns durable coordinates/recovery; Web only confirms or checks status. Retained arrival evidence remains immutable.
- **Applied findings:** database-enforced per-message active claim; reachable same-key uncertain status check; reclassification-aware current source/destination; moved-message findability through existing retained search; exact negative/preservation evidence and corrected reporting.
- **Unapplied findings:** none.

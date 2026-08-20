# Plan — TICK-053 / MAIL-11

## Chosen approach

Extend the existing retained-mail request/result rather than add a search service. Inbox/Sent search remains SQL-paged in `EfRetainedMailboxMessageStore`; attachment-content matching consumes one normalized child projection stored atomically with the intake receipt from the canonical `IIntakeSourceReader` output. No attachment is parsed twice and no second store is introduced.

Deleted Items remains a separate, explicitly bounded read boundary: a Core use case authorizes the request, and one Graph adapter enumerates only the well-known Deleted Items folder of the approved mailbox estate, up to a fixed maximum per explicit search, reads MIME through the existing intake reader, returns exact match locations/full read-only result evidence, and reports truncation/unavailability honestly. It persists nothing, follows no history cursor, creates no intake receipt, and performs no Graph write or backfill.

A JSON-only search blob was rejected because SQL matching would hit property names/escaped text and could not disclose exact match locations reliably. A second parser/search database was rejected by the one-owner rule. Persisting Deleted Items as retained Inbox rows was rejected because it would conflate source custody and create a hidden backlog import.

## Diff estimate and proportionality

Expected implementation: approximately 15–20 existing files plus one focused migration/designer and model snapshot, roughly 700–1,000 production/test lines. Most files receive narrow contract, mapping, query, route-state or test changes; no new project, runtime, top-level directory, generic action framework or repository Markdown file. Six implementation steps are proportional to the cross-layer schema/query/external-read slice.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: implement individual-message body/attachment-name/attachment-content search, visible match locations, unsupported/unsearchable disclosure, explicit mailbox/folder scope, accessible pagination, preserved context, bounded read-only Deleted Items access, and no reconstruction or mutation. The FRD is not modified.
- `docs/design/README.md`: reuse current tabs, fields, table, status, focus, empty/error, constrained-desktop and 200%-zoom conventions. This ticket adds no quick preview or action UI.
- EPIC-006 context: Web and later Automation consume the same Core search contracts; Outlook access is read-only and exact-scope. No ADR is needed because the existing Core port, Infrastructure adapter and Web composition boundaries carry both the persisted projection and external read.

## Ordered implementation

1. **Core request/result and canonical projection.** Extend `MailWorkspaceScope` with a validated optional search term and add typed body/attachment-name/attachment-content match disclosure. Add one `IntakeSearchDocument` list to `IntakeReceiptDraft`; in `ProcessIntake`, derive it only from `IntakeSourceReadResult.Content` plus the reader-produced attachment assets/source labels. Root content becomes message body; each retained attachment gets one combined searchable/unsearchable document. Preserve existing constructors and MCP list behavior when search is absent.
2. **Atomic persistence and SQL search.** Add one receipt-owned search-document entity/table and migration. Store/replace it in the existing `EfIntakeReceiptStore` transaction. Extend `EfRetainedMailboxMessageStore.ListAsync` so mailbox/folder/body/filename/content filtering happens before SQL count/paging, then project exact match kinds/named attachments and detail attachment searchability. Add no full-text engine, JSON search, separate repository or historical backfill.
3. **Bounded Deleted Items read source.** Add a narrow Core port/use case returning a paged, full read-only deleted-message result and explicit unavailable/truncated state. Extend `GraphMailClient` with a GET-only, host/path-validated well-known `deleteditems` listing and MIME reads; implement the source over `IApprovedIntakeMailboxes` and the existing `IIntakeSourceReader`, with a fixed scan bound and no cursor persistence. Add a no-source implementation for local/unconfigured hosts and compose the production Web adapter without adding write permissions.
4. **Real Web caller and context preservation.** Extend `/Inbox` query-string search, visible scope, match-location labels, accessible pagination, refresh fields and honest empty/unavailable/truncated states. For bounded Deleted Items results, render the exact full body and attachment/searchability evidence read-only in the result disclosure; add no mutation or “View in Outlook”. Preserve search through mailbox/folder changes and `/Inbox/{id}` detail/back links for retained results.
5. **Focused evidence and documentation.** Add Core validation/projection tests, receipt/persistence migration tests, SQL scope/count/page/match tests, fake-HTTP Graph boundary tests, and authenticated Web tests. Update `docs/capabilities.md` and `docs/current-architecture.md` only with the exact local caller/evidence and bounded Deleted Items qualification; do not claim deployment or live-mailbox proof.
6. **Locked verification and simplification.** Run locked restore, Release build, focused Core and integration suites, then the full relevant tests. Review the branch diff independently through reuse, simplification, efficiency and altitude lenses; apply behavior-preserving fixes and append dated findings/dispositions here. Write the post-implementation report with exact commands, evidence and residual live-verification qualification.

## Dependencies and sequencing

MAIL-01/02/03/04 and the retained browse/detail caller are already delivered. TICK-064 remains separate folder-policy work and has no source dependency in this reduced diff. This ticket blocks [[TICK-056]] and overlaps [[TICK-057]]; they must consume the final merged request/result and file shapes. Action/association tickets touching message detail must rebase after this work rather than race it.

## Proof

The post-implementation report will cite the migration/model validation, focused Core/persistence/Graph/Web results, Release build, relevant full suite and recorded four-lens simplification dispositions. Production verification is the previously approved authenticated read-only journey only; local fake-HTTP tests do not prove tenant access or deployed Deleted Items data.

## Risks and mitigations

- **Search drift or duplicate parsing:** project only the canonical reader result inside `ProcessIntake`; one receipt-owned representation.
- **False paging/counts:** apply every retained filter in SQL before count/skip/take.
- **Unbounded mailbox reads:** Deleted Items runs only for an explicit nonblank search, exact approved mailboxes, fixed maximum items and visible truncation.
- **Identity escape:** validate Graph host, mailbox, well-known folder path, next links and returned parent folder; never accept a client folder identity.
- **Existing caller breakage:** keep absent-search defaults and prove Web/MCP browse/detail callers.
- **Scope inflation:** no queue policy, quick preview, Case actions, mailbox mutation, generic search framework, deployment or backfill.

## Simplification pass — 2026-08-20

- **Reuse:** Reused the existing `ListRetainedMail` query port, receipt transaction, `IIntakeSourceReader`, `GraphMailClient`, approved-mailbox estate, and Mail page/filterbar conventions. No second parser, search repository, mailbox client, or duplicated scope taxonomy was added.
- **Simplification:** Removed the temporary partial-class split by moving the pure projection into `IntakeSearchProjection.cs`; reused the existing `filterbar` CSS instead of introducing a new UI convention; removed two unnecessary search-document eager loads from ordinary receipt reads. Applied.
- **Efficiency:** Retained filters execute in SQL before count/paging, match evidence is loaded in bounded page-sized batches, projection rows are written in the existing receipt transaction, and Deleted Items stops after 100 newest messages without persistence/backfill. The remaining page-sized in-memory match grouping is bounded to 25 rows and was kept rather than adding another abstraction.
- **Altitude:** The change stays inside existing Core policy/port, Infrastructure persistence/Graph adapter, and Web composition boundaries. No project, runtime, top-level directory, generic search framework, feature flag, or ADR was introduced. No further findings.

## Remaining-blocker simplification pass — 2026-08-20

- **Reuse:** Reused the existing display-reader attachment enumeration, retained SQL predicate, shared operator label owner, authenticated Web integration host, Deleted source port, and pager. No second parser, projection, store, mailbox list, or backfill was added.
- **Simplification:** Materialized the display attachment list once; preserved nameless occurrences with a deterministic label; aligned retained admission to the already-visible retained body or named attachment content; one caller fake covers every remaining Deleted state. Applied.
- **Efficiency:** The new attachment qualifier stays inside SQL before count/paging; display parsing no longer enumerates attachments twice; Deleted source behavior is unchanged and remains bounded at 100.
- **Altitude:** Parsing/persistence/presentation/test responsibilities remain in their existing layers. No framework, flag, migration, permission, deployment, or external write was added.

## Final review-blocker simplification pass — 2026-08-20

- **Reuse:** The final fixes reuse the canonical reader descriptor list, one receipt root projection, route decision, `StaffForwardBodyCleaner`, existing retained match mapper/detail query, outside-view status, Graph client, resolved folder identity, and Deleted unavailable state.
- **Simplification:** Raw retained body was removed from search ownership rather than adding a normalized column/backfill; attached text uses the unsupported descriptor path; one optional term extends detail; common GET/reload scope and MIME response helpers remove duplicated conditions/transport code.
- **Efficiency:** Root/attachment filtering remains SQL-first before count/paging; one detail row gets bounded match evidence; Deleted global 100-message/MIME bounds are unchanged; authentication failure stops before HTTP.
- **Altitude:** Core owns normalization/authorization contracts, Infrastructure owns MIME/persistence/external failure mapping, and Web owns only outside-view/unavailable presentation. No schema, parser, store, retry framework, permission, deployment, or backfill was added.

## Independent-final-blocker simplification pass — 2026-08-20

- **Reuse:** PR-033..036 reuse the existing Deleted-source unavailable catch, Graph response validators, MimeKit attachment disposition, canonical/display occurrence test, Core retained-search validation, Web GET error convention, receipt replacement store, migration stream, bootstrap census, SQL permission reader, and current architecture owner.
- **Simplification:** The pass adds only the missing exception cases, explicit-attachment precedence, one reload catch, and removes one unsupported permission. It adds no retry, validator, attachment identity layer, permission framework, migration, parser, store, or backfill.
- **Efficiency:** Normal Graph and MIME bounds are unchanged; invalid input stops early; attachment processing remains single-pass; SQL runtime behavior is unchanged while the Worker loses an unused verb.
- **Altitude:** Provider failures and MIME classification remain Infrastructure concerns, request-error presentation remains Web, and schema permissions stay in migration/bootstrap/current-state owners. All findings were applied; there are no deferred simplification findings.

## Current-dev reconciliation — 2026-08-20

After PR #474 / [[TICK-047]] landed, `origin/dev` was merged in `eaf2f9f4eac577242ed301dd917f0682d4a77729`. The two conflicts preserved both capabilities: `GetRetainedMail` now applies MAIL-11 search normalization/query context before TICK-047's folder recommendation, and Core tests retain both the Deleted-source fake and approved-mailbox store fake. This reuses both landed owners and adds no bridge abstraction. Release build passed with 0 warnings/errors, the reconciled Core retained-mail class passed 34/34, and the three new exact integration proofs passed 3/3. The final diff remains exactly 31 files against current `origin/dev`.

## PR-037 simplification pass — 2026-08-20

- **Reuse:** Existing Graph parse sites, `InvalidDataException`, exact URI validator, unavailable mapping, HTTP fake, and authenticated Web host carry the fix.
- **Simplification:** Three direct guards replace escaping framework exceptions; the outer catch and all policies remain unchanged. No generic response validator or retry layer.
- **Efficiency:** Malformed folder/page/link responses stop before enumeration or MIME reads; valid bounds and request work are unchanged.
- **Altitude:** Infrastructure validates provider data and Web only proves the existing unavailable presentation. All findings were applied; no deferred simplification work.

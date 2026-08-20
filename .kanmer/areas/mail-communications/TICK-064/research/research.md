# Research — MAIL-23

## Question

How should Pegasus own one canonical mapping from the detailed taxonomy to operational destination and designated Outlook folder recommendation?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: MAIL-02 research proves no authoritative exhaustive mapping exists; classification categories deliberately carry neither queue nor folder, so this should be a separate Core policy consumed by MAIL-05, UI-14 and MCP.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

## Folder hierarchy clarification — 2026-08-19

The prior evidence supports more Outlook destinations than the three application queues. Recovered destination purposes are: instructions, audits, diminution, new clients, case queries, enquiries, billing, pre-instructions, no-action, images, cancellations, case updates and other. These are reference evidence, not yet canonical Pegasus folder identities or exact names.

The plan must model operational queue and Outlook folder separately: a message can appear in the aggregate Other queue while receiving a specific designated folder such as Billing, Case updates or No action. MAIL-23 therefore needs an exhaustive detailed-classification → (operational destination, approved folder identity) table confirmed by the operator.

## Current-state refresh — 2026-08-20

### Question

What remains for MAIL-23 after MAIL-02 and INTK-007 shipped, which current callers and configuration boundaries must it reuse, and where does its implementation overlap the remaining email-workspace tickets?

### Verified findings

- Repository snapshots inspected: `origin/dev` `b36c66662288adb0727299276f675337442a1e22` and its ancestor `origin/main` `2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`. The local `dev` checkout is stale and was not treated as source truth.
- MAIL-02 already owns operational routing in `MailOperationalDestinationPolicy`. It preserves detailed classifications, uses reasoned `Other` only for novel categories, and fails ambiguous/unclassified decisions closed to `Unidentified`. Creating another operational-queue policy in MAIL-23 would violate the one-Core-owner rule.
- The real staff caller exists on both snapshots: `Pages/Mail/Message.cshtml.cs` derives the destination live from the persisted classification. `origin/dev` also has the TICK-062 Automation caller in `MailMcpTools.cs`; it invokes the same Core policy. The destination is deliberately derived rather than persisted.
- INTK-007 is done and deployed. `Unidentified` is the binding operator vocabulary for unresolved retained material; Triage, Blocked intake, incomplete Audit evidence and Image Intake remain distinct. FRD-08's catalogue is mostly reconciled, but its `pre-instruction-emails/triage-request` row still says “missing VRM remains Needs sorting under FRD-03”; this is the exact stale cross-document phrase disclosed in INTK-007 proof.
- FRD-08 already defines the exhaustive classification-to-operational-destination and classification-to-logical-folder-type catalogue. The 13 approved logical folder types are Instructions, Audits, Diminution, New clients, Case queries, Enquiries, Billing, Pre-instructions, No action, Images, Cancellations, Case updates and Other. Unidentified has no automatic folder outcome.
- Repository search found no executable logical-folder vocabulary, category-to-folder policy, approved type-to-exact-identity binding, or folder-recommendation contract. `ApprovedMailboxAdministration.cs` and `EfApprovedMailboxStore.cs` currently bind only mailbox, Inbox and Sent identities; `GraphApprovedMailboxResolver` resolves only the well-known Inbox/Sent folders. The retained-message `FolderIdentity` records where a message was read from and is not a designated-destination catalogue.
- Therefore the missing MAIL-23 behavior is not retained-message persistence. It is (1) one Core logical-folder outcome derived from the canonical classification, including explicit no-recommendation outcomes, and (2) mailbox-scoped administrator-approved bindings from logical folder type to exact Outlook folder identity. Infrastructure may resolve exact identities, but it must not own or copy the classification table.
- Correction/reclassification must cause later recommendation to re-derive from the current dossier. No per-message folder recommendation should be persisted in this ticket; that would duplicate classification state and correction history.
- MAIL-05 is the first message-level consumer: it combines MAIL-23's logical outcome and approved mailbox binding into a recommendation. MAIL-07 executes only a separately confirmed recommendation. AUTO-003 exposes those landed Core use cases later and must not call this policy as a generic arbitrary-folder tool.

### Implications

- Keep MAIL-23 as a foundation and land it before MAIL-05. Reuse `MailCategory`, `MailClassificationResult`, `MailOperationalDestinationPolicy`, approved-mailbox administration/version/history conventions, and the existing Graph identity-validation conventions.
- Do not modify `EfRetainedMailboxMessageStore` or the mailbox detail page merely to prove MAIL-23. Those belong to concrete consumers such as MAIL-05/UI-10; MAIL-23 is proven by exhaustive Core mapping tests plus mailbox-binding persistence/resolution tests.
- Do not add 13 nullable columns or a second list of display strings. Use one typed logical-folder vocabulary and one mailbox-scoped collection/table keyed by folder type; this is justified by the external Outlook identity boundary and the proven staff/Automation consumers.
- The plan must preserve a distinction between a classification's explicit folder outcome and its application destination. In particular, Triage must not be inferred from folder type, and an Unidentified outcome must remain no-recommendation.
- A read-only production resolution check may verify configured identities after deployment, but local implementation must neither create/rename folders nor move mail.

### Dependency and overlap evidence

- Hard prerequisites already satisfied: [[TICK-044]]/MAIL-02 and [[INTK-007]].
- Direct downstream dependency: [[TICK-047]]/MAIL-05. It should be replanned after MAIL-23 lands because its current file map assumes `MailClassificationContracts.cs` and `EfApprovedMailboxStore.cs` without the missing folder contracts.
- Shared external boundary with [[TICK-049]]/MAIL-07: exact Graph folder identity and composition. Serialize changes that touch the Graph adapter or DI registration; MAIL-07 must consume the approved identity and must not introduce its own folder catalogue.
- UI-14 ([[TICK-057]]) depends on MAIL-02's operational mapping, not on MAIL-23's folder binding. It can proceed independently once its Unidentified terminology research is refreshed, except for shared governing-document edits.
- MAIL-11/UI-10/MAIL-08 ([[TICK-053]], [[TICK-056]], [[TICK-050]]) overlap the retained-mail contracts/store/pages. MAIL-23 should avoid those files, allowing those tickets to proceed without a source conflict.
- [[AUTO-003]] overlaps only after MAIL-05/07 land. MAIL-23 should add no MCP tool; the later ticket extends `MailMcpTools.cs` through the landed recommendation/move use cases.

### Open questions

None. The operator-confirmed catalogue and production read-only verification decision remain binding. Planning must resolve exact symbols after prerequisite branch refresh, but no product choice remains.

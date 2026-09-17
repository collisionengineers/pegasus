# Pegasus audit-reference change: review and implementation addendum

Review date: 16 September 2026.

## Evidence and scope

This addendum reviews the supplied `investigation-1.md` and `investigation-2.md`; it does not replace their evidence inventories. Recommendations below are proposed decisions, not previously approved business requirements.

GitHub baselines verified during this review:

- `main`: `8b9d358f71ac02363a3e9fa50549d4fef0fcdac8`.
- `dev`: `9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396`.
- The compared changes between these refs do not modify the identity implementations discussed below. Dev contains additional planning and architecture documentation, including ADR-0051.

The Pegasus MCP reported a clean local checkout at `32f8679d3695e0dcab8f310a1c20f8b129d20190`, different from GitHub main. A small working-tree source read succeeded; commit-pinned reads were rejected by its command catalog and a larger multi-file read exceeded its response budget. Current implementation findings therefore use the pinned GitHub revision above.

This was a read-only source review. No repository files, production data, or Box contents were changed. No builds or tests were run. Source-derived failure conditions are not claims that those failures were reproduced in production, or that affected historical rows exist.

## Recommended scope contract

Adopt the following explicitly before implementation:

| Concern | Proposed rule |
|---|---|
| New standalone Audit | Primary `Cases.Reference` is `a.{allocated base reference}`, for either assessment. |
| New linked Audit | Primary `Cases.Reference` is `a.{inspection base reference}`. The inspection reference and shared sequence remain unchanged. |
| Existing identities | Preserve exact committed references, including `ap.` references and historical secondary identities. |
| Later assessment changes | Never renumber an existing case. |
| Allocation evidence and permissions | Preserve existing business gates. The prefix change does not itself authorize allocation before evidence exists. |
| Canonical identity | `Reference` is the externally emitted Case/PO. `AuditReference` remains a compatibility field whose population differs by record shape; it is not a competing primary identity. |
| Previously unallocated work | Work first allocated under the new policy receives `a.`, even if the email or instruction predates deployment. |
| Already allocated work and exact retries | Continue using the committed identity, including when custody or delivery completes after deployment. |
| Reference interpretation | Never infer total loss from a prefix. Never globally equate `X`, `a.X`, and `ap.X`. |

A retrospective rename, allocation-before-evidence change, or removal of the secondary identity field is a separate change with separate acceptance criteria.

## Findings requiring explicit implementation work

### AR-01 — Public Create Audit replay is blocked before persistence replay

**Verified source:** `src/Pegasus.Core/Lifecycle/CreateAuditCase.cs`, `CreateAuditCase.ExecuteAsync`; `src/Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs`, `CreateAsync` and `Fingerprint`.

The public use case checks for an existing linked Audit and throws `AuditCaseAlreadyExists` before calling the store. Consequently, repeating the same successful request cannot normally reach the store's replay branch. This is distinct from intentionally refusing a second creation with a different operation key.

The store fingerprint includes the derived audit reference and assessment. Recalculating a historical total-loss operation under the new rule changes `ap.X` to `a.X` and can fail its fingerprint check. The replay result also takes its assessment from the incoming reconstructed command rather than a retained creation result.

**Required outcome:** An authorized exact retry returns the originally committed case IDs, references, and creation assessment without recreating history, custody work, or downstream submissions.

**Recommended design:** Authenticate and authorize first. Normalize and validate request identity. Look for the operation's committed result before evaluating preconditions that only apply to a new mutation. Distinguish an exact retry from a new operation, changed actor, changed source case, or changed expected version. Compare legacy operations using their retained historical material; do not recompute legacy output under today's formatter. Preserve the old fingerprint and history rather than overwriting them. For new operations, use a documented fingerprint version based on stable request intent and persist the derived result once.

The store already records `AuditCaseId`, `AuditReference`, and `Assessment` in action-history result JSON under `audit-case-v1`. Reuse existing retained information where adequate rather than automatically adding a new operation database or general-purpose framework. Inspect the workflow-event/action-history linkage before choosing the exact retrieval implementation.

### AR-02 — Persisted assessment encoding is inconsistent

**Verified source:** `EfCreateAuditCaseStore` writes `command.Assessment.ToString()`, yielding `TotalLoss` or `Repairable`. `EfLinkedCaseReplacementStore.ParseAssessment` accepts only `total_loss` and `repairable`. Standalone acceptance uses an explicit code conversion.

**Consequence:** A linked Audit can be created successfully but fail wrong-principal replacement when that replacement tries to parse its assessment. Fixing only the primary prefix does not resolve this.

**Recommended design:** Introduce or reuse one persistence codec: write one canonical representation and explicitly read both historically emitted forms. Reject unknown values. Do not infer the assessment from the reference or default unknown data to Repairable. Reader compatibility can avoid an immediate bulk data update. Inventory existing encodings before deciding whether a separate normalization migration has value.

### AR-03 — The old and new workflows can collide on a unique secondary reference

**Verified source:** `EfRecordEngineerFinding` writes `AuditReference` on the Inspection + Audit parent. `EfCreateAuditCaseStore` writes the derived value to both `Reference` and `AuditReference` on the new child. `PegasusDbContext` has a unique index on `AuditReference`.

**Reproduction condition from source:** An eligible parent has already acquired `a.X` through Engineer Finding but has no linked child. New linked creation does not see an existing child, then attempts to insert the same secondary reference on a different row. The inverse sequence can also collide. A historical parent holding `ap.X` followed by a new child holding `a.X` may avoid this particular index collision while leaving two competing audit identities.

**Recommended transition:** One mechanism owns new Audit identity creation. Stop the legacy endpoint from minting new same-record identities while preserving historical findings, reads, and valid exact replays. Decide separately whether recording an engineer finding remains useful without allocating identity. For parents already carrying legacy secondary identities, either specify an explicit conversion procedure or refuse new child creation with a meaningful resolution message. Do not silently clear historical identities or drop the unique index.

### AR-04 — Replacement needs separate standalone and linked-case contracts

**Verified source:** `EfLinkedCaseReplacementStore` places the unprefixed newly allocated value in `Reference`, puts the audit prefix only in `AuditReference`, and does not set `AuditOfCaseId` on the replacement. It creates replacement workflow links using `OriginalCaseId` and `ReplacementCaseId`, which are not the Audit-parent relationship. `CaseMatchIndexProjector` excludes cases from automatic intake matching when `AuditOfCaseId` is non-null.

**Required correction:** A new standalone Audit replacement must have the canonical `a.` value in its primary reference while leaving its historical predecessor unchanged.

**Additional decision:** Define whether and how a linked Audit, or an inspection already possessing a linked Audit, can be corrected to a different principal. The generic replacement path does not preserve the Audit-parent relationship. Once the encoding failure is repaired, this can affect navigation, custody hierarchy, and automatic match eligibility. Simply copying `AuditOfCaseId` is not a complete fix: the original linked Audit still occupies the unique one-audit-per-parent relationship.

Until that relationship policy is specified, prefer an explicit supported-path restriction to silently producing a detached replacement. The restriction must not block ordinary standalone Audit replacement or ordinary linked Audit creation.

### AR-05 — Reference search needs an identity-preserving contract

**Verified source:** `EfCaseQueryStore.ApplySearchFilters` includes both `Reference` and `AuditReference` in global search, but the dedicated CaseReference filter checks only `Reference`.

Decide how operators find a historical same-record secondary reference through the dedicated reference filter. Keep browsing/search distinct from exact record resolution. A broad search can legitimately return multiple cases; an exact resolver must never select a parent, child, or replacement merely by removing a prefix or taking the first substring match. Do not reinterpret archived `ap.` references as aliases for newly created `a.` cases without a separately recorded mapping.

## Keep the implementation bounded

The formatter need not become a policy engine. For this release, retaining `AuditIdentity.Create(baseReference, assessment)` and its invalid-enum validation is a reasonable minimal-change choice even though both valid assessments now yield `a.`. Removing the assessment parameter is also defensible, but only after equivalent validation is demonstrably retained at every relevant business boundary. It is not necessary for this rollout.

Reject already-prefixed input at the appropriate base-reference boundary rather than silently stripping a prefix. Continue using the existing principal/year/sequence rules; do not introduce a QDOS-only parser, a new sequence, or a reference-format database migration merely for the shorter prefix.

Define the source of displayed outcomes. The current implementation captures an assessment at Audit creation and copies assessment fields into the child. Those fields may subsequently change. An original-report or creation-basis indicator must not be labelled as the current Audit outcome. The canonical reference must remain stable in both cases.

## Additional acceptance tests

These are proposed scenarios and names, not tests already implemented or executed.

| ID | Scenario | Required evidence |
|---|---|---|
| T01 | Standalone repairable and total-loss Audits | Primary reference is `a.`; evidence and assessment remain distinct; normal sequence allocated once. |
| T02 | Linked Audit for each supported outcome | `a.` reference; unchanged parent reference; no new sequence; same Audit parent. |
| T03 | Standalone wrong-principal Audit replacement | Primary and externally emitted reference use `a.`; predecessor identity unchanged. |
| T04 | Linked Audit encoding through replacement | Explicit coverage of `Repairable`, `TotalLoss`, `repairable`, `total_loss`, and an invalid value; obey approved linked-replacement policy. |
| T05 | Legacy Engineer Finding followed by linked creation | Deliberate supported transition or typed refusal, never an accidental uniqueness error. |
| T06 | Linked creation followed by legacy Engineer Finding | No duplicate identity writer and no secondary-reference collision. |
| T07 | Historical parent `ap.X`, then proposed child `a.X` | No unnoticed second audit identity; explicit transition policy. |
| T08 | Exact retry through the real Create Audit use case | Same committed ID/reference/creation assessment; no additional history, custody work, or submission. |
| T09 | Historical v1 operation replay after policy switch | Historical `ap.` result returned; old fingerprint/history preserved. |
| T10 | SQL commit succeeds but response is lost | Retried operation resolves committed result instead of allocating another case. |
| T11 | Same operation key with changed request identity | Conflict or authorization refusal; no disclosure or false successful replay. |
| T12 | Concurrent creation requests | One linked child and one intended external-work set; losers receive controlled conflict/replay behavior. |
| T13 | Exact lookup and browsing of `X`, `a.X`, `ap.X` | Correct record identity; legacy secondary references discoverable under the approved search contract. |
| T14 | Assessment changes after creation | Reference and original creation evidence unchanged; displayed current outcome reflects its correct source. |
| T15 | Pending historical custody/report work crosses deployment | Uses committed reference and stored relationships, not a newly formatted reference. |
| T16 | Linked replacement and parent-with-child correction | Preserves the approved relationship, match-eligibility, history, and custody contract, or refuses explicitly. |
| T17 | Invalid enum/base input; multiple principals; sequence/year boundaries | No silent invalid assessment, prefix stacking, scope regression, or sequence reuse. |
| T18 | New total-loss output through report and EVA paths | Canonical `a.` reference plus correct total-loss data; external acceptance assessed separately. |

Use fixed literal expected references. At least T08–T12 must test the actual application/store boundary and relevant SQL behavior, not only a fake store or helper. For cross-version tests, retain a v1 fixture produced by the old implementation or freeze exact verified v1 material; do not calculate the old expectation using the new formatter.

## Local and CI verification

The inspected workflow currently uses three SQL integration shards and a partition-coverage check. A separate shard-count redesign is not needed for this change. Run the existing Core, Architecture, and non-Corpus SQL lanes plus documentation checks. Verify that newly added tests were actually enumerated and executed; a zero-match filter is not acceptance evidence.

Extend CI path classification only for a demonstrated executable fixture dependency that existing flags omit, not for every documentation or archived-reference change. Run authorized corpus tests when relevant classification/extraction behavior changes; do not use a prefix-only change as a reason to refactor all corpus material.

Perform an authenticated acceptance flow after deployment. Public health checks do not prove that Audit creation, SQL persistence, custody, or external output works.

## Documentation corrections

Use one concise implementation plan with explicit decisions, owners, test IDs, and a cutover procedure; retain both investigations as evidence appendices. Separate files requiring edits from consumers requiring regression coverage and immutable historical material.

Update FRD-01 and active capability requirements first. Dev's ADR-0051, accepted on 16 September 2026, now describes separate linked Audits and still specifies the outcome-dependent prefix. Amend or supersede its numbering clause explicitly without undoing its two-record, shared-sequence, nested-custody decisions. Update ADR-0002 consistently.

Do not classify every `v27_planning` file as frozen merely because its parent directory contains `planning-and-old-designs`. Its README describes a current temporary review artifact for the v27 round, not design authority or implementation evidence. Preserve captures of previous/current behavior; update proposed behavior notes where needed to prevent reintroducing the superseded rule.

Replace machine-specific `/home/...` links in the implementation plan with repository-relative paths and the reviewed commit. Do not globally ban `ap.` from source material: historical fixtures, backward readers, and real evidence must retain it intentionally.

## Cutover and rollback

Before enabling the new writer, obtain an authorized read-only inventory of actual identity shapes, stored assessment representations, legacy parents with secondary Audit identities, existing linked relationships/replacements, and pending operations. This review establishes source risks, not the prevalence of affected data.

Deploy compatibility fixes before or together with the numbering switch. Ensure all allocation-capable Web/API/Worker processes use the intended rule, and account for in-flight operations. Define the effective boundary by committed identity/policy, not solely by email date or wall-clock time. Azure Container Apps can run multiple revisions concurrently; an ingress traffic switch alone is not proof that every allocation-capable process has stopped using the old writer.

Rollback must not rewrite identities already committed under the new rule. A pre-change binary may resume issuing `ap.` references. Prefer a compatible rollback build retaining the `a.` writer, or suspend allocation until the supported version is restored. Log the active policy/build and distinguish new allocations from legitimate legacy replays when looking for unexpected `ap.` output.

## Source register

All repository files below were read at the pinned dev revision above unless a search result explicitly named the equivalent main revision:

- `src/Pegasus.Core/Cases/CaseContracts.cs`
- `src/Pegasus.Core/Cases/CreateLinkedReplacement.cs`
- `src/Pegasus.Core/Lifecycle/CreateAuditCase.cs`
- `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfRecordEngineerFinding.cs`
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`
- `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`
- `src/Pegasus.Infrastructure/Persistence/CaseMatchEntities.cs`
- `tests/Pegasus.Core.Tests/Lifecycle/CreateAuditCaseTests.cs`
- `.github/workflows/ci.yml`
- `docs/adr/0051-linked-audit-case-identity-and-custody.md`
- `design/planning-and-old-designs/v27_planning/README.md`

External primary documentation consulted for retry and deployment design:

- Microsoft Learn, EF Core Connection Resiliency: `https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency`
- Microsoft Learn, Azure Container Apps revisions: `https://learn.microsoft.com/en-us/azure/container-apps/revisions`

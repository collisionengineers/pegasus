# Plan — TICK-208: Preserve final Sent evidence through post-report correction

## Approach

Implement TICK-208 as a separate, sequenced follow-on after [[SIMPLI-014]] and [[DOCS-001]], not as part of the renderer-adapter migration. SIMPLI-014 supplies the Core render port/Infrastructure adapter; DOCS-001 supplies the durable immutable report reference/version/hash aggregate and correction/addendum lineage. TICK-208 then binds each issued report version to its own exact final approved-mailbox Sent evidence through the existing Core workflow and retained-source infrastructure. This avoids inventing a parallel report-version model before DOCS-001 lands and avoids overlapping SIMPLI-014's active branch.

The current one-slot `CaseWorkflowRecord.ReportApproval/ReportSentEvidence` projection is insufficient: correction currently risks replacing the current pointer, and unlink clears the evidence row's Case/link metadata. The chosen change keeps a current-version convenience projection if useful, but makes the durable authority append-only issued-version/evidence association and reasoned association history. Correction creates a new unsent version; it never unlinks, repoints, or inherits the prior version's Sent evidence. Incorrect association correction appends history and may change the active association without erasing the earlier fact. CASE-23 states/transitions remain untouched.

Before execution, re-read merged DOCS-001. If it already implements every version-specific Sent association acceptance condition below, TICK-208 becomes no-code acceptance/traceability; otherwise it takes its own branch/worktree from then-current `origin/dev` and implements only the remaining Sent-evidence slice. [[DOCS-001]] is now recorded as blocking TICK-208; SIMPLI-014 blocks DOCS-001 transitively.

## Governing docs

- **Meets — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.** Each issued artifact/version/hash remains immutable; correction/addendum creates a reasoned successor and retains all earlier artifacts/facts/actors/times/sources; exact Sent evidence remains final; Outlook `sentDateTime` remains business time; generation/upload/assertion does not prove sending. The plan implements these existing requirements without changing their meaning.
- **Also conforms to FRD-08 and FRD-01.** Reuse the exact approved-mailbox evidence identity, hashes, timestamps, proof limits, chronology, and permanent reasoned association history. Preserve reasoned reopen requirements, but do not invent a new lifecycle transition or closure rule.
- **CASE-23 boundary.** [[TICK-055]] still owns post-report query/dispute states, due/chaser, response, correction/reopen interaction, completion, and closure. This plan adds durable version/evidence identity only and leaves those transitions unchanged.
- **No new ADR.** This extends the existing Core workflow/persistence model inside the accepted monolith; it creates no new project, store, runtime, deployment unit, or architectural boundary.

## Steps

1. **Reconcile against prerequisites on merged `dev`.** After SIMPLI-014 and DOCS-001 merge, re-read their exact diffs, plans, proof, Core report-version contracts, persistence entities/migrations, and caller behavior. Name the existing immutable report version ID/hash/lineage types and stores to reuse. If DOCS-001 already satisfies all TICK-208 acceptance, stop repository implementation and close this ticket from evidence; otherwise narrow the file map/plan to the missing association slice before taking it.
2. **Add the smallest Core version-specific Sent association contract.** Extend the existing workflow/report-version contracts so approval and link/reassociate requests name one immutable report version, and projections expose ordered issued-version history with each version's artifact identity/hash, approval, predecessor/reason, and zero/one current exact Sent evidence plus permanent association history. Reuse DOCS-001 identity types and existing approved-mailbox evidence types; do not create a second report aggregate or CASE-23 state model.
3. **Persist append-only version/evidence relationships and migrate conservatively.** Add the minimal entity/configuration/migration joining the DOCS-001 report version, approval, and exact Sent evidence with uniqueness, chronology, concurrency, and non-destructive history. Preserve all existing approvals/evidence and current Case association. Never fabricate an artifact/version match for legacy rows: retain them with explicit legacy/unresolved provenance until a reasoned authoritative reconciliation can name the version.
4. **Make manual and automatic linking version-aware and fail closed.** Update existing link/reassociate/auto-link operations to require an existing approved version, reject evidence before its approval, reject duplicates/mismatched Case/version/hash, and preserve idempotent replay. Auto-link only when the immutable artifact/version match is authoritative; otherwise retain the evidence as unlinked for staff review. Reassociation appends actor/time/reason/before/after history and never clears the source item or former association record.
5. **Preserve correction semantics without changing lifecycle.** Ensure a corrected/addendum report version starts with no Sent status, its predecessor retains its original final Sent item/time, and a new exact Sent item binds only to the successor. Keep current ReportPreparation/PostReport/reopen checks as they are unless the version identity mechanically requires a guard; do not introduce query/dispute, due/chaser, response, completion, or closure behavior.
6. **Expose version history through existing projections and staff mutation surfaces.** Adapt existing Case/detail/report-evidence queries and commands to show/select the exact report version and its Sent evidence while keeping the current-version view convenient. Display final evidence with its proof limits and make legacy/unresolved association explicit. Add no send operation, Outlook mutation, delivery/read claim, or new external API.
7. **Test migration, policy, persistence, and concurrency.** Extend existing Core/polling/linking and integration tests for: original version+Sent evidence surviving correction; successor initially unsent; second evidence binding only to successor; duplicate/replay/idempotency; stale version conflict; ambiguous or hash-mismatched auto-link remaining unlinked; reasoned reassociation history; legacy migration preserving data without fabricated version identity; and concurrent staff/auto-link yielding one valid association. Keep CASE-23 transitions unchanged.
8. **Verify, simplify, and document the implemented slice.** Run the required reuse/simplification/efficiency/altitude pass over this ticket's own diff; update current-state documentation only to the evidence tier actually implemented and do not claim deployment; write the post-implementation report with exact migration/backfill behavior and any deviations. No mailbox, Graph, Azure, Box, or other cloud write is authorized or needed.

## Verification

The post-implementation report will record:

- the prerequisite merge SHAs and the exact DOCS-001 report-version types/stores reused;
- `dotnet restore ./Pegasus.slnx --locked-mode`;
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`;
- focused Core tests for version-specific approval/link/reassociation policy, chronology, idempotency, mismatch/ambiguity, and unchanged CASE-23 lifecycle;
- focused integration tests for append-only persistence, correction/addendum lineage, per-version Sent finality, migration/backfill, projection/UI commands, concurrency, and retained exact-source evidence;
- `dotnet test ./Pegasus.slnx --configuration Release --no-build`;
- migration/script inspection proving existing rows are preserved, no artifact/version relationship is fabricated, and rollback/recovery follows repository conventions;
- negative checks proving a generated artifact, Box/upload/queue result, staff assertion, prior version's Sent item, or ambiguous auto-match cannot mark a new version sent;
- source/diff checks proving no duplicate report identity aggregate, CASE-23 states, send/mailbox mutation, or external/cloud operation was introduced;
- simplification findings/dispositions and accurate current-state documentation.

Proof is local merged-code evidence unless a later separately authorized deployment occurs. No Outlook/Graph, mailbox, Azure, or Box write is part of this ticket.

## Risks / open questions

- **Prerequisite shape is not yet merged:** planning exact fields before DOCS-001 would duplicate or fight its aggregate. Mitigation: hard dependency and mandatory step-1 re-research; reuse its identifiers and stores or close no-code if already complete.
- **Legacy association ambiguity:** current rows do not identify an immutable report version. Mitigation: preserve them with explicit legacy/unresolved provenance; never infer a match from filename, current pointer, or timing alone.
- **Destructive reassociation:** current unlink clears link metadata. Mitigation: append association events and retain former metadata/source identity; correction is never modeled as unlink.
- **Auto-link false positive:** an email may mention a Case without authoritatively identifying the artifact version. Mitigation: require version/hash authority; otherwise retain unlinked.
- **Scope creep into CASE-23:** correction can tempt lifecycle design. Mitigation: keep current lifecycle guards and leave all query/dispute/due/chaser/closure behavior to [[TICK-055]].
- **Migration/concurrency risk:** unique current pointers and competing staff/Worker links can conflict. Mitigation: explicit uniqueness/concurrency constraints, transactional mutation, deterministic idempotent replay, and race tests.
- **No operator question:** the preservation invariant is settled; CASE-23 remains explicitly parked.

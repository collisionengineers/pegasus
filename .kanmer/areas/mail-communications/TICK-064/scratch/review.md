## Independent review — PR #468 at `b6754dd8` (2026-08-20)

### Changes

- Adds the single Core `MailLogicalFolderPolicy` and the 13-type catalogue defined by FRD-08, with exhaustive settled-taxonomy and abstention tests.
- Extends approved-mailbox administration and persistence with mailbox-scoped exact logical-folder bindings, a child-table migration, Web-only runtime grants, Graph read-only discovery, local resolver data, and an Administrator refresh surface.
- Corrects the stale `Needs sorting` phrase in FRD-08 and records the MAIL-05/MAIL-07 ownership split.
- The post-implementation report matches the 21-file PR after its bootstrap-script addendum; `git diff --check` is clean. At review time the changes/documentation/reference-data/infrastructure/unit CI jobs passed and browser plus three SQL integration shards remained pending.

### Comments

#### Blocking — [[PR-013]]

`EfApprovedMailboxStore.ReplaceFolderBindings` clears an already loaded/tracked navigation and creates new child entities for every returned binding. A normal refresh that retains a logical type can therefore put a deleted tracked entity and an added entity with the same `(ApprovedMailboxId, FolderType)` key in one change tracker, causing save-time identity conflict rather than refreshing. The added relational test proves create/replay/preserve, but it never updates an existing logical type, and the Web test deliberately changes Instructions to Billing, so this normal path is uncovered. Diff/update the tracked children and add relational coverage for retained, removed, and added keys.

#### Blocking — [[PR-014]]

The plan missed a governing UI boundary. PR #468 adds an active `Resolve logical folders` staff-shell control and handler, while `docs/design/README.md` still says alpha has no control, route, or placeholder for mailbox taxonomy mapping or folder recommendation/move. The ticket and `docs/capabilities.md` also describe MAIL-23 as Next / 0.3.0 with activation evidence required. Reconcile the accepted activation/release boundary in the canonical owners or remove/defer the active Web surface; changing only FRD-08 leaves contradictory requirements.

No separate non-blocking comments.

### Repository review questions

1. **Did the plan miss anything implied by the ticket?** Yes. It did not include the required design/capability activation reconciliation for the newly visible administration control.
2. **Did implementation miss anything in the plan?** Yes. The replace-semantics step is not reliable for the ordinary case where a refreshed logical type already exists, and the persistence verification does not exercise it.
3. **Did the simplification pass run with honest dispositions?** Yes. The plan records reuse, simplification, efficiency, and altitude lenses with applied dispositions and no hidden unapplied finding. The two blockers are correctness/governance findings outside a behaviour-preserving simplification disposition; they do not indicate that the recorded pass was dishonest.

### Disposition

Created [[PR-013]] and [[PR-014]] in the PR Review area; both structurally block [[TICK-064]]. I did not patch the implementer's branch.

### Verdict

**Changes requested.** PR #468 must not merge and TICK-064 must remain in Review until both blocking tickets are resolved and the full required CI set is green.

## Independent re-review — PR #468 at `268f94bc` (2026-08-20)

### Changes since the first review

- `EfApprovedMailboxStore.cs`: replaces clear/recreate with a keyed diff over the loaded navigation; retained keys update in place, absent keys are removed, and only genuinely new keys create entities.
- `AdministrationPolicyPersistenceTests.cs`: relationally covers unchanged Billing, changed Instructions, removed Audits, and added Other bindings in one refresh, then verifies preservation through the next update.
- `docs/capabilities.md`: records operator-activated local MAIL-23 policy/binding/admin evidence while retaining separate recommendation, confirmation, move, deployment, live-verification, and live-write gates.
- `docs/design/README.md`: records the same narrow Administrator Mailboxes exception without activating an ordinary mailbox workspace or message action.
- TICK-064's plan, refs, and PIR now name the governing capability/design owners and the activation qualification.

### Comments and disposition

- Original blocking [[PR-013]]: **fixed in PR** by commit `0f4ccd96`; focused relational test passes 2/2.
- Original blocking [[PR-014]]: **fixed in PR** by commit `268f94bc`; documentation links pass across 192 files and Markdown placement passes for `origin/dev..HEAD`.
- No new blocking or non-blocking comment.

### Checks

The complete replacement CI run is green: changes, documentation, reference-data, infrastructure, unit, browser, SQL integration shards 1–3, and SQL integration coverage. `git diff --check` is clean. The refreshed report matches the complete diff through its changes table and delivery/governing addenda. Runtime grants remain Web-only SELECT/INSERT/DELETE with no Worker caller/grant; no retained-message state, Graph mutation, or downstream recommendation/move behavior entered scope.

### Repository review questions

1. **Did the plan miss anything implied by the ticket?** No after the PR-014 governing-doc addendum.
2. **Did implementation miss anything in the plan?** No after PR-013's tracked-key replacement fix and relational coverage.
3. **Did the simplification pass run with honest dispositions?** Yes. The original four lenses and delivery addendum are honest, and both review fixes have their own proportionate four-lens records with no unapplied finding.

### Verdict

**Pass.** Both prior blockers are resolved, evidence is green, and PR #468 may merge to `dev`. TICK-064 then moves one stage to Verifying; proof, main promotion, live verification, and closeout remain out of scope.

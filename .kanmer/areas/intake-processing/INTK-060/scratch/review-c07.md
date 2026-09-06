---
verdict: needs-changes
independent: true
head: b46a07452c41b9636158a50f668274e1e7d17e3f
reviewed_at: 2026-09-06T14:10:00Z
slice: C07 (owner ticket INTK-060, three-owner/three-PR exception — no per-slice PR)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c07
branch: c07-precase
diff_base: ab9f3fcd821b604a162e9448d5dd44e0ad9fcb27
ownership_check: PASS — no A-owned file changed
tests_cited:
  - "1-build.md: dotnet build ./Pegasus.slnx --configuration Release --no-restore — exit 0, PASS, 0 warnings 0 errors"
  - "2-core.md: Core.Tests filtered — exit 0, PASS, 207/207, 165 ms"
  - "3-integration.md: IntegrationTests filtered — exit 1, FAIL, 72 passed / 2 failed of 74"
  - "4-integration-repeat.md: same filter, attempt 2 — exit 1, FAIL, same two failures"
  - "5-architecture.md: ArchitectureTests — exit 0, PASS, 100/100, 7 s"
  - "7-baseline-alloc.md: same two tests on unmodified task/pegasus-v1-intake — exit 1, FAIL 3/4, both C07-head failures reproduce"
integration_failures_disposition: >
  Both failures are A-owned and pre-existing, not C07 regressions.
  ConcurrencyTokenPersistenceTests.FreshLocalDbCaseAcceptanceAndTriageInsertUpdateGenerateTokensAndRejectStaleWrites
  fails at ConcurrencyTokenPersistenceTests.cs:206 (SeedPrerequisitesAsync, duplicate
  IX_Principals_Code 'QDOS'); IntakeAllocationConsumerTests.QualifyingTriageRemainsOneAcrossAllocationFailureAndSourceReplay
  fails at QdosAllocationRecoveryTests.cs:1446. Both files are on the A-owned list and
  both reproduce identically on the unmodified baseline (7-baseline-alloc.md), which
  additionally fails a third A-owned test C07's filter does not run.
findings:
  - id: C07-R-1
    severity: major
    file: src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:634-661
    also: src/Pegasus.Infrastructure/Persistence/Migrations/20260906054658_V1PlatformFoundation.cs:1319-1320
    statement: >
      EfPublicUploadRetentionStore.RecordAsync loads the occurrence tracked and mutates
      CustodyState, DocumentId and DocumentVersionId, then calls SaveChangesAsync — a SQL
      UPDATE on [dbo].[PublicUploadOccurrences]. The A-owned foundation migration grants
      only "SELECT,INSERT ON OBJECT::[dbo].[PublicUploadOccurrences] TO
      [pegasus_web_runtime_role]" and grants the worker role nothing at all on that table,
      in contrast to [dbo].[PublicUploadSessions], which does get SELECT,INSERT,UPDATE.
      Nothing fails today because no DI registration exists, so the port is uncalled; the
      first hand-over after A registers IIncomingArtifactRetentionStore will throw a SQL
      permission error on every disposition record. The report's "DI registrations for A"
      and "Handoffs and dependencies" sections name the registration but not the grant, so
      A has no way to know the wiring is incomplete.
    required_disposition: >
      Add the grant to the A handoff explicitly — GRANT UPDATE ON
      OBJECT::[dbo].[PublicUploadOccurrences] TO [pegasus_web_runtime_role] (and to
      pegasus_worker_runtime_role if the worker retains) — in the C07 report's handoff
      list and as an INTK-060 open question. The migration is A-owned and outside C's file
      scope, so this is a statement, not a code change.
  - id: C07-R-2
    severity: major
    file: src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:650-658
    statement: >
      RecordAsync writes the document version's remote identities on every disposition,
      clearing them to null whenever the state is not Confirmed:
      "version.BoxFileId = confirmed ? artifact.BoxFileId : null; version.BoxVersionId =
      confirmed ? artifact.BoxVersionId : null;". The controller's mid-task correction was
      to write the returned identities for a Confirmed disposition only, and the method's
      own remarks justify not asserting identities for a pending or failed retention —
      neither asks for another record's identities to be erased. If custody returns the
      same logical document version for two occurrences — which is exactly the case
      "equal filenames never overwrite occurrences" contemplates — a Pending or Failed
      record for the second occurrence nulls the first occurrence's confirmed BoxFileId
      and BoxVersionId, and FindAsync then reads that retention back as Confirmed with no
      remote identity. The comment and the code disagree.
    required_disposition: >
      Guard the block instead of the value: enter it only when
      artifact.State == IncomingArtifactCustodyState.Confirmed, and never assign null to
      version.BoxFileId or version.BoxVersionId.
  - id: C07-R-3
    severity: minor
    file: src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs:528-570
    statement: >
      PR 671 disposition row C11 carried two flags. Flag (b) — the inactive check running
      before the no-change check — is fixed and correctly explained in the method's
      summary. Flag (a) — reusing LifecycleVersion as the principal-write concurrency
      token, so a principal edit invalidates a concurrently-open Merge/Close form's
      expectedVersion — asked for a deliberate decision. The implementation reuses the
      single token, which is defensible, but neither the summary nor any ASSUMPTION in
      INTK-060 scratch/c07-notes records the choice.
    required_disposition: >
      Record the decision in one sentence — one lifecycle token per Image Intake; a stale
      Merge/Close form is reloaded — in the method's remarks or as an ASSUMPTION on
      INTK-060.
  - id: C07-R-4
    severity: minor
    file: tests/Pegasus.IntegrationTests/PublicUploadSessionTests.cs:1-182
    statement: >
      The suite has no await, no host and no database — it is pure policy over a fixed
      clock, as its own remarks state — yet it lives in Pegasus.IntegrationTests, so it
      pays LocalDB collection setup and runs inside the 90-second integration suite rather
      than the 165 ms Core suite. Its sibling pure suite,
      tests/Pegasus.Core.Tests/Intake/RetainIncomingArtifactTests.cs, is placed correctly.
      Altitude and efficiency lens. The plan's own runner command does list
      PublicUploadSessionTests under the integration project, so this follows the plan
      rather than contradicting it.
    required_disposition: >
      Accept for this slice. Move it to tests/Pegasus.Core.Tests/Documents/ and move the
      filter to the Core command when the accept path is wired (A04), or state the
      placement as deliberate.
  - id: C07-R-5
    severity: minor
    file: tests/Pegasus.IntegrationTests/TriageReferenceAllocationTests.cs:146-170
    statement: >
      Question 1 — the brief did not carry plan step item 3 ("Formal instruction creates
      the normal Case through the existing allocator, links the Triage/Image Intake and
      retains both pre-case references") or its expected output ("Formal instruction
      yields one Case plus retained T/Image links"). TheReferenceSurvivesEveryLaterMutation
      exercises IAwaitTriageInformation only; no test asserts the new T reference survives
      ILinkTriageCase, and QdosTriageIntegrationTests.cs:438 asserts the link without
      touching Reference. This is a proof gap, not a defect: Reference and Sequence are
      assigned only in EfTriageStore.CreateAsync:132-133 and nowhere else in the store, and
      both columns carry unique indexes (PegasusDbContext.cs:787-788), so immutability
      holds by construction.
    required_disposition: >
      Accept. Either add the Case-link mutation to that test or record plan item 3 as a
      residual for the slice that owns the formal-instruction path.
  - id: C07-R-6
    severity: minor
    file: src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs:674-680
    statement: >
      Efficiency lens. The keyset cursor carries (CreatedAtUtc, Guid Id) while the SQL
      bound is expressed on Sequence, so every continuation page spends one extra
      primary-key query resolving Id to Sequence. ICursorProtector's sort key is an opaque
      string, so CursorPaging.EncodeUtcTimestamp could pack the sequence and remove the
      read entirely. The cost and the reason are documented honestly in the method's
      remarks (EF's translation of Guid.CompareTo was unverifiable without running tests,
      and SQL uniqueidentifier ordering is not Guid.CompareTo ordering), and the extra read
      is one indexed seek that also correctly rejects a cursor naming an unlisted Triage.
    required_disposition: >
      Accept as documented. Revisit when the Cases triage tab actually moves off the offset
      list onto IListTriagePage.
  - id: C07-R-7
    severity: nit
    file: tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs:646-697
    statement: >
      The read-count deviation (recorded as ASSUMPTION 4) replaces the disposition's
      "pin that number" with an equal-reads comparison over three and six rows. This is
      the better proof: an absolute count is a fact about one base, which is exactly the
      defect the disposition found in the branch's hard-coded 14, and M6 forbade the
      implementer from measuring one. The observed count travels in the failure message.
      Residual: a constant extra read per request, as opposed to per row, would still
      pass — though C13 and C14 are verified in code as one round trip and one LEFT JOIN
      projection, which is the invariant the plan names.
    required_disposition: Accept as recorded deviation 4. No change.
  - id: C07-R-8
    severity: nit
    file: src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:611-625
    statement: >
      Efficiency lens. FindAsync builds two separate correlated subqueries over
      DocumentVersionEntity for BoxFileId and BoxVersionId against the same row key; one
      subquery projecting both would do. Also EfPublicUploadRetentionStore.ScopeOperationKey
      (:588) has no caller anywhere, production or test, because the port is unwired.
    required_disposition: >
      Accept. Fold the two subqueries when the accept path is wired and the query is
      exercised.
---

# C07 independent review — INTK-060 slice C07 (Image Intake, Triage, PR 671 preservation)

Read-only review at head `b46a07452c41b9636158a50f668274e1e7d17e3f`. I made no edit,
ran no test, pushed nothing, opened and merged no PR, and moved no ticket. Per the
controller override there is no per-slice PR and no execution packet, so no
`gh pr view`, `gh pr merge`, `move_item`, `update_item` or `set_ticket_doc` call was
made. My only Kanmer write is this attestation. I am not the implementer: the slice was
implemented by a separate agent whose report is `c07-report.md`.

## Scope and ownership

`git -C <worktree> rev-parse --show-toplevel` = the worktree; `--git-common-dir` =
`C:/Users/PGUSER/documents/github/pegasus/.git` (the primary repository's); `branch
--show-current` = `c07-precase`. Not `.worktrees/kanmer`, not the primary checkout.

`git diff ab9f3fcd821b604a162e9448d5dd44e0ad9fcb27...b46a07452 --stat` is 28 files,
+3258 / -82. `ab9f3fcd8` is "Merge shared G12 typed Triage actors (c4d09b6e8) into
task/pegasus-v1-intake" and is an ancestor of the head, so the diff is exactly the
C-owned slice beyond the shared foundation and the G merges. First-parent log
`dc3cfd908..b46a07452` shows the expected 14 commits: the WIP carry-over, three G
merges, seven C features, and the two correction rounds `7850a7bd7` and `7000842ed`.

**Ownership: PASS.** `git diff --name-only ab9f3fcd8...b46a07452` filtered for
`PegasusDbContext|Entities|Migrations|DependencyInjection|IntakeWebTestSupport|TriageMcpTools|EfEmailEvidenceStore|ConcurrencyTokenPersistenceTests|SentEvidencePollPersistenceTests|QdosAllocationRecoveryTests|CustodyOutboxIntegrationTests|ArchitectureTests|^docs/|Pages/Cases/`
returns nothing. No A-owned file changed. `IntakeContracts.cs` is untouched, so the
deliberate C01 exclusion of `IntakeEnvelopeLimits` holds.

Four files changed that the report's `files_touched` list omits:
`ImageIntakeContracts.cs`, `ImageIntakeLifecycle.cs`, `EfImageIntakeStore.cs` and
`Pages/ImageIntake/Details.cshtml.cs`. All four are the PR 671 re-application described
at length in report item 3, so this is a list omission, not unscoped work. Not raised as
a finding.

## Question 1 — did the brief miss anything the plan step implies?

Mostly no. Plan items 1, 2, 4, 5 (the Provider API half), 6, 7 and 8 are all in the
brief and all traceable in code. Two gaps:

- **Plan item 3** — the formal-instruction path creating the normal Case, linking the
  Triage/Image Intake and retaining both pre-case references — is not one of the brief's
  seven items, and the slice adds no proof that the new T reference survives a Case link.
  See C07-R-5. Structurally safe, so minor.
- The deliberate exclusions are correct and correctly reasoned: `IntakeEnvelopeLimits`
  and the 100 MiB / 200 MiB+64 KiB / 20-file caps belong to C01's `IntakeContracts.cs`,
  and manual/mailbox custody wiring needs C01's files plus A04. The Provider API half of
  item 5 is delivered against the existing constant without touching it.

## Question 2 — did the implementation miss anything in the brief?

**Every numbered item is delivered.** Verified in code, item by item.

**1. Global T reference.** `TriageReferenceFormat` (`TriageContracts.cs:38-83`) mints
`T-` plus the sequence padded to five digits, `Canonical` is `^T-[0-9]{5,}$` so it
expands past `T-99999`, and `Format` throws `ArgumentOutOfRangeException` at
`sequence <= 0` so a reference can never be built from 0. The allocator order is exactly
as specified:

- the replay probe runs **outside** the transaction on its own context
  (`EfTriageStore.cs:46-54`), holding nothing, so a committed replay returns its
  original reference without reaching the counter;
- `AllocateSequenceAsync` is the **first** statement inside the serializable transaction
  (`:65`), before any read or write of a Triage row;
- it reads `[TriageSequences] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = 1` via
  `FromSqlInterpolated`, guarded by `Database.IsSqlServer()` (`:186-198`);
- the operation key is probed **again** under the counter (`:71-77`), catching a
  creation that committed between the two;
- then the source-identity lookup, receipt and evaluation checks, and the writes.

`checked(++sequence.LastAllocatedSequence)` over a row seeded `Id = 1,
LastAllocatedSequence = 0` (`V1FoundationModelConfiguration.cs:32`) makes the first
sequence 1, and `CK_Triage_Sequence "[Sequence] > 0"` (`PegasusDbContext.cs:773`) plus
`CK_TriageSequences_LastAllocatedSequence "[LastAllocatedSequence] >= 0"` back it.
`Sequence` is never 0. The unique-index backstop is real:
`HasIndex(item => item.Sequence).IsUnique()` and
`HasIndex(item => item.Reference).IsUnique()` (`PegasusDbContext.cs:787-788`). Both
runtime roles hold `SELECT,INSERT,UPDATE` on `TriageSequences`. Never reset or reused:
`Reference` and `Sequence` are assigned only in the `CreateAsync` initializer
(`:132-133`) and in no other statement in the store. Gaps tolerated, and the increment
staying pending until `SaveChangesAsync` means the inner-replay return, the
already-exists return and every validation failure release the number rather than burn
it. Round 2's lock-order inversion is the correct fix and the report's account of why
round 1's reasoning was backwards is honest.

The rename is clean. `TriageSummary.Reference` is the T reference and `ClaimNumber`
carries the provider claim number (`TriageContracts.cs:387-398`), fed by
`EfTriageStore.cs:745` and `:757` from the draft. The one consumer outside C's files is
the B-owned `Pages/Cases/Index.cshtml.cs:594-611`, which labels the value generically as
`("Reference", reference)` — so the rename changes what the row shows without
mislabelling it, and `TriageQueuesWebTests:212-292` pins `T-00001` in the row while
asserting `summary.ClaimNumber` still holds `TRIAGE-032`.

**2. Keyset continuation.** `ListTriagePage` (`TriageQueryUseCases.cs:107-155`) is built
on the shared `CursorPaging`/`ICursorProtector` with no invented codec. The scope is
`CreateScope("triage", query.Actor, query.State?.ToString(), "created_desc,sequence_desc")`
— actor, state and order all bound, so a cursor from another query or another actor
fails to unprotect. `CursorRejectedException` is thrown when the cursor names a Triage
that is no longer listed (`EfTriageStore.cs:679`). The filter, the keyset bound, the
newest-first order and `Take(limit + 1)` are all applied to `TriageEntity` before the
instruction-draft join (`:665-691`), with the join and projection factored into
`ProjectWithDraft` shared with `TriageWithDraftQuery`. Deterministic: the tie-break is
the unique, ordered `Sequence`, not the Guid.

**3. PR 671 re-application.** Every disposition correction is applied, verified hunk by
hunk. C6 fails closed with `Task.FromException(new NotSupportedException(...))`, not a
silent `[]` (`ImageIntakeContracts.cs:249-259`). C11's no-change check runs **before**
the active-principal check (`EfImageIntakeStore.cs:551-563`), with the reasoning stated
in the summary. C13 no longer makes a second round trip: `GetDetailAsync` projects
`PrincipalCode` in the same query (`:895-919`) and `FindForReceiptAsync` uses
`.Include(item => item.Principal)` (`:776`, `:791`). C14's LEFT JOIN projection is in the
one set-based read (`:950-956`). C21's obsolete "Image-initiated Case" wording is gone
from the new handler *and* from the pre-existing `OnPostCloseAsync` message
(`ImageIntake/Details.cshtml.cs:123-126`), and C11's exception message reads "This Image
Intake changed before the principal assignment." T3 rejected as noise, T5's `using`
directives cleaned, S1 not imported, B1 left to B. The remaining "Image-initiated"
strings in the tree are all pre-existing text in files outside this slice.

**4. Public upload session.** `PublicUploadSessionPolicy`
(`RequestUploadPolicy.cs:390-479`) satisfies every named invariant: only
`CountsTowardsTheSession` (Confirmed) can `Start`, so failed attempts leave the window
shut; `Start` returns the session unchanged when `HasStarted`, so a later success never
extends it; `Window` is a single `TimeSpan.FromMinutes(15)` and `ExpiresAtUtc` is fixed
at start, so it is not sliding; `Finalize` returns the same session on replay;
`AcceptsBytes` admits only `NotStarted` and `Open`, so finalized and expired refuse.
`RequestUploadDecision.LimitsVersionMismatch` with `MayReissue: true` replaces the
`InvalidOperationException` (`:545-552`) and `Uploads/Request.cshtml.cs:127-134` renders
"This link is no longer valid. Ask for a new one." — no Case disclosure.

The accept path is still the old synchronous write, and this is **acceptable and
stated**: report item 4 and the open risk both say so, and the reason is sound —
`PublicUploadSessionPolicy`, `PublicUploadSession` and `PublicUploadOccurrence` have
zero production references outside their declaring file, and routing the accept path
through `RetainIncomingArtifact` now would replace the durability contract pinned by
the out-of-scope `DocumentCustodyDurabilityTests` with an unimplemented port. The
15-minute boundary is proved directly over a fixed clock rather than through a host,
which is the right call while unwired (but see C07-R-4 on where that suite lives).

**5. `RetainIncomingArtifact`.** Every invariant holds
(`src/Pegasus.Core/Intake/RetainIncomingArtifact.cs`): only `Confirmed` is success
(`IsConfirmed`, `:69`); a confirmed replay returns the same document and version without
re-offering the bytes (`:140-143`); `Unknown` is reconciled through
`ICaseArtifactCustodyStatus` under the same operation key and never resubmitted
(`:146-149`, `:176-208`), and stays `Unknown` honestly when the port or the identities
are missing; remote identities are carried only for `Confirmed` (`:226-227`); the
occurrence identity is server-issued so two arrivals with the same proposed name are two
occurrences (`:37-45`, `:253-258`); `ParseCustodyState` throws on an unrecognised stored
state rather than reading it as success (`EfDocumentRequestStore.cs:672-681`).
The store-level identity write is where C07-R-1 and C07-R-2 live.

**6. Notes and assignment.** `AddTriageNote` probes the operation key first
(`TriageLifecycle.cs:50-61`), the entry carries state, assignee and case link forward
unchanged while taking the next version (`AddNoteAsync`, `EfTriageStore.cs:354-369`,
mutator `static _ => { }`), and there is no second note store. The bound is 500 from one
constant — `TriageNotes.MaximumLength = TriageReasonLength = 500`
(`TriageContracts.cs:186-194`) matching `TriageHistory.Reason` `HasMaxLength(500)`
(`PegasusDbContext.cs:909`) — and the view's `maxlength="@TriageNotes.MaximumLength"`
(`Triage/Details.cshtml:440`) derives from it, so the form cannot offer more than the
entry accepts. The report's account of catching its own 2000-character defect is honest
and the correction is right. `ICaseEngineerChoices` is a **required** constructor
dependency (`Triage/Details.cshtml.cs:33`), registered by G10 at
`DependencyInjection.cs:174`, and the dead optional-injection gate is gone. "Assign to
me" is removed: the only occurrence of the phrase anywhere under `src/Pegasus.Web` is
the explanatory comment at `Details.cshtml.cs:146`, the `triage-assign-dialog` markup is
fully deleted with no orphan, an unnamed engineer is refused with a field error
(`:147-152`), and `QdosTriageIntegrationTests.cs:489-491` now asserts
`DoesNotContain("Assign to me")`.

**7. Provider API.** `QdosBoundaryContractTests.TheProviderApiEnvelopeBoundsEveryFileItCarries`
asserts `30 * 1024 * 1024` against `IntakeEnvelopeLimits.MaximumProviderApiEnvelopeLength`,
proves the per-file bound can never exceed the envelope, and drives
`ProviderSubmissionPolicy.RequireEnvelope` to a real `EnvelopeExceeded` refusal with a
fixture it first proves is over the envelope. `IntakeContracts.cs` unchanged.

**G12 typed actors.** Carried through correctly. `AddTriageNoteRequest.Actor` is
`ActionActor` (`TriageContracts.cs:169-174`). Kind and subject are in **every** hash —
assign `:248`, note `:372`, the generic mutation `:922`, findings `:1123`, state changes
`:1131` — and `ActorKind` is written on history at `:1067` and `:1191`. No fake default
kind: `RequireActor` (`TriageLifecycle.cs:601-618`) admits `SystemWorker` only when
`allowSystemWorker` is passed, and the only call site that passes it is creation
(`:338`); every other mutation including `ValidateNote` refuses it. The G12 merge
resolutions the report describes match what is in the tree, including the
`chosenEngineer` + typed `actionActor` combination on the assign call.

**Unwired, and whether the report states it.** `IAddTriageNote`, `IListTriagePage`,
`IIncomingArtifactRetentionStore` (implemented by `EfPublicUploadRetentionStore` but
unregistered), `RetainIncomingArtifact`, the A04 `ICaseArtifactCustody` adapter, the
whole `PublicUploadSession*` policy, and the two Test-UI snapshots — all confirmed
unwired by grep across `src/`, and **all stated** in the report's "DI registrations for
A", "Handoffs and dependencies" and "Open risks". The composition gate on the note form
is a closed gate rather than a half-shipped feature. The one thing not stated is the
missing UPDATE grant (C07-R-1).

**Deviations.** All four are recorded as ASSUMPTION 1-4 in INTK-060
`scratch/c07-notes` with reasons and alternatives. Deviation 3 (typed actors) is
genuinely closed by the G12 merge, as the report says. Deviation 1 (nullable
`TriageSummary.Reference`) is correct restraint under M5. Deviation 4 is C07-R-7.

**Hygiene.** No fabricated domain data — the fixtures are `ALPHA`/`BETA`/`QDOS`,
`TRIAGE-032`, a tiny PNG and `new byte[length]`. No weakened assertions: the two
modified existing tests are both **tightened**, `"Permanent history"` becoming
`">Notes</h2>"` plus a new `DoesNotContain("Assign to me")`, and the round-1 note-bound
correction narrowed to `ArgumentOutOfRangeException` exactly rather than loosening to
`ThrowsAny`. No `Guid.NewGuid` in any changed page. Copy is labels-and-values only via
`OperatorLabels.Principal`, `PrincipalNotKnown` and `TriageReference`, consolidated to
one pair per AGENTS.md "One list per concept". Concurrency results surfaced —
`DbUpdateConcurrencyException` renders a reload message on both post handlers. Errors
not suppressed: unimplemented members fail closed with `NotSupportedException` and
unknown stored states throw.

## Question 3 — simplification pass with honest dispositions

**The report contains no simplification pass.** There is no such section and no
disposition table; `grep -i "simplif|altitude"` over `c07-report.md` returns nothing.
I applied the four lenses myself.

- **Reuse — strong.** `CursorPaging`/`ICursorProtector` reused with no invented token
  codec; `EfOrganizationAdministration.ToPrincipal` reused for the options mapping;
  `ProjectWithDraft` extracted so one join expression serves both Triage read paths
  rather than two; the `UPDLOCK, HOLDLOCK` house idiom taken from
  `EfIntakeReceiptStore.cs:388`; `TriageQueuesWebTests`'s receipt and evaluation
  fixtures widened to `internal` and reused by `TriageReferenceAllocationTests` instead
  of copied; one label pair for a concept that now has two surfaces.
- **Simplification — clean.** The replaced assign control removed its dialog entirely
  rather than leaving orphaned markup; the dead optional-injection gate on
  `ICaseEngineerChoices` was deleted once G10 registered it.
- **Efficiency — two residuals**, C07-R-6 (one extra PK read per continuation page) and
  C07-R-8 (two subqueries where one would do; one uncalled helper). Both accepted.
- **Altitude — one residual**, C07-R-4 (a pure unit suite in the integration project).

## Verdict

**needs-changes.** Two major findings, both in `EfPublicUploadRetentionStore`, both
currently unreachable because the port is unregistered and therefore invisible to the
green build and the passing suites — which is precisely why they need catching before A
wires it. C07-R-2 is a small code change in a C-owned file. C07-R-1 is a statement C
must hand to A, since the grant lives in an A-owned migration. Everything else in the
slice is sound: the allocator order, the reference format and its immutability, the
keyset continuation, the PR 671 re-application with all five disposition corrections,
the public session policy, the `RetainIncomingArtifact` invariants, the typed actors
through every command, the note bound and the explicit assignment all verify in code.
The two integration failures are A-owned and reproduce on the unmodified baseline.

---
verdict: needs-changes
independent: true
head: 2ba5e4e21d30a09047b854c549db4af3685e0e7c
reviewed_at: 2026-09-06T14:34:00Z
supersedes: "attestation at b46a07452c41b9636158a50f668274e1e7d17e3f (2026-09-06T14:10:00Z)"
slice: C07 (owner ticket INTK-060, three-owner/three-PR exception — no per-slice PR)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c07
branch: c07-precase
re_review_diff: b46a07452..2ba5e4e21 (4 files, +266/-12)
ownership_check: PASS — no A-owned file changed, in this round or cumulatively
prior_findings: 8 raised at b46a07452 — all 8 correctly dispositioned (4 fixed, 4 accepted with reasons)
new_findings: 1 (C07-R-9, major)
tests_cited:
  - "wave7-tests/1-build.md: dotnet build ./Pegasus.slnx --configuration Release --no-restore — exit 0, PASS, 0 warnings 0 errors"
  - "wave7-tests/2-core.md: Core.Tests filtered — exit 0, PASS, 207/207, 112 ms"
  - "wave7-tests/3-integration.log + .exit: exit 1, FAIL, 68 passed / 4 failed of 72; all four failures are the new IncomingArtifactCustodyTests, all at IncomingArtifactCustodyTests.cs:146"
findings:
  - id: C07-R-9
    severity: major
    status: open
    file: tests/Pegasus.IntegrationTests/IncomingArtifactCustodyTests.cs:36,90,146
    statement: >
      C07-R-2's closure proof does not execute. All four new tests —
      APendingRecordAfterAConfirmedOneLeavesTheRemoteIdentitiesIntact and the three
      ANonConfirmedRecordNeverWritesARemoteIdentity theory cases — fail in the fixture
      before reaching a single assertion, with
      "System.InvalidOperationException : Cannot resolve scoped service
      'Pegasus.Core.Intake.IIntakeReceiptStore' from root provider."
      Both test methods call SeedSessionAsync(factory.Services) at :36 and :90, passing the
      root provider; SeedSessionAsync then hands it to
      TriageQueuesWebTests.StoreMinimalReceiptAsync at :146, which resolves the scoped
      IIntakeReceiptStore at TriageQueuesWebTests.cs:932. Every sibling suite opens a scope
      first ("await using var scope = factory.Services.CreateAsyncScope(); var services =
      scope.ServiceProvider;"); this file is the only place in the project that resolves
      from the root, and ImageIntakeTestData.SeedCaseAsync only survives it because it
      opens its own scope internally. Consequences: the C07-R-2 fix is verified by reading
      but has no executing proof, so the report's "real-SQL proof" claim is not yet true;
      EfPublicUploadRetentionStore.FindAsync's new single-subquery form and
      ScopeOperationKey's "first caller" (both C07-R-8) are likewise unexercised; and the
      integration lane's failures are now C-owned, where at b46a07452 the only two were
      A-owned and reproduced on the baseline.
    required_disposition: >
      Open a scope for the seeding. Either pass scope.ServiceProvider from both test
      methods, or — cleaner, since SeedSessionAsync already owns its own context — wrap the
      receipt seed inside it: "await using var seedScope = services.CreateAsyncScope();"
      then "TriageQueuesWebTests.StoreMinimalReceiptAsync(seedScope.ServiceProvider,
      "incoming-artifact-custody.pdf")". While there, resolve
      IDbContextFactory<PegasusDbContext> at :38 and :92 through the same scope rather than
      the root provider, matching the rest of the suite. Then re-run the integration lane:
      the expected result is 4 more passing tests and only the two A-owned baseline
      failures remaining.
  - id: C07-R-1
    severity: major
    status: closed
    file: src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:634-661
    statement: >
      Required the missing UPDATE grant on [dbo].[PublicUploadOccurrences] to be stated as
      an A handoff. Closed, and answered more completely than asked.
    verification: >
      INTK-060 scratch/c07-notes now carries "OPEN QUESTION 1 (C07-R-1)" as an unchecked
      item with the exact statement "GRANT UPDATE ON OBJECT::[dbo].[PublicUploadOccurrences]
      TO [pegasus_web_runtime_role];", and the report's Correction round 3 repeats it with
      the grant evidence quoted from 20260906054658_V1PlatformFoundation.cs:1319-1320. The
      worker-role grant is correctly left conditional on where A registers the Unknown
      reconciliation caller rather than guessed at. The added claim that DocumentVersions
      needs nothing is true and I checked it: 20260729199000_RuntimeRoleReconciliation.cs
      carries ("DocumentVersions", "SELECT, INSERT, UPDATE") in WebGrants at :126 and in
      WorkerGrants at :232 (the report cites :230; the entry is at :232 — a citation slip,
      not a wrong claim). No migration was touched.
  - id: C07-R-2
    severity: major
    status: closed
    file: src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:644-661
    statement: RecordAsync nulled a document version's Box identities on every non-Confirmed disposition.
    verification: >
      Fixed at 9037b11a2, and fixed more strongly than my required disposition. The whole
      assignment block is now guarded on
      "artifact.State == IncomingArtifactCustodyState.Confirmed && artifact.DocumentVersionId
      is { } versionId", and inside it the writes are
      "version.BoxFileId = artifact.BoxFileId ?? version.BoxFileId" — so it never assigns
      null, and a Confirmed record that arrives without an identity also cannot erase one.
      The comment now states the shared-version reasoning. The report owns the defect
      plainly. The proof is written but does not run: see C07-R-9.
  - id: C07-R-3
    severity: minor
    status: closed
    file: src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs:536-545
    statement: PR 671 disposition flag C11(a) — reusing LifecycleVersion as the principal-write token — was undispositioned.
    verification: >
      Recorded, in the place I asked for it: a new <remarks> block on SetPrincipalAsync
      states one Image Intake, one optimistic token; that a principal save does invalidate a
      concurrently-open Merge or Close form and that this is the intended trade, because a
      second token would let two staff members write the same record each believing they held
      the current version; and that a same-value re-submission leaves the version alone, so
      only a genuine change can invalidate a form. Mirrored as "DECISION (C07-R-3)" in
      INTK-060 scratch/c07-notes.
  - id: C07-R-4
    severity: minor
    status: accepted
    file: tests/Pegasus.IntegrationTests/PublicUploadSessionTests.cs:8-24
    verification: >
      Accepted and now stated as deliberate in the suite's own remarks: the plan names this
      path, the runner filter reads it from this project, tests/Pegasus.Core.Tests/Documents/
      is outside the slice's file scope (M5), and it moves with its filter when the accept
      path is wired (A04). That is the honest disposition; nothing further is owed.
  - id: C07-R-5
    severity: minor
    status: accepted
    verification: >
      Accepted as a residual and recorded as an unchecked item in INTK-060 scratch/c07-notes
      ("Prove the T reference survives ILinkTriageCase, with plan item 3"), assigned to the
      slice that owns the formal-instruction path. The safety argument is restated correctly.
  - id: C07-R-6
    severity: minor
    status: accepted
    verification: >
      Accepted as documented, with a reason I agree with: packing the sequence into the
      opaque sort key is the right fix but changes the cursor payload, and making that change
      on a seam with no production caller and no ability to run a test is how a silent paging
      bug ships. Revisit when the Cases triage tab moves onto IListTriagePage.
  - id: C07-R-7
    severity: nit
    status: accepted
    verification: >
      Accepted as deviation 4, no change, and the residual I named — a constant extra read
      per request would still pass — is restated plainly rather than smoothed over.
  - id: C07-R-8
    severity: nit
    status: closed
    file: src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:609-628
    statement: Two subqueries over DocumentVersionEntity for identities on the same row; ScopeOperationKey had no caller.
    verification: >
      FindAsync now uses one subquery projecting both identities into an anonymous row, read
      back as "row.Remote?.BoxFileId" / "row.Remote?.BoxVersionId". Behaviour-preserving at
      both edges — a null DocumentVersionId and a matched version with null identities both
      still yield null — and it mirrors the existing house idiom "row.Association?.IsActive"
      at EfImageIntakeStore.cs:969-993. ScopeOperationKey gains its first caller in
      IncomingArtifactCustodyTests, which also pins the scoping format the accept path must
      use; that caller does not currently execute (C07-R-9).
  - id: C07-R-0
    severity: nit
    status: closed
    statement: files_touched omitted four PR 671 files.
    verification: >
      Corrected in the report with an accurate explanation — they arrived on the WIP
      carry-over commit rather than one of the implementer's own, and all four are C-owned.
---

# C07 superseding attestation — INTK-060 slice C07, head `2ba5e4e21`

This replaces my attestation at `b46a07452`. Targeted re-review of the dispositions only,
per the coordinator's request. Read-only: I made no edit, ran no test, pushed nothing,
opened and merged no PR, and moved no ticket. No `get_execution_packet`, `gh pr view`,
`gh pr merge`, `move_item`, `update_item` or `set_ticket_doc` call was made; my only
Kanmer write is this attestation on INTK-060 `scratch/review-c07`. I remain independent
of the implementer.

## Scope of the re-review

`git rev-parse HEAD` = `2ba5e4e21d30a09047b854c549db4af3685e0e7c` on `c07-precase`.
`git diff b46a07452..2ba5e4e21 --stat` is four files, +266/-12, over two commits:
`9037b11a2` (the major) and `2ba5e4e21` (the minors). The A-owned filter over
`git diff --name-only b46a07452..2ba5e4e21` returns nothing — ownership still PASS, and no
migration was touched, which is what C07-R-1 required.

## All eight prior findings are correctly dispositioned

Four fixed — R-2 and R-3 as I required, R-8 and the `files_touched` omission as noted.
Four accepted with reasons that hold up, and each accepted residual is recorded on the
ticket rather than only in a report: R-4 as a stated-deliberate note in the suite's own
remarks, R-5 as an unchecked follow-up item, R-6 and R-7 with their trade-offs named. The
R-1 statement is exact and goes further than I asked, verifying that `DocumentVersions` is
already granted for both roles and correctly leaving the worker grant conditional on where
A registers the reconciliation caller instead of guessing. The R-2 fix is stronger than my
required disposition: guarding on `Confirmed` *and* coalescing with `?? version.BoxFileId`
means it can neither assert an unproven identity nor erase a true one. The report's
`## Correction round 3` owns the defect without hedging, and the added
`## Simplification pass` is a real pass over the slice's own diff with two named residuals
and one thing deliberately not done — not a checklist.

**Behaviourally, this head is strictly better than `b46a07452`.** The production change is
correct on reading, and it replaces code that was actively destructive.

## One new major: the closure proof does not run

Wave 7's build is green (0 warnings, 0 errors) and Core is 207/207 in 112 ms. The
integration lane is exit 1 with 68 passed / 4 failed of 72, and **all four failures are the
new `IncomingArtifactCustodyTests`** — the file that exists to close C07-R-2. Every one of
them dies in the fixture at `IncomingArtifactCustodyTests.cs:146` with

```
System.InvalidOperationException : Cannot resolve scoped service
'Pegasus.Core.Intake.IIntakeReceiptStore' from root provider.
```

`SeedSessionAsync(factory.Services)` at `:36` and `:90` passes the **root** provider, and
`SeedSessionAsync` hands it to `TriageQueuesWebTests.StoreMinimalReceiptAsync`, which
resolves the scoped `IIntakeReceiptStore` at `TriageQueuesWebTests.cs:932`. Every sibling
suite opens `factory.Services.CreateAsyncScope()` first; this file is the only place in the
project that resolves from the root, and `ImageIntakeTestData.SeedCaseAsync` only survives
the same argument because it opens its own scope internally — which is exactly why the
pattern looks like it should work.

So no assertion in either test ever executes. The R-2 fix is verified by reading but
unproven by running, the report's "real-SQL proof" claim is not yet true, R-8's new
`FindAsync` shape and `ScopeOperationKey`'s first caller are unexercised for the same
reason, and the integration lane's failures are now C-owned where at `b46a07452` the only
two were A-owned and reproduced on the unmodified baseline. A test that never reaches its
assertions is not evidence, so this cannot be signed off as `pass` on the strength of my
reading alone — the whole point of the new file is that the invariant is proved against
real SQL. The fix is one scope; see C07-R-9's required disposition.

## Verdict

**needs-changes**, on C07-R-9 alone. Everything I raised at `b46a07452` is closed or
honestly accepted, and the two majors that produced the previous verdict are genuinely
resolved in the code. What remains is a fixture defect in the new test file: open a scope
for the seeding, resolve the context factory through it, and re-run the integration lane.
The expected result is four more passing tests and only the two A-owned baseline failures
(`ConcurrencyTokenPersistenceTests.cs:206` and `QdosAllocationRecoveryTests.cs:1446`)
remaining — at which point this slice passes.

---
verdict: pass
independent: true
head: 28148f54f2a7d3cdbce2769d6660fe4c890ccfbc
reviewed_at: 2026-09-06T14:48:00Z
supersedes:
  - "attestation at b46a07452c41b9636158a50f668274e1e7d17e3f (2026-09-06T14:10:00Z, needs-changes)"
  - "attestation at 2ba5e4e21d30a09047b854c549db4af3685e0e7c (2026-09-06T14:34:00Z, needs-changes)"
slice: C07 (owner ticket INTK-060, three-owner/three-PR exception — no per-slice PR)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c07
branch: c07-precase
re_review_diff: 2ba5e4e21..28148f54f (1 file, +7/-3, test-only; 0 files under src/)
cumulative_diff_base: ab9f3fcd821b604a162e9448d5dd44e0ad9fcb27
ownership_check: PASS — no A-owned file changed, in this round or cumulatively
findings_open: 0
findings_total: 10 raised across three rounds — 6 closed, 4 accepted with recorded reasons
merge_action: none — no per-slice PR exists under the controller override; no PR opened, merged or moved, and no ticket boundary moved
tests_cited:
  - "wave8-tests/1-build.md: dotnet build ./Pegasus.slnx --configuration Release --no-restore — exit 0, PASS, 0 Warning(s) 0 Error(s)"
  - "wave8-tests/2-integration.md + .exit + .log: exit 0, PASS, Failed 0 / Passed 72 / Total 72, 1 m 25 s; filter includes FullyQualifiedName~IncomingArtifactCustodyTests"
  - "wave7-tests/2-core.md (head 2ba5e4e21, unchanged production code): exit 0, PASS, 207/207, 112 ms"
  - "wave1/wave5-tests/7-baseline-alloc.md: the two A-owned failures reproduce on the unmodified task/pegasus-v1-intake baseline"
findings:
  - id: C07-R-9
    severity: major
    status: closed
    file: tests/Pegasus.IntegrationTests/IncomingArtifactCustodyTests.cs:143-155
    statement: >
      C07-R-2's closure proof did not execute: all four new custody tests failed in the
      fixture with "Cannot resolve scoped service 'Pegasus.Core.Intake.IIntakeReceiptStore'
      from root provider", because SeedSessionAsync received the root provider and handed it
      to TriageQueuesWebTests.StoreMinimalReceiptAsync.
    verification: >
      Fixed at 28148f54f exactly as required. SeedSessionAsync now opens
      "await using var scope = services.CreateAsyncScope();" and routes all three
      resolutions — StoreMinimalReceiptAsync, ImageIntakeTestData.SeedCaseAsync and
      IDbContextFactory<PegasusDbContext> — through scopedServices, with a comment naming
      why. Proved by execution, not by reading: wave 8's integration lane is exit 0 at
      72/72 under a filter that includes IncomingArtifactCustodyTests, against wave 7's
      68/72 at the previous head where those four were the only failures. The delta is
      exactly the four custody tests, so C07-R-2's invariant is now proved against real SQL
      as the report claims. The secondary suggestion in my required disposition — routing
      the store construction at the old :38/:92 through the scope as well — was
      house-consistency only, and the green lane confirms the root resolution of the
      singleton context factory was never a correctness problem; nothing is owed there.
  - id: C07-R-1
    severity: major
    status: closed
    statement: Missing UPDATE grant on [dbo].[PublicUploadOccurrences] was not stated as an A handoff.
    verification: >
      Closed at 2ba5e4e21 and unchanged since. INTK-060 scratch/c07-notes carries
      "OPEN QUESTION 1 (C07-R-1)" as an unchecked item with the exact statement
      "GRANT UPDATE ON OBJECT::[dbo].[PublicUploadOccurrences] TO [pegasus_web_runtime_role];",
      and the report's Correction round 3 repeats it with the evidence quoted from
      20260906054658_V1PlatformFoundation.cs:1319-1320. The worker grant is correctly left
      conditional on where A registers the Unknown reconciliation caller. The
      DocumentVersions-already-granted claim is true and I verified it at
      20260729199000_RuntimeRoleReconciliation.cs:126 (WebGrants) and :232 (WorkerGrants);
      the report cites :230, a citation slip, not a wrong claim. No migration touched.
  - id: C07-R-2
    severity: major
    status: closed
    statement: RecordAsync nulled a document version's Box identities on every non-Confirmed disposition.
    verification: >
      Fixed at 9037b11a2, stronger than required: the assignment block is guarded on
      "artifact.State == IncomingArtifactCustodyState.Confirmed && artifact.DocumentVersionId
      is { } versionId" and writes "version.BoxFileId = artifact.BoxFileId ?? version.BoxFileId",
      so it can neither assert an unproven identity nor erase a true one. Now proved by
      APendingRecordAfterAConfirmedOneLeavesTheRemoteIdentitiesIntact and the three
      ANonConfirmedRecordNeverWritesARemoteIdentity theory cases, all passing in wave 8.
  - id: C07-R-3
    severity: minor
    status: closed
    verification: >
      The LifecycleVersion-token decision is recorded in SetPrincipalAsync's remarks
      (EfImageIntakeStore.cs:536-545) and mirrored as "DECISION (C07-R-3)" on the ticket.
  - id: C07-R-8
    severity: nit
    status: closed
    verification: >
      FindAsync uses one subquery projecting both identities, read back through the house
      idiom "row.Remote?.BoxFileId"; behaviour-preserving at both edges. ScopeOperationKey's
      first caller now executes, pinning the scoping format the accept path must use.
  - id: C07-R-0
    severity: nit
    status: closed
    verification: files_touched corrected in the report with an accurate explanation.
  - id: C07-R-4
    severity: minor
    status: accepted
    verification: >
      The pure session suite stays in Pegasus.IntegrationTests, stated as deliberate in its
      own remarks: the plan names the path, the runner filter reads it there, and
      tests/Pegasus.Core.Tests/Documents/ is outside the slice's file scope (M5). It moves
      with its filter when the accept path is wired (A04).
  - id: C07-R-5
    severity: minor
    status: accepted
    verification: >
      Plan item 3 and proving the T reference across ILinkTriageCase are recorded as an
      unchecked residual on the ticket, assigned to the slice that owns the
      formal-instruction path. Safe by construction meanwhile: Reference and Sequence are
      assigned only in EfTriageStore.CreateAsync's initializer, and both columns carry
      unique indexes.
  - id: C07-R-6
    severity: minor
    status: accepted
    verification: >
      One PK read per continuation page, accepted as documented. Packing the sequence into
      the opaque sort key is the right fix but changes the cursor payload, and making that
      change on a seam with no production caller and no ability to run a test is how a
      silent paging bug ships.
  - id: C07-R-7
    severity: nit
    status: accepted
    verification: >
      Deviation 4 stands, with the residual stated plainly: a constant extra read per
      request would still pass, while per-row growth is foreclosed and C13/C14 are verified
      in code as one round trip and one LEFT JOIN.
---

# C07 final attestation — INTK-060 slice C07, head `28148f54f`

This replaces my attestations at `b46a07452` and `2ba5e4e21`. Read-only throughout: I made
no edit, ran no test, pushed nothing, opened, merged or moved no PR, and moved no ticket
boundary. No `get_execution_packet`, `gh pr view`, `gh pr merge`, `move_item`,
`update_item` or `set_ticket_doc` call was made at any point; my only Kanmer write is this
attestation on INTK-060 `scratch/review-c07`. I am not the implementer.

## What changed since the last attestation

`git rev-parse HEAD` = `28148f54f2a7d3cdbce2769d6660fe4c890ccfbc` on `c07-precase`. One
commit over `2ba5e4e21` — `28148f54f` "test(precase): seed the custody fixture from a
request scope" — touching one file, `tests/Pegasus.IntegrationTests/IncomingArtifactCustodyTests.cs`,
+7/-3. `git diff --name-only 2ba5e4e21..28148f54f -- src/` is empty: **no production code
changed**, so every code verification in my `2ba5e4e21` attestation still binds unaltered,
and wave 7's Core result (207/207) still describes this head's Core behaviour. The A-owned
filter over the diff returns nothing — ownership PASS, cumulatively as well as this round.

The fix is precisely the required disposition: `SeedSessionAsync` opens
`await using var scope = services.CreateAsyncScope();` and routes `StoreMinimalReceiptAsync`,
`ImageIntakeTestData.SeedCaseAsync` and the context-factory resolution through
`scopedServices`, with a comment naming the reason. It brings the file into line with every
sibling suite rather than working around the symptom.

## Evidence

Wave 8 at this head: build exit 0, "Build succeeded. 0 Warning(s) 0 Error(s)"; integration
lane exit 0, "Passed! - Failed: 0, Passed: 72, Skipped: 0, Total: 72", 1 m 25 s, under a
filter that explicitly includes `FullyQualifiedName~IncomingArtifactCustodyTests`. Against
wave 7's 68/72 at `2ba5e4e21`, where those same four custody tests were the only failures,
the delta is exactly those four. C07-R-2's invariant is therefore proved by execution
against real SQL, which is what the last verdict was waiting on.

One thing worth stating rather than passing over: wave 8's filter is narrower than wave 5's
and 7's — it drops `ConcurrencyTokenPersistenceTests`, `IntakeAllocationConsumerTests` and
`AutomationIntakeParityIngressTests` — so the two A-owned failures I dispositioned at
`b46a07452` were not re-run here. That is acceptable and does not weaken this verdict:
both are A-owned files, both reproduce identically on the unmodified
`task/pegasus-v1-intake` baseline (`wave5-tests/7-baseline-alloc.md`, which fails a third
A-owned test besides), and nothing since `b46a07452` has touched production code they
exercise — rounds 3 and 4 changed only `EfDocumentRequestStore.cs`, `EfImageIntakeStore.cs`
and two integration test files, and this round changed no production code at all.

## Cumulative position

Ten findings across three rounds: six closed, four accepted with reasons recorded on the
ticket as well as in the report. **No finding remains open.** The three review questions
were answered in full at `b46a07452` and nothing since has disturbed those answers:

- **Q1** — the brief carried plan items 1, 2, 4, 5 (Provider API half), 6, 7 and 8
  faithfully; plan item 3 was the one omission, now a recorded residual (C07-R-5), and the
  `IntakeEnvelopeLimits` and manual/mailbox-custody exclusions are correct and reasoned.
- **Q2** — every numbered brief item is delivered and verified in code: the allocator's
  probe-outside/counter-first/second-probe order with `UPDLOCK, HOLDLOCK`, unique-index
  backstop and a sequence that can never be 0; `T-` plus five-or-more digits, immutable
  because `Reference` and `Sequence` are assigned in one initializer and nowhere else; the
  `Reference`→`ClaimNumber` rename with its one B-owned consumer still correctly labelled;
  the keyset continuation over the shared `CursorPaging`/`ICursorProtector` with the scope
  bound to actor, state and order and the bound applied before the draft join; all five PR
  671 disposition corrections; the non-sliding fifteen-minute session with typed
  `LimitsVersionMismatch` and `MayReissue`; every `RetainIncomingArtifact` invariant; G12
  typed actors with kind and subject in all five hashes and the system worker admitted on
  the creation path only; the 500-character note bound from one constant matching the
  column; required `ICaseEngineerChoices` with "Assign to me" gone; and the 30 MiB Provider
  API envelope. Everything unwired is stated, including the grant that was not (C07-R-1,
  now recorded).
- **Q3** — the simplification pass was absent and is now present, a real pass over the
  slice's own diff naming two efficiency residuals, one altitude residual, and one thing
  deliberately not attempted.

No fabricated domain data, no weakened assertions — the two modified pre-existing tests were
both tightened — no `Guid.NewGuid` in any changed page, labels-and-values-only copy,
concurrency results surfaced, and errors failing closed rather than suppressed.

## Verdict

**pass**, bound to `28148f54f2a7d3cdbce2769d6660fe4c890ccfbc`.

No merge action follows: under the controller override this slice has no per-slice PR, so
there is nothing to merge and no gated boundary for me to move. This attestation is the
deliverable.

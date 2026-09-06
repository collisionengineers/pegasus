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

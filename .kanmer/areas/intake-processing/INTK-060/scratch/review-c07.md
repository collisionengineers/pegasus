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

---
kind: source-review-attestation
verdict: NEEDS CHANGES
scope: source-only (no dotnet test, no push, no PR, no ticket move)
ticket: INTK-060
slice: C07 item 5 - channel limits (residual INTK-052)
head_sha: 4ae44e232daa799b343b824484010be8d48c5d64
base_sha: 68488bfa3e896e72bb49bd8f434352d85a1d7152
branch: c07-precase
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c07
worktree_clean: true (git status --porcelain empty at review)
authority:
  - pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md:1043-1051 (C07 change item 5)
  - pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md:1082-1083 (tests paragraph)
  - PR 673 comment 5563992668 (A's confirmation of the derived-2-GiB gap)
  - AGENTS.md rules 7, 8, 19
independent: true (reviewer is not the implementer of 4ae44e232)
blockers: 0
majors: 4
minors: 6
observations: 2
full_attestation: C:\Users\PGUSER\AppData\Local\Temp\claude\C--Users-PGUSER-documents-github-pegasus\5adc2fb3-f15d-4145-84ed-948eb9fde4e4\scratchpad\takeover\c07-limits-review.md
---

# C07 item 5 - source review of `68488bfa3..4ae44e232`

**NEEDS CHANGES (source-only).** No test run, no build run, nothing pushed, nothing
merged. The verdict says nothing about whether the wave passes; it says the source is
not yet ready for the combined tree without the corrections below. All four plan
values are correct; every finding is in the test/doc surface the change dragged out
of date.

## 1. Verification against the dispatch checklist

### (1) The four values, and the multipart budget is pinned - PASS

| Fact | Location | Value | Plan |
| --- | --- | --- | --- |
| Manual per-file | `src/Pegasus.Core/Intake/IntakeContracts.cs:23` | `100 * 1024 * 1024` = 104,857,600 | matches |
| Multipart body budget | `IntakeContracts.cs:102` | `(200L * 1024 * 1024) + MultipartOverhead` = 209,780,736 | matches |
| Staff batch file count | `IntakeContracts.cs:57` | `20` | retained |
| Provider API decoded envelope | `IntakeContracts.cs:69` | `30 * 1024 * 1024` = 31,457,280 | unchanged |

`MultipartOverhead` is `64 * 1024` at `IntakeContracts.cs:108`; 209,715,200 + 65,536 =
209,780,736. Line 102 contains **no reference to `MaximumBatchFileCount` and none to
`MaximumContentLength`** - a literal pinned expression. The derived form is gone from
`src/` entirely. A's PR 673 concern is answered.
`IntakeEnvelopeLimitsTests.cs:55-58` asserts all four exact values; `:68-74` asserts
`MaximumBatchContentLength < MaximumBatchFileCount * MaximumContentLength`, the
inequality that fails the moment anyone re-derives it.

### (2) Provider API per-file bound - PASS

- `IntakeContracts.cs:82`: `MaximumProviderApiFileLength = MaximumProviderApiEnvelopeLength`
  - defined *as* the envelope, not a second copy of 30 MiB (rule 8 satisfied).
- `src/Pegasus.Core/ProviderApi/ProviderSubmission.cs:292` now reads
  `MaximumProviderApiFileLength` at the `RequireEnvelope` per-file check.
- Unchanged as required: `MaximumProviderApiEnvelopeLength` (`:69`),
  `MaximumProviderApiRequestLength` (`:88`); consumers `ProviderSubmission.cs:294,338`
  and `ProviderApiEndpoints.cs:81,100,220` untouched.

See M3: the change at `:292` is real but no test would fail if it were reverted.

### (3) `DurableIntake` switch, one owner, no duplicate constant - PASS

`src/Pegasus.Core/Intake/DurableIntake.cs:325-334`, one switch:
ManualUpload -> `MaximumContentLength` (`:326`), Mailbox -> `MaximumMailboxContentLength`
(`:327`), Automation -> `MaximumContentLength` (`:328`), ProviderApi ->
`MaximumProviderApiRequestLength` (`:329`); default arm still throws. Change is
comment-only.

Sweep across `src/` and `tests/` (build output excluded):
- `MaximumContentLength`: one owner (`IntakeContracts.cs:23`), five consumers
  (`DurableIntake.cs:326,328`, `Mcp/IntakeMcpTools.cs:153`, `Pages/Upload.cshtml.cs:33,40,90`).
  No second declaration.
- `TenMiB`: **no source hit** anywhere (only stale `bin`/`obj` assemblies). Fully retired.
- `10 * 1024 * 1024`: five `src/` hits, none a channel limit -
  `WordBinaryExtractionLimits.cs:9`, `MimeKitPdfPigOpenXmlIntakeSourceReader.cs:38`,
  `Mcp/AutomationMcpErrors.cs:19` and `Mcp/IntakeSourceMcpContent.cs:21` (both
  `maxInlineBytes` ceilings for reading content *out*), `Pages/Cases/Details.cshtml.cs:214`
  (B-owned estimate bound).
- `10.0 MB`/`10 MB`: the two the author listed, plus `Cases/Details.cshtml.cs:1489`
  (B-owned) and prose at `OperatorLabels.cs:803`.

### (4) The two deviations

**Deviation 1 (`IntakeWebNegativeTests.cs`) - assertion-preserving and honest. ACCEPT.**
`:125` swaps `TenMiB` for `PerFileLimit = IntakeEnvelopeLimits.MaximumContentLength`.
The pair keeps its shape: `:226` posts `new byte[PerFileLimit]`, still asserts `Found`
and persisted `SourceLength` (`:236`); `:252` posts `+1`, still asserts `OK`, the
refusal sentence and `AssertNoBusinessPersistenceAsync`. Renames literal. Refusal now
derived at `:257` with a literal `"100.0 MB"` drift guard at `:260`. Verified
`OperatorLabels.FileSize` (`OperatorLabels.cs:807-812`) renders 104,857,600 as
`"100.0 MB"`, and the production sentence comes from the same helper at
`Pages/Upload.cshtml.cs:95`. Necessary, correctly disclosed.

**Deviation 2 - honest, but leaves a dead assertion. See m1.**
The 10 MiB figure is correctly labelled *historical* (`IntakeEnvelopeLimitsTests.cs:19-25`);
nothing in the codebase now claims 10 MiB is live, so it does not keep a stale fact
alive in the sense asked. The defect is different: line 42 compares two compile-time
constants (`17_496_501 > 10L * 1024 * 1024`), constant-folded and unfalsifiable. A live
assertion became a tautology. The load-bearing half survives at `:46-49`, and the
"bounds must not converge" intent is live at `:30-35`.

### (5) Boundary tests - PASS

Manual: `IntakeWebNegativeTests.cs:215` (limit) / `:240` (limit+1) through the real
`/Upload` POST. Provider API: `QdosBoundaryContractTests.cs:176` / `:181` through the
existing `ProviderSubmissionPolicy.RequireEnvelope`. Integration 413:
`ProviderApiSubmissionTests.cs:432`. Nothing weakened - the reworked `overEnvelope`
case (`QdosBoundaryContractTests.cs:148-160`) is *stronger* than the 20 x cap it
replaced: `Assert.All` at `:152-157` proves every file is inside the per-file bound
before the sum is asserted over the envelope.

### (6) Doc comments - PASS, except m2

`grep "2 GiB"/"2GiB"/"2 GB"` across `src/` returns **nothing**. The container sentence
is gone from `IntakeContracts.cs:60-68`; the replacement at `:91-101` says "far past
what the Web instance can hold" with no figure. Every changed constant names
"C07 item 5 (residual INTK-052)".

### (7) Out of scope - PASS

7 files, +165/-47, all Core intake/provider policy plus the three test files asserting
those limits. The only file outside the named list is `IntakeWebNegativeTests.cs`,
declared as deviation 1.

### (8) The host items listed for A / F - each confirmed

| Item | Confirmed | Attribution |
| --- | --- | --- |
| Kestrel `MaxRequestBodySize` unset | **YES.** Zero hits for `MaxRequestBodySize`/`maxAllowedContentLength`/`client_max_body_size` across the whole worktree; none in `infra/`, none in `appsettings*.json`. Kestrel's 30,000,000-byte default now sits *below* the 100 MiB Core cap. | F, correct per C07 item 5. See M2. |
| `Pages/StatusCode.cshtml.cs:60` | **YES.** "Files must be 10 MB or smaller." on the 413/400 arm; now false; no test asserts it. | **Disputed - see m4.** |
| `Mcp/IntakeMcpTools.cs:123` | **YES.** `[Description]` says "limited to 10 MB before decoding" while `:153` passes `MaximumContentLength` to `DecodeContent`. | A, correct. See also m6. |
| `tests/.../ProviderApi/ProviderSubmissionTests.cs:250,264` | **YES.** `:250` = 100 MiB+1; `:264` = 4 x 100 MiB; comment at `:257-258` now false. ~500 MiB. | **Should not have been deferred - M1.** |
| `Program.cs:639` | **YES.** `MultipartBodyLengthLimit = MaximumBatchContentLength`; follows the constant. | none needed |
| `MultiFormatGenuineCorpusWebTests.cs:171` | **YES**, and its sibling at `:292` was missed - m5. | C |
| The `docs/` list | Spot-checked, consistent; held by the doc owner. | doc owner |

## 2. Findings

### M1 (MAJOR) - `ProviderSubmissionTests.cs` keeps a now-false comment alive, and the author's own filter runs it

`tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs:255-270`: the comment
"Four files each inside the per-file bound still exceed the envelope" and the variable
`eachUnderTheFileBound` now describe files of 100 MiB - **3.3x the Provider API
per-file bound**. The block no longer proves anything the assertion twelve lines above
already proved: it is a second copy of the per-file refusal dressed as an envelope
test. This is the same defect the author correctly fixed in `QdosBoundaryContractTests.cs`
and correctly refused to leave in `IntakeWebNegativeTests.cs`; deferring it is
inconsistent with both. The file is in `Pegasus.Core.Tests`, already edited twice, and
`ProviderSubmissionTests` is inside the Core filter the report hands the runner, so the
wave pays ~500 MiB for a block that proves nothing. Rules 19 and 8.

**Exact correction** (mirroring `QdosBoundaryContractTests.cs`):
- `:250` `new byte[IntakeEnvelopeLimits.MaximumContentLength + 1]`
  -> `new byte[IntakeEnvelopeLimits.MaximumProviderApiFileLength + 1]`
- `:264` `new byte[IntakeEnvelopeLimits.MaximumContentLength]`
  -> `new byte[8 * 1024 * 1024]` (4 x 8 MiB = 32 MiB, past the 30 MiB envelope while
  each file stays inside the 30 MiB per-file bound)
- leave the comment text; after the two edits it is accurate again.
Allocation drops ~500 MiB -> ~62 MiB and the block recovers its meaning.

### M2 (MAJOR) - the 100 MiB cap is unreachable in production, and the new integration test cannot see that

No `MaxRequestBodySize` anywhere, so Kestrel's 30,000,000-byte default refuses a 100 MiB
upload before Core policy runs. The author flagged this honestly and assigned it to F,
which matches C07 item 5 - that part is right. What is not stated is the effect on the
evidence: `IntakeWebNegativeTests` runs on `WebApplicationFactory<Program>`
(`tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs:29`), i.e. `TestServer`, which
does not apply Kestrel's body limit. `ExactPerFileLimitUploadPassesTransportValidation`
(`:215`) will go green while the production transport rejects the identical request -
the one thing its name asserts is the one thing it does not establish.

**Exact correction:** rename to `ExactPerFileLimitUploadPassesFormAndCorePolicy` and add
one sentence to the comment at `:230-233`: "TestServer applies FormOptions but not
Kestrel's MaxRequestBodySize; the host limit F still has to raise is out of this test's
reach." Carry the Kestrel gap to the controller as an open item against F, not a report
bullet. A green wave is not evidence that 100 MiB uploads work.

### M3 (MAJOR) - the one production line changed is not pinned by any test

Because `MaximumProviderApiFileLength == MaximumProviderApiEnvelopeLength`, any single
file over the per-file bound is also over the envelope sum, and every test exercising
the new check uses a single file (`QdosBoundaryContractTests.cs:181`,
`ProviderApiSubmissionTests.cs:432`; `QdosBoundaryContractTests.cs:134` and
`IntakeEnvelopeLimitsTests.cs:80-89` assert constants, never the call site). Reverting
`ProviderSubmission.cs:292` to `MaximumContentLength` would fail **nothing** - the sum
check at `:293-294` catches all three cases identically. The change is correct and worth
keeping as the guard against future inheritance, but it is undemonstrated, and
`TheProviderApiPerFileBoundAcceptsItsLimitAndRefusesOneByteMore` claims more than it
proves.

**Exact correction:** independent isolation is impossible while the constants are equal,
so say so. Add to the XML doc at `QdosBoundaryContractTests.cs:167-172`: "While the
per-file bound equals the envelope, a single oversized file trips both checks; this test
pins the boundary, and `AProviderApiFileIsNeverAllowedPastTheEnvelopeThatCarriesIt` pins
the constant the policy must read." Do **not** manufacture a multi-file case to fake
isolation - it cannot exist while the constants are equal.

### M4 (MAJOR) - two Test UI snapshots are stale and will fail the verify gate

`Pages/Upload.cshtml` renders its label from
`OperatorLabels.FileSize(IntakeEnvelopeLimits.MaximumContentLength)`
(`Upload.cshtml.cs:33`), so the page now says "100.0 MB". The committed snapshots still
say 10.0 MB:
- `docs/design/test-ui/pages/upload--default.html:175`
- `docs/design/test-ui/pages/upload--validation.html:176`
  (`<p>EML, MSG, PDF, DOC, DOCX, JPG or PNG &#xB7; up to 10.0 MB each &#xB7; 20 files</p>`)

`pwsh -File ./scripts/Update-TestUiSnapshots.ps1 -Verify` is a stated gate
(`streams/C-intake.md:1204`) and will fail on both. The report's `docs/` list names nine
other documents for the doc owner but not these two - and these are not doc-owner prose,
they are generated artefacts of a C-owned page this change altered.

**Exact correction:** regenerate in this slice (`Update-TestUiSnapshots.ps1`, then
`-Verify`) and commit the two files; or, if the wave regenerates centrally, schedule it
as a required wave step. It cannot be left as-is: the gate fails. (Scripts exist at
`scripts/Update-TestUiSnapshots.ps1`, `scripts/Test-UiCatalogue.ps1`; not run here -
source-only review.)

### m1 (MINOR) - dead assertion at `IntakeEnvelopeLimitsTests.cs:41-45`

Two `private const long` values compared; constant-folded, unfalsifiable. The live
assertion is `:46-49`; the convergence intent is live at `:30-35`.
**Correction:** delete `OneFileBoundAtTheRefusal` (`:19-25`) and the assertion
(`:41-45`); move the fact into the XML doc on `RefusedQdosForwardLength` (`:12-17`):
"...refused as `message_too_large` against the 10 MiB one-file bound then in force,
quarantined, and never read." Record preserved, dead assertion gone, no private constant
impersonating a live bound.

### m2 (MINOR) - `MaximumContentLength` claims a public-request-link path it does not bound

`IntakeContracts.cs:12-13` now says "staff form **or a public request link**". No public
path reads this constant: the public link is bounded by `RequestUploadPolicy.MaximumFileBytes`
(`Pages/Uploads/Request.cshtml.cs:127`, `Core/Documents/RequestUploadPolicy.cs:801`),
bound from configuration at `Program.cs:276`. C07 item 6 is what builds that session.
**Correction:** revert `:12-13` to "One file uploaded through the staff form, which
arrives inside one bounded multipart HTTP request." and, in `<remarks>`, replace
"per-request `DocumentRequests` settings may tighten it and may never raise it" with
"The public request link is bounded separately by `RequestUploadPolicy.MaximumFileBytes`;
that configured value may tighten this cap and may never raise it (C07 item 5)."

### m3 (MINOR) - "may tighten, never raise" is stated but not enforced

`Program.cs:272-280` binds `DocumentRequests:MaximumFileBytes` straight through with no
comparison to `MaximumContentLength`, and no test covers it. (`DocumentRequests` is
absent from both `appsettings*.json`, so nothing is violated today.)
**Correction:** after reading `MaximumFileBytes` in the accepted-limits factory, add
`if (maximumFileBytes > IntakeEnvelopeLimits.MaximumContentLength) throw new
InvalidOperationException("DocumentRequests:MaximumFileBytes may tighten the Core
per-file cap and may never raise it.");`. If `Program.cs` is judged outside this slice,
that is defensible - but then m2's correction must land, so the constant does not claim
an invariant nothing holds.

### m4 (MINOR) - `StatusCode.cshtml.cs:60` attributed to A on no stated basis; rule 8 gives the real fix

Confirmed false as of this commit. But the C08 route matrix (`streams/C-intake.md:1134`)
puts `/status/{code:int}` in "Existing shared error/status pages remain reachable through
the C-owned common layouts/assets; C adds no behavior or explanatory workflow" - it is in
no A-owned row. Correcting a figure this commit made wrong is not "adding explanatory
workflow"; assigning it to A moves a defect this change created onto an owner the plan
does not name. Separately it is a second copy of a limit string a helper already owns
(rule 8).
**Correction:** at `:60` use
`$"Files must be {OperatorLabels.FileSize(IntakeEnvelopeLimits.MaximumContentLength)} or smaller. Choose a smaller file and try again."`
plus the two usings; it then tracks the constant forever. If the controller prefers to
route it elsewhere, route it to the controller as unassigned, not to A.

### m5 (MINOR) - the corpus skip message the author missed

`MultiFormatGenuineCorpusWebTests.cs:292` builds
`"The ignored local genuine corpus has no {extension} source at or below the 10 MB Web limit."`
- an operator-visible string now wrong by 10x. The report lists `:171` but not `:292`.
**Correction:** either point `:171` at `IntakeEnvelopeLimits.MaximumContentLength` and
derive `:292` from it, or keep `:171` as a deliberate corpus filter and reword `:292` to
"at or below the 10 MB corpus sample ceiling", which is what it actually means.

### m6 (MINOR) - the Automation channel was raised 10x without being named

`DurableIntake.cs:328` maps `Automation` to `MaximumContentLength`, so MCP
`pegasus_intake_submit` now accepts a ~133 MiB base64 string decoded to 100 MiB in memory
(`IntakeMcpTools.cs:153`), up from 10 MiB. The report names the stale `[Description]` at
`:123` but never says the *enforced* automation limit moved - the part with a memory cost.
C07 item 5 speaks only of the manual/public cap; Automation rode along because it shares
the constant. Disclosure, not a defect: the mapping is pre-existing and the value is what
the plan sets. But the new comment at `DurableIntake.cs:322` says "one constant per
channel", which is not true of these two.
**Correction:** state to the controller that Automation shares the manual cap and was
raised with it; soften `:322` to "One switch; the manual and automation channels
deliberately share one constant, the other two have their own." No behaviour change.

### O1 (OBSERVATION) - the literal `"100.0 MB"` at `IntakeWebNegativeTests.cs:260`

A hard-coded literal in the same test whose hard-coded literal was removed. Deliberate
and defensible - paired with the derived assertion at `:257` it catches a constant that
drifts while the label still formats. Keep it; noted so the apparent inconsistency is
not read later as an oversight.

### O2 (OBSERVATION) - runtime cost is real and understated

`ExactPerFileLimitUploadPassesTransportValidation` allocates 100 MiB, posts it as
multipart through `TestServer` (buffering past `MemoryBufferThreshold` to disk), stores
it, SHA-256s it, and hands 100 MiB of zeroes named `boundary.pdf` to the PdfPig/MimeKit
reader; `PerFileLimitPlusOneReturnsValidationAndDoesNotPersist` repeats at +1. Peak
working set will be several hundred MiB across the pair, and with M1 unfixed
`ProviderSubmissionTests` adds ~500 MiB on top.

## 3. Test filters required in the combined run

**Core** - `tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj` (author's filter, correct
as written; `ProviderSubmissionTests` must stay in it - it is the file M1 corrects and
must be seen to pass after the correction):

```
--filter "FullyQualifiedName~Pegasus.Core.Tests.Intake.IntakeEnvelopeLimitsTests|FullyQualifiedName~Pegasus.Core.Tests.Qdos.QdosBoundaryContractTests|FullyQualifiedName~Pegasus.Core.Tests.ProviderApi.ProviderSubmissionTests"
```

**Integration** - `tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj`
(blocked until the A-owned `CS0246 'EfCaseArtifactCustody'` at
`DocumentCustodyDurabilityTests.cs(462,35)` clears; that error is A's, not this slice's -
I did not re-run the build, so the author's exit code 1 stands as reported and is
INCONCLUSIVE from this review's position):

```
--filter "FullyQualifiedName~Pegasus.IntegrationTests.IntakeWebNegativeTests|FullyQualifiedName~Pegasus.IntegrationTests.ProviderApiSubmissionTests"
```

Widened from the author's three named methods to both whole classes: the renames touch
class-level shared state (`PerFileLimit` at `:125`) and `ProviderApiSubmissionTests` has
other envelope-adjacent cases that must be seen not to regress. If wave time forbids the
wider filter, the author's three-method filter is the minimum.

**Additionally required, missing from the author's report:** the Test UI snapshot
regeneration in M4 -
`pwsh -File ./scripts/Update-TestUiSnapshots.ps1` then `-Verify`.

## 4. What is right, stated plainly

- Every value the plan pins is exactly the value in the code. No arithmetic, no drift.
- The 2 GiB derived budget is gone from constant and prose, and an inequality test fails
  if anyone re-derives it. A's PR 673 concern is answered.
- The Provider API keeps its own per-file bound, defined *as* the envelope rather than
  copied from it, so the manual channel can move again without dragging it along.
- `IntakeEnvelopeLimits` is the single owner: one switch in `DurableIntake`, no second
  declaration of any channel limit in `src/` or `tests/`, `TenMiB` fully retired.
- The `overEnvelope` rework (`QdosBoundaryContractTests.cs:148-160`) is a genuine
  strengthening, not a substitution.
- Both deviations are disclosed, and deviation 1 is exactly the right call.

## 5. Disposition

**NEEDS CHANGES (source-only).** M4 fails a stated repository gate. M1 leaves a false
comment and a meaningless ~500 MiB assertion in a file the change made wrong. M2 and M3
are honesty corrections on claims the tests do not support. The six minors should land
while the files are open. None of it touches the four values, which are correct.

No file was edited, no command was run against the build or test runner, nothing was
pushed, no PR exists, no ticket was moved. The only write is this `append_scratch` on
INTK-060 slug `review-c07`.

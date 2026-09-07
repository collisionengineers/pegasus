# C07 slice source review — public upload sessions

- **Bound commit:** `df198034aa5b96f6eb7ca150cb3092dacee3134e` ("INTK-060 C07 complete public upload sessions")
- **Branch / worktree:** `task/pegasus-v1-intake` — `C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake`
- **Worktree HEAD at review:** `aa5e669d76ad2f7cc24783f8076644c439509feb` (two later A merges; `git diff df198034a..HEAD` over the eight slice files is empty, so the line numbers below are identical at the bound commit and at HEAD)
- **Reviewer:** independent of the implementer (source review only; no merge, no ticket move, no writes to the worktree)
- **Scope of verdict:** SOURCE ONLY. No build and no tests were run by this review. The author's Web Release build claim was not re-executed; the Integration test project does not compile on the C branch (`tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs:462` references A-owned `EfCaseArtifactCustody`), so **no assertion in this slice has been executed**. Every claim below is read from source.
- **Authority:** `pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md` §C07 items 6–7 and its "Tests and expected outputs"; §C08 route matrix row "Public upload"; residual rows INTK-047 / INTK-051 / INTK-055 (lines 1310, 1313, 1317); `AGENTS.md` Agent conduct 6–22.

## Verdict

**NEEDS CHANGES** — 4 blockers, 7 majors, 7 minors.

The session state machine, the fixed non-sliding window, the server-issued occurrence projection and the finalize/refuse-after-finalize path are all genuinely present and well shaped, and the add/finalize/refuse-later-bytes claims hold on a single-caller happy path. The slice fails on the **replacement** path, which mutates a confirmed occurrence in place, and on the **finalize** path, which is unreachable for two ordinary, reachable link states and is not serialized against a concurrent arrival. Two of these violate invariants that the same file documents at length as inviolable.

---

## Blockers

### R-1 (blocker) — Replacement moves a confirmed occurrence backwards and erases its custody identities, bypassing the retention port

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:493-500`

```csharp
occurrence.CustodyState = EfPublicUploadRetentionStore.ArrivedCode;
occurrence.DocumentId = null;
occurrence.DocumentVersionId = null;
```

This is applied to a row the immediately preceding check has just asserted is `ConfirmedCode` (`:487-491`). It contradicts three rules written into the same file and into `RetainIncomingArtifact`:

- `EfPublicUploadRetentionStore.ForwardSourceCodes` (`:1268-1287`): "nothing transitions out of confirmed or failed, because both are answers custody has given." Every other custody-state write in the codebase is a conditional `ExecuteUpdateAsync` whose WHERE names the permitted source states (`RecordAsync` `:1291-1322`, `TryClaimHandOverAsync` `:1252-1264`). This is the only unconditional, tracked-entity write of `CustodyState`, and it writes the one transition the port exists to forbid.
- `IIncomingArtifactRetentionStore.RecordAsync` doc (`src/Pegasus.Core/Intake/RetainIncomingArtifact.cs:163-170`): "Identities are filled in where they are missing and **never erased**, because the same logical document and version are what a later reconciliation asks about." Nulling `DocumentId`/`DocumentVersionId` orphans the document custody durably holds: after a replacement, no row in the system points from the session to the first retained version, so C07 item 7's "Re-evaluation reads that exact logical version through A04" is unsatisfiable for any replaced file.
- C07 item 7 and `IncomingArtifactOccurrence`'s doc (`RetainIncomingArtifact.cs:63-70`): "One **immutable** incoming artifact… The occurrence identity is server-issued and addresses this arrival." The replacement then hands the *same* `OccurrenceId` to custody with different bytes and a different operation key (`EfDocumentRequestStore.cs:250-268`, `RetainIncomingArtifact.cs:352-364` → `CaseArtifactCustodyRequest.OccurrenceIdentity`). A's adapter is absent on this branch, so the behaviour of A04 when one occurrence identity is presented twice with different content **cannot be determined here** — it is either a duplicate-identity write or a same-identity dedupe that silently returns the first document while the page reports success. Neither is acceptable, and neither is provable on the C branch.

**Expected correction:** a replacement must not rewrite an existing occurrence. Insert a *new* occurrence row with a new server-issued `Id` and the new operation key, leave the replaced row's `CustodyState`/`DocumentId`/`DocumentVersionId` exactly as custody left them, and offer the new occurrence to custody under its own identity. If the replaced slot must be marked superseded, that needs a column on `PublicUploadOccurrences` — an A-owned schema request, which must be raised as an open question rather than worked around by mutation. If mutation was a deliberate decision, it is undocumented and must be recorded and cleared with A before this merges, because it inverts A's stated contract.

### R-2 (blocker) — The replacement read-modify-write has no concurrency guard on a row that carries no concurrency token

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:472-501`; `src/Pegasus.Infrastructure/Persistence/V1FoundationEntities.cs:286-298`

`PublicUploadOccurrenceEntity` has no `Version` and no `ConcurrencyToken` (unlike `PublicUploadSessionEntity`, `V1FoundationModelConfiguration.cs:126-130`). Two concurrent replacement POSTs for the same occurrence both read `ConfirmedCode` in separate transactions, both pass the check at `:487-491`, and both write. Last writer wins on `Sha256`/`ProposedName`/`Size`; then `TryClaimHandOverAsync` lets exactly one of them reach custody and the loser's `RequireSameContent` (`RetainIncomingArtifact.cs:311`) sees a row whose digest is the *other* file's and throws — surfaced to the sender as the generic "could not be retained". A conflict is being resolved by chance, which is AGENTS.md rule 11.

**Expected correction:** make the transition a conditional update whose WHERE names the expected prior state and digest — the same idiom as `TryClaimHandOverAsync` (`ExecuteUpdateAsync` over `Id == replacementId && CustodyState == ConfirmedCode`, rows-affected as the whole decision) — or, per R-1, insert a new row and take no lock at all.

### R-3 (blocker) — An exhausted link can never be finalized, and its public page 404s: a broken finalize path

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:318` (`link.Status != RequestUploadStatus.Active` in `FinalizeAsync`) and `:882` (the same test in `IGetRequestUpload.ExecuteAsync`)

`ApplyAcceptedTotalsAsync` sets `link.Status = RequestUploadStatus.Exhausted` the moment the accepted totals reach `MaximumFileCount` or `MaximumRequestBytes` (`:738-743`). `RequestUploadPolicy.Authorize` treats `Exhausted` as an ordinary, valid link state (`RequestUploadPolicy.cs:757`: `link.Status is not (Active or Exhausted)`), so it is reached by a sender doing exactly what the link invites. Once it is reached:

- `IGetRequestUpload.ExecuteAsync` returns `null`, so the public page 404s on GET (`Request.cshtml.cs:80-83`);
- even if the page were reachable, `FinalizeAsync` returns `Unavailable` → `OnPostFinalizeAsync` `_ => NotFound()` (`Request.cshtml.cs:258`).

A sender who uploads up to the permitted file count is therefore locked out with a 404 and can never press Finish. INTK-051 (plan line 1313): "after policy change return typed refusal/reissue, **never a broken finalize path**."

**Expected correction:** `FinalizeAsync` must accept `Status is Active or Exhausted` (and `IGetRequestUpload.ExecuteAsync` must keep serving an exhausted link in a read/finalize-only shape) so the last permitted file can still be finished. Better: derive both from `RequestUploadPolicy`/`ToUploadLink` rather than restating the rule — see R-8.

### R-4 (blocker) — Finalization is not serialized against a concurrent arrival: bytes can land in a finalized session

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:294-366` (`FinalizeAsync`) against `:381-577` (`AuthorizeAndRecordArrivalAsync`)

`FinalizeAsync` checks "no occurrence is unconfirmed" (`:345-352`) and then writes `FinalizedAtUtc` (`:362-365`). `AuthorizeAndRecordArrivalAsync` reads the session, finds `AcceptsBytes` true, and **inserts** a new occurrence row in a different transaction (`:544-558`). Neither transaction takes the link's `UPDLOCK` (`LockLinkAsync`, `:766-782`, is used only by `RecordAcceptedAsync`), and the session row's concurrency token protects only concurrent *updates to that row* — an insert of a sibling occurrence conflicts with nothing. Under READ COMMITTED both commit: the session is finalized while an arrival is in flight, and the arrival then proceeds through custody and is counted by `ApplyAcceptedTotalsAsync`.

Result: a file is accepted into a finalized session, which is precisely what C07 item 6 forbids ("Finalized or expired sessions refuse later bytes") and what the sender is told is impossible. The single-caller test at `PublicUploadRetentionWebTests.cs:1296` cannot see this.

**Expected correction:** take `LockLinkAsync` as the first statement of both `FinalizeAsync` and `AuthorizeAndRecordArrivalAsync`, so an arrival and a finalization serialize on the link row the way two accepted arrivals already do. (Bumping `session.Version` inside the arrival transaction is an alternative but weaker fix: it makes the session row the contended row and would turn the race into a `DbUpdateConcurrencyException` the arrival path does not currently handle.)

---

## Majors

### R-5 (major) — Replacement silently un-counts the replaced file, so the per-link limits stop bounding what custody holds

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:493-500` with `:715-748` (`ApplyAcceptedTotalsAsync`)

The link's accepted totals are *derived* from the session's occurrence rows (deliberately, `:697-708`). Because a replacement overwrites `Size`/`Sha256` on the existing row instead of adding one, the replaced file's bytes and count vanish from the totals — while the document A04 created for it remains in the Case (R-1). The new test asserts exactly this and treats it as correct (`PublicUploadRetentionWebTests.cs:1338`: `Assert.Equal((1, replacement.LongLength), …)`).

On an anonymous endpoint that means `MaximumFileCount` / `MaximumRequestBytes` no longer bound the bytes one public link can push into custody: repeated replacement stores unbounded documents against a link that reports one file. `RequestUploadAttemptLimiter` caps attempts per window, not cumulative bytes over the 15-minute session. C07 item 5 makes `IntakeEnvelopeLimits` the single channel-limit owner and says configuration "may tighten but never raise Core limits" — this raises them in practice.

**Expected correction:** whatever replacement shape is chosen in R-1, the derived totals must count every set of bytes custody was asked to retain and still holds. If a replacement is meant to release the superseded bytes from the total, that requires an explicit custody removal, not the disappearance of the row that recorded them.

### R-6 (major) — Replacement is not replay-safe while custody has not answered

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:487-491`

The second POST of one replacement (double submit, browser retry, a lost response) finds the occurrence already moved to `ArrivedCode`, fails `occurrence.CustodyState != ConfirmedCode`, and returns `Unavailable` → the page returns 404 (`Request.cshtml.cs:201-202`) on a link that is perfectly alive. The receipt-replay branch in `Authorize` only rescues the case where the replacement already confirmed *and* earned a receipt. C07 item 6 requires add/replace/finalize to be replay-safe, and `Unavailable` is specifically the refusal reserved for "the link itself is gone" (`RequestUploadPolicy.cs:33-36`).

**Expected correction:** a repeated replacement of the same occurrence with the same digest must reconcile the in-flight arrival (the `NotRetained` / same-operation-key retry path), never `Unavailable`. Refuse `Unavailable` only when the occurrence id names nothing in this session.

### R-7 (major) — The public view drops every non-confirmed occurrence and hardcodes the custody state, so Pending/Failed/Unknown are never rendered

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:911-923`; `src/Pegasus.Core/Documents/RequestUploadPolicy.cs:502-505`

The projection filters `CustodyState == ConfirmedCode` and then supplies the literal `IncomingArtifactCustodyState.Confirmed` for every row, so `RequestUploadOccurrenceView.CustodyState` is a constant and the page has no way to show anything else. C07 item 7 requires "Persist and render `Pending`, `Confirmed`, `Failed` or `Unknown`", and a sender whose file is Pending or Failed sees an empty list. This compounds R-3-adjacent behaviour: a failed occurrence blocks Finish through `FinalizeAsync`'s `NotRetained` check (`:345-352`) with the message "A document is still being stored. Try again." while being invisible on the page — the sender is blocked by a row they cannot see and cannot act on.

**Expected correction:** project every occurrence with its real `ParseCustodyState(value.CustodyState)`, render the non-confirmed ones with their own typed wording, and make the Finish refusal name the state that is blocking it. If a `Failed` occurrence is terminal, finalization must be able to proceed past it (or the sender must be able to discard it) rather than dead-ending until expiry.

### R-8 (major) — `FinalizeAsync` restates the link-validity rule instead of reusing the policy, and diverges from it

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:313-327`

The four link checks (`Status`, `RevokedAtUtc`, `ExpiresAtUtc`, `LimitsVersion`) are a second copy of what `RequestUploadPolicy.Authorize` already decides (`RequestUploadPolicy.cs:750-764`), reachable through the existing `ToUploadLink` mapper the upload path uses (`EfDocumentRequestStore.cs:1091-1105`). It has already diverged: it omits `HasAcceptedLifetime` and it treats `Exhausted` as invalid where the policy treats it as valid — which is R-3. AGENTS.md rules 7 (reuse before build) and 8 (one list per concept).

**Expected correction:** call `uploadPolicy` (or a small policy method such as `MayFinalize(link)`) from `FinalizeAsync` so the link-validity rule has exactly one definition.

### R-9 (major) — The unnamed `OnPostAsync` is retained as a compatibility entry point and is the one every pre-existing test drives

`src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs:99-100`; `src/Pegasus.Web/Pages/Uploads/Request.cshtml:26,49,74`; `tests/Pegasus.IntegrationTests/PublicUploadRetentionWebTests.cs:1599`

Every form the page now renders posts `?handler=Upload`. `OnPostAsync` survives only because `PostEvidenceAsync` posts to `/Uploads/{token}` with no handler — so roughly twenty pre-existing web tests exercise a route the production page never uses, and the route the page does use is exercised by exactly one new test. AGENTS.md rule 6: "add no fallback or compatibility path; delete what you replace."

**Expected correction:** delete `OnPostAsync` and change `PostEvidenceAsync` to post `$"/Uploads/{token}?handler=Upload"`, so the suite drives the handler the page actually targets.

### R-10 (major) — The typed limits-version refusal never reaches the sender

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:885-886`; `src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs:241-255`

`IGetRequestUpload.ExecuteAsync` returns `null` when `link.LimitsVersion != uploadLimits.Version`, and both POST handlers read the view first and `NotFound()` on null. So the store's typed `LimitsVersionMismatch` (correctly returned, correctly carrying `MayReissue: true` at `RequestUploadPolicy.cs:753`) can only be rendered in the vanishingly narrow window where the deployed limits version changes between the GET and the POST. The sender's actual experience of a limits-version change is a bare 404. The new `LimitsVersionMismatch` arm in `OnPostFinalizeAsync` (`:254-255`) is dead for the same reason (rule 14, "done means wired"). INTK-051 requires a typed refusal/reissue after a policy change.

**Expected correction:** let the public view survive a limits-version mismatch in a refusal-only shape so the page can render "This link is no longer valid. Ask for a new one." — or state explicitly (as an ASSUMPTION on the ticket) that a 404 is the accepted rendering and delete the two unreachable arms. Do not leave both.

### R-11 (major) — The new tests prove the happy path and leave the slice's own risky paths unproven

`tests/Pegasus.IntegrationTests/PublicUploadRetentionWebTests.cs:1296-1379`

No assertion was weakened or deleted relative to the previous versions — I compared the diff and the surviving file; the new test is purely additive, and the pre-existing policy-level suite (`PublicUploadSessionTests.cs:29-106`, untouched by this slice) already proves the 15-minute window, non-extension, replay-safe finalize, expired-refuses and mismatch/`MayReissue` at policy level, while `PublicUploadRetentionWebTests.cs:244-246,295-296,339,483-484,593` already prove the window opens at exactly `Now.Add(Window)` at store level and that failed/pending/uncertain hand-overs never start it. The tests do use the documented recording custody double (`WithRetention`, `:1508-1527`), not live storage. Good.

What is not proven anywhere, all of it inside this slice's own new behaviour:

1. The store/HTTP path refuses bytes **after expiry** (only the pure policy does; `IntakeWebApplicationFactory(TimeProvider)` exists, so an advancing clock is available).
2. `FinalizeAsync` returns `NotRetained` when an occurrence is Pending/Failed/Arrived, and what the sender can do next.
3. Finalize on an **exhausted** link (R-3).
4. Replacement naming an occurrence in another link's session → refusal.
5. Replacement retry / replay while custody has not answered (R-6).
6. Concurrent arrival vs finalization (R-4) — `TwoSimultaneousSubmissionsOfOneOperationKey…` (`:711`) shows the suite can express this.
7. The refused post-finalization upload asserts only `NotFound`; it never asserts that **nothing was written** (no new occurrence row, totals unchanged), so a refusal that still committed an arrival would pass.
8. `fixedExpiry` is read at `:1305-1311` and compared to itself at `:1341-1343`; the test never asserts it equals `StartedAtUtc + 15 minutes`, so the 15-minute value is not re-proved on the replacement path.

**Expected correction:** add the cases above, and strengthen the post-finalization refusal to assert the occurrence count and link totals are unchanged.

---

## Minors

- **R-12** `EfDocumentRequestStore.cs:353-361` — `catch (InvalidOperationException) → Unavailable` around `PublicUploadSessionPolicy.Finalize`. This is **not** masking an invariant failure (rule 12 is satisfied): the try block contains only `ToSession` and `Finalize`, and the only `InvalidOperationException` reachable there is the documented "Only an open submission session can be finalized" for the NotStarted/Expired states, which are ordinary and correctly answered with a non-disclosing refusal. It is nonetheless exception-as-control-flow for an expected state, in a codebase that argues explicitly against it (`RequestUploadPolicy.cs:745-748`). **Correction:** call `PublicUploadSessionPolicy.Evaluate` first, return the typed refusal for NotStarted/Expired, and let any `InvalidOperationException` surface.
- **R-13** `Request.cshtml.cs:250-253` — the two `Accepted` arms are identical; `result.IsReplay` is computed and discarded. Collapse to one arm or make the replay say something different.
- **R-14** `Request.cshtml.cs:255` duplicates the sentence already at `:193` verbatim, and `:257` adds a third inline sender-facing string, while `OperatorLabels.cs` was open in this same slice. Rule 8. **Correction:** put both new sentences in `OperatorLabels.Upload` beside `RequestReplace`/`RequestFinish`, or at minimum share one private const between the two handlers.
- **R-15** `RequestUploadPolicy.cs:491-500` — `Occurrences` (nullable ctor parameter) plus a computed `Files` is two names for one concept. Keep one.
- **R-16** `Request.cshtml:49,72` — in the `Finalized` and `Expired` states the page renders the limits and the confirmed file list with no controls and **no explanation**. Refusing without Case disclosure does not require silence. Add a typed "this submission is closed" line.
- **R-17** `Request.cshtml:39,41` — the `<label>` and the submit button both read "Replace" and the label does not name the file it belongs to; with several files, every replace control is announced identically. Name the file in the label.
- **R-18** `FinalizeAsync` has no rate limiting; `RequestUploadAttemptLimiter` guards only the upload handler. Each anonymous Finish POST opens a transaction and runs four queries. Low severity, worth a cheap guard.

## Answers to the dispatch's lenses

- **Refusal ordering** is correct as implemented on the upload path: token/link existence → `Authorize` (limits version → lifetime/status/revoked/expiry → rate limit → operation key → receipt replay/conflict → limit exceeded → media/name/size) → key length → Case archived/terminal → session limits version → session `AcceptsBytes` (finalized/expired) → occurrence slot. One consequence worth recording, not a defect: a replay of an already-receipted operation key against a *finalized* session still returns `Replay`/"received and retained securely" because the receipt branch precedes the session check. That is right — no bytes are taken — but it is untested.
- **Replacement addressed by server-issued id** — yes, and correctly refused when the id names another session's occurrence (`:482-491`, filtered on `SessionId`). The addressing is sound; the mutation it performs is not (R-1/R-2/R-5/R-6).
- **Information disclosure** — clean. The public page carries no request reference, no Case identity, no expiry timestamp and no operator wording; only the sender's own file names, the session state and server-issued occurrence ids, all scoped by the token's link. `Unavailable`/404 is used consistently for every state that would otherwise hint at the Case. `[AllowAnonymous]` + `NoStore` are unchanged. Razor encodes the rendered file names, which are the policy's `SafeFileName`.
- **Labels** — `OperatorLabels.Upload.RequestReplace` / `RequestFinish` are correctly placed in the existing `Upload` group and used by the page. The other `Replace` constant (`OperatorLabels.cs:1639`) belongs to a different surface's list, so this is not a duplicate list. Clean, apart from R-14's inline sentences.
- **Scope** — all eight files are within C's ownership (C-owned Core policy, adapter/store methods, C-owned Razor, C-owned labels, C-owned tests). No A-owned schema, migration, snapshot or DI file was touched; the `QdosCustodialWebTests` addition is the minimum test-double conformance for the widened `IUploadToRequest` interface. No rule-1 overreach found.

## Test names whose execution in the combined A+C tree is required before acceptance

Existing, must pass:

1. `PublicUploadRetentionWebTests.PublicPageAddsReplacesFinalizesAndRefusesLaterBytes` (the new one)
2. `PublicUploadRetentionWebTests.AConfirmedHandOverOpensTheFixedWindowAndRecordsTheBoxIdentities`
3. `PublicUploadRetentionWebTests.APendingHandOverIsAcceptedWithNoRemoteIdentityAndNoOpenWindow`
4. `PublicUploadRetentionWebTests.ARefusedOrUncertainHandOverIsNeverAcceptedAndNeverCounted` (all theory cases)
5. `PublicUploadRetentionWebTests.ReplayOfTheSameOperationKeyReturnsTheSameDocumentAndCallsCustodyOnce`
6. `PublicUploadRetentionWebTests.AReplayedArrivalThatEarnedNoReceiptIsStillCountedExactlyOnce`
7. `PublicUploadRetentionWebTests.ASecondDifferentFileUnderAnUnresolvedKeyBecomesItsOwnSubmission`
8. `PublicUploadRetentionWebTests.TwoSimultaneousSubmissionsOfOneOperationKeyConvergeOnOneIntent`
9. `PublicUploadRetentionWebTests.APendingArrivalIsReconciledByItsOwnSenderAndNeverReOffered`
10. `PublicUploadRetentionWebTests.ARecordThatFailsAfterCustodyAcceptedIsRecoveredByTheOriginalKey`
11. `PublicUploadRetentionWebTests.CustodyRefusesEveryAuthorityThatIsNotThisExactActiveLink`
12. `PublicUploadRetentionWebTests.AnActiveLinkCannotReadAnotherActiveLinksAcceptedVersionOnTheSameCase`
13. `PublicUploadSessionTests` — the whole class (policy-level window, non-extension, replay-safe finalize, expiry, mismatch/`MayReissue`)
14. `QdosCustodialWebTests` — the whole class (interface-conformance double)

Required to be **added** and passing before acceptance (R-11), because they are the claims this slice makes and nothing currently proves:

15. finalize refuses (`NotRetained`) while an occurrence is Pending/Failed, and the sender is told which
16. finalize succeeds on an `Exhausted` link, and the page for an exhausted link is still reachable (R-3)
17. an upload after the 15-minute window has elapsed is refused **through the store**, with an advancing `TimeProvider`
18. a replacement naming an occurrence from another link's session is refused
19. a replayed replacement while custody is still Pending reconciles rather than 404s (R-6)
20. a replacement leaves the superseded occurrence's confirmed state, `DocumentId` and `DocumentVersionId` intact, and the link totals still count both sets of bytes (R-1/R-5) — this one requires A's real `EfCaseArtifactCustody`, so it is the single most important combined-tree test
21. a finalize concurrent with an in-flight arrival ends with exactly one of "the arrival is refused" or "the finalization is refused" — never both committed (R-4)
22. the post-finalization refused upload writes no occurrence row and does not change the link totals

## Residual observations

- **B01 Recipient/Reason (PR 670 handoff) — NOT as handed off; report only, do not fix here.** At HEAD, `CreateRequestUploadLinkCommand` and `RequestUploadLink` (`RequestUploadPolicy.cs:339-346` and `:353-366`) carry `string? Recipient = null` and `string? Reason = null`, and `NormalizeMetadata` (`RequestUploadPolicy.cs:845-866`) returns `null` for a null input rather than rejecting it. The bounds are right (Recipient ≤ 500, Reason ≤ 1000, blank rejected, trimmed, replay-compared exactly at `EfDocumentRequestStore.cs:76-82`), and `EfDocumentRequestStore.cs:1103-1104` re-validates on read. But **Recipient is optional, not required** — the branch carries commit `d2afed1a2` "Share optional upload request metadata and Core normalization", and `PublicUploadRetentionWebTests.RequestCreationPersistsAndReplaysOmittedOptionalMetadata` (`:78`) asserts the optional behaviour. This slice does not touch it and preserves it faithfully; whether "required Recipient" was superseded or lost needs a decision from the controller / Stream B.
- **For Stream A.** Two questions this slice raises that only A can answer: (a) what `ICaseArtifactCustody.RetainAsync` does when one `OccurrenceIdentity` is presented a second time with a different `Sha256` and a different operation key (R-1) — if A dedupes on occurrence identity, the replacement bytes are silently dropped; (b) whether `PublicUploadOccurrences` should gain a superseded-by column so a replacement can be recorded immutably instead of overwriting (R-1). Both are blocking for the replacement path.
- **For Stream B.** Nothing in this slice touches Case-side link create/revoke; the C08 route-matrix boundary is respected. If R-10 is resolved by rendering the typed refusal, B's "create a new link only on explicit staff action" path is the counterpart and should be confirmed present.
- **Build/test status.** Not re-run by this review (the controller's read-only constraint, and the Integration project does not compile on the C branch). The author's "Web Release build PASS, 0 warnings" claim is **unverified** here, as is every assertion in the two test files. This verdict is source-only and must not be read as evidence that any test passes.

# C07 correction round 1 (+ round 1a) — independent review

- **Bound head:** `ba8ccd79e1148fe51f7780eb404193c21090f2e5`
- **Commits reviewed:** `324cf08f8`, `64cc0e90e`, `3a13a6e3d`, `ba8ccd79e` (`git diff aa5e669d7..ba8ccd79e`, 7 files, +1020/-175)
- **Base:** `aa5e669d76ad2f7cc24783f8076644c439509feb` (contains the original slice `df198034a`)
- **Branch / worktree:** `c07-retention-caller` — `C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c07b` (toplevel and `--git-common-dir` verified against the primary `.git`; tree clean at review)
- **Execution bound to:** wave 39 — `C:\Users\PGUSER\AppData\Local\Temp\claude\C--Users-PGUSER-documents-github-pegasus\5adc2fb3-f15d-4145-84ed-948eb9fde4e4\scratchpad\takeover\wave39-tests\` — solution build 0W/0E; Core `RequestUpload|PublicUploadSession|RetainIncomingArtifact|IntakeEnvelopeLimits` 36/0/0; browser upload 7/0/0; the five web/custody classes 56 PASS / 1 FAIL. Wave 39 covers head `3a13a6e3d` only. **Head `ba8ccd79e` is unexecuted; its combined run is wave 42, pending.**
- **Full attestation:** `C:\Users\PGUSER\AppData\Local\Temp\claude\C--Users-PGUSER-documents-github-pegasus\5adc2fb3-f15d-4145-84ed-948eb9fde4e4\scratchpad\takeover\c07c-r1-review.md`
- **Reviewer:** independent of the implementer. No git write, no file edit, no test execution, no branch publication, no ticket move, no merge.
- **Authority:** plan `streams/C-intake.md` §C07 items 5–7, §C08 route matrix "Public upload", residual INTK-051; `AGENTS.md` conduct 6/7/8/12/14/19/22/24; the predecessor attestation `c07c-review.md`; the controller's R-1 split and its Finish / `Unavailable` / `OperationConflict` rulings.

## Verdict

**PASS pending the named corrections** — 0 blockers, 4 majors, 10 minors.

R-1a, R-2, R-3, R-4, R-5, R-6, R-7, R-8, R-9, R-10, R-12…R-18 are all genuinely fixed, at the altitude each finding asked for rather than patched at the call site. `RefuseLink` is a real single definition of link validity; the replacement is a real new row that never touches the one it supersedes; the projection carries real custody states; the compatibility handler is gone with the whole suite moved onto the production route; and `ba8ccd79e` fixes the wave-39 failure at its real cause with one owner for the rule. Nothing in either round is scope creep, and no assertion was weakened anywhere.

What remains is not a defect in shipped behaviour: two majors are proof gaps, one is unwired Core code that re-splits the concept R-8 had just unified, one is a product decision the controller owns. Two recorded claims in the test suite are now narrower than the behaviour and must be amended in this round.

**Bound to `ba8ccd79e` on source. Not evidence that any test passes at that head: wave 42 has not run.**

## Round 1a — `ba8ccd79e`, the four questions

The diagnosis is right, and it is the one I reached independently before this commit arrived. The earlier filter (`ConfirmedCode || PendingCode`) already excluded `Failed`; the observed `(2, 77)` was written while the second occurrence was **Pending** — correctly counted — and nothing recomputed it, because `ApplyAcceptedTotalsAsync` had exactly one caller, `RecordAcceptedAsync`. The controller's stated cause ("the derived link totals counted a custody-refused (terminal Failed) occurrence") was wrong, and a membership change alone would have fixed nothing: the row was already `failed` and already excluded. The author's correction of the controller on this point should be accepted and the run record amended.

**(a) One owner of the rule? — Yes.** `ApplyAcceptedTotalsAsync` (`EfDocumentRequestStore.cs:877-908`) is still the single method that decides a link's totals, now with exactly two callers: `RecordAcceptedAsync:834` and `FinalizeAsync:408`. The counted set moved from an inline boolean into one named, documented set `RetainedOrInFlightCodes` (`:1387-1390`), derived from the enum with `Failed` named as the single exclusion — the same idiom as `ForwardSourceCodes` (`:1473-1480`), which is the right precedent. Verified it cannot throw or duplicate: the enum has exactly four members (`RetainIncomingArtifact.cs:11-28`), `ToCode` (`:1595-1602`) covers all four, `ArrivedCode` is not among them, so the set is exactly `{arrived, pending, confirmed, unknown}`. The remark also gets the layering right and says so: the retention port writes the refusal and owns no link, so the rule stays in the store rather than gaining a second home there. Agreed.

**(b) Counting `Arrived` / `Unknown`? — `Unknown` yes; `Arrived` no, and its justification does not hold.**

`Unknown`: accept. Custody may hold those bytes; a limit that bounds what a public link pushes into custody must count what custody may be holding, and the conservative direction is the safe one. I withdraw the objection I would otherwise have raised, on the condition in (d).

`Arrived`: recommend against. The remark defends it as *"a limit that ignored it would let a link exceed itself with everything in flight"*. That hole is real but **this change does not close it**: the limit is enforced by `Authorize` against the **stored** `link.AcceptedFileCount`/`AcceptedByteCount` (`RequestUploadPolicy.cs:814` → `AcceptsMoreFiles:887-894`), and those columns are only written by `ApplyAcceptedTotalsAsync`, which does **not** run in `AuthorizeAndRecordArrivalAsync`. At the moment an `arrived` row is committed (`:566-580`, `:692-706`) the link's columns are unchanged, so the next arrival's limit check still reads the old numbers. What it does change is the cost of a failure: an arrival committed and never answered — a crash before hand-over leaves `arrived`, a dead claimed hand-over leaves `unknown` — now consumes a file slot and its bytes for the life of the session, with no release short of a staff reconciliation recording `Failed`. On a five-file public link, two dropped POSTs cost the sender 40% of the link.

*Correction — pick one, do not leave both unstated:* (1) drop `ArrivedCode` from the set, restoring "nothing is counted before custody answers", and amend the remark to say in-flight arrivals are bounded by the link `UPDLOCK` and the rate limiter instead; or (2) keep it and make the justification true — call `ApplyAcceptedTotalsAsync` inside `AuthorizeAndRecordArrivalAsync`'s transaction (the link is already locked there since R-4) — and then say what releases an abandoned `arrived` row. Option 1 is smaller and is what I recommend for this round.

**(c) Does the test prove both halves? — Yes, and the intermediate state is pinned.** The new block at `PublicUploadRetentionWebTests.cs:1387-1395` asserts `(2, Evidence.LongLength + OtherEvidence.LongLength)` immediately after the Pending upload — this is the half that was previously invisible, and it is what makes the final assertion mean anything: without it, `(1, 32)` at the end would also be consistent with the Pending never having been counted. The final assertion `:1451-1453` proves the refused one is excluded at finalization, with the comment corrected from "was never counted" to "closing the submission re-derives the totals". The two together prove the transition, not just the endpoints. One caveat for the round-2 report, not a correction: the Pending→Failed flip is a direct `ExecuteUpdateAsync` (`:1424-1431`), so the test proves the derivation, not that a production path produces that transition — the production path is `EfPublicUploadRetentionStore.RecordAsync` via staff reconciliation, covered separately by `AStaffReconciliationRecordsCustodysRefusalOfAPendingArrival:571`. The seam between them is simulated.

**(d) Is the ASSUMPTION 6 amendment required this round? — Yes, and a second one with it.**

`:611-615` still states a **general** rule — "the totals are recomputed … the next time an arrival is accepted, and this refusal is not one" — and the general rule is now wrong: they are also recomputed at finalization. The test's assertion stays correct because it never finalizes, which is exactly why the sentence will be believed and exactly why it must be fixed now. Same drift R-8 was raised for; rule 22's "a convention change lands in the same PR as the work that needs it" applies, and ASSUMPTION 6 is a recorded assumption, which rule 22 forbids silencing.

**A second comment is owed the same treatment and the controller's message does not mention it.** `ARefusedOrUncertainHandOverIsNeverAcceptedAndNeverCounted` (`:313-350`) is a `[Theory]` over `Failed` **and `Unknown`**, and its name and doc claim "nothing is counted against the link". After `ba8ccd79e` that is false for `Unknown`: an uncertain occurrence is now in `RetainedOrInFlightCodes` and will be counted at the next derivation. It still passes only because an uncertain hand-over never reaches `RecordAcceptedAsync` (`:282-295` returns `NotRetained` with no version id), so no derivation runs inside it. The assertion is correct at that instant; the claim in the name is not.

*Correction (comments and one name only, no assertion changes):* (1) `:611-615` — the totals are re-derived on every accepted arrival **and at finalization**, and this refusal is neither, so the slot is still held at this instant; keep the ASSUMPTION 6 reference and note finalization now settles it. (2) `:313-316` and the method name — narrow to what is proved: never **accepted**, and not counted **at the time of the hand-over**; add a clause saying an uncertain occurrence goes on counting until custody answers and a refused one never does.

## Majors

- **F-1 — head `ba8ccd79e` is unexecuted.** Wave 39 bound `3a13a6e3d`. The commit changes the counting rule for every public upload path. Four green tests depend on the counted set and none has been re-run: `ARefusedOrUncertainHandOverIsNeverAcceptedAndNeverCounted` (both theory cases), `AStaffReconciliationRecordsCustodysRefusalOfAPendingArrival`, `AThrownHandOverIsAskedAboutAndThenReOfferedUnderTheSameKeyOnce`, `APendingHandOverIsAcceptedWithNoRemoteIdentityAndNoOpenWindow`. I traced each by hand and expect all four green, but that is source reasoning, not a run. **No acceptance until wave 42 reports.**
- **F-2 — R-10 implemented but unexecuted.** `EfDocumentRequestStore.cs:1038-1053`, `Request.cshtml:30-38`, `Request.cshtml.cs:167`/`:238`. `grep -rn "RequestLinkInvalid|Refusal" tests/` returns nothing for this surface. `WithoutAcceptedLimitsTheSubmissionRefusesAndWritesNothing:1258` does **not** cover it — with `AcceptedLimitsVersion` empty the link and limits carry the same version, so `RefuseLink` refuses on `HasAcceptedLifetime` (`RequestUploadPolicy.cs:898-913`) and still 404s, which is why it stayed green. `PublicUploadSessionTests:109` is policy-level only, which is what R-10 said was insufficient. Rule 14. *Correction:* one integration test — seed a link, rebuild the factory with a **different, non-empty** `DocumentRequests:AcceptedLimitsVersion`, then assert GET **200** containing `OperatorLabels.Upload.RequestLinkInvalid`, no dropzone and no Finish form, POST `?handler=Upload` 200 with the same sentence writing no occurrence, POST `?handler=Finalize` 200 with the same sentence.
- **F-3 — `RequestUploadOccurrenceView.IsUnresolved` has no caller and re-splits the concept R-8 just unified.** `RequestUploadPolicy.cs:534-540` against `EfDocumentRequestStore.cs:1370`. Referenced by nothing in `src/` or `tests/` (grep across both trees). The Razor asks `== Confirmed` (`Request.cshtml:53`); the store asks `UnresolvedCodes.Contains(...)` (`:375`, `:554`, `:1062`). Two definitions that agree today — exactly how the R-8 divergence began. Rules 8 and 14. *Correction:* delete it, or make it the definition (render on `!file.IsUnresolved`, derive `UnresolvedCodes` from it the way `ForwardSourceCodes` and `RetainedOrInFlightCodes` are derived). Do not leave both.
- **F-4 (controller decision) — a replacement is impossible on an exhausted link, the state it is most needed in.** `RequestUploadPolicy.cs:814`/`:887-894`; `Request.cshtml:51-53`. `Authorize` refuses `LimitExceeded` when `!AcceptsMoreFiles`, and a replacement passes through `Authorize` before reaching `ReplaceAsync` (`:438-451`); the page removes the replace control under the same condition. So once the link reaches its limits — the state R-3 established a sender reaches "by doing exactly what the link invited" — the sender can neither add nor correct a file, only Finish with the wrong document in the Case. **Item 5** makes counting both rows right (it is the only accounting that keeps the limits bounding what custody holds — R-5's whole point), so consuming a slot is the honest consequence. **Item 6** says replacements "are allowed until explicit replay-safe finalization or expiry", and the implementation makes them not allowed in a state reachable well before either — INTK-051's "never a broken path" applied to replace. *Options, my order of preference:* (1) accept for v1, record an ASSUMPTION, and add one test asserting an exhausted link refuses a replacement with `LimitExceeded` (unproven in either direction today); (2) let a replacement through on an exhausted link with `MaximumRequestBytes` still binding; (3) defer to R-1b — once `ReplacesOccurrenceId` exists, exclude superseded rows so a replacement costs nothing. The author's "replaces three times has used four of five" is accurate and must not be left as an unanswered aside.

## Minors

- **F-5** `EfDocumentRequestStore.cs:1370` — `UnresolvedCodes` still hand-listed while both neighbours in the same class are enum-derived. Derive it too.
- **F-6** `:888-893` — `ApplyAcceptedTotalsAsync` sets `Exhausted` and never unsets it. Now that a re-derivation can **lower** totals, a link whose only over-limit file was refused stays exhausted for ever. Moot at the finalization call site but not stated to be. Add the symmetric arm or say why exhaustion is one-way.
- **F-7** `:894-907` — the same call bumps `link.Version` and runs `CaseMutationGuard.Complete`, so a finalization can now complete the Case workflow. Confirm intended, or split the pure re-derivation from the workflow bump.
- **F-8** `:371-398` — `FinalizeAsync` runs the blocking-occurrence query **before** `PublicUploadSessionPolicy.Evaluate`, so an expired session holding a Pending occurrence answers `NotRetained` / "still being stored, try again in a moment" for ever instead of `Unavailable`. Unreachable through the page, reachable through `IUploadToRequest`. Move the `Evaluate(...) != Open` check above it.
- **F-9** `Request.cshtml:53` offers Replace only for `Confirmed`, while `ReplaceAsync:670-671` also accepts `FailedCode` — the store accepts a transition the UI never offers, and the sender whose file custody refused has no in-page remedy at that slot. Render Replace for `Failed` too, or drop `FailedCode`.
- **F-10** `:918-932` — `LockLinkAsync` degrades to a plain `SingleAsync` off SQL Server, so `AFinalizationRacing…` proves the **ordering** (occurrence committed before hand-over), not the lock; R-4's serialization is itself unexecuted. Separately `FinalizeAsync` resolves the link id in one context (`:310-325`) then `SingleAsync`es it in another, so a vanished row throws `InvalidOperationException` out of an anonymous handler — use `SingleOrDefaultAsync` + `Unavailable`.
- **F-11** `Request.cshtml:57` — the replace form carries the page's `OperationKey`, which is the **unresolved** key of another file whenever one exists; `ReplaceAsync` then finds that file's row with a different digest and refuses `OperationConflict` (`:707-713`) telling the sender to reload, and reloading returns the same key. Pre-existing, but replace is now the only path through it. Mint a fresh key per replace control, or say why the sender must wait.
- **F-12** `Request.cshtml.cs:220-229` — `OnPostFinalizeAsync` reads the public view before acquiring the limiter, so R-18's guard does not protect the work it was added for; and Finish consuming an upload slot means a sender at the rate limit can neither upload nor finish. Acquire the limiter first.
- **F-13** `PublicUploadRetentionWebTests.cs:1470` — `const int MaximumFileCount = 5` restates a configured fixture value (fails safe, but a second copy). Read it from the fixture's `RequestUploadLimits`.
- **F-14** `RequestUploadPolicy.cs:528-531` — `Files` is now an explicit `init` property shadowing the positional parameter, so `view with { Files = null }` yields a null list where the old computed property could not. Nullable parameter + non-null accessor, or drop the `init` setter.

## R-2…R-18 dispositions verified

R-2 correct and the interim guard is **fully removed** — `git diff 64cc0e90e..3a13a6e3d` deletes the conditional-update block, its `moved != 1` arm and the remark; **no dead code remains**. R-3/R-8: `RefuseLink` (`RequestUploadPolicy.cs:857-877`) is the single definition with `Exhausted` valid at `:872`, `AcceptsMoreFiles` (`:887-894`) the separate question, three callers (`:783`, `:346`, `:1038`), proved end to end by `AnExhaustedLinkStillServesItsPageAndCanStillBeFinished`. R-4: `LockLinkAsync` first in `FinalizeAsync:335` and `AuthorizeAndRecordArrivalAsync:431`, matching `RecordAcceptedAsync:782` — one resource, one order, no deadlock. R-6 matches the controller's ruling exactly: `Unavailable` only outside the session (`:664-672`), unanswered addressed slot → `OperationConflict` (`:673-680`), same key different bytes → `OperationConflict` (`:707-713`). R-7: every occurrence projected through `ParseCustodyState` (`:1084-1101`); only `UnresolvedCodes` block (`:371-394`); `BlockingState` → `RequestNotFinished(state)`; terminal `Failed` renders "Not accepted", never counted, never traps. R-9: `OnPostAsync` deleted (it was a pure delegation at base `aa5e669d7:99-100`); every `/Uploads/**` POST now carries `?handler=Upload` (`:2064`, `:1275`, `QdosCustodialWebTests:114`/`:126`); grep finds no unnamed one left. R-12 through R-18 all correct as described (`Evaluate` first with the try/catch gone; one `Accepted` arm; eight labels in `OperatorLabels.Upload:1724-1791` all wired; one name `Files`; `RequestSessionClosed`/`RequestNoMoreFiles`; `RequestReplaceFile(fileName)` on label and `aria-label`; the existing limiter on Finish).

**R-1a verified whole** (`:615-722`): new row with `Guid.NewGuid()`, its own scoped key, `ArrivedCode`; custody offered the **new** identity (`:715-722`), so `CaseDocuments (CaseId, SourceOccurrenceIdentity)` gets a distinct value — the true root cause of the R-0 failure, correctly diagnosed. The superseded row is read `AsNoTracking` (`:657-661`) and never written on any path. Replay of the replacement finds the committed arrival, writes nothing, reconciles under the same key. Exactly one `// round 2 (R-1b):` marker, in the new-row initializer (`:588-593`), no TODO anywhere.

## R-11 — the tests prove what they claim

All six new cases plus the corrected one read assertion by assertion: the window closed through the **store** against an advancing clock with `Now.AddMinutes(15)` asserted; Finish naming the blocking state **in the rendered page** (the sentence, not only the decision); the exhausted link serving 200 with no dropzone and no `ReplacementOccurrenceId` and still finishing; a cross-link replacement refused `Unavailable` with the victim row intact and the stranger's totals `(0,0)`; the finalize/arrival race refusing with `BlockingState == Unknown` and the post-finalization upload writing **nothing** (one occurrence, one receipt, unchanged totals); and the unanswered-slot replacement refused `OperationConflict` with the in-flight row byte-identical. The corrected `PublicPageAdds…` went from three assertions to twelve, every one a strengthening. **No assertion weakened anywhere:** the whole `tests/` diff across four commits is additive except the two strengthened assertions, one corrected comment, and the `?handler=Upload` route. **The ~20 pre-existing tests still assert the same outcomes** — the deleted `OnPostAsync` was a pure one-line delegation, so the new route reaches byte-identical code; wave 39's 56/57 confirms this at `3a13a6e3d`. R-11 (8) is satisfied by cases 1 and 3 rather than in the replacement test, where `fixedExpiry` is still compared to itself (`:1791`) — acceptable, the 15-minute value is now proved twice against the clock.

## AGENTS.md and scope

Rule 6 PASS (compatibility handler deleted, suite moved in the same round). Rule 7 PASS. Rule 8 — one violation (F-3), one nit (F-5); everything else consolidates. Rule 12 PASS (store try/catch gone; `OnPostFinalizeAsync` has no catch, so a real `InvalidOperationException` surfaces). Rule 14 — one violation (F-3) and one at risk (F-2); every other new member has a named production caller. Rule 19 PASS — nothing weakened, and the failing test moved the code rather than the code moving the test. Rule 22 PASS, with the two comment amendments of §(d) owed in this round; R-5's round-1 disposition was superseded honestly by `ba8ccd79e`, naming the superseding commit as the rule requires. Rule 24 not engaged. **Scope clean:** seven files across four commits, all C-owned per §C07; no A-owned entity, mapping, migration, snapshot, grant or DI file; no B-owned Case-side path; no `docs/` change; `.worktrees/kanmer` untouched.

## Test UI snapshots — confirmed, those two and no others

`docs/design/test-ui/catalogue.json` entry 53 is the only one whose `source` is `src/Pegasus.Web/Pages/Uploads/Request.cshtml`, declaring exactly `upload-request--default` and `upload-request--validation`. Of the other six touched files, four are not Razor sources; `OperatorLabels.cs` is **purely additive** across all four commits — no existing constant's text altered — so no other page's captured markup changes. `TestUiSnapshotTests` is inert unless `PEGASUS_TEST_UI_MODE` is set, so ordinary runs are unaffected, but a snapshot verify needs those two captures refreshed. **Sole impact confirmed.**

## Round 2 must carry

1. Both comment amendments (`PublicUploadRetentionWebTests.cs:611-615` ASSUMPTION 6, and `:313-316` + the `ARefusedOrUncertain…` method name) — required this round; comments and one name only.
2. The `Arrived` half of `RetainedOrInFlightCodes`: drop it, or make its justification true by re-deriving inside the arrival transaction; either way say what releases an abandoned `arrived` row.
3. F-2 — one integration test for the limits-mismatch refusal-only view.
4. F-3 — delete `IsUnresolved` or make it the one definition.
5. F-5…F-14 as written.
6. F-4 — controller decision on the exhausted-link replacement, recorded as an ASSUMPTION or resolved by R-1b.
7. F-1 / wave 42 — no acceptance of `ba8ccd79e` until the combined run reports; watch the four counted-set-dependent tests named in F-1.
8. Recorded, not for round 2: the two Stream A questions the original review raised.

## Conduct

No git write, no file edit, no `dotnet test`, no branch publication, no ticket move, no merge, no dispatch. The only Kanmer write is this `append_scratch`. Test outcomes are the controller's wave-39 run at `3a13a6e3d`; head `ba8ccd79e` is reviewed on source only. `skill_sha256`: not applicable — controller override dispatch, no `kanmer-review` packet flow and no SKILL.md named in the dispatch prompt.

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

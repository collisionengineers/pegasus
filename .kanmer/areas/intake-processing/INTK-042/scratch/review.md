# Independent review — 2026-08-26

## Changes checked

PR #553 adds exact-ID post-commit claims to both durable outboxes, calls the immediate publishers from receipt/case/replacement/vehicle/image application paths, moves the two queue senders into Infrastructure for Web and Worker reuse, adds Web queue-sender RBAC at queue scope, and renames the five-second normal dispatch timer to a one-minute recovery sweep.

## Blocking comments

1. **Committed outcomes can still be reported as failures when recovery release fails.** Both `DispatchPendingIntakeWork.ExecuteCommittedAsync` and `DispatchPendingExternalWork.ExecuteCommittedAsync` catch a recoverable send/mark exception, but then await `ReleaseDispatchAsync` without protecting the already-committed caller outcome. A transient SQL failure during release escapes the catch and makes manual upload, case acceptance/replacement, vehicle, or image registration appear failed after its transaction committed. The lease already expires safely, so this secondary recovery failure must not replace the committed acknowledgement. Current tests cover queue failure plus successful release only.

2. **The required publication observability is absent.** FRD-02 requires correlated timings for durable receipt, publication, queue claim, later stages, and terminal state. The immediate publishers silently swallow recoverable publication failures and emit no Activity/metric/log for publication attempt, success, release, or release failure. The report says publication failure “is observed,” but the diff supplies no bounded correlated signal. This prevents INTK-043/DELIV-021 from separating commit-to-publish from queue/dequeue latency and hides the safety-net path the change depends on.

3. **Required immediate publication is optional in every application boundary.** `ReceiveIntake`, `AcceptIntake`, `CreateLinkedReplacement`, `RequestVehicleLookup`, `RegisterImageIntake`, and `ImageIntakeCasePairing` all accept nullable publisher parameters defaulting to null and silently skip publication. Production DI currently registers the service, but the Core contract permits the required behavior to disappear without a composition error. This is also the exact optional-parameter design smell called out by the repository simplicity rails. Make the dependency required (and update callers/tests) or establish one required shared boundary.

4. **The plan’s application-path and RBAC proof is not present.** New unit tests exercise the two dispatcher classes only. No changed test proves `ReceiveIntake` invokes exact-ID publication after commit, nor case/replacement/vehicle/image paths, nor that an image store’s returned `PendingExternalWorkId` is the exact committed outbox row sent. The selected SQL suite contains no new assertions for immediate publication and was not completed locally. Likewise the Bicep uses the correct Message Sender role at the two queue scopes, but there is no focused architecture/template assertion preventing contributor/receive/delete privilege or proving those exact two assignments; general Bicep validation is not the plan’s stated sender-only contract test.

## Non-blocking observations

- Exact-ID claims occur after stores return from committed transactions and do not scan the backlog.
- Enqueue precedes mark-dispatched; a successful release makes failed sends due for the next one-minute recovery sweep, and mark-after-send failure remains duplicate-safe.
- Web’s current Bicep role ID and scopes are sender-only and queue-specific; Worker retains the contributor/trigger role.
- Moving sender adapters into Infrastructure and deleting Worker-only copies is a genuine simplification.
- The report honestly discloses that its selected local SQL integration run did not pass, but that disclosure does not substitute for the missing assertions above.

## Verdict

**NEEDS CHANGES — not fit to merge even if the remaining CI checks pass.** The implementation must preserve truthful committed outcomes across secondary release failure, add the governing publication signal, make publication a required application dependency/boundary, and add focused proof for the real call paths, image work-ID handoff, and sender-only Web authorization. No implementation edits or merge were performed by this reviewer.

# Independent re-review — remediation commit 4e1cc7c4 — 2026-08-26

## Disposition of prior blockers

1. **Committed outcome after release failure — resolved.** Both immediate publishers now catch a recoverable secondary `ReleaseDispatchAsync` failure, retain the committed outcome, and rely on the existing one-minute lease-expiry recovery. Focused intake and external tests prove this case.

2. **Publication observability — resolved.** The existing Core intake ActivitySource and a custody ActivitySource now emit bounded immediate-publication activities carrying the durable identifier, path, outcome, error type, and status. The intake test observes the actual Activity. The tags contain no source content.

3. **Optional publication dependencies — resolved.** The six application boundaries now require `ICommittedIntakeWorkPublisher` or `ICommittedExternalWorkPublisher`; Web and Worker bind those ports to the existing dispatchers. There is no nullable/default no-publication route.

4. **Application/RBAC proof — resolved for code review.** Focused tests now prove ReceiveIntake/manual publication, acceptance, replacement, vehicle request, image registration, and image merge pass their exact committed IDs. The deployment-plan test pins the built-in Storage Queue Data Message Sender role and the two queue-scoped assignments. Code inspection confirms Web receives no queue contributor/processor assignment.

## Updated report and simplification

The post-implementation report now names the remediation and remains honest that the selected local SQL subset did not complete. Required publisher ports are a proportionate composition/test boundary; the same existing dispatcher implements both immediate publication and slow recovery policy, with no parallel business implementation. Shared Infrastructure senders and removal of Worker-only copies remain valid simplifications.

## Verdict

**PASS ON IMPLEMENTATION; CONDITIONALLY FIT TO MERGE.** Commit `4e1cc7c4` resolves all four blocking review findings. It is fit to merge only after the current PR run completes successfully, including all SQL integration shards/coverage (which supplies the repository integration rerun still missing locally), browser, unit, infrastructure, and other required checks. No merge was performed during this re-review.

# Merge gate update — 2026-08-26

The reviewed head remained `4e1cc7c4e62ca700fd8f9e3b0518577979302cf7`, but required CI did not become green. Browser failed 3/49:

- `UploadRowsBrowserTests.SubmittingShowsEveryRowUploadingTogetherThenNavigatesOnSuccess` — navigation timeout after submit.
- `UploadCaseSearchBrowserTests.CaseSearchComboboxIsKeyboardOperableAndCompletesTheAttachDecision` — timeout navigating to Upload/Status.
- `QdosAllocationRecoveryBrowserTests.FailedAllocationShowsSafeRecoveryWithoutRawIdentifiers` — retry action timeout.

All three failures occur on Web mutation paths now awaiting immediate publication, so they cannot be dismissed as unrelated without evidence. SQL integration shards were still running when the browser failure became final. Per the repository merge gate, PR #553 was not merged.

# Merge gate update — head dfda320d — 2026-08-26

The narrow `IntakeWebApplicationFactory` override fixed the original Browser regression: Browser passed 49/49 on the fresh run. The head remained unchanged.

Merge is still blocked because all three SQL integration shards failed. The failures are deterministic composition fallout from making committed publication required:

- Multiple mailbox/estate and other integration service collections register `ReceiveIntake` without `ICommittedIntakeWorkPublisher`, causing DI activation failures.
- Other required external-publisher consumers fail similarly across the shards.
- Production-profile readiness Web factories now fail startup because the newly required `IntakeQueue:ServiceUri` / `ExternalWorkQueue:ServiceUri` values are not supplied.

The TestServer fix is correct but too narrowly applied to `IntakeWebApplicationFactory`; every intentional test composition must now provide the required publisher port or valid production queue configuration. PR #553 was not merged.

# Final independent review and merge gate — head eae300f9 — 2026-08-26

The final remediation is limited to test composition and correct replay expectation:

- the shared LocalDB integration harness supplies the mandatory intake/external publisher ports with the existing test-only in-memory double while retaining real durable outbox persistence;
- Production-profile readiness factories provide syntactically valid inert Azure Queue service URIs so startup validation can reach the readiness behavior under test;
- image registration replay now expects the transient newly-created work ID to be absent, matching the Core rule that replay does not republish old external work.

This resolves the preceding SQL DI/startup failures without weakening production composition or introducing an optional runtime path. The reviewed head remained `eae300f98f86ff3cfda290494d2ad239bafabb3f`. All required checks passed: changes, documentation, local scripts, reference data, infrastructure, unit, browser, all three SQL shards, and SQL integration coverage.

**Final verdict: PASS and eligible to merge.**

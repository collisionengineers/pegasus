# Research — TICK-060: provider Case/PO result lookup

## Current reconciliation — 2026-09-08

Read-only/document-only investigation, requested by the remediation root.
Source pinned to accepted dev `498144b0bb55b68fd53b9a31ffc89ef90622c73a`.
No source change, take/claim, test, build, status move or archival occurred.
The historical sections below are retained evidence, not a freshly approved
execution plan.

### Actual behavior and caller

- `ProviderApiEndpoints.cs:234–260` calls the registered
  `IGetProviderSubmissionResult` and returns HTTP 200 with submission ID,
  received timestamp, provider reference, status, decision, allocation failure,
  failure code and Case reference for every owned found submission.
  Unknown/foreign IDs share 404. The route is bearer-only and rate-limited.
- `ProviderSubmission.cs:564–608` authenticates the action actor, checks
  submission Principal ownership before reading queued/processed receipts,
  permits paused credentials, and uses the existing intake state vocabulary.
  It does not implement empty 202 or generic terminal 422.
- This is a real composed production route, not registration-only code:
  `DependencyInjection.cs:299`, `Program.cs:797,1164`,
  `ProviderApiEndpoints.cs:72`; `platform.bicep:503` sets
  Features__ProviderApi=true. No live deployment or credential state was read
  in this reconciliation; historical claims that the gate is closed must not
  be repeated as current evidence.

### Material remaining source gap

`IntakeContracts.cs:662` derives CurrentCaseId as ManualLinkedCaseId once a
manual-association version exists; an inactive/unlinked association therefore
has no active destination. But CurrentCaseReference at lines 687–690 falls
back to AcceptedCaseReference when ManualLinkedCaseReference is null.
`EfIntakeReceiptStore.cs:425–440` loads the original acceptance reference and
supplies the manual reference only for an active association.
The provider result query returns CurrentCaseReference without checking
CurrentCaseId. A formerly accepted source which staff unlinked can therefore
still disclose its old Case/PO as a resulting reference. This contradicts the
ticket's actual-active-link requirement regardless of the unresolved HTTP
response shape. Source inspection establishes the path; no runtime
reproduction is claimed. Use the existing association/result owner to correct
it after approval, with an inactive-link negative and relink/owned-link
positive. Do not infer a successful link from a decision string.

### Authority and chronology conflict — unresolved

Current FRD-09, Accepted API-01 submission contract / Result, explicitly
describes the detailed GET above. The capability registry still schedules both
API-02 and API-03; the board has retired TICK-059/API-02. That is genuine
documentation/board divergence, not proof that either history wins.

TICK-060 open-questions `ba62332fffe2a7f3` attributes identifier-only/no
transient Processing and active-link/terminal-failure intent to operator
decisions on 21 August, and empty 202 / identifier-only 200 / generic 422 to
2 September. TICK-059's archived body corroborates the earlier attribution.
The board Git records are:
- `504390ef1377f54b37483b0155e1db264c61dadd`, 21 August 14:42:28Z:
  removes earlier “minimal polling default” and explicitly parks exact wire
  codes while retaining no transient Processing / actual link / terminal fail.
- `b74a9070092482845fb7aaa0f551e0fb18f70696`, 2 September 16:11:58Z:
  adds the exact 202/200/422 wording attributed to an operator decision.

These establish recorded historical attribution, not an independently recovered
verbatim operator conversation. get_activity returned no retained entries.
Protected operator-notes contains the general future Provider API and
submission/report contract separation (lines 313,471,581–582), but no exact
GET status/body decision. The reviewed PR comments concern create-only matching
and claim-number identity, not approval to expose processing detail. The
current v1 brief authorizes remediation, not silently reversing these specific
earlier attributed decisions. Root/operator resolution is needed; do not label
them obsolete merely because code and FRD agree.

### PR ancestry and evidence reuse limits

GitHub read-back:
- PR594 MERGED 29 August 14:24:43Z at
  `0d985c9e0b3284f211f824d387e2f36460c0c826`, an ancestor of inspected dev.
- PR646 CLOSED UNMERGED, author head
  `32a5a62ce4f13baba45a0bad06df5498f38dcd19` is not an ancestor.
  Its 7 September closure says superseded by PR674 at
  `3da60bd0c270111d5168dc17246dc831882108ea`; PR674 is merged.
  The current existing-Case rejection behavior must be judged on current dev,
  not by treating PR646 as delivered ancestry.
- TICK-058 proof `3e397a147e0b54ad` records historical exact-PR594 restore,
  Release build, Core1158 / Architecture100 / Integration1147+3skip PASS,
  followed by overall FAIL/plan for a mutable-base Markdown check.
  Preserve that FAIL. Review `3c4a72f504ebe492` at PR646 was needs-changes
  for false closed-gate claims, not a review of the 202/422 result proposal.

Existing source tests:
`ProviderSubmissionTests.ResultsArePrincipalScopedAndPausedCredentialsCanRead`
(name must be checked before any command) covers ownership and paused result
reading; `ProviderSubmissionResultChangesFromReceivedAfterAcceptRecovery`
covers recovery state. `ProviderApiSubmissionTests` covers Received/Complete
detailed 200, invalid authentication, pause, foreign 404, Triage with no Case,
and existing-Case rejection as Failed with null reference. These assertions
prove the current detailed contract, not the proposed 202/422 shape or
active-link withdrawal.

The six reviewed Core/Web/query/test files have changed since PR594
(+1210/-72 in the scoped diff); no full input/tree equality was established.
The result query and Web GET mapping remain visibly unchanged while intake,
receipt projection, composition and tests evolved. Historical evidence is
reusable context, not a sufficient current acceptance proof.

### Proposed disposition for root review

Keep Preparing, unclaimed and unarchived. Do not write a PASS proof or mark
Done. Resolve the response-contract conflict first, retaining the dated
attributions. Plan one bounded correction for the active-link gap on the
existing query/association seam; if identifier-only behavior is confirmed,
fold its response mapping/tests/docs into that same owning TICK-060.
If detailed GET is explicitly reaffirmed, retire only the disproved extra
wire-shape scope, not the actual active-link correction. Reuse named existing
tests plus focused negatives; no new route/store/queue/framework or broad suite.
TICK-058's historic claim/review/false-gate record is a separate root
reconciliation and was not changed here.

## Historical research (preserved)

## Question

How should a provider retrieve the Case/PO produced by its own API submission
without exposing internal processing detail or creating a general lookup
surface?

## Verified findings

- API-01 already owns the authenticated route
  `GET /api/provider/v1/submissions/{submissionId}`, the
  `IGetProviderSubmissionResult` Core port, and its production caller in
  `ProviderApiEndpoints`. API-03 extends that seam; it does not add another
  route, store, or Core query.
- `GetProviderSubmissionResult` already scopes a loaded submission to the
  authenticated credential's Principal before reading queued-intake and receipt
  state.
- `EfQueuedIntakeStatusQueries` and `IIntakeReceiptQueries` already provide
  the durable completion and active Case-link facts. A decision such as
  `case_created` is not Case-existence authority.
- The existing public response leaks internal status, decision, failure, time,
  and provider-reference details. No provider has called this surface, so the
  replacement needs no compatibility or deprecation path.
- API-01's authentication already permits a paused credential to read previous
  results and rejects a revoked or invalid credential.
- The existing Web application, Azure SQL state, rate limiter, and telemetry are
  sufficient. No result table, queue, blob container, webhook, API Management
  instance, dependency, or deployment unit is required.

## Settled public contract

- Unknown, random, or another Principal's submission: indistinguishable
  `404 Not Found`.
- Owned but unfinished submission: empty `202 Accepted`.
- Completed submission with an actual active Case link:
  `200 OK` with only `caseReference`.
- Failed work, or completed work without an active Case link:
  generic `422 Unprocessable Entity`.
- A paused credential may read; a revoked or invalid credential receives
  `401 Unauthorized`.

No public response includes processing states, decisions, failure codes,
provider references, timestamps, files, reports, Case detail, retry hints, or
search/listing capabilities.

## Implications

Replace the detailed public result record with a three-outcome Core result and
map it at the existing endpoint. Keep ownership policy in Core. If a small
persistence signature change can scope the initial submission read by Principal,
make that change in the existing store rather than adding a projection.

## Facts and assumptions

The route, ports, callers, persistence queries, feature state, and absence of
provider use were checked against merged `main`, FRD-09, capabilities, and the
current operations record. No unverified external-service or production-data
assumption is required for implementation.

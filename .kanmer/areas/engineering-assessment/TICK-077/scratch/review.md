---
kind: review-attestation
pr: "574"
head_sha: "31659613d125c9aae65076ee2d42a9be208be212"
verdict: pass
reviewer: "claude-code (review session, not the implementing session)"
independent: true
plan_hash: "fcea78f908d74d6c"
ticket_updated: "2026-08-27T16:11:29.517Z"
findings:
  - id: F1
    severity: blocker
    summary: "Every queued attempt ran under the work row's stable per-case operation key, so the second attempt replayed the first attempt's Unknown from action history and never contacted EVA; the whole retry ladder spent its attempts sending nothing."
    disposition: fixed
  - id: F2
    severity: blocker
    summary: "Any non-200 from the EVA token endpoint, a 500 included, was classified as a terminal Rejected; the work row went to failed and the sweep skips a case that has one, so a brief outage or an unresolved Key Vault reference permanently stranded every case that sweep touched."
    disposition: fixed
  - id: F3
    severity: major
    summary: "The once-per-case rule keyed on Succeeded alone, so a Partial left the button live and a second press created a second EVA claim no API call can withdraw."
    disposition: fixed
  - id: F4
    severity: blocker
    summary: "Test-MigrationGrants could not see that EvaSubmissions was granted, because the grant migration built its GRANT text from a three-element tuple and named neither table nor permissions literally. CI red."
    disposition: fixed
  - id: F5
    severity: blocker
    summary: "The release permission census in Invoke-AzureDatabaseBootstrap.ps1 had no entry for the new grant-carrying migration, which Test-AzureDeploymentPlan requires (rule 16). CI red."
    disposition: fixed
  - id: F6
    severity: major
    summary: "Six Eva:* keys are fail-fast in Production and four of their azd inputs have no default, so the first release carrying EXT-04 crash-loops the whole app unless the two Key Vault secrets and four inputs are created first. Nothing said so in the runbook."
    disposition: fixed
  - id: F7
    severity: major
    summary: "Send.cshtml used a record__facts class defined nowhere; every other definition list in the app uses detail-list."
    disposition: fixed
  - id: F8
    severity: minor
    summary: "current-architecture.md claimed the API submission re-checks Review under the same workflow row lock; the store gates on a non-transactional read and says so in its own comment."
    disposition: fixed
  - id: F9
    severity: minor
    summary: "FRD-07 claimed the reconciliation sweep re-arms a failed automatic submission; it does not, because the sweep skips a case carrying any submission work row."
    disposition: fixed
  - id: F10
    severity: minor
    summary: "The six new EVA bicep parameters were the only undecorated params in main.bicep."
    disposition: fixed
  - id: F11
    severity: major
    summary: "The submission record is written with the caller's cancellation token, so a shutdown immediately after EVA accepted the instruction leaves no row and the once-per-case guard no memory of the delivery."
    disposition: deferred-to-ticket
    ticket: ENG-021
  - id: F12
    severity: major
    summary: "A ten-minute processing lease can expire mid-attempt while the submission record is written only after the EVA call, so a second claimant can submit the same case again."
    disposition: deferred-to-ticket
    ticket: ENG-021
  - id: F13
    severity: major
    summary: "EfAutomaticEvaSubmissionStore catches every DbUpdateException and returns 0, so a denied permission or a constraint violation reports 'enqueued none' forever rather than surfacing. EfVehicleWorkflowStore's IsDuplicateKeyFailure predicate is the existing convention and was not reused."
    disposition: deferred-to-ticket
    ticket: ENG-021
  - id: F14
    severity: minor
    summary: "Unsynchronised token-cache fields are read outside the semaphore, so a reader can pair a fresh expiry with a stale token; the 401 retry masks it at the cost of re-uploading every image."
    disposition: deferred-to-ticket
    ticket: ENG-021
  - id: F15
    severity: minor
    summary: "EvaSubmissionWorkState.Poisoned and .Pending are never produced or consumed, and RecordOutcomeAsync explicitly rejects them."
    disposition: deferred-to-ticket
    ticket: ENG-021
  - id: F16
    severity: minor
    summary: "CaseNotInReviewException derives from InvalidOperationException, so a case that leaves Review between GET and POST is shown the generic failure message rather than the state reason the other three refusals get."
    disposition: deferred-to-ticket
    ticket: ENG-021
  - id: F17
    severity: note
    summary: "Submit to EVA API creates a claim FRD-07 says no API call can withdraw, and carries no consequence sentence. The design authority permits one on a destructive action; adding operator-facing copy is the operator's decision, not a reviewer's."
    disposition: deferred-to-ticket
    ticket: ENG-021
  - id: F18
    severity: note
    summary: "Two outcome-to-text tables (SendModel.Describe, OutcomeLabel) that OperatorLabels could own. Raised by the author's own simplification pass; consolidating changes snapshot-asserted strings."
    disposition: accepted-risk
    reason: "They serve an error banner and a status list, which are different surfaces, and the strings are asserted by snapshot tests. Behaviour-preserving cleanup with a real cost and no defect behind it."
  - id: F19
    severity: note
    summary: "EvaCaseImageReader.SelectedDocument carries two never-read members."
    disposition: accepted-risk
    reason: "It is a verbatim extraction from EvaHandoffStore, and keeping it byte-identical is what makes the 'one query, not two that agree today' claim reviewable. The author named this in the plan."
---

# Review — TICK-077 (EXT-04) Direct EVA API submission

PR [#574](https://github.com/collisionengineers/pegasus/pull/574), reviewed at
`31659613`. Independent: this session did not implement the ticket.

## What was reviewed

The whole branch diff after merging `origin/dev` (the PR was `CONFLICTING` on
arrival). Two independent reviewers were run over disjoint halves — Core and
Infrastructure, then Web, composition, infrastructure-as-code, migrations and
documentation — and every finding either reviewer raised was verified against
the code before being recorded here.

## The merge conflict

One file: `IntakePersistenceIntegrationTests.cs`, whose applied-migration list
gained two entries on the branch and one on `dev`. Resolved by keeping all
three in timestamp order. The model snapshot merged cleanly and its only
difference from `dev` is the 102 additive EVA lines; `dev`'s own migration
`20260827100901` changes no model, so the branch's Designer files remain
consistent with it.

## Findings and dispositions

Nineteen findings. Ten fixed on the branch before merge, seven deferred to
[[ENG-021]], two accepted with reasons. The frontmatter is the authority; what
follows is why the three that mattered most were fixed rather than deferred.

**F1 — the retry ladder never reached EVA.** The automatic sweep derives a
*stable* per-case operation key, and `RecordSubmissionAsync` writes an
action-history row under that key on every attempt, including a retryable
`Unknown`. `FindReplayAsync` then answered attempt two from that row without a
network call, `ProcessQueuedEvaSubmission` saw a retryable outcome and
rescheduled, and the cycle repeated to the cap. Every one of the five retries
sent nothing, and `EvaSubmissionRetryPolicy` was effectively dead. A third
attempt would additionally have thrown, because the history lookup is a
`SingleOrDefaultAsync` and two rows now shared the key. Fixed by deriving a
per-attempt key (`EvaSubmissionPolicy.AttemptOperationKey`) from the row's own
key and the attempt number: a retry is its own operation and reaches EVA, while
a queue message redelivered for the same attempt still replays. This also
repairs the shared Operations external-work retry surface, which had the same
defect.

**F2 — an EVA outage was terminal.** `MintTokenAsync` mapped every non-200 to
`Rejected`, which is not retryable, so the work row went to `failed` — and
`EfAutomaticEvaSubmissionStore` skips a case that carries *any*
`submit_case_to_eva` row. A rotated secret, an unresolved Key Vault reference
or a 500 from EVA would therefore have stranded every case that sweep touched,
with no case-page route back for an automatic-only Principal. Fixed by
classifying only a 4xx as a refusal.

**F3 — a partial delivery did not close the case.** `IsSucceeded` was set for
`Succeeded` alone, and the once-per-case guard, the filtered unique index and
the page gate all keyed on it. A `Partial` — EVA accepted the instruction and
returned no identifier — left the button live, and a second press would have
created a second claim with its own File Reference that no API call can
withdraw. `EvaSubmissionResult.IsDelivered` already encoded the correct rule
and had no callers. Fixed by renaming the column to `IsDelivered` and keying
the check constraint, the index filter and the page gate on delivery.

Neither the migration nor its grants had been applied anywhere, so amending
them in place was correct rather than stacking a corrective migration.

## Governing-doc obligations

FRD-07's one hard requirement — that the four outcomes stay distinct — holds,
and is pinned by `EvaSubmissionPolicyTests` and by the database's own
`CK_EvaSubmissions_Outcome`. Two FRD-07 statements were corrected because the
code does not do what they said: the once-per-case rule now names delivery
rather than success, and the recovery route for a failed automatic submission
now names the Operations retry surface rather than a reconciliation sweep that
does not re-arm. `current-architecture.md` lost a claim of a row lock the store
does not take. `capabilities.md`, `operations.md`, `open-decisions.md`,
ADR-0034 and the ADR index were checked against the code and are accurate,
including their repeated and correct statement that Pegasus has never called
EVA in any environment.

## Evidence

CI green on `31659613`: all eleven required checks, including `browser`, `unit`,
the three `sql-integration` shards and the coverage partition check. Two of
those checks were red on arrival for real reasons (F4, F5) and are green because
they were fixed, not because they were skipped. Locally in the ticket worktree:
Core 1043/1043, Architecture 100/100, and all 32 EVA integration tests,
including a new persistence test proving a partial delivery closes the case at
the database level and a new transport test pinning that a 500 from the token
endpoint is not a refusal.

## Residual risk

The one that matters is not in the code: the six `Eva:*` keys are fail-fast in
Production and four of their azd inputs have no default, so the first release
carrying this crash-loops the entire application — not merely the EVA route —
unless `eva-client-id` and `eva-client-secret` exist in `pegasusprodkv252ow37g`
and the four inputs are set before provisioning. F6 records that in the
runbook's pre-provision list.

Beyond that, EXT-04 ships unproved against the real service: no Pegasus
environment has ever called EVA, and the payload is proved only against the
vendor's recorded traffic. The operator holds the live test. Every Principal
setting defaults to off, so the merge changes no behaviour for any existing
case, and the automatic route in particular has had no live exercise at all —
which is why the seven deferred findings in [[ENG-021]] are worth clearing
before any Principal turns automatic submission on.

# Stream A implementation plan

## Authorized delivery phase — 7 September 2026

The operator now requires autonomous completion of Stream A and continued
coordination of B/C through reviewed integration into `dev`. This supersedes
the earlier open-and-unmerged stop wherever it appears in the retained
implementation plan below. A monitors GitHub and the Chrome remote desktops,
coordinates scoped fixes and publishes a combined verification ref for isolated
cross-machine tests. That ref is not another implementation PR and must not be
merged into the domain source branches during implementation.

Before integration, settle independent findings, preserve original PR behavior,
complete applicable exact-head checks and document any qualified cross-stream
dependency. Merge the required reviewed PRs into `dev`; close originals as
superseded only with preservation evidence, and integrate any non-superseded
original work through review. Preserve original refs and other agents' work.
Then verify the integrated result at its exact merge identity and open the
`dev` to `main` PR. Do not merge that PR or update `main`. No deployment, reset,
mail send, Outlook/Box mutation or live provider write is authorized.

## Governing docs

The user-approved D01-D17 decisions authorize the corresponding FRD corrections in this stream. Existing accounts/access and mail FRDs gain explicit administrator recovery, no periodic reviews, authorized staff sends and truthful Sent evidence; domain FRDs follow their named stream owners. Protected operator notes are not overwritten. Four-project architecture and existing policy owners remain binding.

## Starting state

D = 3284f93fc3ea9fd3bbbea9405ec92dc7818378f2, verified live. Owner tickets A PLAT-075, B CASE-047, C INTK-060. Follow the supplied exact file ownership register. User has authorized autonomous execution and the three-owner exception; no new permission request is needed for this implementation.

# Three-machine execution and handoff

This is an approved exception to one-ticket/one-feature-PR work. Future product
implementation uses three owner tickets and three new branches based on the
same current dev commit. Existing tickets are evidence and residual work owners,
not 210 separate implementation PRs. This planning package has no Kanmer ticket.
All three implementation PRs target dev and remain open and unmerged.

## Startup — Astra coordinates before any coding

Read [DECISIONS](DECISIONS.md), [SHARED-CONTRACTS](SHARED-CONTRACTS.md), your stream
plan and [Git dispositions](registers/git-dispositions.md). Read current
AGENTS/NOW/docs index and native Kanmer status/effective gates. Refresh GitHub
heads and the four old PRs; a changed head requires a delta review and updated
preservation table, not restarting or silently discarding this package.

Planning pin D is `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`; main pin is
`32f8679d3695e0dcab8f310a1c20f8b129d20190`. The shared source checkout is stale
and dirty. Do not reset, stash, clean, checkout, build or implement there.
Create clean worktrees only when product implementation is authorized.

At that time Astra creates exactly three owner records with descriptions below,
reads their effective profiles/gates, supplies this package as research/plan/
checklist and records actual branch/worktree with native Kanmer. Do not force
take the 47 currently claimed records or silently repoint their resume targets.
Use supported native Kanmer commands; never edit board files/branch manually.
If current Kanmer demands a different branch name, store the actual name once
and update all three machine instructions; ownership and three-PR count remain.

| Machine | Owner ticket title | Proposed branch | Worktree relative to own checkout | Final PR title |
| --- | --- | --- | --- | --- |
| Codex / Astra | Pegasus v1 platform, shared foundation and integration | task/pegasus-v1-platform | ../pegasus-worktrees/v1-platform | Complete v1 platform, custody, mail and integration |
| Claude / Fable 5.1 B | Pegasus v1 Case engineering, Glass's and reports | task/pegasus-v1-casework | ../pegasus-worktrees/v1-casework | Complete v1 Case engineering and report workflow |
| Claude / Fable 5.1 C | Pegasus v1 intake, principals and operator shell | task/pegasus-v1-intake | ../pegasus-worktrees/v1-intake | Complete v1 intake, extraction and operator workspace |

Each ticket includes its stream plan, the same frozen D, all shared decisions,
mapped old ticket IDs, allowed files, tests and the PR-open/unmerged stop. Do not
create a fourth foundation owner/PR, planning ticket or generic umbrella batch.
This package remains outside the repository; canonical product documentation
edits follow AGENTS. No secrets or corpus binaries enter tickets or PRs.

## Commit topology — common foundation, then independent work

All three branches are created at the exact same D. Astra authors F01–F03 on A
as common foundation commits F. B and C do read-only source/corpus preparation
until F is reviewed, compiling, and published. Before either has domain commits,
each fetches A and fast-forwards to **the same F commit objects**, not copies:

```text
                 A domain commits ---- A PR -> dev
                /
dev D ---- F ---+--- B domain commits -- B PR -> dev
                \
                 C domain commits ---- C PR -> dev
```

The fast-forward must be `git merge --ff-only <recorded-F-SHA>` on B/C; verify
`git merge-base --is-ancestor <F> HEAD` and record the shared commit identity.
Do not cherry-pick F, rebase it separately, merge dev, merge the foundation into
dev, or target B/C PRs at A. No dev update is authorized. Foundation appears in
all three dev comparison diffs until any later authorized merge; explicitly
label that shared range in each PR. Git ancestry applies it once.

Once streams diverge, do not fast-forward B/C to A's domain head or merge whole
mega branches into one another. A needed shared correction is authored by Astra
on a temporary local branch/checkout rooted at the latest shared F/G boundary,
reviewed, and merged with `git merge --no-ff <G-SHA>` as the **same G commit**
into each stream. Record G and each stream’s distinct merge commit. It changes only
Foundation-owned files/contracts. The temporary helper has no PR; preserve its
SHA in the owner evidence. Resolve conflicts in the owning stream and retest.
Do not make cross-stream contract changes independently.

Composition exception: when a new concrete type exists only on B or C, A
authors a small branch-local DI/host patch against that recorded head. The
stream applies the exact hash-recorded patch in a serialized registration
window; it does not improvise edits to A-owned files. A remains the sole
registration author/reviewer. That patch travels in the domain PR and compiles
there. Common G is reserved for changes whose dependencies exist in every
stream. The combined checkout combines the three registration additions under
A ownership; small composition conflicts are resolved explicitly. This avoids
stubs, reflective registration and importing unrelated domain commits merely
to register a type. Contract/schema changes still require common G.

## Foundation steps — Codex machine only

**F01, Astra + Sol contract review:** freeze the exact shapes and owners in
SHARED-CONTRACTS and the file manifest. Read B/C foundation requests as input;
the accepted shared contract wins when a request uses a different port name.
Port compatible PR670/671 schema hunks and PR639 watermark into the target
schema design, retaining per-hunk dispositions. Reconcile local AGENTS 0.4.2
semantic changes and the explicit task exception without replacing unrelated
dirty work. Publish exact API/enum/field/test fixture signatures in existing
canonical docs and owner-ticket plan, and author the actual shared C# definitions
in every S02 contract path. The A-before-F/B-or-C-after-F manifest exception is
contract-only; domain commands/stores stay in their stream. No consumer branch
may reference an absent contract or create a private copy. No ambiguous
per-stream schema choices.

**F02, Sol implementation; Terra tests:** own all EF entity declarations,
configuration classes, PegasusDbContext, migrations/model snapshot and grants.
Keep the valid existing migration chain and add the single coherent v1 schema
migration needed for the new model; no historical data conversion, dual columns
or old/new implementation switch. A fresh database applies that chain cleanly.
Default-null new facts represent genuinely unrecorded data, not invented domain
values. Configure A/B/C-provided minimal shapes in the existing aggregates.
The A inventory is `handoffs/A-foundation-requirements.json`; include its
credential, mail-attempt/correlation, cache and administrative lease primitive
requirements alongside both B/C inventories. Use
unique constraints for operation keys, T references, one Current estimate,
credential-active session and artifact versions. Add actual runtime-role grants
and bootstrap census in the same diff. F owns global persistence plumbing; B/C
implement their own store methods after the freeze.

F02's lease-clearance verification uses the explicit A-before-F/B-after-F
exceptions on `CaseEditAuthorityTests.cs` and `CaseWorkflowPersistenceTests.cs`.
A adds only that primitive's policy and persistence tests in those files;
B resumes normal domain ownership after the shared F SHA is recorded.

**F03, Terra; Sol independent check:** publish only registrations whose concrete implementations exist at F,
shared test support and stable shared shell markup/class contract. Do not
reference absent B/C types, use no-op registration hooks or add throwing stubs.
New domain handlers and their registration arrive together through the
serialized branch-local registration window below. Foundation alone is an
incomplete development checkpoint. Run locked restore/build, architecture/migration/grant and contract
tests in isolation, record exact F, and invite B/C fast-forward. Missing domain
implementation must be tracked in its exact step rather than falsely passed.

F is one initial synchronization point, not a demand that A finish its entire
platform before B/C start. Later ports use existing local fakes and genuine
source assets for parallel development. Runtime implementations land in their
owners and the combined checkout proves wiring.

## Waves and model delegation

| Wave | Codex A | Claude B | Claude C | Barrier |
| --- | --- | --- | --- | --- |
| 0 | Astra F01; Sol contract audit; Terra F02/F03 | Fable coordinates; Sonnet B01 read-only PR/source inventory | Fable coordinates; Opus C01 read-only evidence/PR inventory | All branches at D; no B/C domain commits |
| 1 | A01 identity and A04 custody: two Sol workers on disjoint paths | Opus B02 transaction; Sonnet records v3 field/manager matrix | Opus C01 correction then C02 provenance; separate Sonnet directory source inventory | Shared F adopted unchanged |
| 2 | Sol A02 Graph; Terra A06 admin query/UI | Opus B03 valuations then B04 estimates; independent Opus importer slice after estimate contract | Opus C03 profiles in bounded batches; second Opus C04/C07 pre-case rules on disjoint files | B totals and C candidate/location interfaces fixed |
| 3 | Sol A03 sending; second Sol A05 connector | Opus B05 reports/Glass's integration; Sonnet B06 Files when schema ready | Sonnet C06 directory and C08 shared shell; Opus C05 third-party extraction | A custody/send and C shared assets available |
| 4 | Terra A07 CI/performance; Luna A08 docs inventory | Sonnet B07 preparation/B08 assembly; Opus resolves complex findings | Sonnet C08 assembly; Opus corpus/failure checks | Each stream's callers wired; no domain placeholders |
| 5 | Fresh Sol A09; Astra unpublished combined verification | Fresh Fable 5.1 B09 full-stream review | Fresh Fable 5.1 C09 full-stream review | Exact heads and all review dispositions recorded |

Fable 5.1 is each Claude orchestrator, never a worker/subagent until the final
fresh whole-stream review. Opus 5 handles complex policy/concurrency/extraction;
Sonnet 5 handles routine UI/adapters/tests/docs. No Haiku. Astra is the Codex
orchestrator; Sol handles complex work/review, Terra routine work, Luna bounded
mechanical inventory. Every delegated task specifies exact files, inputs,
required caller/tests and stop condition. One author per file. A reviewer must
not be the author. A model need not wait idle merely because a separate step
is blocked; continue its already authorized disjoint work.

## Cross-machine evidence and ownership changes

Each owner ticket stores a compact handoff table: item/contract version,
providing commit, consuming stream, files, focused command/result and remaining
operator gate. Record updates at contract freeze, implementation checkpoint,
review and final head. The three root orchestrators communicate through these
shared owner-ticket documents and Git commit identities; one machine's local
path is never another machine's dependency. Subagent notes are merged into the
owner record, not posted as emails/messages to staff. Native MCP writes for
future ticket work are authorized by that implementation task, not by this
planning turn.

File manifest precedence is exact file, then deepest prefix; a tie is a defect.
Unlisted files are closed to edits until Astra assigns one owner. A shared-file
change goes through Astra's common G commit. Domain-file change goes to its
owner; send an exact patch/request, never edit a neighbour's checkout. B/C
domain interfaces frozen in F may be implemented in their files but changes to
the agreed cross-stream signature require G. A change to shared CSS stays C;
B supplies a fixture/expected behavior and uses Case-only assets for Case logic.

Handoff `newPaths` means new relative to D, not permission to recreate a file
already introduced in F. Check the phase fields first: A may have published its
shared records/interfaces at F, after which the domain owner extends that same
file with the real implementation. A domain worker never forks the definition.

## Existing PRs, tickets and worktrees

The 6 September 2026 snapshot accounts for PRs 639/646/670/671, 44 worktrees and
43 local branches. Refresh this census at implementation startup before any
retirement or preservation decision.
Preserve original commits, branches, ticket evidence and any dirty files.
Port required hunks with source SHA and exact target path; compare final
behavior/tests and reject superseded UI/schema churn with a reason. Do not
blind-merge stale branches. After both code preservation and independent review
are proved, authorized closeout may close the old PRs as superseded by named
replacement PRs. This is not a merge and does not prove their tickets Done.
Exactly three replacement PRs remain open when all streams finish. No draft
PRs are created for subagents, helpers, integration or foundation.

Contained branches are preservation/cleanup candidates only. Existing claims
are reconciled individually under current native gates, not forced, silently
released or deleted. Review/verification of already integrated code remains
real work and is included in the three owners' evidence. A genuine post-v1
feature receives the explicit deferred disposition, not a fake implementation.

## Combined verification and final stop

Astra creates an **unpublished** disposable integration checkout from D and
merges the exact A/B/C heads locally. No PR or remote integration branch. Check
conflicts and migration count, run canonical validation and routed UI/corpus
journeys, and record the three inputs plus combined tree/commit. A combined
failure returns to its file owner, then that owner and the combined checkout
retest affected checks. Do not conceal a failed individual PR behind a passing
combination. Refresh integration whenever any input changes.

Final handoff has exactly 3 PR URLs, all to dev and unmerged; three exact heads;
common F/G ancestry; green applicable CI/standalone checks; combined evidence;
old-PR preservation/closure evidence; honest human provider gates; current docs;
and [the operator checklist](OPERATOR-CHECKLIST.md). No dev/main merge, deploy,
reset, live credentials or provider write occurs as a side effect of completion.



# Stream A — platform, shared foundation and integration

Astra orchestrates this Codex machine. This plan starts from the common dev
commit recorded in [COORDINATION](../COORDINATION.md). Read
[DECISIONS](../DECISIONS.md), [SHARED-CONTRACTS](../SHARED-CONTRACTS.md) and the
file ownership register before delegating. A also delivers Foundation F01–F03.
The three streams share those exact commits before domain implementation.

Use Sol for security, Graph side effects, custody and concurrency; Terra for
bounded admin/query/test work; Luna for deterministic inventories and link
corrections. Astra resolves contracts and integrates. At most two implementation
agents edit disjoint files on this machine; one process owns heavy verification.
A fresh Sol reviews the entire stream at A09. No model may send mail, mutate
Outlook, call live Glass's/EVA, write Box/cloud state or deploy during this work.
Local provider substitutes exercise the actual production adapter boundaries.

## A01 — accounts, logout, lease recovery and protected credentials

**Implementer:** Sol; Terra takes the Razor slice after Core contracts freeze.
**Prerequisite:** F01–F03. **Caller:** existing Administration Accounts/Access
pages, per-request staff validation, and B's Glass's adapter.

Reuse `src/Pegasus.Core/Identity/StaffAccountAdministration.cs`,
`src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs`,
`EfStaffAccountQueries.cs`, `EfStaffPasswordChange.cs`, Identity's security stamp
and existing OpenIddict token revocation. Extend these owners for explicit
Force logout and administrator password reset. A reset reveals a temporary
value once through protected UI, requires password change at next sign-in and
revokes previous sessions. It never sends a recovery email or logs the value.
Resolve the account once within a request while retaining per-request
disablement/stamp checks; do not add a cross-request account cache.

Delete active access, roles, password/token material and Glass's credentials.
Keep the smallest stable historical actor record required by accepted Case and
report history. Prevent deleting/disabling the last enabled Administrator and
require a clear consequence confirmation for the selected account. Never
delete Cases or rewrite printed signatories. Force logout does not implicitly
clear leases: expose targeted Case/user lease clearance through Foundation’s new named
Core administrative lease-clearance operation, with actor/reason and token invalidation.

Remove `ReviewStaffAccess`, access-review requests/queries, review dates,
periodic-review policy, handlers, notices, tests and configuration. Inspect
`Pages/Administration/Accounts/Confirm.cshtml*` before removing it: retain any
real destructive-action confirmation under its proper action; remove only
periodic-review behavior. No renamed review or hidden timer remains.

Implement `IPerUserExternalCredentialReader` from F for Glass's using an
encrypted SQL row protected by existing ASP.NET Data Protection. Purpose binds
provider, Pegasus user and credential generation. A provides the writer and
reader; B owns the Glass's admin page and provider integration. UI exposes
configured/enabled/username/updated state and replace/clear actions, never the
stored password. Secret replacement revokes old generation sessions, and
disabled/deleted accounts cannot launch or resume. A supplies the existing durable-key-ring protection primitive; B owns
the Glass session row/store and stores only protected cookie/CSRF ciphertext.
Do not use static dictionaries as recovery storage. Initial alex configuration is an operator
step, never a seeded secret.

**Files:** the existing Identity files above; new
`Core/Identity/PerUserExternalCredentials.cs`,
`Infrastructure/Persistence/EfPerUserExternalCredentialStore.cs`; account/access
Razor pages. F owns entities/mapping/migrations/DI. Tests extend existing staff
administration/session/lease suites; proposed
`tests/Pegasus.IntegrationTests/ExternalCredentialIsolationTests.cs` covers the
new external boundary. B alone edits Glass's pages and signatory/report policy.

**Proof:** disable/role change/logout rejects the next old-cookie/token request;
reset is single-use and forces change; last-admin race has one safe outcome;
cleared lease rejects stale saves; another engineer cannot read/launch/resume
the credential; restart retains valid protected material; replacement and
deletion invalidate it. No secret appears in logs, audit, HTML, MCP or proofs.
Commit Core/adapter and reviewed UI as separate logical slices. Done means
these actions are reachable in Administration and recorded in action history.

## A02 — mailbox onboarding and reliable read-only intake

**Implementer:** Sol for Graph/runtime, Terra for UI after contract freeze.
**Caller:** `/Administration/Mailboxes`, Worker mailbox functions and C's Inbox.

Extend `Core/Identity/ApprovedMailboxAdministration.cs`,
`ApprovedMailboxSubscriptions.cs`, `MailboxChangeNotifications.cs`,
`Infrastructure/Email/GraphApprovedSources.cs`,
`GraphMailboxChangeSubscriptions.cs`, `ApprovedSourceSettings.cs`,
`Persistence/EfApprovedMailboxStore.cs`,
`EfApprovedMailboxSubscriptionStore.cs`, `EfApprovedMailboxPollStatusQueries.cs`
and `EfRetainedMailboxMessageStore.cs`. Reuse stable Graph identity, existing
delta/subscription/retained-message records and failure vocabulary.

Administrator enters/selects an existing authorized mailbox, resolves its
stable identity, performs a read-only access check, selects intake/Sent/send
capabilities and enables it. Creation of a new Exchange mailbox and inclusion
in the approved application scope are Microsoft 365 administrator UI operations
after the one-time infrastructure bootstrap. Saving a Pegasus row never claims
to grant Exchange access. Include a link to that documented setup, not a raw
environment-variable editor. Staff without Administrator cannot configure it.

Initial onboarding records a start boundary; no silent historic backfill.
Disable stops new mailbox work and retains existing receipts/messages. Re-enable
or target identity change invalidates the old cursor/subscription generation
and checks access again. A delayed old-generation worker must not advance a new
cursor. Fixed freshness remains 15 minutes. Expose last successful poll, last
error, start boundary, subscription expiry and capability status.

Fetch the notified Inbox message directly by immutable identity after verifying
mailbox/folder, then retain the periodic delta sweep for recovery. Persist and
follow opaque next/delta links, including empty pages with continuations;
checkpoint only after retained messages commit. Reject URLs outside the
configured Graph origin before authenticated requests. Opening/filtering mail
in Pegasus never marks it read or changes its Graph folder/category/flag.
Two mailbox copies retain distinct arrivals; only a proven shared source
occurrence may prevent duplicate Case allocation through C's existing
idempotency/association policy. Never deduplicate distinct jobs by VRM alone.

**Tests:** extend existing approved-mailbox, notification, delta, retention and
mail workspace suites. Cover permissions absent, mailbox renamed/replaced,
disable race, stale subscription, delta delay, empty next page, replay after
crash, 429 Retry-After, malformed continuation, attachment identity, the same
email CC'd to two approved mailboxes and two real jobs for one VRM. Assert the
mock HTTP trace contains no mailbox mutation. UI-only onboarding works for
instructions/info/desk/engineers once operator scope permits them.

Microsoft documents [delta recovery](https://learn.microsoft.com/en-us/graph/delta-query-messages)
and [Exchange application RBAC](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac).
These define adapter constraints; the workflow above is the chosen Pegasus
implementation. Do not introduce an Exchange provisioning service.

## A03 — staff-initiated send and truthful Sent evidence

**Implementer:** Sol. **Prerequisite:** F mail contract; B07 supplies immutable
generation/artifact preparation. A can implement against contract fixtures
before B finishes. **Callers:** B Case Report Send and C's explicit staff email/
chaser action. Scheduled chasers and MCP autonomous sends are absent.

No current production compose/send implementation was found in the pinned
Core/Email adapters. Add `Core/Operations/StaffMailSend.cs`,
`Infrastructure/Email/GraphStaffMailSender.cs` and
`Persistence/EfStaffMailSendStore.cs`; extend existing
`Core/Operations/EmailOperations.cs`, `Core/Intake/MailboxIntake.cs`,
`Core/Workflow/ApprovedMailboxReportSentEvidence.cs`,
`Persistence/EfSentEvidencePollStore.cs`, `EfCaseReportSentEvidenceStore.cs`
and Worker mailbox functions. Reuse `GraphMailClient`, runtime authentication,
bounded HTTP client, existing retained Sent evidence and operation history.
Do not add a Graph SDK, second outbox framework or transport platform.

One staff send command freezes approved mailbox, actor, purpose, compose mode
(`New`, `Reply`, `ReplyAll`, `Forward`), optional retained original-message
identity/thread, To/CC,
subject/body, Case/generation/version, artifact IDs/hashes and operation key.
Reauthorize staff/mailbox and revalidate B's report freshness immediately before
the external side effect. Save operation state before calling Graph. Duplicate
key/same payload returns the same operation; changed payload conflicts.

Use one Graph draft-based workflow: new mail uses create message; a reply,
reply-all or forward uses the corresponding draft-creation endpoint against
the authorized original immutable message. Use existing MimeKit to provide
the applicable original thread headers and operation marker at creation, not
an unsupported later custom-header edit. The UI shows actual Reply-To and
selected recipients for staff confirmation. A missing/unavailable original
thread refuses a reply; switching to New is an explicit staff choice. Neither
option silently changes the original Outlook message. The remaining attach/
send/reconciliation workflow is identical. Create each draft with
`x-pegasus-operation-id`, persist its immutable identity, attach the selected
artifacts, then send that draft once. Small attachments use the normal attachment
endpoint; supported large attachments use an upload session. Enforce file,
combined encoded-message and mailbox limits before send; refuse oversize with
the offending attachment, never silently omit it or create a public Box link.
Require the Administrator to record the verified effective encoded-message
size ceiling on the approved mailbox before send can be enabled. Unknown is
not a guessed default. Enforce the smaller of that ceiling and the adapter
attachment/session bounds; show the configured ceiling in Mailboxes. Upload in bounded
chunks below 4 MB and at documented 320 KiB multiples. Interrupted draft/upload
work can resume only after locating the recorded draft/session. Do not create
another draft after an ambiguous creation response without reconciliation.

States are `Prepared`, `DraftCreating`, `DraftReady`, `Sending`, `Submitted`,
`Sent`, `Failed`, `Unknown`, `Cancelled`; freeze their single Core definition
in F. `Submitted` means provider acceptance, not delivery. Graph 202 cannot
produce Sent evidence. A confirmed Sent item correlated to mailbox, operation,
message and attachment/generation identity records Sent. External delivery to
the recipient is not claimed. Known rejected requests may be corrected and
explicitly retried; timeout/connection loss after a possible side effect becomes
Unknown and never automatically resends. Staff sees the state and can request
read-only reconciliation. Scheduled recovery may reconcile; it cannot initiate
a new message. Deleting abandoned remote drafts is not a development action.

Replace the single global Sent mailbox setting with one cursor per enabled
approved mailbox/folder/generation. Poll all rows with Sent capability, bounded
fair pagination, independent errors and no starvation. Preserve received,
prepared, submitted and observed-Sent times independently. Caller UI renders
the one Core state mapping. Report generation/preparation does not mark sent.

**Tests:** proposed `StaffMailSendTests`, `GraphStaffMailSenderTests`,
`StaffMailSendWebTests`; extend `SentEvidencePolling`/report evidence tests.
Assert draft-before-send, all attachment bytes/hashes, stale generation refusal,
permission revocation, idempotency, no send on GET, ambiguous draft/send timeout,
restart in each state, expired upload, throttling, two mailboxes with one failed,
and 202 without Sent. Fakes must record HTTP methods/targets and exact sends=0
for negative cases. Human-only later provider proof is in the operator checklist.

Official constraints: [draft creation](https://learn.microsoft.com/en-us/graph/api/user-post-messages?view=graph-rest-1.0),
[reply draft](https://learn.microsoft.com/en-us/graph/api/message-createreply?view=graph-rest-1.0),
[forward draft](https://learn.microsoft.com/en-us/graph/api/message-createforward?view=graph-rest-1.0),
[send draft](https://learn.microsoft.com/en-us/graph/api/message-send?view=graph-rest-1.0),
[attachment sessions](https://learn.microsoft.com/en-us/graph/api/attachment-createuploadsession?view=graph-rest-1.0).
Scoped Application Mail.ReadWrite and Mail.Send are required for this chosen
workflow; RBAC and unscoped grants are additive. The one-time operator bootstrap
must remove unintended tenant-wide grants and verify the intended scope. Live
mailbox/permission changes remain a separate authorized operator step.

## A04 — one durable custody boundary and a 24-hour image cache

**Implementer:** Sol. **Callers:** C intake/source re-evaluation, B Files/images,
Glass's import and generated artifacts, A MCP downloads. **Prerequisite:** F
logical content contract, not a completed B/C implementation.

Extend `Core/Custody`'s existing document/version ports,
`Infrastructure/Custody/BoxDocumentContentStore.cs`,
`Infrastructure/Intake/AzureBlobIntakeArtifactStore.cs`,
`Core/Intake/DownloadIntakeSource.cs` and the existing custody reconciliation
job. A owns the source download port/file despite C's broader Intake ownership.
Name new cache adapter `Infrastructure/Custody/CachedDocumentContentStore.cs`;
reuse current Azure Blob client and current custody store. Extend
`ICaseArtifactCustody` for idempotent generated/Glass originals with occurrence,
Case, immutable version/hash and destination policy. No parallel writer for
each artifact type. Never overwrite an existing version by filename alone.

Box owns original and generated file versions. SQL owns receipt, arrival,
association, document/hash/version and processing history. Azure owns temporary
pending bytes and verified cache entries. Keep the existing small intake
receipt: it distinguishes a retry, a second real email with identical bytes,
and a corrected attachment. A date/hash alone does not. Viewing an image creates
no intake receipt. Do not add a second event ledger or retain every intermediate
payload permanently.

Resolve every retained source by logical document/version identity before
eviction; the original Azure key cannot be its permanent address. Pending,
unallocated, poison and unidentified sources remain recoverable until durable
custody is confirmed. Use a configured Box holding parent for non-Case sources;
it is an operator-created/authorized folder, outside agent live testing. Final
Case custody follows the existing designated Case-folder policy. No required
source is deleted on age alone. D10 permits a later clean application-data
reset, so no obsolete source-key conversion or historical blob repair engine
is required in the implementation.

Cache exact Box file/version plus SHA-256, length and media type. Authenticate
and authorize the Case/source on every hit. Download to a temporary object,
verify length/hash, conditionally publish; partial bytes are never valid. Stream
content with bounded buffers; use exact-version/range reads where supported.
Each successfully served authorized access persists expiry at that access
UTC+24h. After 24 hours idle the object is eligible for deletion; hourly cleanup
removes it by 25 hours idle. Do not add touch coalescing without a measured need.
A missed cleanup is an observable failure, not an extended successful policy.
B prefetches only visible plus the next two images. No whole-Case
eager preload or cross-Case cache warming.

Extend the existing reconciliation timer for hourly eligible-cache deletion.
Use last-access generation/ETag and short read leases so an eviction cannot
delete an entry touched/opened after selection. A cache miss rehydrates the
same Box version; a lost cache cannot lose a Case. A valid cached copy can serve
during Box outage; an uncached unavailable version returns the existing error.
Never substitute another file version. Show bytes, hits/misses, oldest pending
custody and cleanup failure in A06.

The existing custody account also stores `authentication-ring` and `box-links`
and has seven-day soft delete. Retain that protection; it is not a permanent
archive and is not the image-cache timer. Live cache objects expire above;
deleted payloads may occupy recovery storage for another seven days. Do not
disable account-wide key recovery merely to optimize temporary file storage.
Lifecycle rules may be a coarse prefix-scoped backstop, never cover key rings,
Function packages or queues. No new storage account/service is needed.

**Proof:** real genuine image source through fake Box -> cache hit with zero
second download -> expiry/miss -> same hash; revoked access fails warm cache;
touch/delete/read race, concurrent misses, partial upload, restart, failed Box,
pending custody older than 24h, XML/PDF/report generation and source re-evaluation
after staging removal. Measure cold/warm p50/p95 and peak allocations on the
same selected corpus; no unmeasured speed claim. Warm next-image navigation
must use cached bytes without Box network calls. Run through production DI.

References: [Box version content](https://developer.box.com/reference/get-files-id-content),
[Azure lifecycle](https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-overview).

## A05 — connector authority, durable OAuth and bounded content

**Implementer:** Sol. **Prerequisite:** F actor/content contracts; B/C own domain
policy. Reconcile each of the existing 43 tools in the linked
[tool inventory](../../reports/connector/tool-inventory.csv). Apply the exact
per-tool dispositions in [connector changes](../registers/connector-changes.md).
Caller is the existing `/mcp` endpoint and known connector clients.

Own `src/Pegasus.Web/Mcp/**` including `AutomationMcpExtensions.cs`,
`AutomationActorResolver.cs`, `AutomationClientRegistry.cs`, `AssessmentMcpTools`,
`TriageMcpTools`, `DocumentMcpTools`, `IntakeMcpTools`, `AiJobMcpTools` and
`UnidentifiedMcpTools`. Replace production ephemeral OpenIddict signing and
encryption keys with configured persistent certificates loaded using managed
identity from the existing Key Vault. Use separate signing/encryption purposes,
document rotation overlap for issued tokens, fail readiness on missing keys.
Development uses explicit isolated test keys. Reuse OpenIddict, PKCE, audience,
consent and existing scopes; no new OAuth platform. Record connector grant/
authorization identity separately from the human approver and shared client ID.

Carry typed `ActionActor` to every Core mutation. Generic assessment patch must
reject valuation/estimate/signatory/finding-owned fields and call their named
B commands instead. C Triage and association commands retain actor kind, lease,
version and replay rules. A connector scope does not grant Engineer authority.
Rename `needs_sorting` to `unidentified` in tool JSON and known consumers/tests;
there is no released consumer requiring a compatibility alias. Keep stable
document/source version IDs, raw source provenance and explicit unavailable/
ambiguous/conflict outcomes in results.

Use cursor pagination with stable `(sort value, immutable id)` ordering,
default 50, maximum 100 and continuation for jobs, unresolved/intake/document/history
lists. Case detail includes summaries plus continuations; it never silently
truncates. Validate negative/oversize/stale cursors against caller/filter scope.
Check content metadata and permission before fetching bytes. Preserve the
current small embedded-content route under its existing bound. For larger
files, add authenticated streaming GET `/automation/documents/{id}/versions/{v}`
using the same bearer audience and Documents scope, Case/source authorization,
ETag/range support and A04 reader; MCP returns its identity/URL/size/type/hash.
It is not a public signed link or arbitrary URL fetcher. Tests include a client
using the returned endpoint; metadata-only calls download zero bytes.

Add `pegasus_estimate_import` to the existing assessment tool family, invoking
B's canonical raw-estimate import for AUTO-016. Its parser, scope, actor, lease
and replay checks are the same as the Case UI. Do not expose Glass's
passwords/browser sessions or add a valuation
service, generic assessment-bundle tool, autonomous Send tool or second field
policy. No tools expand merely for speculative convenience.

**Proof:** token survives app restart/replica; expired/revoked/wrong scope fails;
cross-grant attribution retained; same lease/version errors through UI and MCP;
generic finding patch refused; all 43 inventory rows have keep/change/remove and
test mapping; pages concatenate without omissions/duplicates; metadata request
has zero content reads; range/cross-Case access and exact-version mismatch fail.
Extend existing automation/MCP integration suites and proposed
`AutomationDocumentStreamingTests.cs`. F alone edits DI/infra registration.

## A06 — complete administration, action logs and useful reports

**Implementer:** Terra, reviewed by Sol. **Prerequisite:** A01–A05 query contracts.
Reuse `Core/Operations/ServiceHealth.cs`, `EmailOperations.cs`,
`Core/Identity/AutomationActivity.cs`, `Core/Reports/EngineerActivityReport.cs`,
`Infrastructure/Persistence/EfServiceHealthQueries.cs`,
`EfAutomationActivityStore.cs` and existing report query/store. Add routed
Administration `Health`, `ActionLogs` and `Reports` pages with bounded filters
and downloads; complete existing Accounts, Access, Roles, Mailboxes and
Automation pages. A owns the Administration base model/index; C owns the
shared admin navigation markup. B owns Workflow/Rate/Valuation/Glass's forms;
C owns Principals/Organizations. Each owner supplies nav entries to C, not
another copied menu.

Health shows each configured service's actual status, last success/failure,
oldest pending intake/custody/job, poison count, mailbox freshness, send Unknown,
cache bytes/expiry and missing configuration. Avoid a new aggregate success
badge concealing a failed component. Logs filter actor/operation/record/time/
result/correlation with pagination and no secrets. Actual account actions and
engineering history remain durable; verbose telemetry is not business history.

Reports include existing Engineer activity, principal report counts and
received-to-ready/generated/Sent turnaround, held/Triage age and failures.
Provide explicit UTC-backed local display date ranges, numerator/denominator,
missing-date counts and CSV export with formula-safe escaping. Do not invent
historical dates from EVA interval arithmetic. Define Engineer activity by the
recorded event actor; assigned Engineer and signatory are separate dimensions.
Counts use actual Sent evidence, not generated/Submitted. Show incomplete
history honestly on the fresh dataset.

Expose every supported operator setting in the configuration matrix in
SHARED-CONTRACTS. Do not expose raw environment settings, databases, passwords,
tenant roles or inactive dummy features as configurable application controls.
AI Administration shows real transport activation, queued/running/failed jobs,
cancel/stop and source visibility. Existing DevelopmentOffline capability is
not labeled a deployed model. Automatic extraction may produce candidates;
it cannot acquire Engineer approval or trigger unattended messages.

**Proof:** non-admin forbidden server-side, valid filter pagination/export,
empty/partial date denominators, CSV formula input, correct event dimensions,
two-mailbox partial failure, unknown send, cache miss and disabled AI states.
Every admin link opens a real page and every mutation has a production caller.
No account-review action or empty Workflow Configuration page remains.

## A07 — measured efficiency, artifact and CI hygiene

**Implementer:** Sol reviews hot paths; Terra applies bounded fixes, Luna
enumerates generated artifacts. Reuse current four projects, typed/named
HttpClient registration, readiness, deployment scripts and tests.

Separate unrelated integration HttpClients so timeout, base URL and headers
cannot leak from the first registration. Keep per-service requests bounded;
no global retry that repeats writes. A owns DI changes; B/C own adapter methods.
Reduce avoidable broad navigation/health reads using existing count queries
and request-scoped projection; C fixes shell queries, B lazy Case queries.
Measure before adding indexes/caches. Preserve immediate account revocation.

Keep Worker triggers disabled in the deployment plan until schema, grants and
bootstrap validation complete; host readiness validates the current schema.
Use actual Web/Worker database roles for grant tests, not db_owner. Verify
NCRONTAB and trigger indexing in the later deployment proof. Idle polling
changes must preserve the accepted 15-minute freshness and notification/delta
recovery; adjust only a timer with measured wasted work. Do not reduce cadence
by intuition or label old cost samples as current savings.

F owns `.github/workflows/**`, `infra/**`, `src/Pegasus.Web/Dockerfile`, project/
lock files and `scripts/**`. C01 owns PR 646's Provider API failure-boundary
behavior and tests; A verifies that preservation in the combined checkout.
Remove tracked generated
`tests/Pegasus-Test-Logs/**` and `t2fix.log` after verifying no caller, and add
appropriate ignore coverage. Keep reference originals outside Git untouched.
Remove unused coverlet collector references only after dependency/caller scan.
Do not change SDK roll-forward just because a different preference exists.
Replace only source-shape gates proven to gate no behavior; retain meaningful
architecture, migration, UI, authorization and delivery tests. Prevent
Git-mutating script fixtures inheriting `GIT_DIR`, `GIT_WORK_TREE`, index or
alternate object settings; child tests must assert their temporary repo root.
Never rerun the old hazardous harness against the shared checkout.

**Proof:** dedicated HTTP clients and failure boundaries; all runtime browsers,
fonts and render dependencies in the built Web image; migration/grant failure
blocks Worker activation; isolated script test does not change parent HEAD,
config or index; measured query counts do not grow per row. Runtime counters,
allocation peaks and cold/warm timings use the same genuine samples and exact
build SHA. Existing broad build passes are supporting history, not new proof.

## A08 — accurate repository documentation and ticket handoff

**Implementer:** Luna prepares exact edits; Terra checks links/commands; Astra
resolves authority. Use [documentation corrections](../registers/documentation-corrections.csv)
and [ticket dispositions](../registers/tickets.csv) as the complete rosters.
Reconcile all affected current canonical docs, not a new in-repo plan library.

F reconciles AGENTS once: updated Kanmer 0.4.2 managed instructions, integration
proof versus later deployed proof, one worktree convention, no duplicate
conduct blocks, exact three-stream exception for this approved batch, no
planning-task ticket and no merges. Preserve unrelated dirty checkout work by
porting only reviewed bytes to the clean A branch. Operator notes are updated
only to record explicit D01–D17 decisions without altering other business
meaning. Current Triage wording in the dirty checkout is evidence to preserve.

Update `NOW.md`, `CONTEXT.md`, `docs/index.md`, governing PRD/FRD/ADR files,
`docs/design/README.md`, capabilities/boundaries/open-decisions,
current-architecture/operations/runbook/engineering and affected caller docs.
Each B/C author supplies exact domain edits; A alone applies shared canonical
pages. Historical accepted ADRs retain IDs and are superseded accurately;
fix ADR0032 frontmatter/body disagreement. No blanket rewrite, renumbering or
fabricated deployment claim. v3's missing verification JSON stays an evidence
limitation; do not claim its 65 checks/240 vectors were run.

Every existing ticket gets an exact stream/step, evidence and residual gate or
explicit defer/retire reason. Being contained in dev is not Done. No force take,
bulk final move or worktree deletion. Three owner tickets are created only at
future implementation startup; retirement of old PR/tickets follows the
preservation register, never this planning task.

**Proof:** all 29 correction groups dispositioned, current links/commands pass,
no active open question contradicts D01–D17, current-state pages distinguish
code/branch/deployment/operator proof, all 210 ticket mappings accounted.
Docs-only checks run once after the final content edits.

## A09 — combined integration, independent review and three-PR stop

**Reviewer:** fresh Sol, not an A implementer. Astra coordinates the unpublished
combined checkout described in COORDINATION. Validate each standalone branch
and the combination against shared contracts and every requirement/register.
Review source/caller reachability, authority, races, failure recovery, no legacy
shims, duplicated policy, excessive abstractions, ignored conflicts and false
delivery claims. Every finding has a fix/rejection/risk/deferral with evidence.

One verification process runs from a clean A or combined worktree:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category=Corpus"
pwsh -File ./scripts/Test-MigrationGrants.ps1
pwsh -File ./scripts/Test-DocumentationLinks.ps1
pwsh -File ./scripts/Test-MarkdownPlacement.ps1
pwsh -File ./scripts/Update-TestUiSnapshots.ps1 -Verify
pwsh -File ./scripts/Test-UiCatalogue.ps1
git diff --check origin/dev...HEAD
```

Compile before any earlier focused `--no-build` test. Record exact SHA/cwd,
command, output and exit code. Missing corpus/runtime credentials are
INCONCLUSIVE, not PASS; provider tests remain the named human release gates.
For commands requiring local SQL use the documented runbook profile and actual
runtime-role grants. No cloud credentials are used by local verification.

Open/update the single Stream A PR to dev after independent review, attach
standalone and combined evidence, link the other two PRs, and stop. Exactly
three replacement PRs remain open after the separately authorized superseded-PR
closeout. None merges. Deployment, data reset, real send, Glass's live acceptance
and v1 declaration require the later operator handoff; do not call this PR-open
state a released product.

## Ticket-by-ticket residual acceptance

This table is additional required scope in the named step, not a separate PR
or licence for adjacent cleanup. Read each linked ticket’s current body/gates.
The current reason overrides stale inherited ticket wording; verify already
integrated clauses and implement only the remaining gap.

| Ticket | Step | Exact residual / acceptance |
| --- | --- | --- |
| AUTO-003 | A05 | Expose completed authorized read/prepare actions only. D06 staff sends use the protected Web command, not autonomous MCP sends; retain scope/actor parity and no mailbox mutation tools. |
| AUTO-010 | A06 | Administer the existing AI job ledger, counts and stop control. The production-ineligible local SendToAi transport is not a second platform to activate. |
| AUTO-014 | A05 | Production callers were added; reconcile by-subject and QueryResponse evidence with the exact merge rather than rebuilding the job surface. |
| DELIV-010 | A07 | Refresh actual checkout size/timing before using the old 700 MB claim. Use shallow CI where history is unnecessary and fetch exact comparison commits where needed. |
| DELIV-018 | A08 | Derive capability summary counts from the current table; the introductory and summary totals disagree. Do not create a second registry. |
| DELIV-021 | A07 | Current telemetry and costs are captured, but readiness probes do not prove arrival-to-usable latency or recovery. Measure named journeys, failure recovery and complete working days. |
| DELIV-022 | A02 | Release 31 is historical and its changes are in later main. Reconcile its own proof/checklist and archive after valid disposition; do not redeploy release 31. |
| DELIV-025 | A07 | Share one CI path-selection owner with DELIV-044/048. Retain full correctness coverage for shared Core/migrations while avoiding duplicate builds. |
| DELIV-032 | F02 | Validate the actual ordered migration chain and resulting schema, including merged migrations. Regex/name checks cannot prove absence of duplicate-column operations. |
| DELIV-038 | A08 | Release-37 gaps remain part of a wider concrete documentation correction register. Correct current claims from code and live configuration, not from ticket stage. |
| DELIV-044 | A07 | Remove duplicate Azure-plan work and correct coverage cancellation behavior. Decide push-dev checks from coverage of exact PR/merge trees; a separate redundant run is not inherently required. |
| DELIV-045 | A08 | 150 commits are ahead of main and v1 adds more work. Refresh architecture and promote only a reviewed exact candidate after the v1 gates, not a blanket current-dev release. |
| DELIV-047 | A08 | Linux release tooling is merged but this Windows review did not execute a Linux release. Record source/test evidence separately from terminal and deployed proof. |
| DOCS-019 | A08 | Remove the stale embedded Andy-signature assertion and point to the configurable Case Sign-off Engineer tuple. Preserve accepted historical report identities. |
| INTK-027 | A04 | Policy re-evaluation reads the logical confirmed Box version through shared content/cache after staging expiry; C02 calls it. |
| INTK-042 | A04 | Immediate publication is implemented; prove committed work is eventually dispatched after queue failure without duplicate Case allocation. |
| INTK-043 | A07 | Source warm-path work is merged, but no observed five-second end-to-end evidence follows from timer cadence or health probes. Measure arrival-to-usable custody/extraction. |
| INTK-050 | A08 | Correct FRD02/12 to one grouped submission decision with useful per-file diagnostics; C08 provides matching UI. |
| KANMER-007 | A08 | Its historical Done list is stale. Reconcile exact proof/checklist discrepancies, preserve failure/waiver history and never mass-close from merge containment alone. |
| MAIL-013 | A02 | Graph wake handling is deployed, but notifications followed by delta can miss a replication-lagged message until recovery. Keep residual latency work in MAIL-035. |
| MAIL-014 | A02 | Disable/address-change/re-enable must establish the correct poll/retention boundary. Reuse mailbox state and prevent an old cursor from silently skipping new work. |
| MAIL-023 | A07 | Snapshot hardening/scoped capture is already merged and catalogue checks now pass. Prove fresh capture determinism and retire claims fixed by UIIMP-005/015. |
| MAIL-026 | A03 | D06 includes one staff-initiated send workflow and truthful Sent evidence through A03; B07 prepares immutable report/fee artifacts and C08 supplies the retained-mail/manual-chaser caller. Flag/delete/folder mutation and unattended messages remain excluded. No agent performs a real send. |
| MAIL-027 | A03 | D06 includes one staff-initiated send workflow and truthful Sent evidence through A03; B07 prepares immutable report/fee artifacts and C08 supplies the retained-mail/manual-chaser caller. Flag/delete/folder mutation and unattended messages remain excluded. No agent performs a real send. |
| MAIL-030 | A03 | D06 includes one staff-initiated send workflow and truthful Sent evidence through A03; B07 prepares immutable report/fee artifacts and C08 supplies the retained-mail/manual-chaser caller. Flag/delete/folder mutation and unattended messages remain excluded. No agent performs a real send. |
| MAIL-031 | A02 | Mailbox freshness is settled at fixed 15 minutes with no historical backfill. Remove configurable-threshold questions; UI must expose actual synchronization and activation state. |
| MAIL-035 | A02 | On an approved Inbox notification, fetch that immutable message directly and verify parent mailbox/folder; retain delta/recovery for completeness. Graph documents replication lag. |
| PLAT-026 | A02 | The mailbox editor stores approvals; it does not create Exchange mailboxes or grant application access. Add discovery, validation, enable/disable and a truthful infrastructure onboarding flow. |
| PLAT-027 | A01 | Remove periodic account review from UI, Core, persistence, jobs, docs and tests. Keep account administration, add disable/delete/password reset and force logout/lease clearance. |
| PLAT-035 | F02 | Local high-privilege SQL tests missed runtime grant failures. Run representative Web/Worker writes using disposable restricted roles and validate migration/bootstrap grants together. |
| PLAT-038 | A04 | Local content addressing and managed-document addressing disagree for retained intake downloads. Reuse one logical content reader with local/production adapters. |
| PLAT-046 | F02 | Old Worker code can run while column changes are being applied. Sequence quiescence, migration/grants, artifacts and readiness on the exact release candidate. |
| PLAT-048 | A06 | Health and Engineer activity queries are implemented. Finish the actual admin caller via PLAT-051; query registration alone is not a delivered report. |
| PLAT-049 | A06 | Operations AI jobs exist; health moved to Administration later and local SendToAi is production-ineligible. Correct claims and finish only missing real callers. |
| PLAT-051 | A06 | Administration currently lacks usable Service health, Action Logs and Reports despite existing queries. Wire the named production queries with filters, access and honest empty states. |
| PLAT-056 | A07 | Finish named external-work state-string deduplication using existing ExternalWorkStatePersistence; B owns EVA adapter changes and F coordinates shared contract. |
| PLAT-058 | A07 | ReceivedToday is computed and not rendered. Remove the unused projection/query rather than adding a dashboard merely to justify it. |
| PLAT-060 | A07 | Reuse LondonCalendar for remaining conversions. Do not silently substitute UTC for an unavailable business timezone. |
| PLAT-063 | A07 | Three unrendered Operations projections generate avoidable EF work. Remove them with PLAT-058 and scope rail counts to actual page needs. |
| PLAT-064 | A01 | Administrator password reset must work without email: one-time temporary password/recovery presentation, forced change and session/token revocation using existing Identity mechanisms. |
| PLAT-066 | A07 | The pack is not an approved representative 2,000-case content workload. Define and obtain the genuine cohort/distribution and peak burst; do not fabricate domain data or label extrapolation as proof. |
| PLAT-069 | A06 | Health was removed from Operations, but the destination is unfinished. Verify the partial-data link only after PLAT-051 provides the real page. |
| PLAT-071 | A08 | DOC/MSG support exists in code; old absent/undeployed assertions conflict. Record exact deployed artifact capability separately from format recognition and live sample proof. |
| PR-066 | A07 | Valid Flex scale-group fix is merged and current Worker is running. Close using its exact release evidence; no further scale change follows from this old card. |
| PR-070 | A07 | Both pinned main and dev catalogue checks pass: 54 routes, 58 prototypes, zero broken references. Reconcile the named old reference before archiving the stale defect. |
| PR-071 | A07 | Main link check fails on the .opencode relative link; dev passes after content correction. Checker exclusion consistency is a residual policy issue, not a current dev broken-link blocker. |
| TICK-036 | A02 | Desk mailbox onboarding is explicitly requested. Configure and verify it through the admin/infrastructure flow; enabling a mailbox need not wait for every sender route if unknown work fails closed. |
| TICK-037 | A02 | Engineers mailbox belongs in the same UI onboarding flow. Do not mutate Outlook or silently enable sending while adding intake configuration. |
| TICK-038 | A02 | Info mailbox belongs in the same supported administration flow, with observed access/cursor health and no database/code editing by operators. |
| TICK-075 | A03 | D06 includes one staff-initiated send workflow and truthful Sent evidence through A03; B07 prepares immutable report/fee artifacts and C08 supplies the retained-mail/manual-chaser caller. Flag/delete/folder mutation and unattended messages remain excluded. No agent performs a real send. |
| TICK-088 | A03 | D06 includes one staff-initiated send workflow and truthful Sent evidence through A03; B07 prepares immutable report/fee artifacts and C08 supplies the retained-mail/manual-chaser caller. Flag/delete/folder mutation and unattended messages remain excluded. No agent performs a real send. |
| TICK-102 | A05 | DevelopmentOffline loopback SendToAi cannot run in production. Use the existing durable job/MCP external-client route and prove its real caller; do not add a daemon merely to preserve the preview. |
| TICK-103 | A05 | Finish existing named-job/MCP external round-trip and paging, with actual caller-backed state; A06 owns admin job viewer/count/stop UI. |
| TICK-105 | A06 | Expose the existing Engineer activity query with explicit period, denominator and query-type definitions. Do not turn missing historical events into zero performance. |
| TICK-106 | A06 | Provide per-principal report counts/types/periods from generated/delivery evidence. Invoice generation is later accounting scope, not a second report-count store. |
| TICK-107 | A06 | Compute holding age and turnaround from distinct received/ready/generated/sent events; receipts cannot be replaced by one file date without losing these measures. |
| UIIMP-005 | A07 | Snapshot hardening is merged. Fresh capture determinism and its own exact proof remain required; catalogue pass alone does not prove pixels or generated-script behavior. |
| UIIMP-010 | A09 | Perform complete combined browser/role journey across all three streams, with B09/C09 owning their domain checks; current plan is not hosted UI proof. |
| UIIMP-015 | A07 | Scoped capture is merged and both catalogues pass. Verify relevant fresh captures and preserve its exact proof rather than rebuilding snapshot selection. |



## Stop condition

All assigned implementation, independent review, standalone and combined checks are complete; exactly three replacement PRs target dev, open and unmerged. No merge, deployment, reset or live provider write. External provider/workload evidence remains honestly named operator gates, never fabricated PASS.

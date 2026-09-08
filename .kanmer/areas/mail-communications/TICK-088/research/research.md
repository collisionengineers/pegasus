# Research — MAIL-12

## Question

How should Pegasus let authenticated staff compose, reply, forward and idempotently send from an approved mailbox with deliberate recipient/content review and permanent send evidence?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: Pegasus has sent-evidence polling but no outbound composition/send Core use case or Graph send adapter. This is Later/0.5.0 and introduces an external write boundary distinct from local planning and tests.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Workflow/PollSentEvidence.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

# Research refresh — 2026-08-20

## Basis and question

Against `origin/dev` at `a3c88a7bbdb43cf4cbd9303022397f6e028d7bf9`, what is the smallest Core-owned boundary for authenticated human compose/reply/forward/send, which current Graph and Sent-evidence conventions are reusable, and how must MAIL-12 remain separate from MAIL-17 and MAIL-19?

The full ticket folder, EPIC-006 context, TICK-054's refreshed documents, TICK-053, TICK-049, TICK-075/MAIL-17, TICK-066/MAIL-19, governing/current-state docs, and the current Core/Infrastructure/Web/test callers were inspected directly.

## Verified current state

- **There is no outbound-mail use case or writer.** `GraphMailClient` in `GraphApprovedSources.cs` performs GET-only delta and MIME reads. `Message.cshtml.cs` is a retained-message read/correction caller. Current architecture excludes Graph mutation and the runbook records `Mail.Read`, not draft/send authority.
- **The approved-mailbox policy does not authorize sending.** `ApprovedMailboxRouteScope` contains only `InboundIntake` and `SentEvidence`; the stored estate has mailbox, Inbox and Sent identities but no outbound-send scope, Drafts identity, or signature configuration. FRD-08 explicitly separates read/intake scopes from draft/send/admin scopes. MAIL-12 therefore needs a distinct `OutboundSend` approval; `SentEvidence` must never be interpreted as write authority.
- **The existing Sent reader is reusable evidence, not a send implementation.** `GraphApprovedSentSource` supplies mailbox/Sent scope, immutable item id, Internet Message-ID, conversation id, reply-chain/In-Reply-To identities, authoritative sent time, MIME SHA-256 and occurrence hash. `PollSentEvidence` already fail-closes on an unapproved Sent scope and records unmatched/ambiguous/malformed outcomes.
- **Report Sent evidence is deliberately narrower.** `ApprovedMailboxReportSentEvidence.cs` and `EfCaseReportSentEvidenceStore.cs` retain immutable report evidence under a system-worker-only boundary with stable request fingerprint, replay/conflict and `ActionHistory`. That table/contract cannot be renamed or widened into general sent mail: it represents an exact report-association claim.
- **No email-signature source exists.** Report-renderer engineer signatures are document assets and are unrelated. MAIL-12 must add mailbox-owned configured correspondence signature data under the existing administrator mailbox setting; it must fail closed when the selected approved mailbox has no active signature and must never fabricate signature content.
- **The real staff seam is proved.** New compose can be a focused `/Mail/Compose` page; Reply and Forward start from the exact retained `/Inbox/{id}` detail, whose Core query already supplies server-owned mailbox/message/conversation, recipients, body and attachments. Client input must carry only the internal source id/kind, never arbitrary Graph mailbox or message identities.

## Minimal Core boundary

Use one focused outbound-mail aggregate/use case, not a generic mail-action framework:

1. A closed kind vocabulary: `Compose`, `Reply`, `Forward`.
2. Draft creation/update input: approved sender-mailbox id; optional exact retained source id required for reply/forward; To/CC/BCC; subject; body; retained/uploaded attachment references; the mailbox signature version; actor; expected draft version; and operation key.
3. Confirmation/send input: draft id and expected version plus an idempotency key and a fingerprint over the exact final sender, source/kind, full To/CC/BCC, subject/body, attachment identities+hashes, and signature version/content hash. The confirmation view is generated from that same persisted version.
4. Separate Core ports for durable draft/operation state and the outbound Graph action. Infrastructure owns Graph ids and transport responses. The Core result owns `Draft`, `Pending`, `Sent`, `Failed`, and `Unknown` state plus permanent attribution. Do not put message bodies into `ActionHistory`; record hashes/identities and keep the drafted content in its purpose-built record.
5. Same-key/same-fingerprint replay returns the recorded result; same key with different input conflicts. Stale draft/signature/source/mailbox state refuses before Graph. A failed send is retried deliberately with a new operation key. An unknown response is never blindly resent.

This is the smallest boundary with two concrete callers already required by the operator: authenticated Web and later Automation MCP. It does not introduce a Worker caller; autonomous outbound belongs to MAIL-19.

## Verified Graph v1 mechanics

- New compose creates a draft in Drafts; create-reply and create-forward create provider-native reply/forward drafts; all return the draft message, and draft recipients/subject/body can be updated later. Send the existing draft with `POST /users/{mailbox}/messages/{id}/send`. Sources: https://learn.microsoft.com/en-us/graph/api/user-post-messages?view=graph-rest-1.0, https://learn.microsoft.com/en-us/graph/api/message-createreply?view=graph-rest-1.0, https://learn.microsoft.com/en-us/graph/api/message-createforward?view=graph-rest-1.0, https://learn.microsoft.com/en-us/graph/api/message-update?view=graph-rest-1.0, https://learn.microsoft.com/en-us/graph/api/message-send?view=graph-rest-1.0
- Every draft/reply/forward/update/send/probe request must use the existing exact mailbox identity, Graph HTTPS host restriction, token convention and `Prefer: IdType="ImmutableId"`. Microsoft documents that a draft created with that preference keeps the same id when its Sent copy is created, although the Sent copy can be temporarily unavailable. This gives MAIL-12 a direct reconciliation key without inventing subject matching or misusing `X-Pegasus-Case-Id`: https://learn.microsoft.com/en-us/graph/outlook-immutable-id
- Draft send returns only `202 Accepted`; it does not prove processing completion or delivery. A successful operation stays pending until the exact immutable id is observed/probed in Sent. If the response is lost, probe that id: Sent proves submission; an extant Draft proves unsent; temporary absence remains Unknown and is not resent automatically.
- File attachments below 3 MB use the attachment POST; 3–150 MB uses an upload session, and tenant message-size policy can be lower. Pegasus should expose the actual provider/configured limit and visible failure, not invent a larger promise: https://learn.microsoft.com/en-us/graph/api/resources/attachment?view=graph-rest-1.0, https://learn.microsoft.com/en-us/graph/api/attachment-createuploadsession?view=graph-rest-1.0
- A custom `X-Pegasus-Case-Id` is currently report matching only. General MAIL-12 reconciliation should use the persisted immutable draft id. A new correlation header/extension is unnecessary unless planning proves a specific uncertain-create recovery caller.

## Sent evidence and history

Add one general outbound operation/evidence record keyed by immutable draft/Sent id and confirmation fingerprint. When the existing Sent poll observes that id it can reconcile the operation and retain the same provider facts it already extracts. Keep its existing Triage exact-reply and report-case branches unchanged; a MAIL-12 operation does not become report evidence merely because its message is associated with a Case. A staff action is an attributed assertion until exact Sent evidence is retained, and neither `202` nor Sent evidence proves delivery, reading, content correctness, Case closure or Triage completion.

Reuse TICK-054's landed external-operation reservation, fingerprint, replay/conflict, `Pending/Succeeded/Failed/Unknown`, actor/reason/history and Graph error/probe conventions after that ticket merges. Add only outbound-specific draft content, recipients, attachments, signature version and Sent reconciliation. Do not create a common command bus or one “mail operation” union for unrelated state actions and sends.

## Capability isolation

- **MAIL-12:** a deliberate authenticated human-authored general compose/reply/forward/draft/confirm/send journey. An Automation caller may invoke the same Core commands with the same exact confirmation/version/idempotency rules; it is not autonomous scheduling.
- **MAIL-17 / TICK-075:** remains the targeted report/fee-note transaction with approved principal destinations/CC/standing notes, immutable report artifact/version, original-thread/provider route, Box filing, completion and management event. MAIL-12 cannot mark a report sent, enter post-report state, file to Box, or satisfy CASE-23. FRD-11 and the MAIL-12 capability row explicitly preserve this boundary.
- **MAIL-19 / TICK-066:** remains Worker-owned automatic chasers/other outbound with eligibility, schedules/templates and automation retry policy. MAIL-12 adds no timer, autonomous caller or reusable “auto send” switch.

## Authorization and live-write boundary

- Local implementation and verification use LocalDB/local files and fake Graph HTTP only. No local-alpha run may create/update/delete a real Outlook draft or send mail.
- Production composition first needs a separately approved exact Entra application-permission/admin-consent change. Draft creation/update/attachments require `Mail.ReadWrite`; send requires `Mail.Send`. Exchange Application RBAC must confine both to the exact approved sender mailbox, and the evidence must include a negative outside-scope test. Adding permission still authorizes no message operation.
- Every production draft creation, reply/forward draft creation, draft update, attachment upload/removal and send is an external Outlook write. Immediately before the live acceptance journey, obtain exact approval naming the sender mailbox, exact reply/forward source messages, every attachment, exact final subject/body/signature, complete To/CC/BCC, each permitted write step and maximum send count. The only approved recipient is `digital@collisionengineers.co.uk`; no operational correspondence. Abort any identity/content/version mismatch.
- Explicit in-product confirmation is a business control, not external-operation approval. The confirmation summary must be generated from the immutable draft version that is sent. Send replay must not create a second message. Capture provider draft state, operation key/version, 202 result, exact immutable Sent/thread evidence and permanent history.

## Dependencies and execution order

TICK-088 should execute after TICK-054 (MAIL-13), which itself follows TICK-049/MAIL-07. Refresh exact symbols after TICK-054 lands. TICK-053 should also stabilize retained-message/thread/source shapes first. TICK-056/UI-10 and AUTO-003 consume the final Core result later and do not own send policy.

## Open questions and assumptions

The operator has already settled full feature scope, configured signatures, approved live recipient and the exact just-in-time approval requirement. No further product question is required for planning: the existing administrator-approved-mailbox setting is the narrow owner for per-mailbox signature content/version and outbound-send enablement. Missing signature/configuration fails closed. Provider/tenant attachment ceilings are runtime facts and visible adapter outcomes, not a fabricated Pegasus limit.

## Current v1 residual audit — 2026-09-08, intake_audit

Evidence base: accepted dev
`96777888bfa7ee7f85d63979a4a09ae10cda7d13`, read with `git show` /
`git grep`; no checkout/source mutation. TICK-088 remains Preparing and
untaken. Research/plan/files/checklist/questions from 2026-08-20 were read
fully and are retained in history; this continuation supersedes their
as-built premises, not the recorded operator answers. EPIC-006 context and
current FRD-08/ADR-0036 were read in full, relevant protected operator notes,
design mail rules, Astra A03/C08 and coverage review were inspected.
No build, test, capture, Graph call, mailbox or cloud write occurred.

### Scope and authority reconciliation

The checked 2026-08-19 answers in open-questions@dd41e2942e0b64b3 require
Bcc, saved drafts, configured per-mailbox correspondence signatures and exact
send confirmation. A missing/disabled signature must fail closed; report
signatory image assets are not correspondence signatures.
Astra A03/C08 implemented a narrower To/Cc, Case/Triage-context transport
slice. `v1_implementation_plans/review/coverage-review.md:34` explicitly
defers generic composer behavior. Therefore neither a historical broad
activation nor Astra's implemented transport alone proves full MAIL-12
acceptance. Root has assigned this residual preparation; implementation
scope still needs the proposed plan's explicit decisions.

Current FRD-08 says no autonomous, scheduled or Automation Actor send.
That current authority supersedes the old ticket sentence permitting
Automation submission. MAIL-17 report delivery/readiness and MAIL-19
scheduled chasing stay separate. Non-Case composition remains excluded
under root's current instruction; no nullable-context redesign is proposed.
Only `digital@collisionengineers.co.uk` is a positive test recipient and
this preparation authorizes no actual messages.

### Verified production path and missing behavior

1. **Existing durable transport is real.**
   `Core/Operations/StaffMailSend.cs:26–32` carries mode, original,
   Case/Triage context/version, To/Cc, body, attachments and operation key.
   `StaffMailSendEngine.cs:154–239` validates staff, persists Prepared,
   obtains the existing execution lock, validates exact attachment bytes
   and encoded size, creates or recovers a provider draft, attaches and
   sends. Same-key changed payload conflicts. Submitted is not Sent and
   Unknown never blindly resends. The existing Sent poll and operation
   correlation are not replaced.
2. **A provider draft is not a saved composer draft.**
   `EfStaffMailSendStore.PrepareAsync:92–130` already stores To/Cc JSON,
   subject, body, selected attachment references, original identities,
   actor/context, payload hash, state, version and concurrency token in
   `StaffMailSendOperationEntity` (V1FoundationEntities). However,
   `IStaffMailSend` exposes Send/Get/GetLatest/Reconcile/Cancel only;
   it has no user Save, editable load/update or draft-list contract.
   Send creates Prepared and proceeds toward Graph in the same call.
   GET returns operation status, not editable composition.
3. **Bcc is absent end to end.** No Bcc member in command, persisted
   Recipients, either composer binding, or
   `GraphStaffMailSender.BuildMimeMessage:140–175` (To and Cc only).
   The adapter already uses MimeKit and the chosen Graph base64 MIME route.
   Microsoft documents BCC as an applicable MIME header for that existing
   endpoint; no SDK/new API is needed:
   [Create message](https://learn.microsoft.com/en-us/graph/api/user-post-messages?view=graph-rest-1.0).
   The [message resource](https://learn.microsoft.com/en-us/graph/api/resources/message?view=graph-rest-1.0)
   also has bccRecipients. These public docs were checked on 2026-09-08;
   they do not supply Pegasus acceptance or authorize any write.
4. **No mailbox signature configuration exists.**
   `ApprovedMailbox` and UpdateApprovedMailboxRequest expose StaffSend,
   Generation and VerifiedEncodedMessageSizeLimit but no correspondence
   signature. AdministrationPolicyEntities/ModelConfiguration and
   EfApprovedMailboxStore agree. The existing Administrator-owned mailbox
   update, optimistic Version and history are the correct extension point.
   A signature-only change must not restart Inbox/Sent retention or increment
   their activation generation. No report signature asset may be borrowed.
5. **No exact saved-version confirmation.**
   `Compose.cshtml` renders a form directly targeting Send;
   `ComposeModel.OnPostSendAsync:115–211` directly calls IStaffMailSend.
   Message.cshtml lines 227–300 contains another inline editable form whose
   Send button targets Reply/ReplyAll/Forward. It is not a confirmation
   dialog. MessageModel.SendCorrespondenceAsync checks current retained
   context and calls the same send port. Neither path previews and confirms
   an exact persisted version. The earlier audit shorthand calling this a
   confirmation dialog was corrected to root before this document.
6. **New compose lacks a production entry.** A whole Web source search
   found no link to `/Mail/Compose` or `/Inbox/Compose`; the route exists
   and tests call it directly. Message's header exposes Reply/Reply all/
   Forward only. `_CaseCorrespondence.cshtml` is query-mail rows, not a
   New-mail action, and is conditional on those rows existing. A Case-context
   entry and draft resumption must be wired without relying on manual URLs
   or exposing a raw Case GUID input.
7. **Current source and context checks need preserving and completing.**
   Reply defaults use retained Reply-To, or original From only when that
   header is absent; missing/unusable targets refuse, and ReplyAll excludes
   the approved sender. Forward accepts explicit To/Cc. These predicates
   currently live in MessageModel and must move intact if one composer
   replaces the duplicate forms.
   The Web checks Case version before sending, but
   `EfStaffMailSendStore.MapExecutionAsync:394–432` only validates
   CaseReport generation/artifacts. GeneralCorrespondence's final Core
   provider boundary has no persisted Case-version/current-association
   recheck. Exact draft confirmation cannot rely solely on that earlier
   Web read.
8. **Known source restrictions remain.** The attachment resolver already
   lists/resolves retained Case versions and Intake asset identities through
   the existing logical-document boundary; no upload framework is required.
   Report/Triage purposes already share the transport but their eligibility
   remains owned by their current callers. No email-specific Case edit lease
   or lifecycle effect is invented.

### Smallest candidate design, not approved execution

Reuse the existing operation row/body/recipient fields and existing
EfStaffMailSendStore rather than creating a second mail table/store. An
explicit pre-confirmation `Composing` state can make that row mutable only
while it has no provider attempt. Save/GET remain local SQL actions.
Confirmation atomically freezes its version/hash into the existing Prepared
state; provider workflow then uses the existing operation identity/lock.
The active-original SEND exclusion must ignore editable draft rows but still
allow only one frozen/in-flight send across actors. A saved draft is owned
by its staff author and cannot be edited by another actor or after freezing.
Existing current-role/version/history checks and safe oversize reporting apply.

This needs a narrow current schema migration for the state constraint,
mailbox signature content/version and its frozen identity on the operation;
no new table, migration stream, grant or worker is justified. Existing
StaffMailSendOperations grants are already SELECT/INSERT/UPDATE for the Web
and Worker; the actual restricted Web caller and Worker negative command
authorization still need focused proof. Bootstrap's existing table census
is context, not an excuse to broaden permissions.

A local SQL save is the recommended minimal interpretation of resumable
drafts. Earlier research described provider-native editable draft updates.
No current requirement proves that editing must be visible in Outlook before
staff confirms sending, so this distinction is an explicit root decision,
not a silent compatibility path. Also decide the bounded signature application
scope (proposed: this human-authored GeneralCorrespondence composer;
MAIL-17/Triage semantics stay separate).

### Existing owners and evidence

- MAIL-026/MAIL-027 remain Backlog, untaken, and describe partly integrated
  original UI/transport plus separate Flag/Delete/EVA-detection work. Preserve
  links/history; do not absorb their unrelated behaviors or claim them Done.
- MAIL-030 is the explicit owner of the approved default outbound mailbox
  setting. FRD-08 requires that default for New; current Compose instead
  offers a selector. Do not invent a first-mailbox fallback or duplicate the
  setting. Root must sequence that owner or authorize a precise handoff.
- ENG-029/ENG-031/ENG-036 share prospective Case action/generated/README
  paths; TICK-085 still has a Verifying DI claim. No claim is released by
  this audit. A fresh exact path census is required before execution.
- Reuse StaffMailSendTests, StaffMailSendPersistenceTests,
  StaffCorrespondenceWebTests, ProductionGraphSourceTests,
  ApprovedMailboxAdministrationWebTests and AzureSqlRuntimeRoleMigrationTests.
  Existing attachment/thread/Unknown/replay assertions remain. Use recording
  HTTP only; positive recipient digital@collisionengineers.co.uk, no sends.
  Tests written around the old direct Send must be updated to the actual
  save/confirm caller, not deleted.
- The configured Kanmer source survey returned zero declarations. No missing
  plugin, provider install, Entra grant or live activation is requested.

### Open decisions before dispatch

Root must confirm the bounded Case-only v1 residual, local resumable draft
state on the existing row, signature format/scope and exact MAIL-030 default
sender sequencing. Explicit current FRD/design/capability synchronization
and path handoffs must be approved before take. Those are plan stops, not a
false Done or credential-block claim.

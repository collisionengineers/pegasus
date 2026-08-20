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

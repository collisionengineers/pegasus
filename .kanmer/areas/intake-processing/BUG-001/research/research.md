# Research — BUG-001

## Question

Why did an earlier authorised QDOS email allocate a Case/PO and create Box custody while the later test stopped after receipt?

## Current source path

The code separates three concerns, but the current orchestration reconnects them incorrectly:

1. `QdosMailRoutePolicy` establishes the accepted QDOS direct-provider route from the effective sender and returns route owner/work provider `QDOS`.
2. `QdosMailClassificationPolicy` classifies the message (including Audit versus Inspection) from the QDOS message rules.
3. `QdosInstructionExtractionPolicy` should extract instruction fields, but it independently scans every readable fragment for `\bQDOS\b` and requires the same fragment to contain at least two instruction labels before it returns an `InstructionDraft`.
4. `ProcessIntake` ignores the already accepted QDOS route when deciding extraction applicability. If extraction does not satisfy that independent same-fragment gate, it returns `needs_sorting` with “The readable content does not provide enough evidence to suggest a principal.”
5. `IntakeAllocation` then reads the principal from `InstructionDraft.SuggestedPrincipalCode`; no draft means no allocation, no external custody work, and therefore no Box folder.

This means the extraction policy is currently re-identifying the principal while extracting document fields. That coupling is the defect.

## Operator clarification

The product rule supplied on 2026-08-17 is authoritative for this investigation:

- QDOS is identified from the email body.
- - Principal identification must not ordinarily be inferred by extracting attached documents; document extraction is used only where necessary for the instruction data.

The prior attachment-token diagnosis and proposed PDF-marker fix are superseded. Attachment branding or extracted PDF wording is not principal-identification evidence.

## Live comparison

All estate operations used for this research were read-only.

### Earlier successful receipt

Receipt `2c4888d6-4098-4d22-a46a-d976286a27b0`:

- route: accepted / QDOS / direct provider;
- classification: Audit / repairable;
- extraction happened to find QDOS plus multiple labels in one attachment fragment;
- allocation produced `QDOS26001` and Audit reference `a.QDOS26001`;
- custody work `3565e349-2535-4f6f-90b3-4e2cc7a5f9b4` completed;
- Box remote ID `409001353539` was confirmed.

### Later failing receipt

Receipt `9a91fe16-d62f-4477-a11e-830fd96f672a`:

- route: accepted / QDOS / direct provider;
- classification: Audit / repairable;
- evidence already recorded a strong `qdos-content-marker` from `EmailBody`;
- standalone Audit evidence was recorded;
- fields/draft were nevertheless empty and the receipt became `needs_sorting`;
- there were zero allocation attempts, Cases, case links, or external-work records.

The later email therefore contained the required QDOS body evidence. It failed because the policy additionally demanded that one fragment contain both the QDOS marker and two extraction labels. The attachments held extractable instruction fields separately, so the incorrect coupling prevented a draft.

## Root cause

The root cause is not PDF extraction, Worker registration, queue execution, allocation recovery, or Box.

It is the contract and orchestration around `QdosInstructionExtractionPolicy`:

- principal evidence from the email body is detected but is not sufficient to establish the QDOS extraction context;
- field labels in attachments are incorrectly required to re-prove that principal in the same fragment;
- `ProcessIntake` bases `CaseCreated` eligibility on this coupled extraction applicability even after QDOS route and classification have succeeded;
- automatic allocation depends on the draft's suggested principal, so the coupled gate stops the entire downstream chain.

The current unit tests pass because they explicitly encode the wrong coupling, notably `QdosMarkerWithoutTwoInstructionLabelsCannotProduceDraft` and `ProofCannotBeAssembledAcrossSeparateContentFragments`. They prove current behaviour, not the clarified product rule.

## Required behavioural correction

For QDOS email intake:

- use the email-body QDOS evidence to establish the principal context;
- keep accepted-route and message-classification checks as separate required gates;
- extract instruction fields from the appropriate readable instruction content without requiring attached documents to identify QDOS;
- build the QDOS draft/principal from that established context;
- fail closed when required route, body identity, classification, or mandatory instruction evidence is missing or ambiguous;
- preserve replay-safe allocation and custody sequencing.

No fallback that identifies QDOS from an attachment is authorised by this clarification. If such a fallback is later required, it needs an explicit product rule and separate tests.

## Conclusion

BUG-001 is current. The later receipt correctly identified QDOS in the email body but the extraction policy discarded that fact by requiring attachment-local principal proof. The plan must decouple email-body principal identification from document field extraction. Once corrected and deployed, the retained receipt can be re-evaluated only with exact-target approval; successful allocation would then enqueue the existing Box custody path.

## Correction and expanded source audit — 2026-08-17

This section supersedes every earlier statement in this document that treats the word `QDOS` in an email body, subject, filename, attachment, or extracted document text as principal-identification evidence.

### Settled identity rule

The operator-confirmed rule is:

- A direct message is identified as the QDOS route only when its effective sender has exact whole-domain equality with one of `qdosassist.co.uk`, `qdosassists.co.uk`, or `qdoslaw.co.uk`.
- When the transport sender is Collision Engineers staff at `@collisionengineers.co.uk`, the single proved prior/original sender is the effective sender and must match one of those domains.
- Content and attachment extraction begin only after that route identity is settled. They do not re-identify or override the principal.
- A missing, malformed, conflicting, or non-matching effective sender fails closed. Text containing “QDOS” cannot rescue it.

This is already the contract in `docs/operator-notes.md` (provider/intermediary routing), FRD-09, ADR-0008, the versioned provider-domain snapshot, and `QdosMailRoutePolicy`.

### Exact defect path

1. `ProcessIntake.AssessAsync` correctly calls `EvaluateMailRoute` for mailbox sources. A non-accepted route returns `NeedsSorting` before extraction.
2. For an accepted mailbox route, `ProcessIntake` records the route, evaluates classification and case matching, but calls `extractionPolicy.Extract(readResult, processedAtUtc)` without the accepted route.
3. `QdosInstructionExtractionPolicy` constructs and runs its own `QdosMailRoutePolicy`, but it does not use the accepted route to establish applicability or the draft principal.
4. Instead it scans every readable content fragment. Any `\bQDOS\b` occurrence is recorded as strong `SupportsPrincipal` evidence. Applicability requires one fragment to contain both that token and at least two recognized field labels.
5. Only after that content gate passes does it extract fields across all content and hard-code `InstructionDraft.SuggestedPrincipalCode = "QDOS"`.
6. `ProcessIntake` maps `Applicable` directly to `CaseCreated`. Therefore the correctly accepted sender route can still be discarded by the unrelated same-fragment content gate.
7. `AllocateIntake.AttemptAutomaticAsync` later takes the allocation principal from `InstructionDraft.SuggestedPrincipalCode`, not from the persisted accepted route selection. The downstream command therefore trusts the value produced by the wrong identity owner.
8. No draft means no automatic allocation, Case/PO, case link, custody work, or Box folder. Box is a downstream non-event, not the failing component.

### Other problem areas found in the same identity boundary

- **Non-mail content can invent QDOS.** `ProcessIntake.EvaluateMailRoute` deliberately returns no route for manual-upload and automation channels, but it still calls the same extraction policy. A manual document containing “QDOS” plus two labels can become `CaseCreated` with principal QDOS despite no accepted QDOS mail route or authenticated provider principal. Existing manual-upload tests encode this behaviour.
- **Arbitrary transport strings are treated as principal hints.** `AddTransportEvidence` treats any transport evidence value containing “QDOS” as weak `SupportsPrincipal` evidence, including filename and sender strings that do not match an approved domain. This cannot currently create a draft alone, but it violates the settled evidence model and can change NotApplicable to Indeterminate.
- **No route/draft consistency invariant exists.** The generic extraction result has no accepted-route context, `EnsureConsistentPolicyResult` checks only applicability-versus-draft nullability, and automatic allocation reads the draft principal. Nothing centrally proves that the route-selected work provider and draft/allocation principal agree.
- **Tests protect the defect.** `QdosInstructionExtractionPolicyTests` explicitly expects content to identify QDOS, expects a same-fragment label threshold, and tests QDOS-like metadata as principal evidence. Several `ProcessIntakeTests` use a manual-upload source with content-only QDOS identification. These must be replaced or reframed around an already-established route/principal context.
- **Classification is adjacent but distinct.** Current tests intentionally say classification never changes the intake decision. That is not the cause of the observed failure: the failed live receipt was classified successfully. Any case-type fail-closed correction must preserve the settled separation between principal route identity and message classification rather than using classification to re-prove QDOS.
- **Case matching already consumes the accepted route.** `EvaluateIntakeCaseMatch.ExecuteAsync(readResult, mailRouteDecision, ...)` is the useful existing pattern: provider-specific work is selected from the accepted route rather than rediscovering provider identity from content.
- **The suffix list is guarded, not the defect.** `QdosMailRoutePolicy.AcceptedDirectDomains` mirrors the QDOS entry in `provider-domains.v1.json`; unit and integration tests assert exact equality and reject subdomain/suffix widening. No domain-list change is required.

### Implications for BUG-001

The correction must make accepted principal identity an explicit, immutable input to extraction and allocation. Once the QDOS route is accepted, field extraction may use the readable instruction content without requiring any QDOS token. Content must never establish QDOS for mailbox, manual-upload, or automation intake. Other channels may create definitive intake only from their own separately authenticated or staff-confirmed principal context.

The implementation must retain the existing fail-closed mail-route behaviour, field ambiguity/missing-value handling, case-match ambiguity handling, replay-safe allocation, and custody sequencing. It must not change Box, queues, PDF/OCR parsing, the three accepted domains, or staff-forward reconstruction.

### Source evidence

- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs`: correct effective-sender and exact-domain route owner.
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`: duplicate route evaluation, arbitrary content identity evidence, same-fragment gate, hard-coded draft principal.
- `src/Pegasus.Core/Intake/ProcessIntake.cs`: accepted route is calculated and persisted but not supplied to extraction; applicability alone selects `CaseCreated`.
- `src/Pegasus.Core/Intake/IntakeAllocation.cs`: automatic allocation principal comes from the instruction draft.
- `src/Pegasus.Core/Intake/IntakeContracts.cs`: extraction contract has no route/principal context.
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` and `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs`: current wrong assumptions are pinned.
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosMailRoutePolicyTests.cs` and `tests/Pegasus.IntegrationTests/ProviderDomainReferenceIntegrationTests.cs`: correct domain and prior-sender boundaries are already pinned.

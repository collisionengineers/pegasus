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
- QDOS was never required to appear as `Of QDOS`.
- Principal identification must not ordinarily be inferred by extracting attached documents; document extraction is used only where necessary for the instruction data.

Therefore the prior `OfQDOS` diagnosis and proposed token-recognition fix are superseded. `OfQDOS` was merely an incidental PdfPig rendering observed in one attachment. Making it an accepted principal marker would encode an accidental document layout as product policy.

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

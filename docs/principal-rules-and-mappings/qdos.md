# QDOS — identification rules and mappings

How Pegasus identifies, classifies, types, associates, and extracts QDOS
email. Derived from the operator-approved QDOS mapping methodology
(2026-08-21, built on the real instruction corpus) and the code that owns each
rule. Policy versions below are the deployed criteria; the cited files are
authoritative.

## 1. Route identification — "this is QDOS mail"

Owner: `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs`
(`qdos_mail_route`, Version 4).

- Intake only reads **approved mailboxes** (mailbox estate:
  `docs/runbook.md#approved-mailbox-estate`); route identity is then proved
  from the **effective sender**, never from message content.
- **Accepted direct domains** (operator decision 2026-08-03, exact
  whole-domain equality — no suffix or subdomain widening):
  `qdosassist.co.uk`, `qdoslaw.co.uk`, `qdosassists.co.uk`.
- **Staff forward unwrapping**: when the single transport sender is on
  `collisionengineers.co.uk` (a desk forward), the effective sender is the
  single **external** original sender proved from attached-original or
  inline-forwarded-original evidence. Zero or multiple original senders, or a
  staff-domain original, yields no effective sender — the route fails closed.
- Route identity is a separate fact from classification and case association;
  each has its own policy (below).

Presentation of the effective sender (bold original sender, "Forwarded by"
context, no desk noise) is owned by the Mail pages and
`src/Pegasus.Core/Intake/RetainedMail.cs` (`EffectiveSenderAddress`).

## 2. Message-type classification

Owner:
`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs`
(`qdos_mail_classification`, Version 5).

Built **only on operator-guaranteed generated tells**, matched
case-sensitively (the casing is part of the tell — a human sentence mentioning
"triage only request" is not the tell). Deliberately absent: body keyword
matching — corpus evidence shows "audit" in a body signals an existing case
being chased, not a new instruction.

| Predicate | Tell | Where it must appear |
| --- | --- | --- |
| `subject.automatic-reply` | `Automatic reply:` prefix | Subject |
| `subject.reply-prefix` | reply prefix (`RE:` family) | Subject — mirrors the underlying category with reply context |
| `body.triage-only-request` | `Triage Only Request` | An email body |
| `subject.engineer-triage` | `Engineer Triage`, opening the subject past any forward or reply prefix | Subject |
| `attachment.audit-report-notification` | `AUDIT REPORT NOTIFICATION` | An attached document's text |
| `attachment.engineer-notification` | `ENGINEER NOTIFICATION` (with or without the `REPORT + AUDIT REPORT` marker) | An attached document's text |

QDOS sends triage requests in two reviewed templates and the templates are
disjoint in the evaluated corpus. The two tells therefore feed **one** triage
candidate — a second candidate for the same category would resolve to
Ambiguous, so a message carrying both tells would classify worse than one
carrying either. Both predicates are still recorded separately, so the
decision says which fired (MAIL-012). Exact current source counts and immutable
hashes live in the versioned
[principal-identification corpus](../../reference/workproviders-and-repairers/principal-identification-corpus.v1.json)
and the corpus-lane output.

The refreshed Version 5 volume evaluation processed 138 genuine messages: 10
were unreadable; routing produced 47 Accepted, 8 NeedsSorting, and 73 NoMatch.
Among accepted routes, classification produced 3 Audit, 2 Inspection, 3
Triage, and 39 Unclassified outcomes. The matched tells were 3 Audit titles, 2
Engineer Notification titles, 3 body Triage phrases, and 29 reply prefixes;
the subject Engineer Triage tell matched none in this particular volume cohort.
All 47 accepted routes yielded a durable claim token. These counts are bound to
the source aggregate in `qdos-policy-v5-volume-evaluation` and supersede the
stale seven/five statement without changing policy behaviour.

Outcomes: exactly one category predicate → that category; more than one → the
recorded **Ambiguous** outcome (never an invented winner); none →
**Unclassified**, failing closed. Nested-message content is excluded from the
attachment tells.

**The triage classification is the Triage trigger.** A `triage-request`
classification is pre-case work: intake does not allocate a case from it, and
the classification decision is written to the receipt as the accepted
Triage-match evidence, carrying this policy's key and version. A known vehicle
registration then opens the Triage; no known registration registers the material
as Unidentified instead. There is no separate triage matcher — that abstraction
had only a null implementation and was retired (INTK-033); one owner answers the
question, and it is this policy.

Display labels for the taxonomy (family · subtype) are owned by
`src/Pegasus.Web/Presentation/OperatorLabels.cs`
(`MailClassification`, exhaustive map, throws on an unmapped value) and the
correction options by
`src/Pegasus.Web/Presentation/MailClassificationSelection.cs`.

### Candidate review rows — not runtime rules

The structured corpus adds genuine-message review rows for the following
shapes. Each remains `observed`, with no shared-taxonomy target, until the
operator labels positive and confusable-negative examples. None changes
Version 5 behaviour:

| Candidate shape | Required review |
| --- | --- |
| Final repair account or final audit request | Label current-content positives and report/attachment confusers. |
| Report chase | Separate a chase from a new instruction and from quoted history. |
| Post-inspection repair authorisation | Prove the sender-authored authorisation wording and case association. |
| Pre-accident-value dispute | Separate a dispute from an amendment or ordinary query. |
| Repair, total-loss, or category amendment | Label each accepted shared-taxonomy outcome without a principal-specific vocabulary. |
| Additional images, estimates, or updates | Separate current evidence from quoted or nested-message evidence. |
| Third-party-insurer comments or query | Prove authorship, direction, and the applicable shared category. |
| Automatic reply and reply-thread exclusion | Pin automatic-reply handling and exclude quoted/nested tells. |

## 3. Case type

The classification decision carries the case type; allocation reads
`receipt.MailClassificationDecision?.CaseType` and **fails closed** with
`CaseTypeUnavailable` when no type is available
(`src/Pegasus.Core/Intake/IntakeAllocation.cs`) — no case is created on an
untyped instruction.

Mapping proved on the corpus: `ENGINEER NOTIFICATION` → Inspection;
`ENGINEER NOTIFICATION` with `REPORT + AUDIT REPORT` → Inspection + Audit
(provable from the instruction letter alone, with no third-party report
attached to the email — operator-confirmed on EREF10);
`AUDIT REPORT NOTIFICATION` → Audit. Missing or ambiguous standalone Audit
evidence withholds only the later Audit reference (product invariant,
`AGENTS.md`). Once normal processing and principal gates pass, normal Case/PO
allocation proceeds; the separate Audit reference still waits for its evidence.

## 4. Case association — linking mail to an existing case

Owner: `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosCaseMatchPolicy.cs`
(`qdos_case_match`, Version 1), the accepted predicates of
`docs/adr/0020-accepted-qdos-case-association-predicates.md`.

- **Label-anchored with a required separator** — free text is never scraped
  (this excludes the predecessor's false registrations: office-address
  fragments, month names, postcode outward codes).
- **Claim reference**: durable identity is the `NNNNN/N` tail, so
  `ABC/DEF/12345/1` and a bare `12345/1` hit the same claim; `qdoslaw.co.uk`
  references keep their own letters-only grammar under the same provider.
- **Vehicle registration**: labelled VRM only, with **TP-prefixed labels
  skipped** — only the client vehicle is a key, keeping two claimants from one
  accident apart. **Name** and **incident date** are labelled keys with the
  same discipline.
- Each key must be **single-distinct** across the message; conflicting values
  for a key drop that key rather than guess.

## 5. Field extraction — populating the instruction draft

Owners: `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs` (the
provider-neutral `InstructionFieldEngine`) and
`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`
(the QDOS grammar, `Version 7`). The engine carries no QDOS knowledge; every
QDOS-specific label, guard, and synthesis rule is supplied by the policy.

Mechanics (engine):

- **Label-anchored candidates** with rank-aware conflict resolution; evidence
  (source, label, fragment) is recorded per candidate.
- **Typographic apostrophes** (`’`, `‘`) normalize to `'` before line/label
  matching (the letters use `Our Client’s Vehicle:`).
- **Typed-value canonical dedupe**: candidates that parse to the same
  canonical value (dates, registrations) are one value, not a conflict —
  resolved to the earliest.
- **Wrapped-line prefix subsumption**: a shorter candidate that is a
  word-boundary prefix of the longest candidate in the earliest fragment is
  subsumed, not a conflict.
- **Ordinal dates** (`15th August 2026`) parse; equal dates in different
  formats dedupe canonically.
- **Guarded prefixes**: a label immediately preceded by a guarded row prefix
  is not the field's label.

QDOS grammar (policy):

- Letter labels: `Our Ref:`, `Our Client:`, `Our Client's Vehicle:` /
  `Claimant's Vehicle:`, `Registration:`, `Date of Accident:` /
  `Accident Date:`, mileage labels, and the third-party rows.
- **Third-party guard**: `GuardedPrefixes = ["TP"]` on every field — the
  letters' `TP Vehicle:` / `TP Registration:` / `TP Representative Name:` rows
  never feed the claimant's fields.
- **Report-sourced facts**: a document that names itself a report in its
  retained file name (`Bodyshopreport…`, `EngineersReport…`) contributes its
  own grammar, rewritten as labelled lines: the report's `Vehicle:` line (cut
  at neighbouring `Colour:`/`Speedo:`/`Reg No:`/`Reg:` columns) becomes
  `Our Client's Vehicle: …`; a `Speedo:` line contributes mileage **only when
  it carries digits**. Appended after all content, so **the instruction letter
  always outranks the report**.
- **Accident circumstances**: the paragraph after the letter's prompt line
  ("…check the damage for consistency with the following accident
  circumstances?"), terminated at the next block (`Damage Area`,
  `Pre-existing damage`, `TP `, `If you need`). Audit letters carry no prompt
  — circumstances legitimately stay empty.
- **Subject facts last**: settled facts in the provider's own subject grammar
  rank below every document: `Our Ref`, `RTA on DD_MM_YYYY`, the titled client
  name, the vehicle description, and the `Vehicle Registration` label. The
  registration rule accepts both recorded spacings (`Vehicle Registration
  YD14VGJ` and `Vehicle Registration : VO75DFJ`) and validates the captured
  value as a UK registration; the vehicle-description rule is held off that
  label, which it used to read as the description `Registration : VO75DFJ`. The
  Triage subject template states its registration nowhere else, so without this
  rule every one of those requests fell to Unidentified (INTK-033).

Corpus tests: `tests/Pegasus.IntegrationTests/QdosMappingExtractionTests.cs`
(per-file expectation table over the real local corpus, skip-if-absent —
`corpus/` is local, ignored, immutable, never committed).

## 6. Body display and excerpts

Owner: `src/Pegasus.Core/Intake/StaffForwardBodyCleaner.cs` (the one owner of
body-display policy; the Mail pages and
`src/Pegasus.Web/Presentation/MailBodyPresentation.cs` consume it).

- `SplitForwardedHeader` — the forwarded `From:/Sent:/To:/Subject:` block is
  shown structured, never as body text.
- `TrimProviderFooter` — cuts at the earliest footer marker (image/cid
  placeholders, tel/mailto/http wrappers, confidentiality/disclaimer/
  registered-office lines), **failing open** to the whole body when trimming
  would leave nothing.
- Stored bodies are truthful: signature-only bodies legitimately show the
  signature; inline-letter emails legitimately show the letter.

## 7. Evidence images

Owner: `src/Pegasus.Core/Intake/InstructionEvidenceImages.cs` (selection) and
the custody processor
(`src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs`,
promotion beside the source in `Evidence/Original instruction`).

Selection rule: attached images always; embedded (PDF-extracted) images only
at or above the 40 KB photograph floor (`EmbeddedPhotographMinimumBytes` —
letterhead logos repeat at 234 B–28 KB, damage photos run 60–320 KB); inline
(cid) images never; hash-deduped preferring the attached copy. Selected images
render on the case Evidence tab and are retained in Box.

## 8. Pointer summary

| Question | File |
| --- | --- |
| Is this mail on the QDOS route / who is the effective sender? | `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs` |
| What type of message is it? | `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` |
| Which case does it belong to? | `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosCaseMatchPolicy.cs` + `docs/adr/0020-accepted-qdos-case-association-predicates.md` |
| What case type is allocated? | `src/Pegasus.Core/Intake/IntakeAllocation.cs` (from the classification decision) |
| What fields are extracted, and how? | `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` + `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs` |
| What does the operator see? | `src/Pegasus.Web/Presentation/OperatorLabels.cs`, `MailClassificationSelection.cs`, `MailBodyPresentation.cs`, `src/Pegasus.Core/Intake/StaffForwardBodyCleaner.cs` |
| Which images become evidence? | `src/Pegasus.Core/Intake/InstructionEvidenceImages.cs` |
| Behaviour owners | `docs/frd/frd-02-intake-and-source-identity.md`, `docs/frd/frd-05-documents-extraction-and-custody.md`, `docs/frd/frd-08-email-mailbox-and-background-processing.md` |

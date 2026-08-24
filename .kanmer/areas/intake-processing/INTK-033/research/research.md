# Research — triage-from-intake

## Method note

Premises below are marked **[verified]** when a read-only check produced them
(source read, corpus read, production record already captured on the ticket) and
**[assumed]** otherwise. No premise here is reasoned into existence.

## 1. Why every QDOS message becomes `case_created`

**[verified]** `QdosInstructionExtractionPolicy.Extract` has exactly one return
statement and it always returns `InstructionPolicyApplicability.Applicable`
(`QdosInstructionExtractionPolicy.cs:151`). There is no path that reports
`NotApplicable` once the QDOS principal is established.

**[verified]** `ProcessIntake.AssessAsync` maps `Applicable` →
`IntakeDecision.CaseCreated` (`ProcessIntake.cs:495-500`), and
`AllocateIntake.AttemptAutomaticAsync` runs for exactly that decision
(`IntakeAllocation.cs:237`). It then reads
`receipt.MailClassificationDecision?.CaseType`, which a triage-request
classification deliberately leaves null — hence the recorded
`case_type_unavailable`.

So fault 1 is not a missing guard in allocation. Allocation is *right* to fail
closed on a missing case type; the defect is that a triage request was routed
into allocation at all.

**[verified]** The repository already has the exact remedy shape. ProcessIntake
carries a post-assessment override that downgrades `CaseCreated` to
`NeedsSorting` on a classification fact (`ProcessIntake.cs:189-198`, the
standalone-Audit rule), and `AssessAsync` carries a second one for ambiguous
case matching (`ProcessIntake.cs:515-520`). A triage request is the same kind of
statement: *the classification already knows this is not a case.*

## 2. The triage gate, and why the matcher has to go rather than be filled in

**[verified]** `CreateTriageIfQualifyingAsync` (`DurableIntake.cs:893`) needs a
registration plus exactly one `AcceptedTriageMatch` evidence entry, Strong, with
a matcher key and a positive version. `EfTriageStore.CreateFromIntakeAsync`
independently re-checks that the same evidence is retained uniquely on the
receipt, and `TriageLifecycle.ValidateAcceptedMatchEvidence` validates its
shape. Three layers agree on the contract, and the downstream behaviour is
complete and replay-safe.

**[verified]** The only producer of that evidence is
`IIntakeTriageMatcher`, called from inside the extraction policy
(`QdosInstructionExtractionPolicy.cs:125-141`), and the only implementation is
`NoAcceptedIntakeTriageMatcher`, which returns `[]` by construction.
`DependencyInjection.cs:152` composes it, and
`ProductionCompositionTests.ProductionProfileKeepsTheTriageMatcherInactive`
pins it so it cannot be activated as a side effect.

The tempting fix is to write a `QdosIntakeTriageMatcher`. **That would be a
second owner of a question the route classification policy already answers**,
and the repository forbids exactly that:

- `docs/open-decisions.md:167` says only the *match predicates* were missing.
  They are no longer missing: MAIL-012 accepted the two QDOS triage tells
  (`body.triage-only-request`, `subject.engineer-triage`), their exclusions
  (case-sensitive, subject anchored past any forward/reply prefix), and their
  ambiguity outcome (recorded `Ambiguous`, no invented winner) as
  `qdos_mail_classification` v4 — shipped in release 26 and written up in
  `docs/principal-rules-and-mappings/qdos.md` §2.
- FRD-03 §Normal workflow: *"Triage begins when the exact accepted route policy
  classifies a provider request as an assessment request…"* — the FRD names the
  **route classification policy** as the trigger, not a separate matcher.
- ADR-0008 makes the route-owned classification policy the only owner of
  message-type classification.
- CLAUDE.md: *"No abstraction without a second concrete caller, an external
  boundary, or an accepted ADR."* `IIntakeTriageMatcher` has one implementation
  and it is the null one.

So the accepted predicates already exist, in the accepted owner, versioned. The
`AcceptedTriageMatch` evidence should be **derived from the classification
decision**, and `IIntakeTriageMatcher` / `NoAcceptedIntakeTriageMatcher` /
`IntakeTriageMatch` retired. The three-layer downstream contract is untouched —
it just starts receiving evidence.

Mapping is one-to-one and needs nothing invented:

| Evidence field | Source |
| --- | --- |
| `MatcherKey` | `classification.PolicyKey` (`qdos_mail_classification`) |
| `MatcherVersion` | `classification.PolicyVersion` |
| `Signal` | the matched predicate key |
| `Detail` | that predicate's recorded detail |
| `Source` | `EmailBody` or `Subject`, per which tell fired |
| `Strength` | `Strong` — a generated, operator-guaranteed tell |

Exactly one entry, because the policy already collapses both tells into **one**
triage candidate (MAIL-012); two candidates for one category would classify as
`Ambiguous` and never reach here.

## 3. Where the registration actually is — read from the corpus, not assumed

**[verified]** `corpus/` holds both templates. Read-only inspection:

**Subject template** (`corpus/qdosmapping/`, 3 messages; plus 2 in `corpus/`):

```
Engineer Triage - Our Claim Reference 46384/1 , Vehicle Registration YD14VGJ
Engineer Triage - Our Claim Reference : 46246/1 - Vehicle Registration : VO75DFJ
```

Body is free prose — a greeting, "Please see the attached images to determine if
the vehicle is repairable or a total loss", the vehicle in a sentence ("It is a
twelve-year old FORD TRANSIT CONNECT 220"), a signature block. **No labelled
fields anywhere.** The registration exists only in the subject, in two spacings
(with and without a colon).

**Body-phrase template** (7 messages) carries a fully labelled letter in the
body:

```
Our Client:  Miss Nicola Granger
Our Client's Vehicle: MERCEDES-BENZ E250 CDI AMG LINE AUTO
Registration:  VN64WNG
Date of Accident: 30 June 2026
Triage Only Request
```

**[verified]** `Registration` is already a label on the `Vehicle registration`
field definition (`QdosInstructionExtractionPolicy.cs:29-35`), so this template's
registration is *already* extracted today. Fault 3 is therefore narrower than
the ticket body states: it affects **only the subject template**.

**[verified]** `SubjectFactLines` (`:396`) emits `Our Ref`, `Date of Accident`,
`Our Client` and `Our Client's Vehicle` — no registration rule. Worse, its
vehicle rule `\bVehicle[:.]?\s+([^,()]+)` matches the *label* in "Vehicle
Registration : VO75DFJ" and yields the nonsense fact
`Our Client's Vehicle: Registration : VO75DFJ`.

So one small, contained change: a registration rule in `SubjectFactLines`, and a
lookahead so the vehicle-description rule stops swallowing the registration
label. `IsUkRegistration` already exists to validate the captured value
(`InstructionFieldExtraction.cs:384`), and the emitted label `Vehicle
Registration:` is one the field engine already reads.

## 4. The Unidentified branch, and an ordering hazard

The operator's rule (`operator-notes.md` §Stage 0, restated in FRD-03: *"without
a VRM it remains `Needs sorting`; with a VRM it opens as `Open`"*) needs both
outcomes wired, and there is a real ordering trap between them.

**[verified]** `RegisterUnidentifiedIfTerminalAsync` runs **inside**
`ProcessIntake.ExecuteRetainedAsync` (`:276`), whereas
`CreateTriageIfQualifyingAsync` runs **later**, in `DurableIntake`
(`:618`). Simply setting a triage request to `NeedsSorting` would therefore
register an Unidentified item for *every* triage request — including the ones
about to open a Triage a few milliseconds later.

**[verified]** The codebase already solved this exact race once, for image-only
material: `IsUnidentifiedEligible` (`ProcessIntake.cs:304`) deliberately
excludes it so automation gets its chance, and `DurableIntake`'s
`SynchronizeUnidentifiedAsync` (`:700`) registers it afterwards only if nothing
resolved it. The comment at `:296-303` states that intent in as many words. The
triage branch is the same shape and should reuse it rather than invent a second
deferral mechanism.

**[verified]** `ExecuteRetainedAsync` has exactly one caller —
`DurableIntake.cs:549` — so deferring cannot strand a receipt on some other
path.

**[verified]** `ReconcileUnidentifiedDestinations.ResolveForReceiptAsync` also
reads `IsUnidentifiedEligible`. A deferred triage receipt reaching it has
decision `NeedsSorting` and no case, so it falls to the final `return false`
no-op. No behaviour change there.

## 5. What already works and must not be touched

**[verified]** A Triage queue (`Pages/Triage/Index`) and detail page
(`Pages/Triage/Details`) exist, as do assign/finding/link/complete use cases and
their stores. The operator's *"did not show in the triage queue"* is fully
explained by there being no Triage row — the queue itself is fine.

**[verified]** `MailOperationalDestinationPolicy` already routes the
`pre-instruction-emails/triage-request` category to the Triage destination
(`:105-109`), which is why the inbox correctly labelled the message. The literal
`"triage-request"` appears there and in the classification policy — two copies
of one concept, worth collapsing to a named predicate while this change is
touching both.

## 6. Deliberately out of scope

- **Images on a triage request.** Both templates attach client damage photos.
  Retaining them as Triage evidence is real work with no operator instruction
  behind it, and a Triage record has no evidence surface today. Not in this
  ticket.
- **Claim reference from the subject.** `TriageRecord` stores only
  `NormalizedVehicleRegistration`; a claim reference has nowhere to go.
- **Unidentified → Triage promotion once a registration is later learned.**
  `UnidentifiedResolutionTargetKind` has no Triage member. The operator's rule
  says the material *waits* in Unidentified; it does not say the promotion is
  automatic. Filed separately rather than guessed at.

---

## Correction — 2026-08-24, after independent review

Two statements in §6 above were **wrong when written**, and the second was a
claim about work I had not done. Left uncorrected they would have been inherited
as fact.

**§6 said `UnidentifiedResolutionTargetKind` has no Triage member. It does** —
`UnidentifiedContracts.cs:37`, with `UnidentifiedValidation.ValidateResolve`
already handling it against `ITriageQueries` at `:375`. I did not check before
asserting it, and that error is what made me defer the wrong thing: because I
believed the destination did not exist, I treated the whole Unidentified →
Triage transition as out of scope. It is not. The supersession half belongs
here and now ships: `ReconcileUnidentifiedDestinations` resolves a stale open
item to the Triage that now exists. Without it, the operator's own transition —
material waits in Unidentified, its registration becomes known, the Triage opens
— left a live U-reference open beside the Triage with nothing able to close
either, reachable through the ordinary **Re-evaluate** button.

**§6 said the deferrals were "filed separately rather than guessed at". They
were not filed at all** when that sentence was written. They are now:
[[INTK-034]] (images as Triage evidence) and [[INTK-035]] (the staff action that
supplies a registration and opens the Triage). What remains genuinely deferred
is only the *staff-initiated* promotion; the automatic supersession is in this
ticket.

Both errors were caught by the independent pre-merge review, not by me, and not
by the simplification pass either.

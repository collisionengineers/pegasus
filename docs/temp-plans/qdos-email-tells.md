# QDOS email tells: evidence from 329 genuine emails

Evidence for [`qdos-email-classification`](qdos-email-classification.md).
Read-only analysis of the local corpus. Source emails stay in `corpus/`; the
working copy and generated indexes are under
`artifacts/evaluation/qdos-emails/`, which is git-ignored.

## What was analysed

329 emails whose sender is a QDOS domain, drawn from 1040 local `.eml` files.
327 from `qdosassist.co.uk`, 2 from `qdoslaw.co.uk`, across 27 distinct
senders. This is a partial sample of real traffic, not a designed cohort, so
counts show what exists rather than true frequency.

## The subject line is templated

QDOS generates its subjects to a fixed shape:

```
(EREF8) RTA on 18/06/2026 : Mr Nick Jones (Our Ref: MFI/AKH/46553/1, Vehicle: ...)
 ^^^^^  ^^^ ^^^^^^^^^^^^   ^^^^^^^^^^^^^   ^^^^^^^^^^^^^^^^^^^^^^^
 type   kind  incident date  claimant        claim reference
```

| Part | Meaning | Present in |
| --- | --- | ---: |
| `(EREF<n>)` | QDOS's own email-template code | 316 / 329 |
| `RTA` / `PL` | Incident kind (285 RTA, 2 PL) | 287 / 329 |
| `on DD/MM/YYYY` | Date of the incident | 287 / 329 |
| `: <name>` | Claimant name | most |
| `Our Ref: …/NNNNN/N` | QDOS claim reference (operator-confirmed) | 305 / 329 |
| Claim number `NNNNN/N` anywhere in subject | The durable claim identity | **326 / 329** |
| `RE:` / `FW:` / `Automatic reply:` prefix | Reply or forward context | 49 / 329 |

Optional trailing fields also seen: `Vehicle:`, `Registration:`, `Your Ref:`.

## New case versus existing case

**The claim reference is the answer, and it is present in 326 of 329 emails.**

The rule is a lookup, not a guess: extract the claim number, look it up. Known
means existing work; unknown means potentially new. This needs no keyword
matching and no confidence score.

In this sample:

| | Count |
| --- | ---: |
| Distinct claim references | 226 |
| References appearing once | 159 |
| References appearing more than once | 67 |

The prefix reinforces it: `RE:` on 42 emails means an existing thread, never a
new instruction.

Caveat: a known claim reference proves QDOS has an existing claim. It does not
prove Pegasus has a Case — early emails on a claim arrive before any Case
exists. So the lookup answers "have we seen this claim", and Case association
remains a separate step under the existing matching rules.

## EREF is the email-type code, and it moves through a case

The same claim carries different EREF codes as work progresses. Claim
`46913/1`, in order:

```
EREF5   akhoyer        initial
EREF6   jmoseley
EREF9   lbirchenough
EREF12  nduncombe
EREF12  engineers      RE: ...
```

And a claim can repeat one code across a thread — claim `46528/1` has seven
emails, all `EREF8`, six of them `RE:`.

So `EREF` identifies **what kind of message this is**, not where the case has
got to. That makes it a strong candidate for the category predicate, which is
exactly what MAIL-22 needs.

### What the codes mean is not yet established

Codes seen: 1–26, 28, 29, 31, 33–39, 42, 87. Volume is concentrated in 5–13
(203 of 329).

Two patterns are visible but **unconfirmed**, and must not be built on until
the operator confirms them:

- Low codes (5–13) carry images and video (`.jpg`, `.png`, `.mp4`, `.mov`) and
  come from many different senders — consistent with instruction and image
  traffic.
- Higher codes (21–42) carry only `.pdf` and `.png`, and come largely from
  `accounts@qdosassist.co.uk` — consistent with billing and post-report
  correspondence.

A code-to-category mapping is the single most valuable thing to confirm, and
confirming it is an operator review, not an inference.

## The 13 without an EREF

Not QDOS's automated template, and they matter:

- 2 from `qdoslaw.co.uk`: `Our ref: ELM/NAK0011 - Mutual Client: … - Vehicle
  Reg; LT17 UCU` — a different reference format entirely.
- `Engineer Triage- Our claim Reference : 46684/1 , vehicle registration : …`
  — these are Triage requests, matching the separate Triage workflow.
- Bare `46670/1 - Mohammed Jameel` — claim number with no template at all.

These prove the classifier cannot depend on `EREF` alone. The claim-number
extraction still works on all of them, which is another reason to treat the
claim reference as the primary key and `EREF` as the type hint.

## The Audit tell is in the attachment filename, not the subject

QDOS attaches a generated instruction letter, and its filename states which
kind of work is being instructed:

| Attachment filename pattern | Meaning | Emails |
| --- | --- | ---: |
| `…LtrtoAuditEngin….pdf` | Letter to Audit Engineer — **Audit instruction** | 27 |
| `…LtrtoEngineerInstructionCollisionEngineersLtd….doc` | Letter to Engineer Instruction — **Inspection instruction** | 17 |

Examples: `39980_1_LtrtoAuditEngin.pdf`, `48692_1_LtrtoAuditEngin.pdf`,
`1_LtrtoEngineerInstructionCollisionEngineersLtd-V1.doc`. The leading number is
the claim, so the claim reference appears in attachment names as well as
subjects.

**No email carries both letters.** The two sets are disjoint across all 329,
which makes this a clean separator rather than a weighted signal.

### This corrects an earlier reading

`EREF` does **not** separate Inspection from Audit. Audit-letter emails appear
under EREF 9, 10, 11, 13, 15, 17, 21, 22, 23, 24, 28 and 35, and
Inspection-letter emails appear under EREF 5, 6, 7, 8, 9, 10, 11, 13 and 15 —
the codes overlap heavily. `EREF` remains a useful type hint for other
categories, but the Inspection/Audit distinction is carried by the attachment.

### Inside the letter: an explicit notification title

The attachments were extracted and their text read, not just their names. Each
instruction letter carries a title line that states the work type outright:

| Title inside the document | Meaning | Files |
| --- | --- | ---: |
| `AUDIT REPORT NOTIFICATION` | Standalone Audit | 26 / 26 |
| `ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)` | Inspection **and** Audit | 17 / 17 |

43 of the 44 extracted letters were examined (one filename collision), and
**every document's title agreed with its filename**. Zero mismatches. So the
filename and the document content are two independent tells that corroborate
each other, which is what makes this safe to rely on.

The title is the stronger of the two: a filename can be changed by whoever
forwards the mail, but the title is inside the generated document.

### No plain-Inspection instruction appears in this sample

All 17 non-audit letters say `REPORT + AUDIT REPORT`. Not one says
`ENGINEER NOTIFICATION` alone.

That means the three work types the operator asked about map like this:

| Operator's category | Tell found | Present in sample |
| --- | --- | ---: |
| Standalone Audit | `AUDIT REPORT NOTIFICATION` | 26 |
| Inspection + Audit | `ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)` | 17 |
| Base instruction (Inspection only) | none found | **0** |

Either QDOS routinely instructs report and audit together, or plain Inspection
instructions exist but none reached this 329-email sample. This must be settled
before anything is built: a classifier that has never seen the third type
cannot be trusted to recognise it, and a missing category silently becoming
`Inspection + Audit` would be a wrong Case type.

### What else the letter contains

The audit letter states the instruction in plain words — "Please can you
prepare an audit report based on the attached engineers report" — and carries
structured fields usable for extraction (INT-19/INT-20): `Our Ref`, `Date`,
`Our Client`, `Our Client's Vehicle`, `Registration`, `Date of Accident`,
`CLIENT DETAILS` with address and phones, `REPAIRER DETAILS`, damage-area
description, pre-existing damage, `TP Vehicle`, `TP Registration`, and
`TP Representative Name`.

It also carries conditional business instructions, for example: "this is a
Right Choice Insurance Brokers case. If the vehicle is a total loss, DO NOT
produce an audit report", plus repair-authority limits ("do not authorise
repairs", "not in excess of 80% of the vehicle value"). These are operator
instructions inside the source document, not classification signals, but they
show the letter is the authoritative instruction artifact rather than the email
body.

### File formats differ by type, and that matters for reading them

| Type | Format | Detail |
| --- | --- | --- |
| Audit letter | PDF (`%PDF`) | Text extracts cleanly |
| Inspection letter | Legacy binary `.doc` (OLE2, `D0 CF 11 E0`) | Not DOCX; needs legacy-format reading |

INT-14 (legacy DOC extraction) is allocated `Next` / `0.2.0`. If the
Inspection + Audit instruction letter is always a legacy `.doc`, then reading
that filetype is on the critical path for QDOS intake, not a later nicety. This
should be checked against the capability allocation.

### The audit verdict is in the attached third-party report

Operator statement, 2026-08-03: a standalone Audit is auditing **another
engineering firm's report**. That report is attached alongside the instruction,
and it carries a verdict that can be extracted.

Confirmed. The 27 audit-instruction emails were opened and every other readable
document in them was extracted and read — 35 documents. The third-party report
states its verdict in its own **title**, exactly as the instruction letter
states the work type in its title:

| Report title | Verdict | Documents |
| --- | --- | ---: |
| `Repairable Damage Assessment Report` / `REPAIRABLE REPORT` | Repairable | 21 |
| `Total Loss Damage Assessment Report` | Total loss | 1 |
| Narrative only (`deemed repairable`, `physically repairable`) | Repairable | 1 |
| No verdict — not an engineer's report | — | 12 |

Variants seen on the repairable title include `REPAIRABLE REPORT - Amended
Report` and `- Supplementary Report`, so the match must tolerate a suffix.

**23 of 27 audit emails carry an extractable verdict.** The 12 documents
without one are correctly not reports: nine are image sheets
(`1_Images-V1.pdf`), plus `BodyshopEngimgs`, one `Bodyshopreport-V1.pdf`, and
`1_Estimate-V1.pdf`. So the absence is meaningful rather than a parsing
failure.

This satisfies the requirement that a standalone Audit derives lowercase `a.`
or `ap.` only from an unambiguous repairable or total-loss assessment in the
original Engineer report: the assessment is in the report, it is stated in the
title, and where no report is attached there is no verdict and the case must
fail closed rather than assume repairable.

#### A total-loss audit does exist in the sample

One `Total Loss Damage Assessment Report` is present. Earlier in this document
the absence of `AP.` was noted from corpus **folder names**; that remains true
of the folder set but must not be read as "total-loss audits do not occur".
They occur and are rare — roughly 1 in 23 here. A rule trained only on
repairable examples would be wrong in exactly the case that matters most.

#### Two false-positive traps in this data

- `No write-off recorded` appears in 6 documents. It is a vehicle-history
  check result, not a verdict, and naive matching on "write off" would invert
  the meaning.
- The bare words `Write Off` and `Repairable` appear in parts lists and
  boilerplate (`Repairable parts:-Front Bumper`). The **title** is the reliable
  carrier; loose keyword matching over the body is not.

### Supporting, not decisive

- 26 of the 27 audit-instruction emails come from `nduncombe@qdosassist.co.uk`,
  the highest-volume sender. Suggestive of a handler specialism, but a person
  can change role, so this must never decide a category.
- 56 emails attach a third-party or bodyshop engineer's report
  (`Bodyshopreport-V1.pdf` ×21, `Bodyshopsuppreport-V1.pdf` ×17,
  `TPIengineersreportforCLV-V1.pdf`, `EngineersReport-V1.pdf`). Consistent with
  Audit work, since an Audit reviews another firm's report — but the count far
  exceeds the 27 audit letters, so an attached third-party report alone does
  not mean Audit.

Nothing found so far distinguishes **Inspection + Audit** from either single
type. That may be correct rather than a gap: `requirements.md` says
Inspection + Audit begins with the normal Inspection reference and the Audit is
created inside the case later, after the Engineer records the assessment. If so
it is not an intake-time distinction at all, and only standalone Audit needs
detecting at intake. This needs operator confirmation.

## What the bodies show

All 329 bodies were extracted and searched. Keyword hits: `inspection` 51,
`repairable` 48, `assessment` 39, `triage` 38, `audit` 31, `total loss` 19,
`estimate` 10, `salvage` 7. Zero hits for "double check" and "previous
engineer".

The `audit` mentions are **not** instruction language. They are conversational,
and mostly chasers for work Collision Engineers owes:

- "Please can you forward your final audit report as soon as possible." —
  repeated near-verbatim from `accounts@`, the largest cluster
- "Can we do an audit report on this or is this the audit figures"
- "it was cancelled our end as we couldn't do an audit on it"
- "See attached initial and AUDIT Pav reports"

So body text mentioning "audit" indicates an existing audit case being chased
or queried, not a new audit instruction. Treating the word as an instruction
tell would misclassify chasers as new work.

One body shape is worth noting: EREF5 contains the instruction letter inline as
formatted text — `Our Ref:`, `Date:`, `Dear Sirs`, `Our Client:`, `Our Client's
Vehicle:`, `Registration:`, `Date of Accident:`. So the same instruction
content arrives sometimes inline and sometimes as an attached document, and
extraction must handle both.

## Operator-confirmed guarantees

Stated by the operator on 2026-08-03 and each verified against the 329-email
corpus. These are guarantees about QDOS's generated output, not inferences, so
they are exact-string rules rather than heuristics.

| Guarantee | Where | Verified |
| --- | --- | --- |
| `AUDIT REPORT NOTIFICATION` — standalone Audit | Extracted attachment text | 26 occurrences, **all upper case**, and only ever in audit-letter documents |
| `REPORT + AUDIT REPORT` — Inspection + Audit | Extracted attachment text | 17 occurrences, **all upper case**, always as `ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)`, and only ever in inspection-letter documents |
| `Triage Only Request` — Triage | Email **body** | 37 bodies, in exactly one casing, `Triage Only Request` |

Casing is consistent enough to rely on, but a case-insensitive match costs
nothing and survives a future template tweak; the value is that the phrase is
generated rather than typed.

**These live in different places and both must be read.** No email body in the
corpus contains a notification title — zero of 329. The work-type titles exist
only inside the attachment. The Triage phrase is the reverse: body only. A
classifier that reads only one of the two surfaces cannot decide work type.

### Triage is orthogonal to everything else

The 37 `Triage Only Request` emails carry ordinary instruction-shaped subjects
and are spread across EREF 2, 5, 7, 9, 10, 11 and 15. Some are `RE:` replies,
one is `URGENT PLEASE - (EREF10) …`. Nothing in the subject marks them.

That is 11% of the corpus, and Triage is a separate pre-case workflow
(`TRI-01`, `TRI-02`) with its own record, states, and no case creation. Without
the body phrase these would read as ordinary instructions and would be
routed as work. This single phrase is the difference between two entirely
different destinations.

## The decision rule this produces

Work type cannot be read from the subject, the sender, or the EREF code. It
requires extraction, and only from the two surfaces above:

1. Body contains `Triage Only Request` → **Triage** — the separate pre-case
   workflow, not a Case.
2. Attachment text contains `AUDIT REPORT NOTIFICATION` → **standalone Audit**.
   Then read the attached third-party engineer's report for the verdict:
   `Repairable …` → `a.`, `Total Loss …` → `ap.`
3. Attachment text contains `ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)` →
   **Inspection + Audit**. Begins on the normal Inspection reference; the audit
   identity is created inside the case later.
4. Attachment text contains `ENGINEER NOTIFICATION` **without**
   `REPORT + AUDIT REPORT` → **plain Inspection**.
5. None of the above → not a work instruction; classify by the other tells.

### Rule 4 has no example in this corpus

Every one of the 17 inspection letters carries the `(REPORT + AUDIT REPORT)`
parenthetical. Not one is a bare `ENGINEER NOTIFICATION`.

The operator expects plain Inspection to be the most common outcome in
practice, so its absence here is a property of this sample rather than of the
business. It still has to be handled as an unseen case: rule 4 must be written
from the guarantee, not fitted to examples, and it must not be reachable by
default. An instruction letter whose title matches none of the three known
forms is an unknown work type and fails closed for staff review — it must never
fall through to Inspection because Inspection is the common case.

### Audit with no engineer's report attached

Operator decision, 2026-08-03: where the instruction is a standalone Audit and
no engineer's report is attached, this fails and is flagged to staff as
**report missing**. It is not an assumption of repairable, and it is not a
silent hold.

Exactly one of the 27 audit emails is in this state — its only other
attachment is `Images-V1.pdf`. So the case is real, rare, and now has a defined
outcome. This matches the requirement that missing or ambiguous audit evidence
blocks case creation and reference allocation.

Note the asymmetry: a missing report is fatal only for **standalone Audit**.
For Inspection + Audit there is no third-party report to expect, because
Collision Engineers produces the report being audited.

## Proposed tells, in priority order

1. **`Triage Only Request` in the body** — operator-guaranteed; decides Triage
   before any work-type question is asked.
2. **Notification title in the extracted attachment text** —
   operator-guaranteed; decides Audit versus Inspection + Audit versus
   Inspection.
3. **Report title in the attached third-party report** — decides the `a.` /
   `ap.` verdict for a standalone Audit.
4. **Claim reference** — primary identity; drives new versus existing. Present
   in 326 of 329, in subjects and in attachment filenames.
5. **Instruction-letter attachment name** — corroborates the notification
   title. Useful as a cheap pre-filter and as a cross-check, but the title
   inside the document is the authority, since a filename can be changed by
   whoever forwards the mail.
6. **`EREF` code** — email type for the remaining categories, once the operator
   confirms the mapping. Not usable for Inspection versus Audit.
4. **Subject prefix** (`RE:`, `FW:`, `Automatic reply:`) — reply and
   auto-reply context; `Automatic reply:` maps to the settled `General` /
   `autoreply` subtype.
5. **Sender mailbox** — `accounts@` concentrates in chasing and billing;
   `nduncombe@` in audit instructions. Supporting only, never decisive.
6. **Attachment shape** — images and video versus PDF-only; supporting only.

Body keyword matching is explicitly **not** proposed as a tell. The evidence
above shows the word "audit" in a body signals an existing case being chased,
which is close to the opposite of a new audit instruction.

Incident kind (`RTA`, `PL`) and incident date are extraction fields, not
category tells.

## What still has to be answered

- The `EREF` code to category mapping, confirmed by the operator. This is the
  single highest-value confirmation available and it is a review, not a sort.
- Whether the instruction-letter filenames are stable and generated by QDOS's
  system rather than typed by a handler. The whole Inspection/Audit split rests
  on this, so it needs operator confirmation before it is built.
- Whether **Inspection + Audit** is an intake-time distinction at all, or
  whether it only appears later inside an existing Inspection case as
  `requirements.md` implies.
- Whether `EREF` numbering is stable over time or QDOS reuses and renumbers
  codes. If it can change, the classification policy version must record which
  mapping was in force.
- Coverage limits of this sample: 27 audit and 17 inspection instruction
  letters is thin for threshold setting, and `qdoslaw.co.uk` contributes only
  2 emails with a different reference format entirely.

## The corpus is already labelled by case reference

The `test folder` corpus is filed one folder per case, named with Collision
Engineers' own reference:

```
corpus/test folder/test folder/A.QDOS26016/message.eml
```

Across the 329 emails there are 233 such folders:

| Reference shape | Meaning under [CASE-08](../capabilities.md) | Folders |
| --- | --- | ---: |
| `A.QDOS…` | Repairable standalone Audit | 146 |
| `QDOS…` (no prefix) | Normal Inspection sequence | 87 |
| `AP.QDOS…` | Total-loss standalone Audit | 0 |

This is existing human-filed ground truth, produced as ordinary business work
rather than for evaluation. It can label a cohort without anyone sorting
emails by hand, which directly serves the MAIL-21 acceptance cohort.

Two cautions before relying on it:

- The folder records what the case **became**, which is not always what a
  single email in it announced. A chaser filed under `A.QDOS26016` is still a
  chaser, not an audit instruction.
- No `AP.` case appears here, so the total-loss audit reference form has no
  example in this sample.

## Worked example: an audit chaser

Operator asked for an exact path. This is the email containing "Please can you
forward your final audit report as soon as possible":

```
corpus/test folder/test folder/A.QDOS26016/message.eml
```

From `accounts@qdosassist.co.uk`, subject
`(EREF26) RTA on 20/05/2026 : Mrs Vivien Healey (Our Ref: TG/45497/1)`.

It illustrates the trap: the case reference is an Audit, the body says "audit",
the claim reference is present — and yet the email is a chase on existing work,
not an instruction. Only the absence of an instruction-letter attachment
separates it from a real audit instruction.

## How this evidence was produced

Read-only scripts over the working copy, with outputs beside it under
`artifacts/evaluation/qdos-emails/`:

| File | Contents |
| --- | --- |
| `index.csv` | 329 rows: source path, sender, date, subject, attachment flag |
| `eref-profile.csv` | Per-email EREF code, senders, attachment names and extensions |
| `subject-parse.csv` | Parsed subject fields: EREF, claim ref, incident kind/date, prefix |
| `body-analysis.csv` | Decoded body text, attachment name list, keyword hit flags |

Nothing under `corpus/` was modified, and no email content is committed.

# CollisionSpike email logic: findings for QDOS classification

Evidence for [`qdos-email-classification`](qdos-email-classification.md).
Read-only scan of the predecessor application at
`collisionsuite/active/collisionspike`, run 2026-08-03.

**Status of this material.** Reusing predecessor application code is a
permanent product boundary (`BND-03`), and the predecessor is evidence, not
requirements. Nothing here is a specification. It is read for two things:
problems already discovered the expensive way, and decisions Pegasus will have
to take a position on.

Two checkouts exist. The main tree is the shipped system. A separate worktree,
`.claude/worktrees/email-engine-rebuild/`, holds a from-scratch rewrite
(`services/intake-engine/`) on a different branch line; findings from it are
marked **[rebuild]** because they represent second thoughts rather than
shipped behaviour.

## What already exists there

| Area | Shipped behaviour |
| --- | --- |
| Provider identification | Exact case-insensitive match, address-level first then domain-level. More than one active hit returns `ambiguous` and never auto-picks. Domains live in database columns, not config. |
| Intermediaries | A separate layer above provider matching; intermediary domain checked *before* domain-level provider match so a miss cannot mis-resolve. |
| Classification | 9 categories, 17 subtypes, taxonomy v4. Decided by a hand-ordered first-match-wins ladder of roughly 20 rules over case-insensitive substring matching. No ML on the live path. |
| Phrase data | 18 collections externalised to one schema-validated JSON file, not hard-coded. |
| Case-type markers | `A.` audit, `AP.` audit total loss, `D.` diminution. Regex is case-insensitive, whitespace-tolerant, longest-first so `AP.` is never half-read as `A.`. |
| Attachments | Filename/extension gives a cheap evidence kind; document *content* gives an honest type; an explicit per-file rule reconciles the two. |
| Mailbox | Graph change-notification webhooks with immutable message ids, deterministic idempotency keys, and a mailbox-qualified dedup key. |

## The findings that change what we do

### 1. Their own conclusion is that the flat taxonomy was the wrong shape

An open backlog ticket in the rebuild states it plainly:

> The shipped taxonomy is a flat nine-category list. The corpus that grounds
> it is organised as lifecycle **stage** × **intent** — every special-case rule
> wedged into the classifier compensates for a dimension the flat taxonomy
> cannot express.

Their proposed replacement is two axes — `stage` (`pre_instruction`,
`new_work`, `in_progress`, `post_report`, `non_case`) crossed with `intent`
(`instruction`, `update`, `chase`, `query`, `cancellation`, `billing`,
`acknowledgement`, `automatic`, `undeliverable`, `other`) — so that "does this
mint a case" becomes a formula (`stage = new_work AND intent = instruction`)
instead of a hand-maintained list a new category can silently fall out of.

It is unimplemented there. It matters here because Pegasus's settled taxonomy
in [requirements](../requirements.md#settled-mailbox-taxonomy-and-correction)
already separates the same concerns: the Received families are close to
lifecycle stages, and the settled clause insists classification, queue, Triage
routing and folder destination are separate facts. The lesson to carry is the
diagnosis, not their proposed enum: **a category that conflates stage with
intent forces special-case rules.** Worth testing our family/subtype design
against before writing code.

### 2. Confidence scores were built and never used

Four fixed bands exist (0.95 / 0.8 / 0.6 / 0.3) and are persisted per message.
Nothing branches on them. The comment says routing on confidence was to be a
later stage; that stage was never built. The rebuild drops scores entirely and
replaces them with an explicit `needs_review` outcome, documented as never a
default and never guessed into.

This corroborates our position. `docs/open-decisions.md` records that no
numeric confidence score or threshold is accepted and none should be inferred.
The predecessor's experience is that the score was dead weight and an explicit
review outcome was the useful thing.

### 3. Everything we found in the subject line is unexploited there

Verified absent from both checkouts: any parsing of `EREF`, any incident-type
(`RTA`/`PL`) extraction, and any subject-borne incident-date extraction.

What does run over the subject is a loose job-reference regex and a two-tier
VRM extractor. The job-reference tier is what would capture
`Our Ref: MFI/AKH/46553/1`, and it is explicitly documented as too loose to
mint a case — surfaced as an "about existing work" hint only.

So the templated-subject findings in
[qdos-email-tells](qdos-email-tells.md) are genuinely new information, not a
rediscovery. The same applies to the instruction-letter filenames and the
document notification titles: neither `LtrtoAuditEngin` nor
`AUDIT REPORT NOTIFICATION` is matched anywhere. They appear only as data in
ticket evidence files.

The one near-miss: their content typer matches the phrase
`"report + audit report"`, and a comment records that the
`ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)` heading is what forced that
carve-out. They found the same signal and read it as a phrase rather than as a
document title.

### 4. Repairable versus total-loss is not knowable from the instruction

Stated directly in their case-type detection: `audit_total_loss` is never
emitted at classification time because **the real QDOS letters are byte-identical
either way**. The distinction is a review-time refinement read back from an
`AP.`-marked reference.

This independently corroborates our requirement that a standalone Audit derives
`a.` or `ap.` only from an unambiguous assessment in the original Engineer
report, and that missing or ambiguous evidence blocks creation. It also means
our own corpus finding — 146 `A.` folders and zero `AP.` — is expected rather
than a sampling gap in the letters themselves.

### 5. A live case was minted in error, and the class is not yet prevented

Four operator notes record that case `QDOS26007` was wrongly minted from a
provider *query* email that quoted Collision Engineers' own delivered report.
The subject was in the normal QDOS instruction format, so subject shape alone
did not distinguish it. Their note states prevention of the class rests
entirely on the classifier never wrongly saying "receiving work".

The operator-approved decision table is worth carrying over as a test matrix:

| Attachment situation | Outcome |
| --- | --- |
| **Our** report attached, any classification | Correspondence: link or route to the matched case; never mint |
| **Their** report plus instruction-typed document or instruction body signals | Mint audit case |
| **Their** report, no instruction signals anywhere | No mint; abstain and locate |
| No report involved | Unchanged |

Three preventions were designed and none implemented: recognising our own
report by hash or reference, holding when classification says new work but no
instruction document is found, and probing the archive for an existing folder
before minting.

This maps onto our fail-closed invariant and is a concrete argument for making
"instruction letter present" a *necessary* condition for new work, not merely
supporting evidence.

### 6. Staff forwards break sender identification, and it was found late

**[rebuild]** The rewrite's forwarded-sender extractor carries this rationale:

> every alpha instruction arrives as a STAFF FORWARD into
> `instructions@collisionengineers.co.uk`, so the envelope's `From` is a
> Collision Engineers address and Stage 1 correctly returns 'unmatched' — the
> pipeline then short-circuits before it ever classifies anything

Pegasus already handles this: `QdosInstructionExtractionPolicy` unwraps a
staff forward and uses the proved original sender for route identity. Worth
recording that the predecessor shipped without it and had to add it, and that
their operator guidance separately warns staff never to use "forward as
attachment" because photos then cannot be brought into the case.

### 7. Parse order matters when the instruction is a `.doc`

Their document-parse ordering puts Word and RTF first and PDF last, within a
three-document cap. The recorded reason:

> on the audit-email corpus the instruction is a Word `.DOC` while the
> third-party engineer's report is a PDF, so Word-first puts the instruction
> inside the bound. (The old single-doc picker preferred PDF, which is exactly
> how an audit email got its EVA report parsed as 'the instruction'.)

This matches our corpus exactly: audit letters are PDF, Inspection + Audit
letters are legacy binary `.doc`. It is a live argument that reading legacy
`.doc` is on the QDOS critical path rather than deferred, and that document
selection order is a correctness concern, not a performance one.

Their legacy `.doc` reader is a five-method cascade, and one of their unfixed
findings is that its binary scrape silently drops single-token 4–8 character
lines — which is the shape of a VRM or a case reference.

### 8. Their VRM false positives are a ready-made test set

Every guard in their extractor was added after a live incident. Real failures
recorded include postcode outward codes (`B8`, `LS8`, `BD8`), the month name
`OCTOBER`, `X5 NOW` from "Model X5 now…", a postcode-shaped provider job ref,
and — from a QDOS email — `AND2`, extracted from the registered-office line
"Offices 1 and 2". Our own corpus contains that same footer.

If Pegasus does registration extraction from these emails, these are free
negative test cases.

## Ideas worth adopting on their merits

- **Suggested versus chosen classification stored separately.** The original
  automated suggestion is written once, and the chosen value can be overridden
  by staff, so disagreement is visible without reconstructing history. This
  matches what MAIL-21 asks for in decision evidence.
- **An explainable signals list** — every rule that fired, recorded by id, so a
  decision can be explained without re-running it.
- **Phrase data externalised and schema-validated** rather than compiled in.
- **Append-only integer code tables** with an explicit "never renumber" rule,
  so stored classifications keep meaning across releases.
- **Abstain rather than drop.** Nothing is silently discarded; unrecognised
  mail lands in an explicit `other` outcome.

## Contradictions the predecessor never resolved

Two live disagreements between the shipped system and its own rebuild. Pegasus
will need a position on both, and both are already settled in our requirements
— which is worth knowing, because it means we are not required to relitigate
them:

| Question | Shipped | Rebuild | Pegasus position |
| --- | --- | --- | --- |
| Sequence numbering | Per-marker independent sequences | One shared counter per principal and year | [Requirements](../requirements.md) already state Inspection, standalone Audit and Inspection + Audit consume one principal/year sequence — the rebuild's shape |
| Marker case | Uppercase `A.` / `AP.` | Literal lowercase `a.` / `ap.` | Requirements state lowercase `a.` / `ap.` |

## What this does not change

Our task remains MAIL-21 and MAIL-22, and none of the above is a
specification. Specifically: their nine categories are not our settled
taxonomy, their rule ladder is not an accepted precedence model, and
`docs/open-decisions.md` still owns multi-rule precedence and ambiguity for
Pegasus. Nothing here closes that gate.

The concrete carry-over is a list of failure modes to write tests against
before writing rules, and one design question — whether our family and subtype
split can express stage and intent without special-case rules — worth
answering before code.

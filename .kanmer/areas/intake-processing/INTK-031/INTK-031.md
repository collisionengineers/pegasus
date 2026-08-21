---
id: INTK-031
type: ticket
title: Identify the third-party engineer behind an audit's original report
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - extraction
  - audits
  - corpus
links:
  - INTK-028
  - INTK-032
  - CASE-014
docs_todo: true
archived: false
created: '2026-08-21T20:10:24.265Z'
updated: '2026-08-21T23:30:55.367Z'
---

## What

Recognise which third-party engineering firm issued the original report that
arrives with an **audit** instruction, and select the extraction method for that
document from what the issuer is known to produce — rather than running one
grammar over every report and hoping the labels line up.

Scoped to **audits only**. Audit + inspection is out of scope for this ticket.

**The corpus and the registry are keyed by engineering firm, not by principal.**
QDOS is the principal we have seen audits from first, but another principal may
send an audit carrying a report from the same firm, and the same firm's layout
must be recognised either way. Nothing here belongs under a principal's
direct-provider namespace.

## The report's outcome is a required fact, not an optional one

Operator direction, 2026-08-22:

> "Audits are either a. or ap. depending on whether the original report said it
> was Repairable or Total Loss."

So the extractor must read, per issuer, **whether the report declares the vehicle
Repairable or a Total Loss** — and confirm it, not infer it. That fact decides the
case's own reference prefix ([[CASE-014]]), and a reference is immutable once
allocated, so a wrong or guessed reading cannot be corrected afterwards.

This raises the bar for this ticket in two ways:

- the per-issuer record must include **where and how each firm states the
  outcome**, and the vocabulary each uses for it — firms will not all write
  "Total Loss";
- abstention matters more than coverage. A report whose outcome cannot be read
  must say so rather than defaulting to either prefix. What happens then is
  [[INTK-032]]'s subject.

## Why

An audit instruction arrives with an original report written by a different
engineering firm each time — in practice the same few firms, but with no
consistent file naming, and each with its own report layout. [[INTK-028]] fixed
one such layout by de-anchoring the `Speedo:` rule after the deployed grammar
missed a multi-column line; that fix is correct but it is the general shape of a
recurring failure. A rule tuned to one firm's layout silently reads nothing, or
reads the wrong column, on another firm's.

Today nothing records **which** firm's document is in front of the extractor, so
there is no way to say which method applies, no way to see that a firm's layout
has changed, and no way to measure extraction quality per issuer. Building that
labelled evidence base is the prerequisite for every later per-issuer rule.
Keying it by firm also means each principal that starts sending audits inherits
whatever firms are already recognised, instead of restarting the survey.

## Approach

- Survey the local corpus for audit instructions across **every** principal that
  sends them, take the non-instruction document attachment as the original
  report, and label each by issuing firm — from the report's own content
  (letterhead, footer, issuer block), never from the file name and never from
  which principal forwarded it.
- Record, per issuer: the layout tells that identify it, which facts its reports
  carry (vehicle, registration, speedo, make/model, colour, VIN), where they sit,
  **and how that issuer states Repairable versus Total Loss**.
- Turn that into an issuer identification step in the intake extraction route
  that names the issuer on the extracted facts' provenance, and abstains rather
  than guessing when no issuer matches.
- Unknown issuers must degrade to today's behaviour, not fail the intake.
- Reuse the existing extraction route (`InstructionFieldExtraction` and the
  report grammar [[INTK-028]] corrected) — this adds issuer selection ahead of
  the grammar, it does not become a second extractor. The issuer registry lives
  beside the shared extraction code, not under
  `Intake/DirectProviders/Qdos/`, because it is not QDOS's.

**Constraint:** `corpus/` is local, gitignored and immutable. The labelling work
happens against it in place; the committed artefact is the issuer registry and
its rules, never corpus content or excerpts of it.

## Verification

- [ ] Corpus survey recorded in the ticket's research: audit instructions found,
      issuers identified, count per issuer, which principal each arrived via,
      and which reports could not be attributed.
- [ ] The survey records, per issuer, how Repairable and Total Loss are stated,
      including the wording each firm uses.
- [ ] Extraction tests cover at least two distinct issuers' real layouts, plus
      an unattributable report that still extracts what it can.
- [ ] The same issuer is recognised identically regardless of which principal
      sent the audit.
- [ ] Extracted report facts carry the identified issuer in their provenance.
- [ ] The Repairable/Total Loss outcome is extracted with its issuer and its
      location cited, and a report that does not state it clearly **abstains**
      rather than choosing a default.
- [ ] A report from an unknown issuer produces no issuer attribution and no
      regression against current extraction.

## Outcome

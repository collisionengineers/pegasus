# Proof — PLAT-042

Merged to `dev` as PR #531, promoted to `main` in release 27 at
`7d4c8f005261d3963cdecf806b3e06c17552be9b`. Docs-only, so the proof is what the
governing documents say on merged `main` and what that authorised.

## The first attempt was refused, correctly

Independent review returned **fail** on two blocking findings, and a third that
mattered more than either. All three are recorded in the PR, and all three were
fixed before merge:

- `### Before cutover` was a level-3 sibling of `### Production recovery`, so it
  silently reparented the 1–8 recovery contract, the down-migration prohibition
  and the point-in-time restore block — including an "above" that then crossed a
  section boundary. Fixed by removing the heading entirely and putting the
  exemption inside rollback step 3, where it is read.
- The same additive rule stood **unqualified 30 lines above** at `runbook.md:1120`,
  so the runbook contradicted itself inside one section and ENG-014 still read as
  a violation. Now qualified.
- The counter-argument was right: "rebuild-from-empty rather than artifact
  rollback" would have burned the sequence tables and re-issued a used reference —
  the thing the operator twice declined. The text now points at the
  operator-approved **selective** wipe and names what it preserves.

## The runbook could not authorise this on its own

A second review found the amendment insufficient at a level the first pass
missed. `ADR-0002` requires expand-and-contract schema changes, and that clause
sits **outside** the historical block ADR-0007 superseded — so it was still
binding. The documentation authority chain puts ADRs above the runbook, so a
runbook exemption left release engineers with a lower-authority document
contradicting an accepted decision, and ENG-014 remained unauthorised.

Recorded at the right altitude as **ADR-0030**, following ADR-0007's
partial-supersession precedent: status stays `accepted`, frontmatter stays
empty, scope named in the body and in ADR-0002's status line.

Two substantive corrections came with it:

- The exemption now requires compatibility to be **re-established before
  cutover**. Ending it at the cutover date does not repair a migration shipped
  before it, so it is a prerequisite of open-decisions step 7.
- The consequence is stated in full: migrations are applied before the new
  packages activate, so a non-additive migration breaks the **currently running**
  revision for that window too, not only a hypothetical rollback.

## Verified on merged main

| Claim | Evidence |
| --- | --- |
| ADR-0030 exists and is accepted | `docs/adr/0030-non-additive-schema-changes-before-cutover.md`, `status: accepted` |
| ADR-0002 records the partial supersession | its Status line names ADR-0030 for pre-cutover releases only |
| The decision index lists it | `docs/adr/README.md` accepted table |
| The runbook cites the ADR rather than asserting the exemption | `runbook.md` § Production recovery and rollback step 3 |
| Cutover carries the compatibility prerequisite | `docs/open-decisions.md` step 7 |
| Every relative link resolves | `Test-DocumentationLinks.ps1` — 195 files |

## What it authorised, in practice

Release 27 shipped `20260824090400_DropEvaHandoffProvenanceAndManifest`, a
non-additive migration, under this exemption — with its affected capability
named in the release record as obligation 1 requires. That is the first use, and
it worked as designed: the naming forced the exposure to be **checked** rather
than asserted, which is how it was found that the query every Case page reaches
projects a summary and so the previous artifact keeps serving.

## Not proved

Obligation 3 — re-establishing rollback compatibility before cutover — cannot be
proved until cutover. It is written into the step that will need it.

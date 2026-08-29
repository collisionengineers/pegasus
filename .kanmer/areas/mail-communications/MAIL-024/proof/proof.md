# Proof — MAIL-024: FRD-08 and ADR-0036 (outbound mail, EVA-sent detection)

## What was verified, and where

Verified on merged `dev` at `b92cb9a7` (`b92cb9a7b8bf7727b452aa397d9df04084da1270`)
in the primary checkout `C:/Users/PC/Documents/GitHub/pegasus`, working tree clean
(`git status --porcelain` → empty). MAIL-024 shipped through PR #584, merged as
`acc8c8e7` (`acc8c8e76e8a6054d36349308877324676411e18`, 2026-08-28 09:21:27 +0100),
carrying the ticket's two recorded commits `47d9144a` and `b65081db`. This is a
docs-only ticket: the artefacts are four Markdown files, and the evidence below is
that those files exist on `dev`, say what the ticket said they would say, are cited
from the places that make them reachable, and pass the repository's own two
Markdown gates.

## Evidence

### The ticket's two recorded commits are reachable on merged `dev`

Tier: build/test (repository history)

```
git merge-base --is-ancestor 47d9144af70005f97efb8f1540b400dee3905646 b92cb9a7
  -> ancestor of dev@b92cb9a7
git merge-base --is-ancestor b65081db010c27df8dab93fe78f9f43765bfdaaf b92cb9a7
  -> ancestor of dev@b92cb9a7
git merge-base --is-ancestor acc8c8e7 b92cb9a7
  -> ancestor of dev@b92cb9a7
```

### The diff is the four files the plan named, and nothing else

Tier: build/test (repository history)

```
git show --stat 47d9144a
  docs/adr/0036-outbound-mail-via-approved-mailbox.md | 83 ++++++++++
  docs/adr/README.md                                  |  1 +
  docs/boundaries.md                                  |  2 +-
  docs/frd/frd-08-...-background-processing.md        | 56 +++++++
  4 files changed, 141 insertions(+), 1 deletion(-)

git show --stat b65081db
  docs/adr/0036-outbound-mail-via-approved-mailbox.md | 8 ++++----
  1 file changed, 4 insertions(+), 4 deletions(-)
```

No source file, test, or neighbouring doc was touched. `docs/boundaries.md` shows
exactly one changed line — the correspondence row the ticket owns — so the
UIIMP-007 coordination constraint held.

### ADR-0036 exists on `dev` with valid frontmatter and one decision

Tier: deployed (the artefact itself is the deliverable)

`docs/adr/0036-outbound-mail-via-approved-mailbox.md:1-10` on `b92cb9a7`:

```
---
id: ADR-0036
status: accepted
date: 2026-08-28
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-08]
tags: [mailbox, outbound-mail, graph]
---
```

All eight fields of the CLAUDE.md template are present and in the template's own
order; the shape matches the neighbouring ADR-0034 frontmatter field for field. The
body carries `## Status` first (line 14), then `## Context`, one `## Decision`
(line 39), `## Consequences`, `## Links` — a single decision, no bundle. `0036` is a
new number: the index below shows 0033/0034/0035/0036 with nothing renumbered or
reused.

### FRD-08 carries both new sections

Tier: deployed (the artefact itself is the deliverable)

Headings on `b92cb9a7:docs/frd/frd-08-email-mailbox-and-background-processing.md`:

```
366:### Outbound correspondence evidence      (pre-existing anchor, preserved)
378:### Outbound correspondence               (new)
415:### EVA-sent report detection             (new)
```

The pre-existing `#outbound-correspondence-evidence` anchor that FRD-11 and
ADR-0036 both cite survived intact, as the plan's premise required.

`Outbound correspondence` (line 378 onward) states who may send ("a signed-in staff
member with the casework right. There is no autonomous, scheduled, or Automation
Actor send"), from which mailbox ("Reply and Forward send as the approved mailbox
the retained message belongs to; Compose sends as the default approved mailbox …
never a staff member's own address or any mailbox outside the approved allowlist"),
what is retained ("The immutable Sent item Graph writes for the send is the
evidence … The draft text is not evidence until that Sent item exists"), and Flag
and Delete ("Requires a reason, moves the exact item to Deleted Items — never a hard
delete"). That is D4, clause for clause.

`EVA-sent report detection` (line 415 onward) states:

```
A report sent through EVA rather than from Pegasus is detected, not asserted
(EPIC-011 D10). … On that match Pegasus attaches the PDF to the Case as the
report document, links the Sent item as `Report sent` evidence … and records
the report-sent event that moves the Case into post-report work; the Case's
own closure outcome remains a separate reasoned step.
```

That matches the corrected D10 exactly, including the correction PLAT-047 made on
2026-08-28: post-report work, not closure.

### The documents are reachable — the docs-tier equivalent of a caller

Tier: deployed (three live citations on `dev`)

`git grep -n "0036" b92cb9a7 -- docs/`, excluding the ADR itself:

```
docs/adr/README.md:46      | [0036](0036-outbound-mail-via-approved-mailbox.md) |
                             Outbound mail via the approved mailbox | FRD-08 |
docs/boundaries.md:22      … staff-initiated Reply/Forward/Compose from an approved
                             mailbox is in scope under [ADR-0036](adr/0036-…md) …
docs/frd/frd-08-…md:382    … are the technical decision of [ADR-0036](../adr/0036-…md).
```

The ADR index row sits after 0035 in `docs/adr/README.md:38-46`, and
`docs/index.md:21` routes "What durable technical decisions apply?" to that index —
so ADR-0036 is reachable from the documentation root, not orphaned.

### Every link target the change introduces resolves

Tier: build/test

```
pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
  -> All relative Markdown links resolve (129 files checked).
  -> EXIT=0
```

That script deliberately does not check anchors (`# External URLs and same-file
anchors are not checked`; it splits on `#` before testing the path), so the four
cross-file anchors the change introduces were checked by hand against `dev`:

- `../runbook.md#live-operation-approval-matrix` → `docs/runbook.md:794`
  `## Live-operation approval matrix` — resolves.
- `#outbound-correspondence-evidence` → FRD-08 line 366 — resolves.
- `#outbound-correspondence` → FRD-08 line 378 — resolves.
- `#eva-sent-report-detection` → FRD-08 line 415 — resolves.

`docs/adr/0024-…md` and `docs/frd/frd-11-…md`, the ADR's other two link targets,
both exist on `b92cb9a7` (`git ls-tree`).

### Markdown placement gate passes over the ticket's own range

Tier: build/test

```
pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base 47d9144a^ -Head b65081db
  -> Markdown placement passed for 47d9144a^..b65081db.
  -> EXIT=0
```

The one new file is an ADR under `docs/adr/`, which is one of the three permitted
new-Markdown placements.

### Restore, build and test

Tier: build/test — cited, not re-run

Covered by the canonical gate evidence for merged `dev` at `b92cb9a7`
(2026-08-29, Windows 11 / PowerShell 7): restore exit 0; `Build succeeded. 0
Warning(s), 0 Error(s)`; ArchitectureTests 100 passed, Core.Tests 1133 passed,
IntegrationTests 1022 passed / 2 pre-existing skips. MAIL-024 changes no compiled
file, so this tier only establishes that the change broke nothing.

### The code premises the ADR asserts do exist

Tier: registration

The ADR's composed-or-absent argument rests on facts that hold on `dev`:

```
src/Pegasus.Core/Intake/RetainedMailFolderMove.cs:41    interface IRetainedMailFolderMover
src/Pegasus.Core/Intake/RetainedMailFolderMove.cs:134   UnavailableRetainedMailFolderMover
src/Pegasus.Infrastructure/DependencyInjection.cs:83    services.TryAddSingleton<
                          IRetainedMailFolderMover, UnavailableRetainedMailFolderMover>();
src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs:1077  GraphRetainedMailFolderMover
src/Pegasus.Worker/EmailEvidenceFunctions.cs:9          SentEvidencePollFunction
src/Pegasus.Core/Workflow/PollSentEvidence.cs:20        ReportEvidenceAutoLinked
src/Pegasus.Core/Workflow/ApprovedMailboxReportSentEvidence.cs:62
                                                        IRetainApprovedMailboxReportSentEvidence
```

The default registration really is the unavailable implementation, so the ADR's
"local alpha never mutates a mailbox" claim is true of the code as shipped.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| ADR frontmatter valid; one decision | Proven | `0036-…md:1-10` carries all eight template fields in template order, matching ADR-0034; body has exactly one `## Decision` (line 39) |
| `scripts/Test-DocumentationLinks.ps1` passes | Proven | Re-run on `dev@b92cb9a7`: "All relative Markdown links resolve (129 files checked)", EXIT=0 |

Both items the ticket ticked are independently reproved here; neither was taken on
trust.

## Outstanding

- **The behaviour FRD-08 now specifies is not implemented on `dev`.** This is by
  design — the plan's "Out of scope" names the code (`Core/Mail/OutboundMail.cs`,
  the Graph adapter, the Worker) as the EPIC-011 wave-3 lane, `waves.md:15`
  ("Outbound mail + flag + delete + EVA-sent detection"). Stated plainly so no
  reader mistakes this proof for a runtime claim: `git grep -l "Mail\.Send"` and
  `git grep -l "OutboundMail"` over `src/` at `b92cb9a7` both exit 1 — no matches.
  There is no send port, no composer, no Flag or Delete handler. MAIL-024 shipped
  specification at documentation tier only, and claimed nothing more.
- **Two EVA-detection clauses have no counterpart in the existing poll.** FRD-08
  line 415 onward requires the match to consider "a PDF attachment classified as a
  report" and requires Pegasus to attach "the PDF to the Case as the report
  document". `src/Pegasus.Core/Workflow/PollSentEvidence.cs:479-522` today branches
  only on candidate counts — one Case identity auto-links
  (`ReportEvidenceAutoLinked`), several give `Ambiguous`, a refused link gives
  `ReportEvidenceRetainedUnlinked`, none gives `Unmatched`; a grep of that file for
  `pdf|Pdf|Attachment|attachment` returns nothing. The FRD's unlinked/ambiguous
  fallbacks therefore already hold in code, but the PDF condition and the attach
  step are forward-looking requirements owned by the wave-3 outbound-mail lane.
- **One ADR sentence is broader than the code.** ADR-0036's Consequences say "the
  Graph adapter, Worker poll and Core ports already exist". The adapter project,
  the poll and the folder-move port exist (evidence above); an outbound *send* port
  does not. Read as "no new project, store, migration stream, runtime or deployment
  unit is introduced" — the clause it belongs to — the sentence is true; read as a
  claim that the send port is written, it is not. Recorded, not filed as a defect:
  the wave-3 lane adds the port inside the existing projects.
- **Not applicable to this ticket:** the 1580/1100/760 layout walk. MAIL-024 renders
  nothing; UIIMP-010 owns that check for the epic's UI lanes.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not been
promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.

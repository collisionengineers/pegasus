# MAIL-024 plan — FRD-08 and ADR-0036 (docs-only)

## Diff estimate

About +140 / -1 lines across four files:

| File | Change |
| --- | --- |
| `docs/adr/0036-outbound-mail-via-approved-mailbox.md` | new, ~75 lines, one decision |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | +2 sections after "Outbound correspondence evidence": "Outbound correspondence" and "EVA-sent report detection" (~55 lines) |
| `docs/adr/README.md` | one new index row (0036) on its own line, after 0034 (AUTO-009 adds 0035 in parallel) |
| `docs/boundaries.md` | the automated correspondence/chasing row only |

## Premises checked (read-only)

- `docs/boundaries.md` line 22 is the only correspondence row; UIIMP-007 owns the rest of the file.
- FRD-08 already owns Sent-item evidence ("Outbound correspondence evidence") and says "The local alpha must not mutate a mailbox"; FRD-11 cites that anchor — the anchor is kept.
- Existing seam pattern: `IRetainedMailFolderMover` (Core/Intake/RetainedMailFolderMove.cs) is `UnavailableRetainedMailFolderMover` by default and `GraphRetainedMailFolderMover` only when composed (Infrastructure/DependencyInjection.cs:83). Sent-evidence retention is `RetainApprovedMailboxReportSentEvidence` + `PollSentEvidence` (auto-link outcome `ReportEvidenceAutoLinked`). Casework right is `StaffAccessRight.PerformCasework`.
- Sent taxonomy in Core: `SentMailFamily` ReportSent / CaseRejected / QuerySent / AdditionalImageRequest; received `PostReportEmails`.
- ADR-0024 §4: `SentEvidencePollFunction` stays disabled unless separately approved — the ADR-0036 activation clause is consistent with it.

## Operator-notes statements consulted

- "send the response on the original reply chain; and complete the Triage only when the exact approved-mailbox reply-chain Sent item is confirmed." (Stage 1 Triage) — consistent: a staff Reply from the approved mailbox produces that Sent item.
- "Preparing or copying a chaser is not evidence that it was sent, delivered, or answered." (Stage 1.5) — consistent: the draft is not evidence; only the Sent item is.
- "The Engineer sends the report to the provider." / "A retained acknowledgement, source receipt, outbound message record, or `Report sent` event is not post-report completion." (Stage 3) — consistent: EVA-sent detection enters post-report work through the existing Report-sent event; D10 wording "Case completed" is expressed as the existing `Report sent` transition, not closure.
- "EVA currently generates the final provider report ... a PDF's existence or custody does not prove that the report was sent or received." — consistent: detection requires the Sent item in the approved mailbox, not a PDF alone.
- No operator statement contradicts staff-initiated send from the approved mailbox; nothing stops.

## Steps

1. Create ADR-0036 (frontmatter; Status · Context · Decision · Consequences · Links).
2. Add the two FRD-08 sections; keep existing anchors intact.
3. Add the 0036 index row.
4. Rewrite the boundaries correspondence row.
5. `pwsh ./scripts/Test-DocumentationLinks.ps1`; commit; push; PR to `dev`.

## Out of scope

Code (wave 3 `Core/Mail/OutboundMail.cs`, Graph adapter, Worker), the Graph scope grant, capabilities.md rows (UIIMP-007), ADR-0035 (AUTO-009).

## Simplification pass — 2026-08-28

n/a — docs-only.

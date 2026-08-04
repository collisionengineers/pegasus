# Additions — shipped capability with no UI visibility

Companion to `ui-standards-and-review.md`. Each entry is functionality that exists in
Core/Infrastructure/Worker on `dev` @ `b6d030b` but that an operator cannot see or reach from any
screen. For each: evidence, why it matters, and a surfacing recommendation that follows the same
standards (show don't tell, business vocabulary, no dev-speak). These are planning
recommendations only — nothing here is implemented by this review.

## 1. E-mail classification (QDOS mail families) — recorded per message, shown nowhere
- Evidence: `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`,
  `.../Qdos/QdosMailClassificationPolicy.cs`, persisted by migration
  `20260803123935_MailClassificationDecisions`, projected at `IntakeContracts.cs:318`
  (`MailClassificationDecision`). Zero hits for "classif" under `src/Pegasus.Web/Pages`.
- Why it matters: the operator's requested Inbox categories ("Received today", "Queries
  outstanding", "Needs sorting", and the capabilities-tracked UI-14 queues — Receiving work /
  Queries / Other) are exactly what this decision already records. The data is there; the UI isn't.
- Recommendation: the new **Inbox** page's filter chips and the Dashboard's **E-mail activity**
  counts should be driven by the recorded classification (business labels only — "Receiving
  work", "Queries", "Other"). This aligns with `docs/capabilities.md` UI-14 (Next / 0.3.0).

## 2. Automatic case matching and association — promised visible, isn't
- Evidence: `src/Pegasus.Core/Intake/CaseMatching/*`, `DurableIntake.cs:579-620`
  (`AssociateCaseIfUnambiguousAsync`) — silently associates a received e-mail to a case on a
  unique match; the code comment says "the recorded decision stays visible for a staff link", but
  nothing in Web renders `CaseMatchDecision` (zero grep hits).
- Why it matters: an e-mail can attach itself to a case with no operator-visible provenance, and a
  non-unique match records a decision nobody can inspect. That is invisible automation of exactly
  the kind the product's own principles say must be evidenced.
- Recommendation: on the received-item review screen, a small "Matched to case" panel: the matched
  Case/PO as a link, or "No unique match — link manually" with the candidates. On the case
  workspace history, the association event with its plain-language basis ("Matched by claim
  number").

## 3. Automated report-sent evidence — Worker records it, no operator view
- Evidence: `src/Pegasus.Core/Workflow/PollSentEvidence.cs`,
  `ApprovedMailboxReportSentEvidence.cs`, `AutoLinkReportEvidence`
  (`Infrastructure/DependencyInjection.cs:264`), hosted in
  `src/Pegasus.Worker/EmailEvidenceFunctions.cs`. Web surfaces only the manual link path
  (`_CaseWorkflow.cshtml:262-290`).
- Why it matters: the automatic path runs in production and changes case evidence with no screen
  showing what it did or failed to do.
- Recommendation: the case workspace's report-sent section shows the automatically linked Sent
  item exactly as a manually linked one (with "linked automatically" provenance); the e-mail
  activity drill-down lists auto-link outcomes with retry, replacing today's generic Sent
  retry rows.

## 4. Chaser scheduling — runs on a timer, invisible schedule
- Evidence: `src/Pegasus.Core/Tasks/RunDueChasers.cs`, `EfCaseDueChaserStore`, Worker-hosted;
  Web shows only the capped "Due case work" list (20 max) and a manual chase recorder.
- Recommendation: the case workspace shows the next scheduled chase date on the case ("Next
  chase: 11 Aug") and the Queues page's Not ready rows carry it as a sortable column. No new
  page needed.

## 5. Automation actor (MCP ingress) — nine tools, gated off, admin page permanently inert
- Evidence: `src/Pegasus.Web/Mcp/` (9 tools across Case/Intake/Document tool classes), gate
  `Features:AutomationMcp` set in no shipped configuration; `Administration/Automation/Index.cshtml:27`
  therefore always renders the "not composed in this deployment" card. `_CaseHistory.cshtml:24-26`
  even ships an "Automation" actor chip that can never appear.
- Recommendation: per presentation rules, a non-composed capability is absent — hide the
  Automation admin card and page while the gate is off. When it is on, the existing
  activity log becomes the visibility surface (relabelled per page-31 plan). Track the future
  Send-to-AI/assessment surfaces (`docs/temp-plans/mcp-assessment-toolset.md`,
  `send-to-claude-channel-integration.md` — both currently plans, not implementations; commit
  `b6d030b` is a NOW.md claim only) against the same standards when they land.

## 6. Engineer assessment workbench ("Send to Claude") — full markup, deliberately routeless
- Evidence: `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` and `Suggestions.cshtml` — no
  `@page`, no PageModel, recorded operator widening 2026-08-03 (`design/README.md` §Deferred
  casework and advanced surfaces). Deliberate, not a defect.
- Recommendation: no action now; when the wiring task restores the route, its copy must pass the
  same standards (the current markup's "Decide on each suggestion. Nothing reaches the case until
  you apply what you have accepted." is narration; the confirm-dialog flow is good).

## 7. Registered-but-callerless services (no UI implication, recorded for completeness)
- `QdosAlphaAcceptanceGate` (`CoreAssembly.cs:52-316`, registered `Program.cs:539`) — release
  evaluator used only by tests/scripts. Not an operator surface; leave headless.
- Provider reference-data catalog (`IProviderReferenceCatalog`, `EfProviderReferenceCatalog`,
  registered `DependencyInjection.cs:106`) — no Web/Worker consumer despite build scripts existing.
  If/when consumed, principals and organisation screens are the natural surface (e.g. validating
  provider domains); until then, nothing to show.
- Standalone desktop e-mail evaluator (`scripts/email-eval-desktop/`) and the three
  `workspaces/` imports (report-renderer, document-extraction, ai-centre) — separate non-caller
  source by policy; no UI surface owed.

## 8. Dead Web components — remove or adopt
- `_ReasonDialog.cshtml` (focus-trapped reason modal), `_MetricCard.cshtml`,
  `_ProvenancePanel.cshtml`, `_ErrorSummary.cshtml` — referenced by nothing. The reason dialog is
  actually the interaction the redesigned lifecycle Actions panel wants (per-action dialog with
  required reason): adopt and finish it, or delete all four. The other three use inline styles and
  should not be adopted as-is.

## 9. Navigation gaps for existing pages (also in defects register)
- `/Operations/Email` and `/Operations/Requests`: only entry is dashboard cards labelled
  "Unavailable". `/ImageIntake`: no nav path at all. The new IA (root doc §3) gives all three a
  home; listed here because they are, in effect, hidden features today.

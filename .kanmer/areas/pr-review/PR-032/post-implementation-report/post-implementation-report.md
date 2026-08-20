# Post-implementation report — PR-032

## Summary

The authenticated exact-message page now renders the existing Core unavailable folder recommendation even when no classification dossier exists. The fix adds one semantic heading/definition list and one exact LocalDB-backed Web test; it changes no business policy, data access, or mutation boundary.

## Changes

| File | Change | Why |
|---|---|---|
| src/Pegasus.Web/Pages/Mail/Message.cshtml | Added a null-dossier sibling branch that renders the existing unavailable recommendation reason and policy. | Closes the real caller omission while preserving classified/ambiguous layout and behavior. |
| tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs | Added an authenticated no-classification test asserting heading, unavailable value/reason, no move control, and no new POST. | Proves the exact blocker through the production Razor/SQL pipeline. |

## Governing docs

- Meets docs/frd/frd-08-email-mailbox-and-background-processing.md by showing an honest unavailable state while keeping classification, recommendation, and later move separate.
- No governing docs or ADR changed.

## Simplification pass — 2026-08-20

- Reuse: consumes Detail.FolderRecommendation and its Core-owned reason/policy; no mapping or wording copy outside rendering.
- Simplification: preserved the existing classified branch and added the narrow missing sibling; a new partial would have only one caller and two rows.
- Efficiency: Razor-only branch; no new query, store call, allocation, or client script.
- Altitude: no Core/Infrastructure/persistence/DI/MCP/Graph/operation-key/mutation change.
- Unapplied findings: none.

## Risks / follow-ups

None inside PR-032. MAIL-06/07, deployment, and live verification remain TICK-047's existing separate boundaries.

## Verification hand-off

- dotnet build ./Pegasus.slnx --configuration Release --no-restore — passed, 0 warnings/errors.
- MailWorkspaceWebTests — 17/17 passed against LocalDB.
- git diff --check — passed (only normal CRLF conversion notices).
- Replacement CI should run on the pushed commit before independent re-review.

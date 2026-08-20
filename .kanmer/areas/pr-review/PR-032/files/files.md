# Files — PR-032

## Changed files

| Path | Change | Risk |
|---|---|---|
| src/Pegasus.Web/Pages/Mail/Message.cshtml | Render the existing Detail.FolderRecommendation in its own accessible evidence section outside the dossier condition. | Preserve classified/ambiguous output and do not add any form or input. |
| tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs | Add exact authenticated null-dossier unavailable-state coverage. | Keep the fixture immutable and assert no new mutation control. |

## Context files

| Path | Why read |
|---|---|
| src/Pegasus.Core/Intake/RetainedMail.cs | Already owns the null-dossier unavailable result and reason; must not change or duplicate it. |
| docs/frd/frd-08-email-mailbox-and-background-processing.md | Keeps classification, recommendation, and move separate and requires honest unavailable state. |
| [[TICK-047]] plan/files/PIR | Owns the feature scope, no-write boundary, and existing focused verification evidence. |

## Out of scope

No Core, page-model, persistence, DI, Graph, mailbox, docs, MCP, operation-key, confirmation, move, deployment, or external write change.

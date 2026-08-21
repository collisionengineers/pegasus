# Files — CASE-009

## Where the change lands

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | Add the Case Details read shape/port for linked email classified as Query. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | Populate the new Core read shape from retained-email association and classification data. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Carry the read-only query-email data into Case Details. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` | Rename the heading to Queries, remove the disabled manual control, and render the auto-attached list/empty state. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Verify linked Query emails render, non-Query emails do not, the empty state is truthful, and manual creation is absent. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | The shared Case Summary partial is rendered in the Case Details overview, so its markup is the actual page surface. |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | `CaseDetails` is the established Core-owned read model to extend rather than creating a second Web-specific query path. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Query classifications and their Queries destination are email-workspace rules; read selection must rely on the canonical classification and Case link. |
| `docs/frd/frd-12-operator-experience.md` | Operator-facing state and detail journeys remain governed by the FRD and design authority. |

## Ripple effects

The change crosses Core, Infrastructure, Web, and integration tests but remains read-only. It must reuse the existing retained-email association and classification records, without changing association policy, email classification, mailbox operations, or deployment documentation.

## Out of scope

Creating, replying to, resolving, or manually associating queries; mailbox mutation; and fabricated query data.

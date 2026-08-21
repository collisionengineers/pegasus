# Files — CASE-009

## Where the change lands

| Path | Why |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` | Rename the static heading, remove the disabled manual creation button, and update its now-inaccurate placeholder/comment. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Add a focused rendered-HTML assertion for the renamed panel and absence of the manual action, if the ticket remains presentation-only. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | The shared Case Summary partial is rendered in the Case Details overview, so its markup is the actual page surface. |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | `CaseDetails` has no correspondence/query collection; do not imply linked query emails are already being supplied. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Query classifications and their Queries destination are email-workspace rules, separate from Case Details rendering. |
| `docs/frd/frd-12-operator-experience.md` | Operator-facing state and detail journeys remain governed by the FRD and design authority. |

## Ripple effects

The copy/control-only option changes no Core policy, storage, email association, mailbox operation, or deployment documentation. A linked-email-list option expands into Core, Infrastructure, Web, and integration-test surface and needs functional requirements.

## Out of scope

Creating, replying to, resolving, or manually associating queries; mailbox mutation; and any fabricated query data. Whether a read-only list of already-linked Query emails is in scope awaits the operator decision recorded in `open-questions`.

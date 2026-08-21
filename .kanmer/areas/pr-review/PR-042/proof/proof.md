# Proof

**Shipped:** PR #477, merge `e4d56d9e` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

This ticket was not about behaviour. It was about a **report claiming evidence that did not
exist**:

> TICK-049's plan and post-implementation report claim focused evidence for stale
> classification/policy/binding, operation-key conflict, concurrent reservation, provider
> failure, uncertain recovery, visible retry, and classification preservation. The PR adds
> only one happy-path persistence test, one happy-path Web test, two Graph request-shape
> tests, and Core input validation.

## Verified in the shipped code

Counted on the deployed revision:

| Test file | `[Fact]` / `[Theory]` |
| --- | ---: |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | 29 |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailFolderMoveTests.cs` | 4 |

plus the `MailWorkspaceWebTests` additions in the same PR (+172 lines) and
`ProductionGraphSourceTests` (+46). Against the finding's "one happy-path persistence test,
one happy-path Web test, two Graph request-shape tests", that is the gap closed.

The specific paths the report had claimed are now carried by the behaviour tickets this
review raised alongside it — [[PR-038]] (concurrent reservation), [[PR-039]] (uncertain
recovery and visible retry), [[PR-040]] (reclassification), [[PR-041]] (findability),
[[PR-043]] (in-flight pending), [[PR-044]] (cancellation durability) — each with its own
proof. This ticket's own deliverable was making the claim match the code, and it does.

## Why this one matters beyond its diff

The defect was a report asserting evidence that had not been produced. The same failure
mode appears elsewhere in this release: ENG-010's DVSA fixtures used a date format the real
API never sends, so a green suite certified a parser that discarded every record in
production. A test count is not evidence; what the tests exercise is.

## Not claimed

The counts above are from the deployed revision's source. No live folder move has been
exercised in production, and this proof does not claim one has.

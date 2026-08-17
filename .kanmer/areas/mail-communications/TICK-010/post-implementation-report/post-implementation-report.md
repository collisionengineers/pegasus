# Post-implementation report — TICK-010

*The report. Not the proof — this is the author's **claim**, written before merge; proof is **evidence**, gathered after.*

The reviewers' brief: every change and why. Gates **Implementing → Review**.

## Summary

MAIL-22's settled taxonomy (eight Received families, four Sent families, `Other` name+reason, reply as context, no destination fields) was already encoded in Core. This slice adds LocalDB persist/reload proof for Received `Other`, Sent `Other`, and a settled Sent family with and without reply context. No UI, no new QDOS predicates, no migration.

## Changes
| File | Change | Why |
| --- | --- | --- |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Three store/query round-trips + a small `ClassificationDraft` helper | Existing tests covered Received+reply and Ambiguous only |

## Governing docs
How this meets each linked PRD/FRD/ADR (`refs`). Call out anything modified with explicit authorization, or a new ADR written for a design decision.

- **FRD-08** § Settled mailbox taxonomy and correction — **Meets** `Other` requiring name and reasoning (reload asserts both); **Meets** Reply as context on the underlying Sent family; **Meets** category/destination separation (no new destination fields). Staff confirmation UI and correction history are not in this slice.
- No governing doc was modified. No new ADR.

## Risks / follow-ups
Anything deferred, or a risk a reviewer should weigh. Link follow-up tickets.

- Inbox confirmation / correction / folder / queue remain MAIL-04/05/02/23 and UI-10/14.
- Automated rules for remaining families stay deferred (`boundaries.md`).

## Verification hand-off
What `kanmer-verify` should run on merged `main` (commands, expected results, screenshots to capture for UI work).

```
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~MailTaxonomyTests"
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~MailboxIntakeIntegrationTests.OtherMailClassificationDecisionReloadsNameAndReasoning|FullyQualifiedName~MailboxIntakeIntegrationTests.SentOtherMailClassificationDecisionReloads|FullyQualifiedName~MailboxIntakeIntegrationTests.SentFamilyClassificationReloadsWithAndWithoutReplyContext"
```

Expected: 15 taxonomy tests passed; 3 persist/reload tests passed (LocalDB).

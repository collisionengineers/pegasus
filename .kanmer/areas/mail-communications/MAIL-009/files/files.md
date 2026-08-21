# Files

Committed in `1a86f5db`.

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/StaffForwardBodyCleaner.cs` | `ForwardedSenderAddress` — reads the forwarded header out of the retained body | the existing forward-body parsing |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs` | `ProvisionalEffectiveSender`, kept beside `Evaluate` so the provisional rule and the authoritative one cannot drift | the same unwrap `Evaluate` uses |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | `BodyHead` (raw body, newlines intact, first 600 chars) and the provisional effective sender written at retention | the retention write itself |

## Not changed

`MailRouteDecision` stays authoritative and supersedes the provisional value as soon as
intake processing lands.

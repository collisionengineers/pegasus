# Proof

**Shipped:** PR #490, merge `4baae5f0`, fix commit `563bb2ec` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

The compensation for a definitive association refusal called `IReleaseCaseEditLease`, caught
**every** recoverable release failure, and then unconditionally cleared the only retained
lease token. A transient release failure therefore recreated [[PR-049]]'s five-minute
stranded lease, with the retry path thrown away in the same breath.

The subtle part: the compensation was correct, and the error handling around it undid it.

## Verified in the shipped code

`src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:780-820`,
`ResolveFailedAssociationLeaseAsync` — the two release-failure paths are now distinguished:

```csharp
catch (Exception releaseException) when (IsDefinitiveAssociationFailure(releaseException))
{
    ClearAssociationLease();
    return false;
}
catch (Exception releaseException) when (IntakeExceptionPolicy.IsRecoverable(releaseException))
{
    return true;                    // <- state deliberately NOT cleared
}
ClearAssociationLease();
return false;
```

A **recoverable** release failure returns without clearing, so `AssociationLeaseCaseId` and
`AssociationLeaseToken` survive and the same compensation can be retried — the guard
condition `AssociationLeaseCaseId is not { } caseId || AssociationLeaseToken is not { }`
(`:793`) is what that retained state satisfies on the next attempt. A **definitive** release
failure clears, because there is nothing left to retry. Successful release clears
(`:818`), which is the third bullet.

`IntakeExceptionPolicy.IsRecoverable` is the existing shared taxonomy, and
`IReleaseCaseEditLease` is the existing port. No background worker, no Mail-specific store,
no second lease policy — the finding's constraint held.

## Not claimed

Proved by the exception paths, with the ticket's four verification bullets ticked at the
time of the fix. No transient release failure has been forced against production, and this
proof does not claim one has.

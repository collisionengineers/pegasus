# Proof

**Shipped:** PR #506, commit `ca564ac5` · **Deployed:** Release 18, `1f3be493`, smoke-asserted source SHA.

## The section is gone; the values it supplied are not

At the deployed revision, `_CaseWorkflow.cshtml` contains **no** reference to `MotTests` —
the history table and its caption are removed, along with the rows that described the call
rather than the vehicle ("Latest lookup outcome", "Provider", provider version).

What remains is what the lookups are for. Production shows both live cases holding exactly
the gap-filling values the operator described:

```
DF18FEJ  BMW      mileage 72312 Miles   (latest-mot-observation v2)
LG64JAU  RENAULT  mileage 128343 Miles  (latest-mot-observation v2)
```

Registration, retrieval time, vehicle details and mileage with its evidence classification
still render; the accept and correct forms — how an operator promotes a looked-up value to a
confirmed one — are untouched.

## Collection is unchanged, deliberately

`MotTestsJson` still holds 702 and 1565 bytes on those observations. [[ENG-010]] had just
proved every MOT test was being silently discarded, and the derived mileage is computed from
them. This ticket removed a **display**, not the data behind it — removing both would have
undone the fix that made the mileage possible.

## Evidence tier

**Deployed-code for the removal, observed-in-production for what survives.** The
authenticated case page has not been viewed, because that needs a sign-in I must not
perform; the persisted observations behind it are read directly.

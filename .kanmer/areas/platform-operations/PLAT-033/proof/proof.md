# Proof

**Shipped:** PR #506, commit `3d7f87d6` · **Deployed:** Release 18, `1f3be493`, smoke-asserted source SHA.

## Both overloads carry it

At the deployed revision, `OperatorLabels.cs`:

```csharp
IntakeSourceChannel.Mailbox => "E-mail",
"mailbox"                   => "E-mail",
```

No occurrence of "Approved inbox" remains in `src/Pegasus.Web`. Changing only one overload
was the trap this ticket had to avoid — the same one-list-per-concept split that produced
[[CASE-015]]'s Odometer/Mileage confusion.

The sibling channel labels (`ManualUpload`, `Automation`) were reviewed and needed nothing;
neither describes configuration rather than what the operator sees.

## Evidence tier

**Deployed-code.** A label map with no runtime input: the constant is either "E-mail" or it
is not, and the build containing it is the build serving. The case Origin row has not been
viewed in the live app, because that needs a sign-in I must not perform.

The production case data confirms the label applies here — QDOS26010's receipt carries
`IntakeSourceChannel.Mailbox`, which is the branch that changed.

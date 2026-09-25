# Sign in: states

| State | Query | Shows |
| --- | --- | --- |
| Default | `?state=default` | Username, Password, Sign in |
| Error | `?state=error` | The invalid-credentials summary and the emptied password |
| Signed out | `?state=signed-out` | The green check and **You are signed out** above the form |
| Forced password change | `?state=forced-password` | New password and Confirm new password in the same frame |
| Access denied | `?state=access-denied` | Area eyebrow, **Access denied**, one sentence, Return to Work Centre |

## Three initial-login alternatives

Each of the new A/B/C files supports `default`, `validation`, `error` and
`signed-out`. `validation` adds the exact required-field messages and
invalid-field treatment. Filled demo submissions show a brief pending
button, then the error state; they never contact an account. Forced change
and access denied remain in the earlier navless-family mockup above.

See the [comparison](../../../current/pegasus_signin_designs_v30.html) and
[proposal](../../../current/signin-design-proposals.md#interaction-and-coverage).

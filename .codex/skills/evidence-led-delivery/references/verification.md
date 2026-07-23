# Verification language

Use these labels precisely:

| Label | Required evidence |
|---|---|
| Planned | reviewed sequence and acceptance criteria exist |
| Implemented | code or configuration exists in the working tree |
| Called | the intended entry point reaches it |
| Locally verified | stated local checks pass on stated inputs |
| Deployed | target environment accepted the deployment |
| Live verified | fresh production-like traffic reached the expected path and result |
| Accepted | authorized operator or stakeholder accepted the observed result |

For every validation command, state the exact command and exit result, input class, boundary exercised, what the check cannot establish, and skipped evidence. Do not collapse these labels into `done`.

# Proof — TICK-216

## Verification tier

Corrected no-code acceptance proof against [[SIMPLI-014]]'s merged implementation on current `origin/dev` (`7afd18037acfa78927c4b4ffdf8e0f74c7ecc688`).

## Evidence

- FRD-11 states that exact matching tuples are required and names one currently complete tuple: `A Patterson | M.Inst.IAEA | andy_patterson`.
- FRD-11 explicitly withholds Ed Mawdsley and Neil O'Reilly until accepted qualifications complete their tuples; `docs/open-decisions.md` records the same open evidence.
- `origin/dev:src/Pegasus.Core/Reports/AssessmentReportRendering.cs` contains one `AcceptedEngineers` entry, for Andy.
- `origin/dev:src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` embeds only `andy_patterson.png`.
- `AssessmentReportRendererTests.OnlyActiveSignatureResourceIsEmbeddedByteForByte` verifies Andy's governed bytes and asserts no Ed/Neil resource is embedded.
- Core negative tests reject wrong qualifications, Neil with an empty qualification, and unknown identities before adapter invocation.
- SIMPLI-014 PR #415 merged at `b548b674e31d05de6f43eeb285a25dedd7d2a768`; its proof records 11/11 Core tests, 5/5 real-Chromium tests, 39/39 architecture tests, and green required CI.

## Result

PASS at the exact boundary proved. Andy Patterson is the only complete selectable engineer tuple. Ed Mawdsley and Neil O'Reilly remain unavailable pending supplied and accepted qualifications. Missing, mismatched, substituted, custom, placeholder, and otherwise unaccepted content fails closed; draft generation does not authorise issue or send.

The former TICK-216 claim that all three tuples were accepted has been removed from its research, files, plan, checklist, open questions, PIR, Outcome, and proof. TICK-216 has no repository commit, PR, worktree, deployment, or cloud action. Deployment: `n/a`. PR/merge: `n/a — acceptance slice subsumed by PR #415`.

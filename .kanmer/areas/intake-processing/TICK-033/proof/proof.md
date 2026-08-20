# Proof — INT-31 request-scoped upload links (verified on merged main / production release 13)

Verified 2026-08-20 during the board groom (read-only git + repository evidence):

- The ticket's branch work merged via **PR #408** (merge commit `60fde326`); its commit `b32532d8` is an ancestor of `origin/dev`, `origin/main`, **and `2325ed4a`** (release 13, currently served by production) — confirmed with `git merge-base --is-ancestor`.
- The PR itself was a docs alignment (`docs/capabilities.md` wording); the capability is long-shipped in code: `src/Pegasus.Core/Documents/RequestUploadPolicy.cs`, `src/Pegasus.Web/Pages/Uploads/Request.cshtml(.cs)`, `EfDocumentRequestStore`, migration `20260729150000_DocumentCustodyAndRequests` — all present on the deployed SHA.
- The one unchecked checklist item ("run focused request-upload integration tests locally") was a local-timeout convenience run; the suite runs in PR CI, which was green at merge.

Provenance note: the claim (worktree `../pegasus-worktrees/tick-033`, branch `task/tick-033-request-upload-reconciliation`) was stale — both were removed after the merge without the ticket being closed. Claim released and `deployment` corrected to `production` as part of this closeout.

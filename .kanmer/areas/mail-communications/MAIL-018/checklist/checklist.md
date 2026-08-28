# MAIL-018 checklist

- [x] `IApprovedMailboxSubscriptionStore.ListAsync` added; EF implementation; webhook test fake updated
- [x] `MailboxesModel` loads subscriptions and exposes `SubscriptionStatusFor`
- [x] `Mailboxes.cshtml` shows Activated and Subscription columns (labels + values only)
- [x] Integration test covers populated and absent cases
- [x] Test UI snapshot regenerated via `scripts/Update-TestUiSnapshots.ps1` — Mailboxes page only committed (47ebad54); 49 unrelated regenerations deferred to [[MAIL-023]], see `scratch/snapshots`
- [x] restore --locked-mode / build Release 0 warnings / Core (1001) + Architecture (100) tests / focused integration filter (6/6) PASS; controller full suite 987/988 with unrelated QdosMapping regex timeout, class rerun 7/7
- [x] Simplification pass recorded in plan
- [x] Committed (77989d47, 47ebad54) and pushed on `task/mail-018-mailbox-subscription-health`
- [x] PR #577 opened against `dev`

## Closeout — MAIL-018

- [x] PR merge verified (`gh pr view --json state,mergedAt`) — MERGED 2026-08-27T18:38:59Z, 3a1a017c
- [x] proof.md finalised (PR URL + merge date appended)
- [x] Moved to final stage (done, 2026-08-27T18:45:53Z)
- [x] Outcome recorded in ticket body (PR link, follow-ups)
- [x] cd out of worktree; `git worktree remove` of the recorded ticket worktree (no `--force` needed; only ignored artifacts/bin/obj)
- [x] `git branch -d task/mail-018-mailbox-subscription-health` (plain `-d`, fully merged into origin/dev); `git push origin --delete` done
- [x] `git fetch --prune` + `git worktree prune`
- [x] `take_ticket action: "release"`

# Checklist — PLAT-027

- [x] `origin/dev` (`b92cb9a7`) merged into `task/plat-027-staff-accounts-roles` — clean fast-forward
- [x] `OperatorLabels.StaffAccounts` appended (own nested class, +24 lines, nothing reordered)
- [x] `Accounts/Index.cshtml.cs` reads `IListStaffAccounts` + `IGetAccessReview`
- [x] Handlers `Create`, `Roles`, `Disable`, `Review` all call their existing Core use case
- [x] `[Authorize(Policy = Administrator)]` preserved on the consolidated page
- [x] Accounts table renders Username, Role select, State, Last reviewed, Save, Account
- [x] Role select carries every `StaffRole`, pre-selected from the account's set
- [x] Access-review readout renders from Core's `ReviewIsOutstanding`, not a Web re-derivation
- [x] Disable and Review use `_ReasonDialog`; Disable carries one consequence sentence
- [x] Create staff account panel present; the old field-hint sentence deleted
- [x] No inert control on the page — every drawn control has a handler
- [x] No legacy-block CSS class used
- [x] `TestUiFocusedRenderTests` empty-state assertion retargeted at the exact new markup
- [x] `StaffAccountsAndRolesWebTests` added — 4 passed
- [x] `dotnet build ./Pegasus.slnx --configuration Release` — succeeded, 0 warnings, 0 errors
- [x] Focused filters run; real counts recorded in the post-implementation report
- [x] Simplification pass recorded in the plan under a dated heading
- [x] Superseded routes listed for UIIMP-009 in the post-implementation report
- [x] Committed in four slices, pushed, PR [#619](https://github.com/collisionengineers/pegasus/pull/619) opened against `dev` — not merged

Deliberately not done, per the lane brief and the 2026-08-29 decisions:

- [ ] Browser category and snapshot regeneration — run once per merge on the merging branch, not in-lane
- [ ] Deletion of the superseded `/Administration/{Roles,Access}` and `Accounts/Edit` routes — UIIMP-009, wave 5
- [ ] `proof` and the move to `done` — written against merged `dev` by the verify step (D15)

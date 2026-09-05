## PR

https://github.com/collisionengineers/pegasus/pull/663
(branch `task/case-042-awaiting-instruction-queue`, head
`60c80769ffa045ba49b79a3c7115313cd67a0594` — see "Review round fixes" below;
the round-1 review head was `353f3da1b82ff8d0079c01ae791cc066f52aa0eb`)

## Review round fixes (2026-09-05)

PR review (round 1, at head `353f3da1b82ff8d0079c01ae791cc066f52aa0eb`)
returned two blockers and one should-fix; all three are fixed at the new
head `60c80769ffa045ba49b79a3c7115313cd67a0594`, along with two additional
small fixes riding the same commit. Full findings, dispositions, and
re-verification are recorded in `plan/plan.md` under "## Review round fixes
(2026-09-05)":

1. **BLOCKER** — `Index.cshtml.cs:289, 341-343`: a successful "Add to an
   existing case" redirected to `?tab=awaiting&selected=<intakeId>`, but the
   attached intake had left the Awaiting rows, so `OnGetAsync`'s stale-
   `selected` guard 404'd past the confirmation message. Fixed: the guard
   now 404s on every tab except `awaiting`, which instead drops the stale
   selection and falls back to the first remaining row.
2. **BLOCKER (test)** — `TriageQueuesWebTests.cs:508-512`: the success test
   never followed the redirect, so it masked finding 1. Fixed: it now
   follows `response.Headers.Location`, asserts 200, asserts the
   confirmation text, and asserts the row is gone.
3. **SHOULD-FIX** — `Index.cshtml:36`: the failure banner used a
   nonexistent `alert alert--error` class. Fixed: changed to
   `validation-summary`, the class the two upload pages it was copied from
   actually use.

Also in the same commit: corrected the stale "(Triage)" remark at
`Index.cshtml.cs:24-25` to name the Awaiting instruction tab; reworded the
simplification-pass finding 3 wording (`plan/plan.md`) to stop calling the
shared `ProjectAsync` image-count subquery "bounded" (`ListAsync(false, …)`
reads the Awaiting queue unpaged) while keeping the accepted-risk substance;
and added `maxlength="60"`/`"500"` to the Awaiting attach form's
`reference`/`reason` inputs, matching the sibling `UploadGroupStatus.cshtml`
form.

Re-verified at the new head: `dotnet build` (0 errors), `Pegasus.Core.Tests`
(1240 passed), `Pegasus.ArchitectureTests` (100 passed),
`TriageQueuesWebTests`/`AccessibilityTests` (39 passed, including the
strengthened success-redirect assertion), scoped
`Update-TestUiSnapshots.ps1` capture and `-Verify -SkipCapture`, and
`Test-UiCatalogue.ps1` — all green. No `queues--*` snapshot content changed
(neither the attach form's `maxlength` nor the failure banner's class
appear in the default/empty captures).

Branch `task/case-042-awaiting-instruction-queue`, PR
https://github.com/collisionencineers/pegasus/pull/663. Head
`60c80769ffa045ba49b79a3c7115313cd67a0594` pushed and CI awaited.

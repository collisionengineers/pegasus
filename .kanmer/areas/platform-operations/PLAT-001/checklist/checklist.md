# Checklist — Claude Design UI implementation

## Shell
- [x] `.app-rail` block added to `wwwroot/css/site.css` (grid, rail-link, aria-current, counts, narrow-viewport collapse)
- [x] `Pages/Shared/_Layout.cshtml` rewritten to the left rail
- [x] Skip link, `_LucideSprite`, `inboxEnabled` gate, `CurrentWhen`, Administrator-only item, auth branch and `TempData["Confirmation"]` all preserved
- [x] `_LayoutAuth` / `_LayoutExternal` confirmed unchanged and still correct

## Screens
- [x] `Pages/Index.cshtml` — Dashboard
- [x] `Pages/Upload.cshtml` — Upload
- [x] `Pages/Uploads/Request.cshtml` — UploadLink
- [x] `Pages/Account/PasswordChange.cshtml` — ChangePassword
- [x] `Pages/Triage/Index.cshtml` — Queues
- [x] `Pages/Cases/Index.cshtml` — Cases
- [x] `Pages/Search/Index.cshtml` — no change needed; the route is a redirect into Cases, not a screen
- [x] `Pages/Mail/Index.cshtml` — Inbox
- [x] `Pages/Mail/Message.cshtml` — InboxMessage
- [x] `Pages/Operations/Index.cshtml` — Operations
- [x] `Pages/Administration/Index.cshtml` — Administration
- [x] `Pages/Administration/Accounts/Index.cshtml` — AdminAccounts (already matched; no lede to remove)
- [x] `Pages/Administration/Roles/Index.cshtml` — AdminRoles
- [x] `Pages/Administration/Access/Index.cshtml` — AdminAccess
- [x] `Pages/Administration/Organizations/Index.cshtml` — AdminOrganizations
- [x] `Pages/Administration/Principals/Index.cshtml` — AdminPrincipals
- [x] `Pages/Administration/Configuration.cshtml` — AdminConfiguration
- [x] `Pages/Administration/Mailboxes.cshtml` — AdminMailboxes
- [x] `Pages/Administration/Automation/Index.cshtml` + `Activity.cshtml` — AdminAutomation
- [x] `Pages/Cases/Create.cshtml` — CreateCase
- [x] `Pages/Cases/Details.cshtml` + `Cases/Shared/_CaseSummary.cshtml` — Case
- [x] `Pages/Cases/Assessment/Index.cshtml` — Assessment

## Unbound sections
- [x] Deferred controls carry a Razor comment naming the capability ID and allocation
- [x] No `asp-for`, model binding or POST handler on any unbound section
- [x] No fabricated operator data anywhere

## Documentation
- [x] `docs/design/README.md` records the shell divergence (underline → left rail, `aria-current` + weight retained)
- [x] `docs/design/README.md` records Lucide over the prototype's PNG marks
- [x] `docs/design/README.md` states what unbound markup does and does not prove

## Verification
- [x] `dotnet build --configuration Release` clean, 0 warnings 0 errors
- [x] Architecture tests: 94 passed
- [x] Web integration tests: 42 + 135 passed after three copy fixes
- [ ] Browser accessibility suite green
- [ ] Local `DevelopmentOffline` run; visual proof of the rail and one screen per family
- [x] No diff under `Pegasus.Core`, `Pegasus.Infrastructure`, `workspaces/`, `corpus/`

## Progress notes

**Shell landmark, resolved by test.** The rail went through three element
choices before the accessibility suite was satisfied, and the suite was right
each time:

1. `<aside class="app-rail">` — axe `landmark-unique` on 9 routes. The design's
   `Notice` is also an `<aside>`, so any screen with a notice had two unnamed
   complementary landmarks.
2. `<div class="app-rail">` — axe `region` on 22 routes, worse. The brand, the
   nav and the signed-in controls were then outside every landmark.
3. `<header class="app-rail">` — the banner landmark, which is what the top bar
   already was. The rail is the page banner turned on its side.

**No inline styles.** The prototypes style almost entirely through inline
`style` attributes. The accessibility suite asserts that server markup never
carries one, because the production CSP discards them — a rule that had already
cost a ~1,900px blank band once. Every prototype inline style was therefore
translated into a named class in `site.css` rather than copied across.

**Three sentences kept against the design.** `Cases` empty state, the Inbox body
excerpt and "Not associated with a case." are each separately asserted by an
integration test. The design shortened or dropped all three; they are settled
operator copy and were restored.

**Rail counts are not wired.** The layout supports a count per route through
`ViewData["RailCounts"]`, and no page sets it, so no count renders. Supplying
them means a per-request query in the shell; FRD-12 forbids a stale zero
placeholder, so rendering nothing is correct until a real figure exists. Worth
a follow-up ticket.

## Verification (2026-08-18, rebased HEAD)

- [x] Browser accessibility suite green — 32 passed, 0 failed
- [x] `dotnet build --configuration Release` clean, 0 warnings 0 errors — rebased onto origin/dev
- [x] Architecture tests: 94 passed — on rebased HEAD
- [x] Core tests: 580 passed — on rebased HEAD
- [x] Web integration tests: 504 passed — on rebased HEAD
- [ ] Local `DevelopmentOffline` run; visual proof of the rail and one screen per family — deferred to verifying stage on merged main
- [x] No diff under `Pegasus.Core`, `Pegasus.Infrastructure`, `workspaces/`, `corpus/` — verified on rebased HEAD
- [x] Marks: 10 placed PNGs copied at 128×128 Lanczos (78 KB total), SHA-256 recorded in marks README and design authority
- [x] Open-questions: blocked item (mark files) ticked — resolved 2026-08-18

## Closeout — PLAT-001

- [x] PR merge verified (`gh pr view --json state,mergedAt`) — MERGED 2026-08-18T09:23:05Z
- [x] proof.md finalised (PR URL + merge date appended)
- [x] Moved to final stage (done, 2026-08-18T09:36:55Z)
- [ ] Outcome recorded in ticket body (PR link, follow-ups)
- [ ] cd out of worktree; `git worktree remove .worktrees/claude-design-ui`
- [ ] `git branch -d task/claude-design-ui` (`-D` if squash/rebase-merged)
- [ ] `git fetch --prune` + `git worktree prune`
- [ ] `take_ticket action: "release"`

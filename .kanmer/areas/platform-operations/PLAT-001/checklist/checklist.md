# Checklist — Claude Design UI implementation

## Shell
- [ ] `.app-rail` block added to `wwwroot/css/site.css` (grid, rail-link, aria-current, counts, narrow-viewport collapse)
- [ ] `Pages/Shared/_Layout.cshtml` rewritten to the left rail
- [ ] Skip link, `_LucideSprite`, `inboxEnabled` gate, `CurrentWhen`, Administrator-only item, auth branch and `TempData["Confirmation"]` all preserved
- [ ] `_LayoutAuth` / `_LayoutExternal` confirmed unchanged and still correct

## Screens
- [ ] `Pages/Index.cshtml` — Dashboard
- [ ] `Pages/Upload.cshtml` — Upload
- [ ] `Pages/Uploads/Request.cshtml` — UploadLink
- [ ] `Pages/Account/PasswordChange.cshtml` — ChangePassword
- [ ] `Pages/Triage/Index.cshtml` — Queues
- [ ] `Pages/Cases/Index.cshtml` — Cases
- [ ] `Pages/Search/Index.cshtml` — aligned with Cases
- [ ] `Pages/Mail/Index.cshtml` — Inbox
- [ ] `Pages/Mail/Message.cshtml` — InboxMessage
- [ ] `Pages/Operations/Index.cshtml` — Operations
- [ ] `Pages/Administration/Index.cshtml` — Administration
- [ ] `Pages/Administration/Accounts/Index.cshtml` — AdminAccounts
- [ ] `Pages/Administration/Roles/Index.cshtml` — AdminRoles
- [ ] `Pages/Administration/Access/Index.cshtml` — AdminAccess
- [ ] `Pages/Administration/Organizations/Index.cshtml` — AdminOrganizations
- [ ] `Pages/Administration/Principals/Index.cshtml` — AdminPrincipals
- [ ] `Pages/Administration/Configuration.cshtml` — AdminConfiguration
- [ ] `Pages/Administration/Mailboxes.cshtml` — AdminMailboxes
- [ ] `Pages/Administration/Automation/Index.cshtml` + `Activity.cshtml` — AdminAutomation
- [ ] `Pages/Cases/Create.cshtml` — CreateCase
- [ ] `Pages/Cases/Details.cshtml` + four `Cases/Shared/_Case*.cshtml` — Case
- [ ] `Pages/Cases/Assessment/Index.cshtml` — Assessment

## Unbound sections
- [ ] Every deferred section carries a Razor comment naming its capability ID and allocation
- [ ] No `asp-for`, model binding or POST handler on any unbound section
- [ ] No fabricated operator data anywhere — inputs empty, figures em-dashed or `EmptyState`

## Documentation
- [ ] `docs/design/README.md` records the shell divergence (underline → left rail, `aria-current` + weight retained)
- [ ] `docs/design/README.md` records Lucide over the prototype's PNG marks
- [ ] `docs/design/README.md` states the unbound sections prove nothing

## Verification
- [ ] `dotnet restore`
- [ ] `dotnet build --configuration Release` clean, no new warnings
- [ ] Web tests green; shell assertions updated rather than deleted
- [ ] Local `DevelopmentOffline` run; visual proof of the rail and one screen per family
- [ ] No diff under `Pegasus.Core`, `Pegasus.Infrastructure`, `workspaces/`, `corpus/`

## Progress notes

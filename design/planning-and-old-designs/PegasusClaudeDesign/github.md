repo: collisionengineers/pegasus
branch: main

## Last sync
date: 2026-08-16T17:06:19Z

### Updated in this project
- Rebuilt every template screen in screens/ from the live Pegasus.Web Razor pages
- Added the Engineer assessment workbench (Cases/Assessment) with estimate, valuation and findings
- Added Operations, Automation administration, Inbox message, New case and external upload link screens
- Folded EVA-derived features in (live estimate totals, guide valuation evidence, salvage detail, decision ratios, Experian history check)

## Screen map
| screen | repo files |
| --- | --- |
| screens/Dashboard.html | src/Pegasus.Web/Pages/Index.cshtml |
| screens/Inbox.html | src/Pegasus.Web/Pages/Mail/Index.cshtml |
| screens/InboxMessage.html | src/Pegasus.Web/Pages/Mail/Message.cshtml |
| screens/Upload.html | src/Pegasus.Web/Pages/Upload.cshtml, src/Pegasus.Web/Pages/Operations/Index.cshtml |
| screens/UploadLink.html | src/Pegasus.Web/Pages/Uploads/Request.cshtml |
| screens/Queues.html | src/Pegasus.Web/Pages/Triage/Index.cshtml |
| screens/Cases.html | src/Pegasus.Web/Pages/Cases/Index.cshtml, src/Pegasus.Web/Pages/Search/Index.cshtml |
| screens/Case.html | src/Pegasus.Web/Pages/Cases/Details.cshtml, src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml, _CaseDocuments.cshtml, _CaseHistory.cshtml, _CaseWorkflow.cshtml |
| screens/Assessment.html | src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml |
| screens/CreateCase.html | src/Pegasus.Web/Pages/Cases/Create.cshtml |
| screens/Operations.html | src/Pegasus.Web/Pages/Operations/Index.cshtml |
| screens/Administration.html | src/Pegasus.Web/Pages/Administration/Index.cshtml |
| screens/AdminAccounts.html | src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml |
| screens/AdminRoles.html | src/Pegasus.Web/Pages/Administration/Roles/Index.cshtml |
| screens/AdminAccess.html | src/Pegasus.Web/Pages/Administration/Access/Index.cshtml |
| screens/AdminOrganizations.html | src/Pegasus.Web/Pages/Administration/Organizations/Index.cshtml |
| screens/AdminPrincipals.html | src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml |
| screens/AdminConfiguration.html | src/Pegasus.Web/Pages/Administration/Configuration.cshtml |
| screens/AdminMailboxes.html | src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml |
| screens/AdminAutomation.html | src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml, Activity.cshtml |
| screens/ChangePassword.html | src/Pegasus.Web/Pages/Account/PasswordChange.cshtml |

---
id: PLAT-039
type: ticket
title: Refresh the Box access token instead of minting it once per process
status: verifying
area: platform-operations
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-23T12:13:46.823Z'
  review: '2026-08-23T14:48:33.792Z'
  verifying: '2026-08-23T14:48:38.369Z'
taken_at: '2026-08-23T12:11:34.164Z'
branch: task/qdos26012-regressions
worktree: ../pegasus-worktrees/qdos26012-regressions
labels:
  - qdos26012
  - production-defect
  - found-during-qa
  - box
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-23T12:09:36.762Z'
updated: '2026-08-24T16:54:32.250Z'
---

## Every Box read from the Web app fails one hour after the container starts

Captured from the production console stream (Log Analytics, workspace
`pegasus-prod-logs-252ow37gij`), 2026-08-23 11:46:27Z, reproducing the
operator's export of `ap.QDOS26012`:

```
fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware[1]
System.Net.Http.HttpRequestException: Box returned 401; response length 0.
   at BoxContentClient.ReadSuccessJsonAsync(...)  BoxCaseCustody.cs:line 472
   at BoxContentClient.CollectChildrenAsync(...)  BoxCaseCustody.cs:line 195
   at BoxContentClient.FindChildAsync(...)        BoxCaseCustody.cs:line 215
   at BoxDocumentContentStore.ResolveCaseFolderAsync(...)  line 148
   at BoxDocumentContentStore.OpenReadVersionAsync(...)    line 83
   at EvaHandoffStore.LoadEligibleImagesAsync(...)         line 796
   at EvaHandoffStore.IExportCaseBundle.ExecuteAsync(...)  line 712
   at ExportModel.OnGetAsync(...)                          line 45
```

It fails resolving the case folder — before any file is read.

## Root cause — confirmed against the SDK source

`BoxJwtAuthorizationHeaderProvider` (`BoxCaseCustody.cs:116`) is a **singleton**
holding one `BoxJwtAuth` in a `Lazy`, and asks it for a header on every call:

```csharp
var header = await auth.RetrieveAuthorizationHeaderAsync(session)...
```

`Box.Sdk.Gen` 1.12.0's `BoxJwtAuth.RetrieveTokenAsync` is:

```csharp
AccessToken? oldToken = await this.TokenStorage.GetAsync();
if (oldToken == null) {
    return await this.RefreshTokenAsync(networkSession: networkSession);
}
return oldToken;                 // <-- no expiry check, ever
```

**It returns any cached token unconditionally.** The SDK never re-mints on
age; its own `NetworkClient` handles a 401 by calling `RefreshTokenAsync` and
retrying. Pegasus takes only the header and calls Box with its own
`HttpClient`, so that retry path is never reached.

A Box JWT access token lives 60 minutes. The Web container therefore mints one
token at first Box use and reuses it **forever** — every Box call fails with
401 from an hour after start until the replica restarts.

## Why it looked intermittent

| Fact | Evidence |
| --- | --- |
| One Web replica, up since 01:34:54Z, never restarted | `az containerapp replica list`; one `ContainerId` spanning 03:40Z–11:55Z |
| Box read worked at ~01:40Z on that replica ([[CASE-019]] proof) | the QDOS26011 archive downloaded and hash-verified |
| Six 401s, all Web, first at 10:36Z | `ContainerAppConsoleLogs \| where Log has 'Box returned'` |
| Worker's Box **writes** still succeed | QDOS26012 custody confirmed 10:58:46Z, folder `411262029174` |
| Web and Worker hold identical credentials | both resolve `box-config-json/285b5c83…` and `box-client-secret/34b9ca84…` |

The Worker escapes only because a Function host recycles often enough that its
cached token is usually young. The defect is in shared Infrastructure, so the
Worker carries it latently too.

## Blast radius

Every Box **read** from the Web app: the case export ([[CASE-019]]), the
Evidence tab photographs, and case-document downloads. This is the second
fault behind the broken images — see [[DOCS-010]] for the first.

## Second, smaller defect in the same path

`HttpRequestException` is not in `ExportModel.OnGetAsync`'s catch list
(`ArgumentException | InvalidOperationException | InvalidDataException |
IOException | UnauthorizedAccessException`), so the operator gets the hard
"We could not complete that request" error page instead of the case page with
"The case could not be exported."

## Diagnosis note worth keeping

Application Insights was over quota again ([[PLAT-036]]), but container console
logs are **also** routed to Log Analytics workspace
`pegasus-prod-logs-252ow37gij` by diagnostic setting `pegasus-prod-aca-diagnostics`,
table `ContainerAppConsoleLogs` (resource-specific, no `_CL` suffix). That is
queryable, retains history, and is unaffected by the App Insights cap — far
better than the `az containerapp logs show` polling loop [[DOCS-010]] used.

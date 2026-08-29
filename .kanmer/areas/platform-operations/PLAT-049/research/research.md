# PLAT-049 research — Operations AI Job List, Service health, Send Unidentified to AI

Taken at `origin/dev` = `b92cb9a7` (merged into the lane branch before any
work; clean merge, no conflicts).

## Premises verified by reading the merged tree (not assumed)

| Premise | Verified how | Result |
| --- | --- | --- |
| `Pages/Operations/Index.cshtml` is restyled onto the design system | read the file | TRUE — header, partial-data notice, Service health, Attention required, Active upload links |
| `Core/AiWork/AiJobs.cs` carries the ledger | read the file | TRUE — `AiJobKind` (Estimate, UnidentifiedResolution, QueryResponse, UnidentifiedQueuePass), `AiJobState`, `AiJobCounts`, `IAiJobQueries`, `ICreateAiJob`, `ICancelAiJob`, `IConfirmAiJob` |
| Those ports are composed in production | `src/Pegasus.Infrastructure/DependencyInjection.cs:335-341` | TRUE — all four registered unconditionally |
| `Core/Operations/ServiceHealth.cs` exists | read the file | TRUE — `GetServiceHealth`, `ServiceHealthRow(Area, Service, State, LatestEvidenceAtUtc, Dependency, RetryTarget?)` |
| `GetServiceHealth` is registered only with the Automation composition | `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs:34` | TRUE — hence the page's optional constructor parameter stays |
| `OperatorLabels` already carries four `ServiceHealth*` helpers and `RequestOperationState` | read the file | TRUE (lines ~505-576) |
| No `AiJobKind` / `AiJobState` label map exists anywhere in Web | grep over `src/Pegasus.Web/Presentation` and `Pages` | TRUE — nothing exists; this ticket writes the first and only one |
| `ICancelAiJob` and `IConfirmAiJob` have a production caller | grep over `src/` | FALSE — registered but unreachable. `ICreateAiJob` has one caller (`Pages/Cases/Assessment/Index.cshtml.cs:442`, `AiJobKind.Estimate`). This ticket gives Cancel and Confirm their first production callers |
| `EfAiJobStore` maps every record through `AiJobPolicy.EffectiveState` | `EfAiJobStore.cs:350` | TRUE — a lapsed lease reads `Queued` and a stale `Queued` reads `Expired` without touching the row, so the page must filter on the mapped state, never on the persisted one |
| `IUnidentifiedStore.ListQueueAsync(null, ct)` returns open items oldest-first with reference | `UnidentifiedContracts.cs:303`, registered `DependencyInjection.cs:116` | TRUE — this is the U-reference source for Send Unidentified to AI |
| A Core port lists EVA handoffs (Case, Route, Engineer, State, Result) | read `Core/Eva/EvaApiContracts.cs` in full | **FALSE** — see gaps |
| `RequestOperationProjection` carries a recipient or an item identity | read `Core/Operations/RequestOperations.cs` | **FALSE** — see gaps |
| `ServiceHealthRow` carries a view target | read `Core/Operations/ServiceHealth.cs` | **FALSE** — see gaps |
| Baseline build is green before any edit | `dotnet build ./Pegasus.slnx --configuration Release` | TRUE — Build succeeded, 0 warnings, 0 errors |

## The binding contract, and one conflict inside it

Three documents describe the AI Job List. Two agree and one does not.

- **FRD-11 § AI Job List** (a `refs` governing document of this ticket):
  the panel shows *every non-terminal job and the terminal jobs of the current
  day*; the action is `Review estimate` / `Open query` / `Review` for a
  `Draft ready` job, `Complete job` for a `Draft ready` **Query response or
  Unidentified-queue pass**, `Cancel` (reason required) for **any non-terminal
  job**, otherwise nothing. And: "`Send Unidentified to AI` creates an
  **Unidentified-resolution** job for a chosen U reference."
- **The PLAT-049 ticket body** agrees: "actions Review estimate / Open query /
  Review / Complete job / Cancel", "`Send Unidentified to AI` creating an
  **UnidentifiedResolution** job".
- **The lane brief handed to this session** says instead that the button
  "creates an Unidentified-queue pass job".

**Resolution: FRD-11 and the ticket body win, and the button creates an
`UnidentifiedResolution` job.** Reasons, in order:

1. EPIC-011 **D5** says scheduled queue passes "are created by external crons
   through the Automation Actor". A Pegasus button creating a queue pass is
   exactly the thing D5 assigns elsewhere.
2. `AiJobPolicy.RequireCreator` encodes the same split: the Automation Actor
   may create *only* `UnidentifiedQueuePass`.
3. FRD-11's kinds table gives `Unidentified resolution` the "Started from"
   value `Operations "Send Unidentified to AI" for one U reference` — the
   button is that kind's named origin.

The lane brief's other instruction — "creates the job through the existing
seam, do not write a second dispatch path" — is honoured either way: the page
calls `ICreateAiJob`, which itself consults `ISendToAiControl`.

## Core gaps found — none of them fixable inside this lane's boundary

The lane owns `Pages/Operations/**`, `tests/.../OperationsWebTests.cs` and its
own nested class in `OperatorLabels.cs`. It must not touch `Core/AiWork/**` or
`Core/Operations/ServiceHealth.cs`. Each gap below is stated precisely so the
orchestrator can place it.

1. **EVA handoffs panel cannot be built.** `IEvaSubmissionQueries` offers
   `GetLatestAsync(caseId)`, `GetRecentFailuresAsync(since, max)` and
   `GetActivityAsync()`. None lists handoffs, and `EvaSubmissionFailure`
   carries `CaseId` only — no case reference, no engineer, no route. The
   contract's columns (Case, Route, Engineer, State, Result) need a new Core
   read model and port plus an EF adapter and registration. That is
   `Core/Eva/**` + `Infrastructure/Persistence/**`, outside this lane.
2. **Service health has no `View` target.** §1.11 asks for "Retry/View".
   `ServiceHealthRow` carries `RetryTarget` and nothing else. Inventing an
   area-to-route map in the page would be a second list of routes owned by
   nobody, and the plausible destinations (`/Administration/Mailboxes`,
   `/Administration/Automation`) are Administrator-only while `/Operations`
   is open to Engineer and User — the link would 403 for most readers.
3. **Attention required has no `Item` column.** §1.11 lists Case, Work,
   **Item**, Attempts, Failure, Retry. `RequestOperationProjection` has no
   field naming the thing the work is about.
4. **Active upload links has no `Recipient` column.** §1.11 lists it; the
   projection has `CaseReference`/`PrincipalCode` and no recipient.
5. **A `QueryResponse` job carries no message identity.** Its
   `SubjectKind` is `Case` (`AiJobPolicy.SubjectKindFor`) and its
   `SubjectReference` is the case reference, so FRD-11's `Open query`
   ("opens the message") can only open the Case the job names.

## Reuse decisions

- Job creation, cancellation and confirmation: `ICreateAiJob`,
  `ICancelAiJob`, `IConfirmAiJob` — no new use case, no second dispatch path.
- The reason-carrying row action: the `<details><summary class="btn">` +
  `form.row-confirm` shape this same file already uses for **Withdraw link**.
  The shared `Shared/_ReasonDialog` partial is the other candidate and is
  rejected: it renders one full dialog per invocation, so a per-row control
  would put N dialogs in the DOM.
- The state chip: `Shared/_StatusChip`, with its own documented
  `ViewData["StatusTone"]` override (precedent:
  `Pages/Cases/Eva/Send.cshtml:58`). Editing the partial's tone map is not an
  option — `Pages/Shared/*` belongs to the shell lane, and UIIMP-009 /
  TICK-223 are in flight over it.
- Europe/London conversion: `OperatorLabels.OfficeDate` / `OfficeTime`, the
  single existing owner. No fifth `FindSystemTimeZoneById` call is added
  (PLAT-060 already counts four).
- Retry: the existing `RetryExternal` handler and `RetryExternalWorkCommand`.

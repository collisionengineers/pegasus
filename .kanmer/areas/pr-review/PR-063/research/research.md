# Research — PR-063: default-page fidelity

## Question

Which Test UI `default` prototypes fail to represent a valid defining normal branch of their current Razor owner, and what correction and evidence are required before [[UIIMP-002]] can pass review?

## Verified findings

Read-only comparison covered all 39 visual inventory entries against their current `.cshtml`, PageModel branches, shared layouts and partials on PR #556 at `63ce690`. The catalogue validator passes route/link coverage but does not prove branch fidelity. `git diff --check origin/dev...HEAD` currently fails on 45 HTML files because they end with an extra blank line.

### Default-state mapping

| Razor/default prototypes | Valid branch and required correction |
|---|---|
| Account AccessDenied, PasswordChange, SignIn | AccessDenied is exact. PasswordChange and SignIn represent valid ordinary branches; retain their defining forms and exact live field/copy vocabulary. |
| Administration landing, Access, Accounts, Account Edit | Valid current branches. Preserve the eight-card uncomposed landing and the populated/empty account scenarios; static forms remain visual evidence only. |
| Administration Automation, Activity | Automation represents a valid composed branch; Activity represents the valid empty branch. Record those branches explicitly so their cross-page scenario difference is not mistaken for one runtime session. |
| Administration Configuration | Correct impossible `Policy: Default` to the source-owned default `case-workflow`. |
| Administration Mail Categories | Valid empty/add branch; populated editing is a separate state, not the default currently selected. |
| Administration Mailboxes | Correct impossible `Ready` polling text to an actual `PollStatusFor` result such as `Not yet polled.`. |
| Administration Organizations, Organization Edit | Index is a valid empty/create branch. Edit must use an evidence-backed loaded organization name because Razor emits `Manage {Organization.Name}`, never generic `Manage organization`. |
| Administration Principals, Principal Create, Principal Replace, Roles | Principals is a valid no-organizations branch. Create and Roles currently choose exceptional empty branches and omit the defining create/assignment interactions; default must select populated normal branches and exceptional alternatives must be separate states if retained. Replace must use a loaded evidence-backed principal and organization and expose the replacement form. |
| Dashboard | Current prototype invents/relabels metrics. Default must reproduce Active cases (Not ready, Review, Held), E-mail (Received today, Unidentified, Blocked intake), and the five Today/week metrics. |
| Queues | Current prototype combines mutually exclusive Triage and Unidentified content. Default must select one real PageModel branch, retain the five queue tabs, and show that branch's exact filters/table. |
| Inbox | Default must retain Inbox/Sent/Deleted folder tabs, applicable mailbox tabs, show-view submit, clear-search path and current row metadata. |
| Inbox message | Default must select one `ActiveSection`; it cannot show Message, Attachments and Linked case together. Hidden correction dialog must not render visibly. |
| Case assessment | Default top actions must be Open in Glass's, Open in Audatex, Import estimate and Back to case; remove non-live Preview suggestions/Send to Claude controls and keep conditional send content in its live branch. |
| Case create | Preserve the normal create branch and restore the defining instruction-draft fields/provenance controls supplied by `_InstructionDraftFields`. |
| Case details | Keep one valid overview/review branch; respect action gates and exact labels (`Save case data`, `Transition to report preparation`), restore defining summary/task controls. |
| Cases | Successful result branch is valid; restore Received from/to and avoid a contradictory pre-filled advanced state. |
| Connector authorize, Error, Status code | Authorize must keep exact client wording. Error and status-code prototypes must state the conditional branch they select instead of presenting invented unconditional combinations. |
| Vehicle images list/detail | List is a valid populated branch. Detail must include Registered and the Images section when choosing an image-bearing branch, or explicitly select the no-images branch. |
| Received details | Select one valid blocked-intake branch; keep its exact field-card/provenance and association/lease structure instead of replacing it with invented summary tables. |
| Inbox/Operations | Operations default must keep exact table columns/actions, accepted/last-activity fields and collapsed withdrawal reason form; do not combine the `LimitReached` sentence with the ordinary branch. |
| Triage detail | Select one valid open/assigned branch and use exact actions (`Reassign to me`, `Unassign`, `Await information`, `Cancel Triage`, `Record and link exact response`). |
| Unidentified detail | Open branch is valid; restore retained source receipt and `Open the received file` where that branch has a source receipt. |
| Upload | Default GET is valid; restore file count/size guidance and enhanced choose-files control. |
| Upload group status | Current prototype combines incompatible open-decision and completed-outcome branches. Select one valid branch and render its exact Files/outcome or decision forms. |
| Upload status | Select a valid outcome branch and restore received time, duplicate/thumbnail conditions, refresh link and the defining `_UploadOutcome` content. |
| External upload request | Valid upload-policy branch; restore max-size guidance and choose-file control. |

### Shared-shell boundary

Static files intentionally do not execute handlers, antiforgery, authorization, concurrency or business policy. That is an evidence boundary, not permission to change rendered content. The authenticated prototypes must preserve the live rail/user controls and active navigation semantics while adding only a clearly identified Test UI marker. Auth and external shells must likewise retain their current layout structure.

## Implications

- Correct every mapped default, not only the two examples in the ticket body.
- Add an explicit branch description to each visual inventory state and validate its presence, making the claimed Razor branch reviewable beside the route mapping.
- Keep scenario fixtures evidence-safe and already established in repository tests/source; do not claim they are live data.
- Update [[UIIMP-002]] checklist/report to withdraw the false `git diff --check` claim and record the corrected rerun results.
- Deployment is not required; the catalogue is excluded from runtime/release inputs.

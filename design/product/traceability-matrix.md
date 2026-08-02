
# Complete feature traceability matrix

Status: **Planned exhaustive source trace for the selected Operations-first `0.1.0-alpha.1` direction.** Exactly one row appears for every ID in the canonical [capability inventory](../../docs/capabilities.md) (229 total). Each capability's durable-outcome wording lives only in that inventory — rows here carry the ID, its copied `Horizon / target`, and the design-owned actor/rule/surface columns. Intended owner/caller is Planned unless stated current elsewhere; it is not runtime-call evidence.

## Selected shell and rejected directions

Operations-first was selected on 2026-07-27 because office-wide receiving,
requests, Triage, due work, queries, and stale-work visibility must precede deep
case navigation. Its count tiles link to the exact filtered queue, with
day/week ranges and no stale zero placeholders.

Worklist-first was rejected because one named queue weakened whole-office
day/week scanning. Case-first was rejected because search/deep context could not
be the earliest shell and weakened queue visibility. Their comparison rationale and retained [Operations-first](../references/mockups/candidate-a-operations-first.png),
[Worklist-first](../references/mockups/candidate-b-worklist-first.png), and
[Case-first](../references/mockups/candidate-c-case-first.png) rasters are selection
evidence only; they are not runtime assets or pixel-level design authority.

Current Development runtime divergences are recorded in one place — the
[design index divergence notes](../README.md) — rather than restated here. The
approved amber/navy state tokens are implemented in `site.css`; the design
index owns their exact values.

## Development evidence qualifiers

The canonical inventory retains exact `Now / 0.1.0-alpha.1` allocations for
`OPS-22`, `EVAL-01` through `EVAL-05`, and `MAIL-20`, although current user
direction assigns their Development/local evaluator outcomes to separate
delivery. QDOS therefore has no route, caller, report campaign, or acceptance
checkpoint for those rows; the evaluator allocation boundary in the capability inventory records that delivery/caller boundary
without changing their exact allocation. `OPS-10`, `MAIL-21`, and `MAIL-22`
retain QDOS development evidence qualifiers without creating a local evaluator
surface.

| ID | Horizon / target | Role / state | Intended owner / caller | Negative rule | UI destination |
|---|---|---|---|---|---|
| OPS-10 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-22 | `Now / 0.1.0-alpha.1` | Separate evaluation delivery | Separately owned prerequisite / no QDOS caller | No QDOS route, command, report campaign or acceptance checkpoint | No QDOS UI |
| EVAL-01 | `Now / 0.1.0-alpha.1` | Separate evaluation delivery | Separately owned prerequisite / no QDOS caller | No QDOS route, command, report campaign or acceptance checkpoint | No QDOS UI |
| EVAL-02 | `Now / 0.1.0-alpha.1` | Separate evaluation delivery | Separately owned prerequisite / no QDOS caller | No QDOS route, command, report campaign or acceptance checkpoint | No QDOS UI |
| EVAL-03 | `Now / 0.1.0-alpha.1` | Separate evaluation delivery | Separately owned prerequisite / no QDOS caller | No QDOS route, command, report campaign or acceptance checkpoint | No QDOS UI |
| EVAL-04 | `Now / 0.1.0-alpha.1` | Separate evaluation delivery | Separately owned prerequisite / no QDOS caller | No QDOS route, command, report campaign or acceptance checkpoint | No QDOS UI |
| EVAL-05 | `Now / 0.1.0-alpha.1` | Separate evaluation delivery | Separately owned prerequisite / no QDOS caller | No QDOS route, command, report campaign or acceptance checkpoint | No QDOS UI |
| MAIL-20 | `Now / 0.1.0-alpha.1` | Separate evaluation delivery | Separately owned prerequisite / no QDOS caller | No QDOS route, command, report campaign or acceptance checkpoint | No QDOS UI |
| MAIL-21 | `Now / 0.1.0-alpha.1` | Production mail classification foundation | Core mail policy / production intake and Graph replay/live callers | No local evaluator workbench is required or implied | Non-UI policy used by Intake |
| MAIL-22 | `Now / 0.1.0-alpha.1` | Production mail taxonomy | Core mail policy / production intake and Graph replay/live callers | No local evaluator workbench is required or implied | Non-UI policy used by Intake |
| ACC-01 | `Now / 0.1.0-alpha.1` | Administrator or protected staff access | Identity/administration / authenticated Web route | No credential/cloud/permanent-delete UI | Administration or protected shell |
| ACC-02 | `Now / 0.1.0-alpha.1` | Administrator or protected staff access | Identity/administration / authenticated Web route | No credential/cloud/permanent-delete UI | Administration or protected shell |
| ACC-03 | `Now / 0.1.0-alpha.1` | Administrator or protected staff access | Identity/administration / authenticated Web route | No credential/cloud/permanent-delete UI | Administration or protected shell |
| ACC-04 | `Now / 0.1.0-alpha.1` | Administrator or protected staff access | Identity/administration / authenticated Web route | No credential/cloud/permanent-delete UI | Administration or protected shell |
| ACC-05 | `Now / 0.1.0-alpha.1` | Administrator or protected staff access | Identity/administration / authenticated Web route | No credential/cloud/permanent-delete UI | Administration or protected shell |
| ACC-06 | `Now / 0.1.0-alpha.1` | Administrator or protected staff access | Identity/administration / authenticated Web route | No credential/cloud/permanent-delete UI | Administration or protected shell |
| ACC-07 | `Now / 0.1.0-alpha.1` | Administrator or protected staff access | Identity/administration / authenticated Web route | No credential/cloud/permanent-delete UI | Administration or protected shell |
| ACC-08 | `Now / 0.1.0-alpha.1` | Administrator or protected staff access | Identity/administration / authenticated Web route | No credential/cloud/permanent-delete UI | Administration or protected shell |
| ACC-09 | `Now / 0.1.0-alpha.1` | Staff/automation business-action attribution | Permanent action-history policy / each authenticated business mutation caller | No routine views, refresh, polling, retries, leases/heartbeats or adapter mechanics | Intake, Triage, Case and Administration business-record flows |
| ACC-10 | `Now / 0.1.0-alpha.1` | Security boundary | Authentication/security owner / authorised security boundary | No operational log content in the staff application | Non-UI; at most authorised security boundary if separately authorised |
| ACC-11 | `Now / 0.1.0-alpha.1` | Operational telemetry | Telemetry owner / Web and Worker instrumentation | No telemetry, worker or adapter mechanics in the staff application | Non-UI |
| INT-01 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-02 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-03 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-08 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-09 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-10 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-11 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-12 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-13 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-17 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-18 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-19 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-20 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-21 | `Now / 0.1.0-alpha.1` | Evidence / non-production | Separately supplied reviewed evidence / QDOS consumes accepted evidence | No QDOS evaluator UI, local review workflow, or report campaign | No QDOS UI; evaluation artifact is separately owned |
| INT-22 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-23 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-24 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-25 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-26 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-27 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-29 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| INT-30 | `Now / 0.1.0-alpha.1` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| MAIL-14 | `Now / 0.1.0-alpha.1` | Staff / exact report evidence | Report-evidence policy / Case Web route | No Triage use and no general mail composition | Case report-evidence flow only |
| MAIL-15 | `Now / 0.1.0-alpha.1` | Staff / exact report evidence | Report-evidence policy / Case Web route | No Triage use and link changes require reason | Case report-evidence flow only |
| MAIL-16 | `Now / 0.1.0-alpha.1` | Staff / exact report evidence | Report-evidence policy / Case Web route | No Triage use and ambiguity remains visible | Case report-evidence flow only |
| MAIL-18 | `Now / 0.1.0-alpha.1` | Staff / manual chaser | Case chasing policy / Case Web route | No Triage chaser and no automated send | Case manual-chaser flow only |
| TRI-01 | `Now / 0.1.0-alpha.1` | Staff / pre-case Triage | Triage workflow / Triage Web route | Distinct inbox label/pre-case record; never a case state; no due/chaser | Triage flow |
| TRI-02 | `Now / 0.1.0-alpha.1` | Staff / pre-case Triage | Triage workflow / Triage Web route | Distinct inbox label/pre-case record; never a case state; no due/chaser | Triage flow |
| TRI-03 | `Now / 0.1.0-alpha.1` | Staff / pre-case Triage | Triage workflow / Triage Web route | Distinct inbox label/pre-case record; never a case state; no due/chaser | Triage flow |
| TRI-04 | `Now / 0.1.0-alpha.1` | Staff / pre-case Triage | Triage workflow / Triage Web route | Distinct inbox label/pre-case record; never a case state; no due/chaser | Triage flow |
| TRI-05 | `Now / 0.1.0-alpha.1` | Staff / pre-case Triage | Triage workflow / Triage Web route | Distinct inbox label/pre-case record; never a case state; no due/chaser | Triage flow |
| TRI-06 | `Now / 0.1.0-alpha.1` | Staff / pre-case Triage | Triage workflow / Triage Web route | Distinct inbox label/pre-case record; never a case state; no due/chaser | Triage flow |
| TRI-07 | `Now / 0.1.0-alpha.1` | Staff / pre-case Triage | Triage workflow / Triage Web route | Distinct inbox label/pre-case record; never a case state; no due/chaser | Triage flow |
| TRI-08 | `Now / 0.1.0-alpha.1` | Staff / pre-case Triage | Triage workflow / Triage Web route | Distinct inbox label/pre-case record; never a case state; no due/chaser | Triage flow |
| TRI-09 | `Now / 0.1.0-alpha.1` | Staff / pre-case Triage | Triage workflow / Triage Web route | Distinct inbox label/pre-case record; never a case state; no due/chaser | Triage flow |
| CASE-01 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-02 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-03 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-04 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-07 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-08 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-09 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-10 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-11 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-12 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-13 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | Provider policy may define evidence but cannot remove either judgement | Case flow |
| CASE-14 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No direct named-Engineer assignment; EVA retains assignment through alpha | Case flow |
| CASE-15 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No direct named-Engineer assignment; EVA retains assignment through alpha | Case flow |
| CASE-16 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-17 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-18 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-19 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-20 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-21 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-24 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-25 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-26 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-27 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-28 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-29 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| CASE-30 | `Now / 0.1.0-alpha.1` | Staff / lifecycle | Case identity/lifecycle / Case Web route | No mutable allocated principal/reference or deletion | Case flow |
| UI-01 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-02 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-03 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-04 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-05 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-06 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-07 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-08 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-09 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-11 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| UI-13 | `Now / 0.1.0-alpha.1` | Authorised staff / accessible UI | Operational query/page / authorised Web route | No colour-only state or mobile staff product | Shared `0.1.0-alpha.1` shell/flows |
| DOC-01 | `Now / 0.1.0-alpha.1` | Staff / allocated Case document work | Document custody / Case Web route | Case/PO allocation precedes its immutable-reference-named folder; Box failure retains the Case as `Not ready` with staff-initiated idempotent recovery evidence; no background retry, physical deletion, or closed-case changes; actual happy-path and target-fence proof required | Case document flow |
| DOC-02 | `Now / 0.1.0-alpha.1` | Staff / allocated Case document work | Document custody / Case Web route | Day-one accepted Case custody; Blob is temporary staging only; no physical deletion or closed-case changes | Case document flow |
| DOC-03 | `Now / 0.1.0-alpha.1` | Staff / case document work | Document custody / Case Web route | No physical deletion or closed-case changes | Case document flow |
| DOC-04 | `Now / 0.1.0-alpha.1` | Staff / case document work | Document custody / Case Web route | No physical deletion or closed-case changes | Case document flow |
| DOC-05 | `Now / 0.1.0-alpha.1` | Staff / case document work | Document custody / Case Web route | No physical deletion or closed-case changes | Case document flow |
| DOC-06 | `Now / 0.1.0-alpha.1` | Staff / bounded in-house upload request | Request-scoped upload policy / authenticated staff creator and isolated public upload route | No external account, cross-request access, case/reference/request-history disclosure, or upload after expiry/revocation; success proves request-local custody only, not Box custody or delivery | Case initiation; bound upload fields and immediate request-local result only |
| DOC-07 | `Now / 0.1.0-alpha.1` | Staff / case document work | Document custody / Case Web route | No physical deletion or closed-case changes | Case document flow |
| DOC-08 | `Now / 0.1.0-alpha.1` | Private processing custody | Worker staging boundary / Worker caller | No staff surface or downloadable staging area | Non-UI |
| EXT-01 | `Now / 0.1.0-alpha.1` | Staff / case enrichment | Case enrichment/effect / Case Web route | No invented external result or unapproved mutation | Case enrichment/evidence flow |
| EXT-02 | `Now / 0.1.0-alpha.1` | Staff / case enrichment | Case enrichment/effect / Case Web route | No invented external result or unapproved mutation | Case enrichment/evidence flow |
| EXT-03 | `Now / 0.1.0-alpha.1` | Staff / case enrichment | Case enrichment/effect / Case Web route | No EVA call or Pegasus-owned image selection/presentation order | Case enrichment/evidence flow |
| EXT-14 | `Now / 0.1.0-alpha.1` | Staff / case enrichment | Case enrichment/effect / Case Web route | No invented external result or unapproved mutation | Case enrichment/evidence flow |
| EXT-18 | `Now / 0.1.0-alpha.1` | Staff / case enrichment | Case enrichment/effect / Case Web route | No invented external result or unapproved mutation | Case enrichment/evidence flow |
| MCP-01 | `Now / 0.1.0-alpha.1` | Automation Actor / non-browser API | Shared Core use case / MCP caller | No ordinary-staff MCP access, browser UI, or security/configuration route | Non-UI |
| MCP-02 | `Now / 0.1.0-alpha.1` | Automation Actor / non-browser API | Shared Core use case / MCP caller | No ordinary-staff MCP access, browser UI, or security/configuration route | Non-UI |
| MCP-03 | `Now / 0.1.0-alpha.1` | Automation Actor / non-browser API | Shared Core use case / MCP caller | No ordinary-staff MCP access, browser UI, or security/configuration route | Non-UI |
| MCP-04 | `Now / 0.1.0-alpha.1` | Automation Actor / non-browser API | Shared Core use case / MCP caller | No ordinary-staff MCP access, browser UI, or security/configuration route | Non-UI |
| OPS-01 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-02 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-03 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-04 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-05 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-06 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-07 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-08 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-09 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-11 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-13 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-14 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-20 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-24 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| DATA-01 | `Now / 0.1.0-alpha.1` | Data/persistence | Core data policy / persistence caller | No UI surface | Non-UI |
| DATA-02 | `Next / 0.2.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| INT-04 | `Next / 0.2.0` | Staff / pre-case intake | Shared intake and acceptance / Intake Web route | Reference allocation waits only for safe processing and identity-critical Principal/type determination; ordinary gaps are `Not ready` | Intake workbench |
| OPS-23 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| OPS-25 | `Now / 0.1.0-alpha.1` | Release/operations | Operational policy / authorised operator | No internal implementation terms in staff UI | Non-UI |
| INT-05 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| INT-06 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| INT-07 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| INT-14 | `Next / 0.2.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| INT-15 | `Next / 0.2.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| INT-16 | `Next / 0.2.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| INT-28 | `Next / 0.2.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-01 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-02 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-03 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-04 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-05 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-06 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-07 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-08 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-09 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-10 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-11 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-13 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| CASE-23 | `Next / 0.4.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| UI-10 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| UI-14 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| API-01 | `Next / 0.4.0` | Provider API | Provider API owner / provider caller | No staff-browser surface | Non-UI |
| API-02 | `Next / 0.4.0` | Provider API | Provider API owner / provider caller | No staff-browser surface | Non-UI |
| API-03 | `Next / 0.4.0` | Provider API | Provider API owner / provider caller | No staff-browser surface | Non-UI |
| API-04 | `Next / 0.4.0` | Provider API | Provider API owner / provider caller | No staff-browser surface | Non-UI |
| MCP-05 | `Next / 0.3.0` | Automation Actor / non-browser API | Shared Core use case / MCP caller | No ordinary-staff MCP access, browser UI, or implied `0.1.0-alpha.1` email workspace | Non-UI |
| AI-05 | `Later / 1.0.0` | Staff / Case image set | Image-readiness policy / future Case and AI caller | No Case allocation, state, eligibility, chase, or AI-proposal effect; report-image reflection exclusion remains separate | Future Case evidence/review surface; no `0.1.0-alpha.1` surface |
| MAIL-23 | `Next / 0.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-19 | `Later / 0.5.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| CASE-05 | `Later / 0.5.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| CASE-06 | `Later / 0.5.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-15 | `Later / 0.5.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| AI-01 | `Later / 0.6.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| AI-02 | `Later / 0.6.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| AI-03 | `Later / 0.6.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| AI-04 | `Later / 0.6.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| AI-06 | `Later / 0.6.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| MAIL-17 | `Later / 1.2.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| CASE-22 | `Later / 1.0.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-04 | `Later / 0.7.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-05 | `Later / 1.0.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-06 | `Later / 1.0.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-07 | `Later / 1.0.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-08 | `Later / 1.1.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-09 | `Later / 1.0.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-10 | `Later / 1.0.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-11 | `Later / 1.2.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-12 | `Later / 1.0.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| EXT-13 | `Later / 1.0.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| AI-07 | `Later / 1.3.0` | Future capability; role/state to be specified | Future owning plan and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required / no `0.1.0-alpha.1` surface |
| ACC-12 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| ACC-13 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| ACC-14 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| MAIL-12 | `Later / 0.5.0` | Staff / future email work | Email policy / future authenticated staff caller | No implied `0.1.0-alpha.1` control; must not weaken the separately gated MAIL-17 report-send transaction | Future case-centred email route required / no `0.1.0-alpha.1` surface |
| UI-12 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| DOC-09 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| DOC-10 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| DOC-11 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| DOC-12 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| DOC-13 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| DOC-14 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| DOC-15 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| OPS-12 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| OPS-15 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| OPS-16 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| OPS-17 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| OPS-18 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| OPS-19 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| OPS-21 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-01 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-02 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-03 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-04 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-05 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-06 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-07 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-08 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-09 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-10 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| BND-11 | `Not planned / unallocated` | Permanent absence | No product caller or UI owner | Not planned: no route, control, placeholder or backlog | Permanent absence / no route or control |
| EXT-16 | `Later / 1.4.0` | Future capability; role/state to be specified | Future direct decision, owning plan, and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required after direct decision / no `0.1.0-alpha.1` surface |
| EXT-17 | `Later / 1.4.0` | Future capability; role/state to be specified | Future direct decision, owning plan, and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required after direct decision / no `0.1.0-alpha.1` surface |
| EXT-19 | `Later / 1.4.0` | Future capability; role/state to be specified | Future direct decision, owning plan, and caller | No implied `0.1.0-alpha.1` control, navigation or workflow | Future UI route required after direct decision / no `0.1.0-alpha.1` surface |
| INT-31 | `Now / 0.1.0-alpha.1` | Staff / bounded in-house upload request | Request-scoped upload policy / authenticated staff creator and isolated public upload route | No external account, cross-request access, case/reference/request-history disclosure, permanent link, or implied acceptance; token, limits, custody, retry, revocation, expiry, and abuse contracts gate use | Case initiation; bound upload fields and immediate request-local result only |
| INT-32 | `Next / 0.2.0` | Staff / deferred intake pairing | Shared intake and matching policy / future Intake caller | No merged age, inferred match, case creation, or hidden unresolved half | Future Intake workbench; no `0.1.0-alpha.1` surface |
| CASE-31 | `Later / 1.0.0` | Engineer / canonical accepted data | Case and engineering policy / future accepted output callers | No retyping, duplicate truth owner, or output-specific source fork | Future case-centred Engineer workbench; no `0.1.0-alpha.1` surface |
| ENG-01 | `Later / 1.0.0` | Named Engineer / repair specification | Engineering policy / future accepted Glass's, Audatex, or AI-proposal caller | No unaccepted source, route conflation, or autonomous AI acceptance | Future estimate/repair section; no `0.1.0-alpha.1` surface |
| ENG-02 | `Later / 1.0.0` | Named Engineer / engineering judgement | Engineering policy / future case workbench caller | No retyping, inferred judgement, or output-owned calculation | Future valuation/salvage/report sections; no `0.1.0-alpha.1` surface |
| UI-15 | `Later / 1.0.0` | Engineer / progressive case work | Operator-experience arrangement / future Case Web route | No copied EVA navigation, duplicate domain owner, or permanently exposed irrelevant sections | Future case-centred Engineer workbench; no `0.1.0-alpha.1` surface |
| RPT-01 | `Later / 1.1.0` | Engineer / deterministic output | Report policy and renderer / future report caller | Imported renderer is non-caller evidence; no duplicate calculation or unvalidated render | Future report section; no `0.1.0-alpha.1` surface |
| RPT-02 | `Later / 1.1.0` | Engineer / assessment output | Assessment-report policy / future report caller | No unapproved wording, recomputation, or omitted itemised specification | Future report section; no `0.1.0-alpha.1` surface |
| RPT-03 | `Later / 1.1.0` | Engineer / Audit output | Audit-report policy / future report caller | No single-specification shortcut or unrecorded uplift | Future report section; no `0.1.0-alpha.1` surface |
| RPT-04 | `Later / 1.1.0` | Engineer / diminution output | Diminution-report policy / future report caller | No inferred percentage, duplicate case data, or unapproved wording | Future report section; no `0.1.0-alpha.1` surface |
| RPT-05 | `Later / 1.1.0` | Engineer / addendum output | Addendum policy / future report caller | No retyped case, anonymous amendment, or unapproved revision | Future report section; no `0.1.0-alpha.1` surface |
| AI-08 | `Later / 1.3.0` | Named Engineer / proposal review | AI proposal policy / future query-response caller | Foundry remains subject to evaluation; no autonomous mutation or send and no proposal without case evidence and review | Future query-response review; no `0.1.0-alpha.1` surface |
| AI-09 | `Later / 1.3.0` | Staff and scoped worker / proposal request | AI work-request policy / future Case Web and Worker callers | No stale/duplicate/expired/cancelled mutation, generic model call, or autonomous acceptance | Future capability-scoped action and proposal review; no `0.1.0-alpha.1` surface |
| MI-01 | `Later / 1.2.0` | Authorised management / Engineer measures | Management-information policy / future reporting caller | No unaccepted metric definitions or unrestricted coaching view | Future authorised management view; no `0.1.0-alpha.1` surface |
| MI-02 | `Later / 1.2.0` | Authorised finance/management / principal measures | Management-information policy / accepted report events and fee rules | No invoice truth inferred from draft reports or unaccepted fee rules | Future authorised management/finance view; no `0.1.0-alpha.1` surface |
| MI-03 | `Later / 1.2.0` | Authorised management / operational measures | Management-information policy / accepted workflow and MAIL-17 events | No fabricated timestamps, duplicate report-send event, or unaccepted visibility | Future authorised management view; no `0.1.0-alpha.1` surface |

## Matrix rules

Every planned row mirrors its exact canonical `Horizon / target`; that allocation creates no `0.1.0-alpha.1` surface for deferred features. Every `Not planned / unallocated` feature is permanently absent from route, control and backlog. Deferred features that still require a direct decision remain prohibited from implementation until that decision and a complete owning UI route. The currently called UI proofs are the authenticated `/Intake`, `/Triage`, and `/Uploads/{token}` callers recorded in [architecture](../../docs/architecture.md).

# Operator experience requirements

Status: **Planned `0.1.0-alpha.1` requirements with Operations-first shell selected.** This is the canonical publication of the reviewed `0.1.0-alpha.1` inventory. Shell selection does not approve every comparison-raster detail or prove a staff caller.

## Evidence state and scope

The actual called UI is the Development-only `/Intake/Upload` pre-case upload/receipt path through `ProcessIntake`, including the retained-asset handler. It is unauthenticated, creates no case/reference, and is not `0.1.0-alpha.1` staff UI. Operations, Intake, Triage, Cases and Administration are all Planned `0.1.0-alpha.1` staff surfaces.

The intended setting is a small office of approximately eight users. Staff accounts use Pegasus-managed usernames and passwords; the authentication and authorisation behaviour remains Planned until an authenticated Web caller exists.

| Actor | May manage | Must not access or perform |
| --- | --- | --- |
| Administrator | staff accounts, disable/access review/roles; principals/successor cutover; configuration; approved mailbox allowlist; all ordinary staff intake, Triage, case and document work | credentials or cloud/release administration through the UI; permanent deletion; a generic mailbox-rule editor before policy resolves |
| Engineer, User | authorised intake, Triage, case, document, lookup, chaser, evidence and lifecycle work | account/role/access review, principals, configuration, mailbox allowlist, credential/cloud administration or permanent deletion |
| Automated processing | named Core intake/evidence actions under its durable identity | a UI account, guessed matching or independent business policy |
| Provider client (`Next`/`unallocated`) | principal-scoped receipt/status/result API only | staff shell, general case workflow or administration |
| External/customer | no application account | every application surface (`Not planned`) |

Every protected route/action has unauthenticated, disabled-session, stale-role, denied, loading and successful outcomes. Route hiding is never authorisation.

## `0.1.0-alpha.1` flows

**Intake** retains source and provenance, attachments/images, suggestions, validation, conflicts and origin. A definitive authorised instruction automatically creates exactly one case through shared fail-closed acceptance: **Review** when instruction and image completeness requirements are met, otherwise incomplete **Not ready**. Staff-resolved acceptance creates **Review** only through explicit staff confirmation of both instruction and image completeness; otherwise it creates **Not ready**. Explicit staff confirmation of both completeness requirements also moves an existing **Not ready** case to **Review**. `Blocked intake` requires a reason and remains pre-case: no case/reference exists while it is blocked. Resolve/retry re-enters the shared fail-closed intake path and may create exactly one case/reference only when the ordinary acceptance gates then pass. Identity/Audit ambiguity, unsupported or incomplete source, limits/custody/persist/retention failure, integrity/replay/occurrence conflict and missing evidence remain pre-case, usually `Needs sorting`.

**Triage** is a distinct inbox classification/label plus a separate pre-case reference record; it is never a case state. Registration is required; otherwise the source remains `Needs sorting`. It has Open, Awaiting information, Finding recorded, Completed and Cancelled states; two independently optional findings, with at least one required before Finding recorded/Completed: Roadworthiness = Roadworthy/Unroadworthy and Assessment = Repairable/Total loss. A case's `has Triage` is Boolean/reference-only; Triage findings have no bearing on Case/PO/reference, workflow, final outcome, Engineer report, Audit suffix/allocation or any other decision. It has an optional assignee; exact approved-mailbox reply-chain evidence for completion; reasoned replacement/reopen/linking; no due date and no chasers.

**Case** keeps immutable Case/PO, principal, registration, type/secondary Audit identity, workflow state, due date and the EVA proxy limitation visible. It includes source/provenance, data, documents/images, inspection address or `Image Based Assessment`, vehicle/MOT, tasks/reminders, Box requests/copyable chasers, manual WhatsApp material, EVA export, exact report evidence, lease/conflict recovery and permanent action history. It supports Not ready, Review, Held, due/overdue, the four terminal outcomes (Post-report completion, Provider cancellation, Collision Engineers rejection, Created in error), archive and reasoned reopening. Archive never deletes; Created in error never reopens.

**Administration** is Administrator-only: account creation/disable/access review/roles, principal successor cutover, configuration and mailbox allowlist. It has no generic rules editor or credential/cloud operation.

## UI-07 search and filters

Case/PO, registration, claimant, claim number, principal, state, Engineer, received/instruction dates and range, and origin.

## Operations and state boundaries

Operations shows Not ready, Review, Held, Needs sorting, exact `Blocked intake`, separate Triage, Due today, In today, Sent to Engineer today/week, and Reports sent today/week. It uses Europe/London midnight days and Monday-week boundaries. Counts open their exact filtered queues; zero is distinct from stale/unavailable; last updated and manual refresh are visible. Receiving work, Queries and Other are `Next`/`unallocated` only.

### `0.1.0-alpha.1` surface inventory

- Intake includes manual upload; definitive/staff-resolved paths; origin/custody; extraction and reviewed VRM suggestion; field provenance, validation, missing/conflict, duplicate/retry and missing/integrity asset/source failures.
- Case identity covers Inspection, standalone Audit and Inspection + Audit with the secondary Audit identity. Allocated reference/principal never change. Wrong principal closes `Created in error` with a reason and linked replacement; neither reference is reused and the original never reopens.
- Case work covers Not ready, Review and Held; due/overdue; seven-calendar-day chasers with the Held interval preserved; Box file request/copyable chaser; tasks/reminders; manual WhatsApp material; DVLA/DVSA and MOT/mileage; inspection address or exact `Image Based Assessment`; and successful EVA JSON/image export only as the Sent-to-Engineer proxy.
- Documents/evidence covers automatic Box folder, upload/version, logical removal, closed-case lock/reopen-before-change, Box unavailable/pending/retry/unknown, exact report-Sent evidence and reasoned manual link/unlink/relink.
- Terminal/aftercare names Post-report completion, Provider cancellation, Collision Engineers rejection and Created in error. Archive never deletes. Reopen requires a reason and valid nonterminal destination; Held is not a reopen destination and Created in error never reopens.

### Complete state matrix

| Scope | Explicit states |
| --- | --- |
| Queries | loading; empty; success; stale/partial with last-good time; transient error/retry; unauthenticated/disabled/stale-role/denied |
| Mutations | validation; confirmation; success; denied; stale version; lease lost; dependency unavailable; idempotent/replayed result; conflict and recovery |
| Intake | empty/oversize; replay; retention/custody failure; Draft ready; Needs sorting; Unsupported; missing/integrity asset; evidence missing/contradictory; Blocked intake reason/resolve/retry; every acceptance path; refusal with no case/reference |
| Triage | registration missing; unassigned/assigned; every named state; missing/ambiguous/unapproved/technical reply evidence; finding replacement/correction/new response; cancel/reopen/link/unlink/relink |
| Case | Not ready/chasing; Review; Held/preserved interval; due/overdue; gate refusal; documents locked; Box/external-effect states; EVA proxy limitation; report evidence absent/ambiguous/manual/exact; every terminal outcome; archive; reopened; Created-in-error nonreopenable; lease held/expired/lost/stale |

Permanent action history records business mutations, accepted external evidence, exports and material denied/failed business actions with actor/time/outcome/reason/before-after. It excludes routine views, refresh/polling, retries, leases/heartbeats and adapter/Worker mechanics, which stay in telemetry/security evidence outside the operational UI.

## Accessibility, desktop and data boundary

Use semantic landmarks/headings/tables, labels and associated errors, keyboard operation, visible focus, screen-reader announcements, practical 44px targets, forced-colours and reduced-motion support; state is never colour-only. At 1280px+ use dense multi-pane desktop. At 1024–1279px and 200% zoom, reorder essential desktop content into labelled tabs/drawers/sections without loss. Mobile staff UI is **Not planned**; a supported-device notice is only for genuinely unsupported devices, never a CSS-width substitute.

The contained visual boundary is warm off-white ground, white panels, warm-charcoal navigation, near-black text, CE-red primary/urgent accents, amber incomplete/pending, restrained navy Review and green only confirmed completion. Use system-sans 14–16px body text, sharp 2–3px corners, rare shadows and Lucide-style line icons; do not expose Azure, OCR, AI, queues or implementation mechanics in operator copy.

Evaluation and operator review use approved genuine local immutable material only. Do not invent operational inputs. `Next`/`unallocated` and `Later`/`unallocated` features have no `0.1.0-alpha.1` control, navigation, workflow or placeholder and must re-enter the complete UI route before a later UI change.

## Selected shell and open gates

Operations-first is selected for the `0.1.0-alpha.1` landing and navigation strategy. The three comparison rasters remain selection evidence; only Direction A's shell strategy is approved, not every raster detail. Policy-specific email predicates and acceptance evidence still block only their named automatic paths. `Next`/`unallocated` and `Later`/`unallocated` UI remains outside this selection.

## Historical material

The selected Operations-first direction and the rejected Worklist-first and Case-first comparisons are preserved in [traceability](traceability-matrix.md). Their obsolete planning files and rasters are retired; the current design route is [design](../README.md), with interaction detail in [ui-spec.md](ui-spec.md).

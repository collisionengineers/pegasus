# Operator experience requirements

Status: **Planned `0.1.0-alpha.1` requirements with Operations-first shell selected.** This is the canonical publication of the reviewed `0.1.0-alpha.1` inventory. Shell selection does not approve every comparison-raster detail or prove a staff caller.

## Evidence state and scope

The actual called UI is the Development-only `/Intake/Upload` pre-case upload/receipt path through `ProcessIntake`, including the retained-asset handler. It is unauthenticated, creates no case/reference, and is not `0.1.0-alpha.1` staff UI. Operations, Intake, Triage, Cases and Administration are all Planned `0.1.0-alpha.1` staff surfaces.

The intended setting is a small office of approximately eight users. Staff accounts use Pegasus-managed usernames and passwords; the authentication and authorisation behaviour remains Planned until an authenticated Web caller exists. Core owns the exact [staff role access matrix](../../docs/requirements.md#staff-role-access-matrix), automated-actor boundary, and [case edit authority and recovery](../../docs/requirements.md#case-edit-authority-and-recovery); this design must not create broader permissions or a second role policy.

| Actor | Planned UI boundary |
| --- | --- |
| Administrator | Staff shell plus Administration surfaces for accounts/access/roles, principals, configuration, and approved mailbox allowlist. |
| Engineer, User | Staff shell without Administration surfaces. The ordinary case/action controls are the same for both roles. |
| Automated processing | No UI account or interactive control. |
| Provider API client ([API-01–API-04, `Next / 0.4.0`](../../docs/capabilities.md#capabilities)) | No staff shell or Administration surface. |
| External/customer | No application account or application surface (`Not planned`). |

Every protected route and action visibly handles unauthenticated, disabled-session, stale-role, denied, loading, and successful outcomes. Route or control hiding is never authorisation. The UI offers neither permanent deletion, credential/cloud/release administration, a generic mailbox-rule editor, bulk case editing, nor external direct Case editing.

## `0.1.0-alpha.1` flows

**Intake** presents the immutable source occurrence and its derived evidence separately from the editable candidate and accepted Case projection; matching conflict, ambiguity, manual association, reversal, and reassociation remain visible rather than rewriting the source. The evidence pane retains the exact `All`/`Instructions`/`Images` filters. Opening source evidence or supporting detail preserves the current list/detail position and every unsaved candidate edit; returning restores the Intake or Case-detail context without silently discarding or replacing proposed values. Controls invoke the Core-owned [source and Case association](../../docs/requirements.md#matching-conflicts-and-reversible-association) and [mandatory pre-case gate](../../docs/requirements.md#mandatory-pre-case-gates) contracts. The result view shows provenance, attachments/images, suggestions, validation, conflicts, origin, dispatch/retry state, the accepted `Review` or incomplete `Not ready` Case, or the explicit reason no case/reference exists.

**Triage** remains visually and navigationally distinct from a Case and from generic inbox sorting. Its list/detail workspace presents the registration gate, assignee, named findings and states, missing/ambiguous reply evidence, replacement history, completion/cancellation, reopen, and optional later Case association. Core owns the [normal Triage workflow and completion evidence](../../docs/requirements.md#normal-workflow-and-completion-evidence); the design must distinguish ordinary acknowledgement or information correspondence from the exact reply-chain evidence required to complete the workflow.

**Case** keeps Case/PO, principal, registration, [Inspection, standalone Audit, or Inspection + Audit identity](../../docs/requirements.md#case-types), workflow state, due date, and EVA proxy limitation visible. It presents the accepted Case projection alongside source/provenance, data, documents/images, parties and inspection address, vehicle/MOT, tasks/reminders, outbound evidence, external-work states, and permanent history. Core owns [principal and historical case-party identity](../../docs/requirements.md#principal-reference-organisation-and-case-party-identity), [lifecycle closure and correspondence](../../docs/requirements.md#lifecycle-closure-and-correspondence), [outbound correspondence evidence](../../docs/requirements.md#outbound-correspondence-evidence), and one-case [edit authority and recovery](../../docs/requirements.md#case-edit-authority-and-recovery). The workspace identifies the active editor and stale version, becomes read-only after lease loss or named closure, and offers only the authorised retry/reopen/reacquire routes; one control mutates one current Case at a time.

**Administration** is an Administrator-only surface implementing the linked role matrix. It exposes account/access/role, principal successor, configuration, and approved-mailbox-allowlist controls, but no generic rules editor, credential/cloud operation, bulk predecessor import, bulk Case edit, or direct external Case-edit surface.

## UI-07 search and filters

Case/PO, Image Intake Reference, registration, claimant, claim number, principal, state, Engineer, received/instruction dates and range, and origin. Each result is one keyboard-focusable full-row link or button with a visible affordance.

## Operations and state boundaries

Operations shows Not ready, Review, Held, Needs sorting, exact `Blocked intake`, separate Triage, Due today, New cases today, Sent to Engineer today/week, and Reports sent today/week. It uses Europe/London midnight days and Monday-week boundaries. `New cases today` has the exact Case-creation definition in the [requirements](../../docs/requirements.md#dashboard-freshness-and-reconciliation). Counts open their exact filtered queues; zero is distinct from stale/unavailable; last updated and manual refresh are visible. Receiving work, Queries and Other are `Next / 0.3.0` in the [capability inventory](../../docs/capabilities.md#capabilities), with no `0.1.0-alpha.1` surface. |

An intake row always presents received date above received time and its precise processing outcome. At constrained desktop width, long Case/PO or Image Intake Reference text moves to a labelled second line; it must not overlap the received timestamp or another row field.

### `0.1.0-alpha.1` surface inventory

- Intake includes manual upload; definitive/staff-resolved paths; immutable [source occurrence/dispatch](../../docs/requirements.md#source-occurrence-and-dispatch-identity) beside the Case projection; origin/custody; extraction and reviewed VRM suggestion; pre-Case Image-intake registration with its Image Intake Reference, association/await-instruction outcome, and no Case state; field provenance, validation, ambiguity/conflict, association history, duplicate/retry, and missing/integrity asset/source failures. Each row identifies its exact outcome rather than a generic `New`.
- Case identity presents the Core-owned [Inspection, standalone Audit, and Inspection + Audit](../../docs/requirements.md#case-types) distinctions, secondary Audit identity, immutable [Case/PO and principal](../../docs/requirements.md#principal-reference-organisation-and-case-party-identity), and linked `Created in error` replacement without offering identity rewrite.
- Case work covers Not ready, Review and Held; separate mandatory instruction-completeness, image-completeness, and staff-review decisions before Engineers-queue eligibility, with no Pegasus named-Engineer assignment in alpha; due/overdue; seven-calendar-day chasers with the Held interval preserved; the Core-owned [staff-created temporary, revocable, expiring, request-scoped in-house upload-token](../../docs/requirements.md#request-scoped-upload-links) isolation, non-disclosure, and request-local custody contract; [copyable manual chasers](../../docs/requirements.md#due-work-chasing-and-action-history); tasks/reminders; manual WhatsApp material; DVLA/DVSA and MOT/mileage; inspection address or exact `Image Based Assessment`; and successful EVA JSON/image export only as the Sent-to-Engineer proxy.

- Case evidence shows retained source images, their provenance, category, staff-confirmed third-party exclusions, and advisory findings. It does not contain EVA or report-image selection/order controls; the focused alpha exports every eligible Case-vehicle image, EVA owns downstream ordering, and the accepted future Engineers screen owns those decisions after EVA replacement.
- Documents/evidence covers automatic Box folder, upload/version, logical removal, closed-case lock/reopen-before-change, Box unavailable/pending/retry/unknown, exact report-Sent evidence and reasoned manual link/unlink/relink.
- Terminal/aftercare presents the exact [Core-owned lifecycle and correspondence](../../docs/requirements.md#lifecycle-closure-and-correspondence) outcomes and reasoned recovery paths. It must not turn acknowledgement, report-Sent evidence, or other correspondence into a generic completion action.

### Complete state matrix

| Scope | Explicit states |
| --- | --- |
| Queries | loading; empty; success; stale/partial with last-good time; transient error/retry; unauthenticated/disabled/stale-role/denied |
| Mutations | validation; confirmation; success; denied; stale version; lease lost; dependency unavailable; idempotent/replayed result; conflict and recovery |
| Intake | empty/oversize; replay; retention/custody failure; Draft ready; Needs sorting; Unsupported; missing/integrity asset; evidence missing/contradictory; Blocked intake reason/resolve/retry; every acceptance path; refusal with no case/reference |
| Triage | registration missing; unassigned/assigned; every named state; missing/ambiguous/unapproved/technical reply evidence; finding replacement/correction/new response; cancel/reopen/link/unlink/relink |
| Case | Not ready/chasing; Review; Held/preserved interval; due/overdue; gate refusal; documents locked; Box/external-effect states; EVA proxy limitation; report evidence absent/ambiguous/manual/exact; every terminal outcome; archive; reopened; Created-in-error nonreopenable; lease held/expired/lost/stale |

The UI presents the [Core-owned permanent action history](../../docs/requirements.md#permanent-action-history) with enough actor, time, outcome, reason, and before/after context to understand each business event. Routine views, refresh/polling, retries, leases/heartbeats, and adapter/Worker mechanics stay out of the operational history panel.

## Accessibility, desktop and data boundary

Use semantic landmarks/headings/tables, labels and associated errors, keyboard operation, visible focus, screen-reader announcements, practical 44px targets, forced-colours and reduced-motion support; state is never colour-only. At 1280px+ use dense multi-pane desktop. At 1024–1279px and 200% zoom, reorder essential desktop content into labelled tabs/drawers/sections without loss. Mobile staff UI is **Not planned**; a supported-device notice is only for genuinely unsupported devices, never a CSS-width substitute.

The contained visual boundary is warm off-white ground, white panels, warm-charcoal navigation, near-black text, CE-red primary/urgent accents, amber incomplete/pending, restrained navy Review and green only confirmed completion. Use system-sans 14–16px body text, sharp 2–3px corners, rare shadows and Lucide-style line icons. Each semantic action or state uses one consistent icon everywhere; decorative or generated replacement icons are prohibited. Do not expose Azure, OCR, AI, queues or implementation mechanics in operator copy.

Evaluation and operator review use approved genuine local immutable material only. Do not invent operational inputs. Every deferred `Next` or `Later` capability carries its exact target in the [capability inventory](../../docs/capabilities.md#capabilities) and has no `0.1.0-alpha.1` control, navigation, workflow, or placeholder; [traceability](traceability-matrix.md) mirrors that allocation. Every later UI change must re-enter the complete design route.

## Selected shell and open gates

Operations-first is selected for the `0.1.0-alpha.1` landing and navigation strategy. The three retained comparison rasters are selection evidence; Direction A's shell strategy is approved, but no raster is pixel-level authority or runtime proof. Policy-specific email predicates and acceptance evidence still block only their named automatic paths. Deferred `Next` and `Later` UI remains outside this selection regardless of its exact allocated target.

## Historical material

The selected Operations-first direction and the rejected Worklist-first and Case-first comparisons are preserved in [traceability](traceability-matrix.md). Their obsolete planning files are retired; the [Operations-first](../references/mockups/candidate-a-operations-first.png), [Worklist-first](../references/mockups/candidate-b-worklist-first.png), and [Case-first](../references/mockups/candidate-c-case-first.png) rasters remain immutable selection evidence. The current design route is [design](../README.md), with interaction detail in [ui-spec.md](ui-spec.md).

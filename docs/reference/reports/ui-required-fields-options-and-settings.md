# UI required fields, options, and settings

**Prepared:** 2026-07-24

**Purpose:** Consolidate the staff-facing fields, choices, filters, actions, and Administrator settings that the current Pegasus UI must support, while keeping predecessor UI material clearly separated from current authority.

**Evidence state:** Requirements and planning only. The Development-only `/Intake/Upload` page is the sole current intake caller. The authenticated dashboard, inbox workbench, case workspace, staff administration, and principal/configuration pages are planned and are not proved by the legacy application.

## Source and interpretation boundary

Current UI direction comes from [design authority](../../../design/README.md), [operator experience requirements](../../../design/product/requirements.md), operator truth, the [settled questionnaire](../../history/product/project-discovery-questionnaire.md), [the `0.1.0-alpha.1` gap](../../product/qdos-alpha-gap.md), product areas, and the [open-decision register](../../product/open-decisions.md). The former UI plan pack is historical evidence only.

The legacy design and review guide (`../guide/04-design-and-reviews.md`) was used to route the predecessor interaction rules (`../docs/design/ui-ux.md`) and dated reviews. Those files supply terminology, pain points, and candidate controls only. Their old screen layouts, completion claims, API/EVA field rules, theme decisions, and configuration model are not Pegasus authority.

“Required field” has two distinct meanings in this report:

1. a field the relevant screen must display or allow staff to capture when applicable; and
2. a value that must exist before a particular action can succeed.

Pegasus does **not** have a universal required-field matrix. Cases may be accepted with incomplete ordinary information into `Not ready`. The UI must show missing and contradictory values rather than silently guessing or preventing every incomplete case.

## Actual blocking rules

These are the settled action-blocking rules the UI must communicate:

| Action | Required value or decision | UI behavior when absent |
| --- | --- | --- |
| Allocate a formal Case/PO | Confirmed principal with a principal code | Retain the source pre-case; do not allocate a reference. |
| Identify image-led intake before a principal is known | Readable vehicle registration | Retain/block the source with a warning and reason; do not invent a registration. |
| Create a standalone `Audit` | Original Engineer report with an unambiguous `Repairable` or `Total loss` assessment | Retain the source pre-case with a blocking warning; allocate neither `a.` nor `ap.`. |
| Place an inbox item in `Blocked intake` | Staff-entered reason | Retain the source, reason, warning, and retry action; create no case/reference. |
| Enter `Held` | Staff-entered reason | Refuse the transition without a reason. Due dates stay visible; progression and chasers pause. |
| Assign/pass to an Engineer when the completeness gate is on | Staff-confirmed `Instruction complete` and `Images complete` | Refuse assignment, but do not undo case creation. |
| Cancel, reject, reopen, or reverse a mistaken merge | Reason and authenticated actor | Refuse a reasonless action and retain prior state/history. Reopen additionally requires an otherwise-valid nonterminal destination, applies its normal gates, excludes `Held`, and refuses `Created in error`. |
| Close a case | One named terminal outcome | Offer `Post report`, `Provider cancellation`, `Collision Engineers rejection`, or `Created in error`. The last requires a reason and a link to a new replacement case under the corrected principal. |

## Needs sorting and intake workbench

### Source and evidence presentation

| Field or control | Requirement | Input/display behavior |
| --- | --- | --- |
| Source identity | Required display | Preserve the original channel occurrence after acceptance, matching, or merge. Never replace it with only a content hash or Case/PO. |
| Received metadata | Required display | Received date/time, sender/source, subject or equivalent transport context, without exposing Graph, queues, functions, or storage mechanics. |
| Source content | Required display | Email/freehand content, instruction document preview, attachments, image thumbnails, and unsupported/corrupt-file state. |
| Evidence provenance | Required display per extracted value | Show which email, document, page/region, filename, or image proposed a value and show competing values. A visible model-confidence percentage is not required. |
| Suggested versus confirmed state | Required | Extracted values remain visibly suggested/editable until a staff member confirms them. |
| Missing/conflicting/unsupported/failure state | Required | Preserve each outcome explicitly; never coerce it into a plausible value or zero/empty success. |
| Intake evidence filter | Required in the workbench | `All`, `Instructions`, `Images`. |

### Case data fields

These fields must be supportable in intake review and later case editing. “Conditional” means the UI captures the value when available or required by the particular identity rule; it does not create a universal case-creation block.

| UI field | Status | Required behavior and options |
| --- | --- | --- |
| Work provider / Principal | Identity-critical | Staff confirm a configured active principal. Required before Case/PO allocation. Do not use claimant, insurer, or repairer as the principal. |
| Principal code | Derived, read-only | Display from the selected principal; never accept it as free text on the case. |
| Case/PO | Calculated, read-only | Allocate from the confirmed principal/year sequence. Never let staff type or overwrite it. |
| Work type | Identity-critical | `0.1.0-alpha.1` options: `Inspection`, `Audit`, `Inspection + Audit`. `Diminution` and `Commercial` remain deferred and must not appear as active `0.1.0-alpha.1` choices. |
| Original Engineer report | Conditional blocker | Required evidence for a standalone `Audit`. Keep the source report available beside the assessment decision. |
| Original-report assessment | Conditional blocker | Staff-confirmed evidence outcome: `Repairable`, `Total loss`, or `Missing/ambiguous`. Calculate `a.` or `ap.` from the first two; the last blocks creation. Do not offer a free-form prefix. |
| Claimant name | Capture when available | Show missing/conflicting state. Its absence alone is not a universal creation blocker. |
| Claim number / provider reference | Capture when available | This is the external reference; keep it distinct from Collision Engineers' Case/PO. |
| Vehicle registration | Conditional identity field | Required as the provisional identity for image-led intake; normalise only by accepted typed rules and retain the source value/provenance. |
| Vehicle make | Capture/enrich when available | Editable confirmed value; later DVLA/DVSA data must show its source. |
| Vehicle model | Capture/enrich when available | Keep separate from make in the UI/data even where a source supplies a combined phrase. |
| Vehicle mileage | Capture/enrich when available | Show source and estimation warning. MOT-based estimation is planned; exact provider response fields remain to be accepted. |
| Accident circumstances | Capture when available | Multiline confirmed value with source evidence; do not silently append unrelated document sections. |
| Date of incident | Capture when available | Use the current business label, not the predecessor label `Date of loss`; reject impossible values rather than guessing. |
| Instruction date | Defaulted when absent | Use the source value when supplied; otherwise default to the current date and make the default/provenance visible. |
| Due by | Capture/confirm when available | Extract the inspection date or equivalent deadline from instructions. Show source, missing state, and overdue state. It is not the same field as Instruction date. |
| Inspection address mode | Required choice when completing the address | `Physical vehicle/repairer address` or the exact valid value `Image Based Assessment`. Collision Engineers does not perform an on-site inspection. |
| Physical inspection address | Conditional | Capture the vehicle, claimant, garage, or repairer location when that mode applies. The legacy six-line EVA format is not yet the accepted Pegasus UI/storage contract. |
| Claimant contact/address | Conditional supporting data | The product may store relevant claimant contact/address details, but the current operator field list does not make every contact element a universal intake requirement. |
| Repairer/garage/bodyshop | Conditional case party | Link a reusable organisation where known; do not reduce it to only free-text inspection-address content. |
| Third-party insurer and operational contacts | Conditional case parties | Support deliberate case associations where relevant; not universal intake blockers. |
| Intake origin | Derived, read-only/filterable | `Instruction initiated` or `Image initiated`; preserve both origins and association history after a definitive match. Do not make `Instructions only`, `Images only`, `Both`, or `Merged` a manually configured case type. |
| Instruction complete | Required independent staff decision | `Confirmed` / `Not confirmed`. It describes instruction completeness and is not a hard-coded field checklist. |
| Images complete | Required independent staff decision | `Confirmed` / `Not confirmed`. Keep independent from instruction completeness. |
| Missing-material reason | Required for due/chaser presentation | Describe the missing details, images, or documents in business language; use it for visible due work and copyable chaser text. Exact category options are not yet settled. |

### Intake outcomes and actions

| Control | Required outcome |
| --- | --- |
| `Block intake` | Requires a reason; retains the source and warning in `Blocked intake`; creates no case/reference. |
| `Create incomplete` | Accepts a case into `Not ready`, preserving all missing/conflicting values for chasing. |
| `Create for review` | Accepts a complete case into `Review` only after staff separately judge instructions and images complete. |
| `Retry` | Re-runs a retained blocked item after staff resolve its blocker without losing source identity/history. |
| Match/link | Staff can confirm a definitive association between instruction-led and image-led material. Uncertain matches remain in `Needs sorting`. |
| Manual case/evidence upload | Required `0.1.0-alpha.1` capability using the same Core intake path; allow additional email/document/image evidence to be attached to the intended case. |

`Hold` in the generated intake mockup is not an accepted fourth intake outcome. `Held` is a reasoned state of an existing case, while `Blocked intake` is the pre-case action.

## Dashboard, queue, and search options

### Settled tiles and views

| Group | Required labels/options | Notes |
| --- | --- | --- |
| Case queues | `Not ready`, `Review`, `Held` | Not ready = incomplete and being chased; Review = complete and awaiting approval; Held = reasoned pause with due date still visible. |
| Inbox queues | `Receiving work`, `Queries`, `Other`, `Needs sorting` | `Triage` is reserved for its actual roadworthiness workflow, not a generic inbox label. |
| Manual inbox filter | `Blocked intake` | Must expose reason, warning, retained source, and retry, and must never resemble a case queue. |
| Activity labels | `In today`, paired `Sent to Engineer` today/week, paired `Reports sent` today/week | Use Europe/London midnight calendar days and Monday-to-Monday weeks. `In today` counts cases created. `Sent to Engineer` counts once per case from the first successful EVA JSON/image export generation in the `0.1.0-alpha.1`; this proxy does not prove EVA receipt. `Reports sent` counts every successfully sent report. |
| Due work | Due date, overdue state, seven-day chaser visibility, copyable chaser text | The first chase is due at the same Europe/London local clock time seven calendar days after entering `Not ready`. `Held` preserves the remaining interval; returning to `Not ready` resumes it, while choosing `Review` ends the missing-information chase. No automatic sending in the `0.1.0-alpha.1`. |
| Refresh | `Refresh`, last-updated time, refresh/failure state | Zero, stale, partial, unavailable, and failed are distinct states. Every count links to the exact filtered view it represents. |

`In today` is the required case-created activity label. Keep due/overdue work as a separate operational view rather than conflating due dates with cases received today.

### Search and filters

The case list/workspace must support these structured search/filter inputs:

- Case/PO;
- vehicle registration;
- claimant;
- claim number/provider reference;
- principal;
- stage/status;
- assigned Engineer;
- received date;
- instruction date;
- date range; and
- intake origin: `Image initiated` or `Instruction initiated`.

Queue rows must show enough identity and action context to distinguish cases, including Case/PO, registration, principal, relevant claimant/claim information, current state, due/age information, and the state-specific reason where applicable. The predecessor's exact per-queue column sets, five-row cap, bulk actions, and quick-peek drawer are not current requirements.

## Case workspace fields and actions

### Persistent header and sections

| Field/section | Requirement |
| --- | --- |
| Case identity header | Keep Case/PO, vehicle registration, principal, work type, Due by, current status, assigned Engineer, and reopened state visible. Assigned Engineer remains downstream/EVA-authoritative until an approved replacement exists. |
| Related references | Show `a.` and `ap.` references without replacing the parent Inspection reference. Principal and Case/PO are immutable once allocated; show a `Created in error` original and its linked replacement case rather than rewriting either reference. |
| Overview | Confirmed case fields, missing/conflicting indicators, completeness decisions, current gate/next action, origin and provenance. |
| Documents | Original instructions/email, later correspondence, original Audit report where applicable, Engineer report, and Box-backed document states. |
| Images | Reviewable images, provenance and image-completeness decision. Automated VRM/image analysis is deferred and must not be presented as current fact. |
| Report | Report-sent evidence state. There is no pre-send report review gate. Evidence is one exact Outlook Sent item from an approved mailbox; Outlook `sentDateTime` is authoritative. Pegasus detects but does not send reports. Automatic matching remains in the combined mailbox/email research decision, with reasoned exact-item linking available when a match is absent or ambiguous. |
| Action history | Actor, timestamp, action, prior/new state, reason/context, external outcome, merges/reversals, corrections, closure and reopening. |
| Box folder | Link plus explicit `Missing`, `Pending creation`, `Inaccessible`, and `Conflict` states rather than a false success. |
| Chaser | Due/overdue and missing-material context with `Copy message`. Copying never means sent or delivered. |

### Case action choices and required inputs

| Action | Required UI input/options |
| --- | --- |
| Enter Held | Required reason. |
| Leave Held | Required reason plus `Return to <prior state>` or `Review`. Returning to `Not ready` resumes the preserved chase remainder; choosing `Review` ends that chase. |
| Pre-assignment review | Explicit approval; when the completeness gate is on, both completeness confirmations must already be true. |
| Report sent evidence | No pre-send review action. Show the exact Outlook Sent item, approved mailbox, Outlook `sentDateTime`, and association state. Pegasus detects but never sends the report. When automatic matching is absent or ambiguous, exact-item linking requires a reason. |
| Close | `Post report`, `Provider cancellation`, `Collision Engineers rejection`, or `Created in error`; cancellation/rejection require a reason and every outcome records the actor. `Created in error` also requires a replacement-case link. |
| Reopen | Required reason plus any otherwise-valid nonterminal destination. Apply the normal destination gates, exclude `Held` because it has a separate action, and refuse a case closed as `Created in error`. |
| Correct a wrong-principal allocation | Do not offer principal/reference reassignment. Close the original as `Created in error` with a required reason and linked new replacement case under the corrected principal; never reuse either reference. |
| Reverse mistaken merge | Required reason and retained association history. |
| Delete | No control. Cases and history are never permanently deleted. |

Case editing must have an explicit edit mode and visible active-editor/stale-version conflicts. Status, references, work-type prefixes, provenance, and intake origin are controlled business data, not free-form dropdowns or editable text.

## Administrator settings

Only Administrators may see or use account, principal, and application-configuration controls. Engineer and User roles retain case actions/review gates but must be refused by the server if they attempt administration.

### Staff accounts

| Field/control | Required options/behavior |
| --- | --- |
| Username | Required unique application-managed staff identity. Public registration is absent. |
| Password flow | Initial secure bootstrap/change-password and normal sign-in; never display stored passwords. Exact reset wording/workflow belongs to implementation, not legacy screens. |
| Role | `Administrator`, `Engineer`, or `User`. |
| Account status | Active/enabled or disabled; display locked/failed access safely where applicable. Disabling invalidates continued protected access. |
| Account actions | Create, review role/status, disable, and sign out. Stale Administrator edits must be refused with refresh/review guidance. |

### Principal settings

| Field/control | Required options/behavior |
| --- | --- |
| Principal name | Required editable business name; first configured record is QDOS. |
| Principal code | Required unique code consumed by Case/PO allocation and immutable after first use. A legitimate replacement creates a new linked principal and atomically deactivates its predecessor; it never rewrites a case or issued reference. |
| Active state | `Active` / `Inactive`; unknown or inactive principals cannot allocate a reference. |
| Stable principal identity | Read-only/system-owned; cases retain it after later metadata changes. |
| Save conflict | Refuse a stale save and preserve the newer value; surface a refresh/review message. |

The predecessor `Corpus` label, corpus imports, provider file counts, and “last used” administration are not current principal-setting requirements. Use `Principal settings` or another operator-approved business label, not a predecessor corpus-management concept.

### Operational configuration

| Setting | Required options/behavior |
| --- | --- |
| Require completeness before Engineer assignment | Administrator-only `On` / `Off`. When on, both `Instruction complete` and `Images complete` must be confirmed. It affects assignment only, not case creation. Changes are versioned and recorded in permanent action history. |

Explicitly excluded from the first settings UI:

- a principal-specific required-field matrix;
- arbitrary key/value settings;
- provider API credentials until their separate contract and administration workflow are accepted;
- Box, EVA, or other vendor secrets;
- external/customer accounts;
- bulk predecessor-data import; and
- dormant integration or feature switches.

The operator note that some providers are always recorded as `Image Based Assessment` is a useful future principal-setting question, but no current plan authorises that principal-specific default. Inspection-address suggestions are also deferred beyond the `0.1.0-alpha.1`.

## Required cross-screen states and accessibility

Every production surface needs designed, labelled behavior for loading, empty, stale, partial data, transient integration failure, unauthorized action, validation conflict, duplicate/idempotent intake, Blocked intake, Held, terminal, reopened, and successful completion.

Counts and colours cannot be the only signal. Use keyboard-visible focus, screen-reader names/status announcements, AA contrast, reduced motion, readable zoom/reflow, and practical 44px interaction targets. Operator copy must avoid Azure, functions, queues, OCR/AI mechanics, payloads, tickets, feature flags, or `dev copy` wording.

## Remaining UI and contract design

These items still need their named research or implementation design. They do not reopen the settled workflow rules above:

| Area | Remaining design |
| --- | --- |
| Mailbox categorisation and automatic email matching | The combined research dossier owns category and matching predicates, precedence, ambiguity, policy governance, and automatic sent-report matching. Do not invent a rule engine, table, editor, or transport-specific classifier. |
| Missing-material reason choices | Whether this remains reason text or gains a constrained category list. |
| Current EVA export | Exact versioned JSON field mapping, image rules, readiness/release presentation, and recovery behavior. |

## Legacy findings not promoted into Pegasus requirements

The routed legacy reviews raised useful questions but do not establish these current requirements:

| Legacy proposal or field | Current treatment |
| --- | --- |
| Exact `X-Api-Key`, EVA/API forms, Base64 upload rules, 50 MB limits, and old error messages | Predecessor contracts only; not internal UI requirements. |
| EVA fields such as VAT status, mileage unit, Cover Type, In Use, instruction email, and a fixed six-line address | Potential later mapping evidence. They are not a universal `0.1.0-alpha.1` case form or accepted focused-`0.1.0-alpha.1` export contract. |
| “Vehicle Reg + Principal are the only manual EVA blockers” | Describes predecessor EVA behavior, not Pegasus case-acceptance rules. |
| `Inspect on` defaulting to today | Not adopted. Current authority defaults **Instruction date** when absent and treats the stated inspection/equivalent deadline as `Due by`. |
| `Instructions only`, `Images only`, `Both`, `Merged` as manually configurable case types | Rejected as a field model. Intake origin/association is derived; business work type is separate. |
| Manually selectable initial status | Not adopted. Intake outcome and Core lifecycle policy determine state. |
| Single condensed inbox, `Show dismissed`, mailbox chips, `E-mail type` dropdown, and Suggested Outlook action | Legacy design prompts. `0.1.0-alpha.1` source scope and mailbox-classification/correction policy do not yet authorise this exact control set. |
| Bulk Hold/Release/Log chase, exact per-queue columns, quick-peek drawer, notification centre | Not required by current plans. Reconsider only through a real operator flow and caller. |
| Visible confidence percentages | Not required. Current Pegasus requires suggestion/confirmation distinction and source provenance; it does not require a numerical score in the operator UI. |
| Provider corpus/import administration and last-used statistics | Not part of the bounded QDOS principal/settings slice. |
| Exact predecessor red/amber/nav/table theme rulings | Historical visual input only. Current application-specific UI principles live in `design/`; the CE website/letterhead kit excludes the internal app. |

## Real caller and proof boundary

The intended callers are authenticated Razor Pages for the dashboard, intake workbench, case list/workspace, staff accounts, principal settings, and operational configuration. None currently exists as a production operator surface. `/Intake/Upload` proves only a Development review path and cannot prove these UI requirements.

Implementation evidence must eventually exercise each real authenticated page through the shared Core owners, with operator review of genuine-shaped QDOS data. Legacy screenshots, predecessor deployment records, mockup filler, documentation consistency, and a rendered page without a called Core action are not acceptance evidence.

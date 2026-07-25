# Mailbox categorisation and email matching decision dossier

Status: **Research and operator decision required**

Decision owner and rule author: **Alex**  
Final approver: **Alex**

This dossier keeps the still-open mailbox categorisation and email-matching
questions in one place. It is a decision aid, not an accepted architecture,
implementation plan, or authority to add production behavior. The canonical
blocking entry remains `docs/plans/open-decisions.md` until Alex answers the
questions in this dossier one at a time and the answers are recorded in the
authoritative product material.

## Outcome this decision must enable

CollisionSpike needs one Core-owned policy for decisions that depend on email
content or message relationships. Transport adapters may retrieve and normalise
Outlook data, but they must not each invent their own categorisation or matching
rules.

The policy must eventually cover:

- incoming mailbox categorisation as `Receiving work`, `Queries`, `Other`,
  `Needs sorting`, or the real business `Triage` workflow;
- association of incoming messages and attachments with an existing case or a
  later-created case;
- automatic matching of an exact Outlook Sent item as evidence that a report
  was sent; and
- correction, reversal, and re-evaluation when a prior decision was wrong.

The following behavior is already settled and is not reopened here:

- uncertain or conflicting incoming evidence goes to `Needs sorting`; it must
  never be guessed into a category or case;
- a `Triage` record can be associated automatically only when a definitive
  shared match exists; otherwise staff confirm the association;
- report evidence is an exact item in the Sent Items folder of a mailbox on the
  Administrator-maintained approved-mailbox allowlist;
- when automatic report matching is absent or ambiguous, staff may link an
  exact sent item with an entered reason;
- Outlook `sentDateTime` is the authoritative report-sent time; discovery and
  link times are retained separately;
- any authorised staff member may unlink or relink a mistaken association with
  an entered reason, and derived counts and business events are recomputed;
- a recorded report-sent event remains final if Outlook later moves or deletes
  the source item; and
- CollisionSpike detects report sending. It does not send the report.

## Questions still requiring a product decision

Alex must decide:

1. whether approved rules are code-versioned, Administrator-managed at
   runtime, or use a defined hybrid;
2. the exact incoming-category predicates and acceptable evidence;
3. the exact incoming case-association and sent-report matching predicates;
4. precedence when more than one rule matches, and the conditions that make a
   result ambiguous;
5. who may draft, review, activate, suspend, and roll back a rule version;
6. version identifiers, effective dates, and treatment of messages received
   before or after a rule change;
7. the evidence retained for each decision without storing message bodies or
   secrets in permanent action history;
8. correction, reversal, re-evaluation, and downstream-notification behavior;
9. the acceptance cohort, negative cases, rollout controls, and rollback
   threshold; and
10. whether a runtime-managed approach justifies a new architectural boundary.

## Common requirements for every option

Whichever option is selected must satisfy all of these requirements:

- One named Core policy owner is called by Web and Worker entry points. Graph,
  manual upload, provider API, and later channels provide normalised evidence
  through that owner rather than implementing parallel classifiers.
- A decision records the policy key and version, outcome, material evidence
  references, confidence or ambiguity facts where applicable, actor or
  automated identity, timestamps, and reason for any staff override.
- Rule evaluation is deterministic for the same policy version and evidence.
- A decisive match must be distinguishable from no match, multiple plausible
  matches, incomplete evidence, and technical failure.
- Unsupported and ambiguous inputs remain visible and reversible. They are not
  silently discarded or forced into a convenient outcome.
- Corrections preserve the original decision and append structured before and
  after values to permanent action history. Message bodies, file bodies,
  credentials, tokens, and secrets do not belong in that history.
- Routine polling, retries, leases, and adapter mechanics remain in
  content-safe telemetry rather than permanent action history.
- Rule changes have an explicit activation and rollback process. A change must
  not silently reinterpret historical records unless an approved re-evaluation
  operation says which cohort is affected.
- Security uses least-privilege Graph access and the approved mailbox allowlist.
  This dossier does not grant access to another Inbox or to any Sent Items
  folder.
- Performance must be bounded per message, observable, retry-safe, and suitable
  for the expected mailbox volume without making case creation depend on an
  unbounded external call.
- Acceptance tests include decisive positive cases, ambiguity, conflicts,
  missing evidence, duplicate delivery, retry, correction, rollback, and
  evidence that unsupported paths remain in `Needs sorting`.

## Option comparison

| Option | Rule source and activation | Main benefit | Main downside | Architectural consequence |
|---|---|---|---|---|
| A. Code-versioned Core policy | Developers implement reviewed rules in Core; deployment activates a version | Smallest first-release surface, compiler-reviewed behavior, straightforward source control and rollback | Every rule change requires engineering work and deployment; Administrator independence is limited | Fits the accepted modular-monolith boundary without a new runtime authoring system |
| B. Administrator-managed runtime policy | Administrators author and activate persisted rules through CollisionSpike | Operational changes need no deployment and can respond quickly to mailbox variation | Requires a safe authoring model, validation, permissions, storage, preview, versioning, rollback, and support for malformed or conflicting rules | A runtime evaluator and management surface are a material new boundary and require an ADR before implementation |
| C. Defined hybrid | Code owns predicates and safety invariants; Administrators select or parameterise a constrained approved policy version | Preserves hard safety rules while allowing bounded operational changes | The code/config dividing line can become confusing and migration-heavy if it is not explicit | May fit the monolith, but any runtime expression model or general evaluator still requires an ADR |

### Option A: code-versioned Core policy

Authoring and approval would occur through normal repository review, with Alex
as rule author and final product approver. Each policy version would be an
explicit Core contract or immutable data definition released with tested code.
Activation and rollback would use the application release process.

Benefits:

- the least new administration, storage, security, and support surface;
- precise reviews of predicates, precedence, and failure behavior;
- easy pairing of a rule change with its positive and negative fixtures; and
- reliable reproduction of a historical result from a source revision and
  policy version.

Downsides and risks:

- small operational changes wait for engineering and deployment;
- urgent rollback is coupled to the application release process;
- administrators cannot safely preview or schedule a change themselves; and
- frequent provider-specific exceptions could pressure the policy into a large
  conditional block unless its business concepts stay narrow.

Migration to a later runtime or hybrid option would require translating active
code rules into an approved persisted representation, mapping historical policy
versions, and proving equivalent results over the accepted cohort.

### Option B: Administrator-managed runtime policy

Administrators would create drafts, preview them against a fixed evidence
cohort, obtain Alex's approval, and activate a version with an effective time.
The application would need explicit author, reviewer, approver, activation,
suspension, and rollback actions with permanent action history.

Benefits:

- approved changes can be made without deploying application code;
- scheduled activation and rapid rollback can be first-class operations; and
- operational ownership is visible within the product.

Downsides and risks:

- a general rule language is difficult to make safe, understandable, and
  deterministic;
- malformed, overly broad, or conflicting rules could create or associate the
  wrong case at mailbox speed;
- the editor, evaluator, schema, preview environment, and support model are a
  large first-release cost;
- runtime access becomes a high-impact privilege and increases the security
  review surface; and
- query cost and latency can become unpredictable if arbitrary expressions or
  external calls are allowed.

This option needs research into a constrained predicate model, validation,
transactional activation, caching, version pinning, preview isolation, recovery
from an unusable active version, and database migration. It must not begin with
a generic expression engine or rule table inferred from this dossier.

### Option C: defined hybrid

Core code would own the finite evidence predicates, safety invariants,
precedence algorithm, and conservative outcomes. Administrators could choose
only from specifically approved parameters or named policy profiles. Alex would
author and approve the permitted operational choices and their bounds.

Benefits:

- code protects no-guessing, definitive-match, and ambiguity behavior;
- bounded operational variation can avoid a deployment; and
- the runtime surface can be smaller and easier to validate than a general rule
  language.

Downsides and risks:

- an unclear boundary between code and runtime parameters can make results hard
  to explain or reproduce;
- each new parameter can become a disguised feature flag or provider-specific
  exception;
- code and stored policy versions require coordinated migrations and rollback;
  and
- administrators may still need engineering work for any new predicate.

The hybrid must define its finite parameter vocabulary before schema or UI work.
If the vocabulary becomes an expression language, Option C has acquired the
cost and architecture risk of Option B and needs the same ADR.

## Predicate and precedence research

The next research pass must describe candidate predicates using stable business
evidence, not Graph transport details. At minimum it must investigate:

- authenticated mailbox and sender identity, approved principal instructions,
  recipients, conversation and reply-chain identities, stable Graph item and
  internet-message identifiers, subject and attachment evidence, provider
  reference, Case/PO, vehicle registration, claimant, claim number, dates, and
  prior confirmed associations;
- which evidence can establish `Receiving work`, `Queries`, `Other`, real
  `Triage`, or `Needs sorting` without guessing;
- which shared identifiers are definitive enough to associate an incoming item
  with exactly one case;
- which combination proves that an outbound sent item is the report for exactly
  one case, including reply/forward chains, attachments, recipients, Case/PO,
  and mailbox identity;
- explicit exclusions that prevent chasers, queries, corrections, internal
  forwards, and unrelated attachments from being counted as reports; and
- conflict and ambiguity handling when evidence points to different categories
  or cases.

Precedence must be explicit and testable. A future decision must say whether
rules use ordered first-match, mutually exclusive predicates, scored evidence,
or another finite model. Ties, partial evidence, and contradictory high-value
identifiers need named conservative outcomes. No scoring threshold should be
invented without an operator-reviewed evidence cohort.

## Versioning, evidence, and effective dates

The selected option must define:

- a stable policy key plus immutable version identifier;
- draft, approved, active, suspended, superseded, and rolled-back lifecycle
  meanings if runtime state exists;
- whether the applicable version is chosen by durable receipt time, first
  evaluation time, or another approved business time;
- how delayed delivery and replay retain the originally applicable version;
- whether a correction applies only to one record or triggers a deliberately
  selected cohort re-evaluation;
- evidence references sufficient to explain a result without copying message or
  file bodies into permanent action history; and
- how a historical result remains explainable after a rule, mailbox item, or
  external folder is removed.

Rollback must activate a known prior version atomically and leave the failed
version and affected decisions traceable. It must not automatically undo cases,
references, or external side effects. Those require explicit, reasoned business
correction paths; issued references are never reused.

## Correction and reversal behavior

The research must distinguish:

- correcting only the displayed category;
- re-running the same evidence under a new policy version;
- linking or unlinking an incoming item, attachment, Triage record, or sent item;
- reversing an automated match before it causes case creation;
- correcting an association after a case or report event exists; and
- a technical replay that must remain idempotent and must not count as a new
  business decision.

Each mutation needs authorisation, an entered reason where it overrides or
reverses a prior decision, structured before/after values, actor, event time,
outcome, and policy/evidence references in permanent action history. Recomputed
dashboard counts and derived events must be deterministic. A correction must not
delete the original history or silently reuse a case reference.

## Test, rollout, and operational evidence

Before Alex accepts an option, the decision package needs:

- an operator-reviewed, immutable cohort of genuine local examples for each
  category and matching path, plus a separate untouched holdout;
- negative cases for ambiguity, conflicting identifiers, misleading subjects,
  forwards, duplicated attachments, repeated deliveries, and wrong sent-item
  candidates;
- an explicit expected result and evidence explanation for every example;
- offline replay against pinned policy versions without changing production
  state or the immutable corpus;
- performance measurements at expected and burst mailbox volumes;
- failure behavior for Graph throttling, missing permissions, deleted or moved
  messages, stale identifiers, database failure, and partial downstream work;
- observability for decision outcomes and technical failures without message
  bodies or secrets in logs;
- staged activation criteria, a monitored rollback threshold, and a recovery
  exercise; and
- migration proof if active rules or policy versions move between code and
  persisted configuration.

Repository consistency tests are separate from behavior proof. A registered
policy with no Worker or Web caller is unfinished.

## Security, cost, and performance considerations

- The approved-mailbox allowlist is business configuration maintained by an
  Administrator. It does not itself grant Graph access.
- Reading Sent Items is a separate permission and data-access boundary from the
  currently planned shared Inbox. No Graph scope expansion is authorised here.
- Individual staff mailboxes create consent, least-privilege, departure,
  monitoring, and privacy questions that must be resolved before access.
- Runtime policy administration would be a high-impact permission. Drafting,
  approval, activation, and rollback may need separation even though Alex is
  the final product approver.
- Content used for evaluation must be bounded. External model calls, arbitrary
  code, regex without limits, and unbounded attachment inspection are not safe
  default predicates.
- Cache design must pin a complete immutable policy version and invalidate it
  atomically. Nodes must not evaluate the same message under different active
  versions.
- Operational cost includes support time, false-positive correction, Graph
  calls, storage of evidence metadata and versions, database reads, preview
  runs, telemetry, and release/rollback effort—not only Azure resource price.

## Deferred-capability impact

This decision could constrain several named deferrals. The future seams and
remaining migrations must therefore be explicit without building dormant
components now.

- **Future mailbox coverage:** stable mailbox and message identities plus the
  allowlist permit later shared or individual mailbox coverage. Each new scope
  still needs product approval, privacy review, Graph permissions, deployment,
  and evidence that its messages fit the policy.
- **WhatsApp coexistence and later automation:** channel-neutral source and
  conversation identities can feed the same Core association concepts later.
  No WhatsApp ingestion, account, webhook, message store, or matching rules are
  built now.
- **EVA API use or replacement:** `Sent to Engineer` remains a stable business
  event. The first release uses first successful EVA JSON/image export
  generation as an explicit proxy that does not prove EVA receipt. A future EVA
  replacement must migrate the producer to the actual Engineer-assignment event
  without changing dashboard meaning.
- **Estimating, valuation, invoicing, Diminution, and Commercial cases:** stable
  case and document identities can support later message association, but no
  category, matcher, schema, or workflow is added for them now. Each needs an
  accepted product slice and evidence before extending policy vocabulary.
- **Guided claimant/mobile capture:** stable submission, source, and case-link
  identities preserve a later adapter seam. No mobile channel or guided-capture
  rules are created now.
- **AI and vision assistance:** retained source evidence can support a future
  reviewed suggestion, but AI output must not silently become a definitive
  category or match. Model choice, evaluation, cost, privacy, and operator
  approval remain future decisions.
- **External/customer accounts:** actor and source identities can later
  distinguish external submissions. No external role, mailbox access, or policy
  authoring right is added now.
- **Malware scanning:** message and attachment identities allow a future scan
  result to gate processing. No scanner, quarantine state, service, or release
  requirement is added now.
- **Later infrastructure options:** the policy remains inside the approved
  modular monolith until measured scale or reliability evidence justifies
  another boundary. No queue, service, data store, private network, region,
  deployment slot, or environment is created for this research.

The irreversible boundary is case identity: an issued reference is never
reassigned or reused. If a wrong definitive match caused allocation, the
erroneous original follows the approved `Created in error` replacement path;
rule rollback cannot rewrite it.

## Recommendation for the decision conversation

The recommended direction is a **phased, constrained hybrid**: begin with
code-versioned Core predicates, precedence, and safety invariants; allow runtime
selection only from explicitly approved, finite parameters once the required
authoring, preview, versioning, security, and rollback evidence exists. This
keeps the first usable release small while preserving a deliberate route to
Administrator-managed changes. It is a research recommendation only. It does
not approve a schema, editor, evaluator, configuration key, or production code.

If the required operational changes cannot be expressed through a small finite
parameter vocabulary, prefer Option A until Option B has a reviewed ADR and its
cost and safety case are accepted. Do not let a nominal hybrid become an
unreviewed general rule language.

## One-by-one operator decision sequence

In a future decision session, use the ask-user-question interaction for exactly
one decision at a time. Do not batch the questions or infer later answers from
an earlier choice. The sequence should be:

1. Select Option A, B, or C after reviewing the comparison and recommendation.
2. Approve the author, reviewer, activator, rollback operator, and final
   approver roles.
3. Decide the policy-version and effective-time rule.
4. Decide incoming category predicates and evidence, one category at a time.
5. Decide precedence and ambiguity behavior.
6. Decide incoming case-association predicates.
7. Decide automatic sent-report predicates and exclusions.
8. Decide correction, reversal, and cohort re-evaluation behavior.
9. Approve the acceptance cohort, holdout, rollout, and rollback thresholds.
10. Confirm the Graph mailbox/folder scope in a separate security decision.

Record each accepted answer in `PROJECT_DISCOVERY_QUESTIONNAIRE.md` and narrow
the canonical open-decision entry as it is settled. If Alex selects a runtime
evaluator, general expression model, or another architectural boundary, produce
and review an ADR before implementation. Only after the complete policy is
accepted should a thin implementation plan identify the real Web and Worker
callers, persistence changes, migrations, failure behavior, observability,
tests, extension seam, and any classifier it replaces.


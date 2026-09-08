# Research — INTK-066: manual destination confirmation

## Question

How can staff confirm a viable existing Case or an editable extracted new-Case
proposal directly from manual upload without premature association or Case/PO?

## Findings

- Operator decisions on 8 September require direct searchable viable Case
  selection, only viable options, confirmation even for one match, and a
  new-Case proposal when no viable Case exists. Staff can alter the extracted
  details, accept or reject; Case/PO is allocated only on acceptance.
- Source baseline is origin/dev 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c.
  Shared checkout has foreign Principal-document moves and is not an execution
  location. Read-only Git object inspection established all source facts.
- FRD-02 Matching conflicts and Upload confirmation, and FRD-12 Upload, still
  describe channel-agnostic automatic association/per-file decisions. The
  operator explicitly changes manual confirmation; reconcile those affected
  clauses without changing non-manual email/provider allocation or full-v1
  deferred lifecycle requirements. INTK-050 is the separate general grouped-doc
  owner; preserve its history, link any now-obsolete affected clause.
- Upload.cshtml.cs stages a ManualUpload source and returns Status or Group.
  UploadOutcomeQueries currently reports CurrentCaseId before any decision and
  gives open Unidentified precedence over its attach offer, reproducing the
  two retained SQL/browser failures. The status page must carry a real decision,
  not invent a successful prior association.
- ProcessQueuedIntake.AssociateCaseIfUnambiguousAsync runs on first and replay
  processing; AllocateIntake.AttemptAutomaticAsync separately allocates; both
  currently accept ManualUpload. ImageIntakeCasePairing also automatically links
  registered images through its first pass and recovery sweep. All three must
  withhold automatic Case destination for ManualUpload until staff acceptance.
  Keep extraction, custody, source identity, image reference registration,
  unrelated mailbox/provider automation and supported recovery intact.
- Existing production mutation owner is ILinkIntake / LinkIntake in
  DurableIntake.cs, backed by EfIntakeMutationStore.LinkAsync and ExecuteAsync.
  It validates staff rights, reason, operation key, receipt version, Case
  version, edit lease, archive/terminal state and registered-image eligibility;
  Serializable transaction records association and immutable history.
  ReconcileUnidentifiedDestinations is the existing eventual resolution owner.
- UploadCaseDecision already orchestrates ISearchCases, IGetCase, lease and
  ILinkIntake. It lacks route/member binding, receipt-scoped viable suggestions,
  submitted-version binding, and honest same-decision retry comparison.
  Group attachment silently skips missing members. No second link/allocator,
  workflow framework, queue, schema or dependency is justified.
- ISearchCases returns broad CaseSearchItem and cannot express all receipt/image
  eligibility (including report-sent evidence). A small Core destination query
  port implemented in existing persistence scope is justified by the real
  status/suggestion/submit callers. Share Core eligibility with transaction-time
  write checks rather than duplicate state strings in Web or SQL policy.
- Cases/Create is the existing extracted-details proposal and one-button
  correction/address/allocation chain with expected receipt version, operation
  identity and preserved input on failure. It has no proposed Case/PO. Its
  blanket Audit refusal must not strand a retained classified Audit after
  automatic ManualUpload allocation is withheld: support acceptance only of
  genuinely classified retained Audit evidence, not arbitrary hand-keyed Audit
  or deferred full-v1 Audit classification changes.
- Existing Razor native disclosure and progressive case-search combobox in
  site.js are reusable. Search currently turns HTTP failure into empty results
  and can render an older in-flight result after input shortens; distinguish
  failure from no matches and invalidate stale requests immediately. Preserve
  keyboard semantics, reasons and selected values on recoverable form errors.
- Tests: UploadConfirmationWebTests and UploadCaseSearchBrowserTests cover the
  real route; UploadOutcomeQueriesTests covers the projection. Core allocation
  and image-pairing tests own channel guards. CaseCreateWebTests owns editable
  proposals. Shared seed helpers that need accepted Cases must explicitly
  accept through an existing path, not silently restore automatic ManualUpload
  behavior or rewrite genuine manual tests as mailbox tests.
- Snapshot owner scopes are upload-status, upload-group-status and case-create
  when its Razor changes. Use actual catalogue names and owning real capture
  cohorts, not a default broad all-Web capture. Existing helper always appends
  TestUiFocusedRenderTests; account for that in the verification plan.
- No project sources are declared (get_sources returned zero). No tests, build,
  cloud operation or source edit was run during this research.

## Implications

One coherent manual-confirmation feature spans its automatic entry blockers,
existing staff mutation, scoped query, Razor and tests. Keep extraction and
original bytes; explicit staff acceptance alone may allocate a new Case/PO.
Only supported viable destinations are shown, then rechecked when submitting.
An active competing lease is a surfaced conflict, not authority to steal it.
Rejection/cancel declines the proposal and retains the unallocated source for
later handling; it is not deletion or a fabricated permanent Case outcome.

## Open questions

The operator settled both destination and new-Case decisions. No outstanding
product choice is needed for this bounded implementation. Final source/detail
differences must be surfaced against the packet rather than patched silently.

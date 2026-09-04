# Research — CASE-045 (2026-09-04, gpt-5.6-terra medium)


## Verified findings

`ImageIntakeRecord` does **not** carry a principal id. It carries its origin,
registration, Image Intake Reference, lifecycle state, and merge fields only
([ImageIntakeContracts.cs:18-31](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs:18)).
`ImageIntakeSummary` similarly exposes only an optional associated Case id and
reference, not a principal ([ImageIntakeContracts.cs:100-109](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs:100)).

The existing canonical relationship is therefore:

`ImageIntake.OriginReceiptId` → active `IntakeManualAssociation` or
`CaseIntakeLink` → `Case` → `Case.PrincipalId` → `Principal.Code`.

`EfImageIntakeStore.ProjectAsync` currently resolves the associated Case from
the origin receipt and projects only that Case's id/reference
([EfImageIntakeStore.cs:857-920](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs:857)).
A formal Case has required `PrincipalId` and a `Principal` navigation
([PegasusDbContext.cs:1119-1124](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:1119)).
The existing case-search projection joins that relationship and uses
`principal.Code` as the operator value
([EfCaseQueryStore.cs:230-245](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs:230)).

Today `/Cases` loads unassociated image records through
`IImageIntakeQueries.ListAsync(false, ...)`
([Index.cshtml.cs:372-414](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:372)).
It builds each image row with `ImageRow`, including a per-row `ListImagesAsync`
count call ([Index.cshtml.cs:409-413](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:409)).
The current row has lifecycle state, retained-image count, received date, and
State/Registered/Chase quick-view facts; it has no principal fact
([Index.cshtml.cs:543-558](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:543)).

The standalone Image Intake detail page has a “Case association” fact, but no
principal fact ([Details.cshtml:69-106](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Web/Pages/ImageIntake/Details.cshtml:69)).
Its page model gets `ImageIntakeDetail` and, only when unassociated, finds
eligible Cases by registration ([Details.cshtml.cs:26-45](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs:26)).
That candidate lookup must not be repurposed as principal inference.

Formal-case display convention is the literal label `Principal` with the
canonical principal code as its value, for example in the Case detail ribbon
([Details.cshtml:114-120](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Web/Pages/Cases/Details.cshtml:114))
and case-summary definition list
([_CaseSummary.cshtml:43-47](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml:43)).
There is no `OperatorLabels` mapping for principal codes; Core owns the
relationship/value and Web renders it directly. `OperatorLabels` owns
presentation labels for state vocabulary, including image custody/lifecycle,
not principal identity.

The intake boundary is explicit: `IntakeDecisionPolicy.CanBecomeCase` returns
false for `ImageIntakeRegistered`
([IntakeDecisionPolicy.cs:16-40](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/src/Pegasus.Core/Intake/IntakeDecisionPolicy.cs:16)).
FRD-02 says image-only material is not a formal Case/PO and an
Image-initiated Case stays Awaiting instruction until it can associate with one
eligible instructed Case ([frd-02-intake-and-source-identity.md:172-176](/C:/Users/PC/Documents/GitHub/pegasus/.worktrees/research/docs/frd/frd-02-intake-and-source-identity.md:172)).
No CASE-045 change may alter that policy or use matching candidates to invent a
principal.

## CASE-032 branch delta, verified

I fetched `origin/task/case-032-queue-row-projections` and compared it with
`origin/dev`.

On `origin/dev`, `ImageIntakeSummary` has no custody, source, image-count, or
principal field. CASE-032 adds `ImageCustodyState? Custody` to the summary and
detail projection, reads `ImageIntakeEntity.CustodyState`, and adds
`OperatorLabels.ImageCustodyState`. It does **not** add a source, aggregate
image count, principal, principal id, or Case-to-Principal join.

Its `/Cases` delta keeps the existing `ImageRow(ImageIntakeSummary, int
fileCount)` shape, appending custody to the subtitle and quick-view facts. It
does not add a principal fact. CASE-032's changed files are the projection
contract/store, Case queue row builder, custody labels, and
`TriageQueuesWebTests`; its branch also changes unrelated triage-row
projection work.

## CASE-042 intended end state, supplied packet

The supplied CASE-042 plan defines `/Cases?tab=awaiting` as the Pre-Case
Awaiting instruction queue. It removes image rows from Not ready and builds
`ImageRow` rows from `ListAsync(false, ...)`, with a separate quick-detail
shape. It requires image count, custody, Received (`RegisteredAtUtc`), Source,
and Chase; it explicitly forbids a Vehicle fact and removes Create Case in
favour of “Add to an existing case” only.

Its packet correctly notes that CASE-032's present branch supplies custody
only. Count and source must either be added to CASE-032 before CASE-042 lands,
or CASE-042 must add them in its own projection delta. CASE-045 must extend
that post-CASE-042 row/quick-detail shape rather than restore the Not ready
implementation or create a parallel projection.

The CASE-042 packet says `OperatorLabels.cs` was withdrawn from its own file
list: Awaiting instruction is an inline tab literal. This conflicts with the
approximate owned-path note in CASE-045 that says CASE-042 edits
`OperatorLabels.cs`; the checked supplied CASE-042 packet is the more specific
evidence. CASE-032 does edit that shared label file for custody.

## What is missing

No image-intake projection currently exposes a principal value. More
importantly, the only verified canonical principal relationship is through an
associated formal Case, while CASE-042's Awaiting instruction queue is defined
as unassociated image records. Under that predicate, the canonical association
is null, so a derived principal would also be null.

A display-only implementation can extend `ImageIntakeSummary`, the
`EfImageIntakeStore` projection, and CASE-042's `ImageRow` quick-view facts
with an optional principal code only if the agreed “recorded principal” is
available through a supported canonical relationship. Current evidence does
not establish such a relationship for an unassociated Awaiting record.

**Note (Sonnet wrapper, not from the researcher):** the ticket body's Approach
section and EPIC-012 `decisions/d51-image-initiated-principal.md` already
specify the answer this section treats as an open question: storage is one
nullable `PrincipalId` column added directly to `ImageIntakeEntity` (a new
stored field, not a derivation through the Case association), written by
staff on the detail page and optionally by a future principal-authenticated
intake route. The researcher's "what is missing" / risk / question below were
produced without fully reconciling that controller fact — they read as if a
new stored column were still undecided. The plan should treat D51 and the
ticket Approach as settled and not re-open whether to add the column; the
researcher's evidence about the *absence* of any such column or relationship
today is otherwise sound and directly supports doing so.

## Risks

- The intended queue predicate may make the optional value permanently absent:
  associated image intakes leave Awaiting instruction through the normal merge
  transition.
- Joining candidate Cases by registration would fabricate/infer a principal and
  violate the ticket and FRD-02.
- Adding a stored principal field to `ImageIntake` would be a schema and
  ownership change, contrary to the “no migration expected” condition unless
  explicitly authorised. (See wrapper note above: D51/the ticket Approach
  already authorise exactly this.)
- CASE-032 is still in review and supplies only custody. CASE-042 is not
  started and owns the tab/row boundary. CASE-045 must rebase its file map on
  their merged exact heads before implementation.
- `Pages/Cases/Index.cshtml(.cs)` is a shared ordered-merge path. CASE-045 is a
  delta after CASE-042, not a concurrent rewrite of its older Not ready shape.

## Reuse candidates

- Record/contract: `ImageIntakeSummary` in
  `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`; extend it with an
  optional projected display value only after the relationship question is
  resolved.
- Query: `EfImageIntakeStore.ProjectAsync`; extend its existing bulk projection
  and Case-reference lookup. Do not introduce a per-row query or principal
  matcher.
- Row builder: CASE-042's post-merge `ImageRow` and its `QueueRow` quick-view
  facts in `Pages/Cases/Index.cshtml.cs`.
- Display: the existing literal `Principal` / canonical code convention in Case
  details; no `OperatorLabels` member fits or is needed.
- Tests: extend `TriageQueuesWebTests` for the Awaiting tab/quick view, and
  `ImageIntakeWebTests` only if the resolved display location remains the
  Image Intake detail page.

## Premises

Verified: all repository source, FRD, CASE-032 fetched diff, and the supplied
CASE-042 packet described above.

Assumed from controller facts: CASE-032 and CASE-042 merge before CASE-045;
D38/D50 and the ordered EPIC-012 merge queue remain binding; CASE-042's plan
will land materially as supplied. These are not claims about the current
`origin/dev` checkout.

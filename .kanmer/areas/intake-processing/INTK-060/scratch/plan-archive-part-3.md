# Archived original plan — part 3 of 4

Original ticket document: `plan/plan.md`
Original SHA-256: `62649b22a7e43d771820d36c4126a65867fc38d99b636c54a20cc5a6468f3a95`
Character range: 60000–89999 of 115556
Reconstruction: concatenate the payload sections from parts 1–4 in order.

## Payload

blank placeholders, repairer, hire and rates. No sender domain is currently accepted. | Five PDFs emit policyholder/claim/registration/make-model/date/circumstances plus explicit role-scoped optional fields. Blanks remain unavailable; international values preserve raw form; document match alone returns no automatic route activation. |
| [BLACK](../../reports/principals/BLACK/method.md) | Parse combined Vehicle text with a terminal validated registration; keep claimant address role. Never split on an arbitrary hyphen or fixed line. | Five PDFs preserve combined raw vehicle text and emit VRM only when terminal form validates. Make/model is emitted only where its boundary is proved; address remains claimant/location evidence, not company footer. |
| [RJS](../../reports/principals/RJS/method.md) | Match the exact `Client vehicle registration` family; allow make=`none` and a separately present model; preserve claimant contact/address roles. | Five DOC originals emit client, solicitor reference, VRM and available contact/location. Literal `none` remains an explicit source value/absence state; model may survive independently; no fixed header offsets. |
| [DFD](../../reports/principals/DFD/method.md) | Read the PDF form-field relationships (`Text3`-`Text17`) and keep `Date instructed`, accident date and `Your Reference` roles explicit. | Five PDFs emit only fields whose form geometry proves their label/value association, each with form-field/page locator. Date roles never swap. If original geometry is unavailable, the affected field is ambiguous/unavailable. |
| [KBS](../../reports/principals/KBS/method.md) | Normalize curly/straight apostrophes and whitespace; use explicit client/vehicle/location/contact blocks. A make-only vehicle is genuine absence of model. | Five DOCX/PDF originals emit client/reference/registration/make and scoped location/contact; missing model and VAT stay unavailable; today's date is never emitted. |
| [MP](../../reports/principals/MP/method.md) | Support PDF, Word and affected-page OCR variants. Scope `Our Vehicle` person-only forms. Distinguish `Instruction` from `Inspection`; reject malformed report years. | Eleven originals emit role-correct client/reference/VRM/date/location where proved, preserve scan locators, and withhold malformed dates. Requested inspection never becomes completed inspection. |
| [YML](../../reports/principals/YML/method.md) | Route the confirmed HDUK-branded instruction family to principal YML while preserving issuer HDUK. No generic alias layer or unproved sender-domain activation. | Five HDUK PDFs emit YML as principal candidate and HDUK as document issuer, plus client/reference/VRM/vehicle/date/location/circumstances. The two identities remain separately queryable and source-linked. |
| [ALS](../../reports/principals/ALS/method.md) | Preserve paired client/third-party columns, owner versus client, garage/repairer evidence and explicit `VAT Registered`. Source vehicle classes are not salvage categories. | Five DOC originals emit the correct party-column values, explicit VAT state, reference/VRM/model/date/location/circumstances and repairer. Column swaps, footer addresses and vehicle-class-as-salvage are negative assertions. |
| [BC](../../reports/principals/BC/method.md) | Recognize the unlabelled RTA header token in context rather than requiring `Our Ref`; support desktop/physical request variants; allow a blank inspection address. | Five DOC originals emit client, contextual reference, terminal VRM, incident/instruction dates and available address/narrative. Blank address stays unavailable, physical request remains requested method, and Baker & Coleman spelling alone does not activate a route. |

For every sample, serialize expected candidates as field, normalized value,
raw value, role, document role, source hash, occurrence, page/cell/form/region,
policy key/version and disposition. Include negative assertions for neighboring
party/address/date labels and unchanged confirmed values. Keep the original
five/eleven samples as development evidence; create holdouts only from new
genuine, operator-labelled originals. Do not relabel a design sample as an
untouched holdout.

**Production wiring:** replace the single injected extraction policy with the
smallest Core-owned document-profile selection that reuses
`IInstructionExtractionPolicy` and the existing evidence-state registry.
`AnalyzeRetainedInstruction` is the caller for all fifteen profiles and selects
by versioned document signature and role independently of allocation authority;
accepted route identity is corroborating evidence, never a prerequisite or a
fact inferred from the document. `ProcessIntake` keeps QDOS automatic allocation
and may allocate another profile only after independently accepted route or
staff-confirmed principal evidence. Multiple document profiles remain
ambiguous. Stream A performs the post-C03 DI patch only after the concrete
classes exist and records the C head and patch hash; no reflection, discovery,
stub or no-op registration hook is introduced.

Implement INTK-049 in the same Core profile path. A machine-read VRM from only
document OCR or vehicle-image recognition expands through one finite map,
`O` <-> `0` and `I` <-> `1`, into at most eight distinct stable-order candidates
that match GB current/prefix/suffix/dateless or Northern Ireland syntax. The
valid original comes first. `VehicleRegistrationCandidateLookup` delegates each
candidate to B's existing `IVehicleLookupAdapter`; it does not call DVLA/DVSA or
create another lookup client. Accept exactly one `Current`, `Stale` or `Partial`
candidate only after every other attempt is conclusively `NotFound`. Zero or
multiple viable results and any throttled/unavailable/failed unresolved attempt
remain ambiguous. Preserve raw reading plus every request/result/provenance;
never reinterpret staff-confirmed, ordinary embedded-text or Case-search input.

**Focused commands and expected result:**

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~InstructionExtraction|FullyQualifiedName~MailRoute|FullyQualifiedName~MailClassification"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Corpus|FullyQualifiedName~PrincipalIdentificationCorpusEvidenceTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RetainedInstructionAnalysisTests|FullyQualifiedName~MachineReadRegistrationLookupTests"
```

Both commands exit 0. The result report contains a per-profile/per-field matrix,
ambiguity and missing counts, no unresolved evidence reference, and no claimed
accuracy threshold without operator-labelled holdouts. Stop on any activation
whose only support is E01-E28, a domain-only principal choice, or a second list
of principal codes.

## C04 - QDOS attachment Triage and fail-closed allocation

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements.

Use the exact local deltas and source limitations in
[QDOS_IDENTIFICATION_AND_FIELDS](../../../more_docs/QDOS_IDENTIFICATION_AND_FIELDS.md)
alongside the accepted QDOS method and current policy code. E01-E28-only
examples remain unavailable evidence under C01.

**Production callers:** `ProcessIntake` calls `QdosMailRoutePolicy`,
`QdosMailClassificationPolicy` and the selected extraction profile; the
existing Triage registration path allocates Triage; normal case allocation
continues through the existing Case allocator.

**Change:**

1. Add the locally evidenced attachment `Triage Only Request` predicate without
   broad body-keyword matching. The current message body and subject predicates
   remain supported.
2. Collapse byte-identical or proven format-equivalent PDF/DOC renderings into
   one category candidate while retaining both occurrences. Distinct plain and
   combined notification categories remain ambiguous; never invent a winner.
3. Route a definitive triage request to the global Triage sequence
   `T-00001`, `T-00002`, ... with no yearly/principal reset or reuse. It never
   allocates a normal Case/PO. A later formal instruction uses the current Case
   allocator and links the Triage.
4. Remove QDOS damage concatenation into accident circumstances and unsupported
   first-token make/model inference. Use C03's distinct candidates and preserve
   the full vehicle description.
5. Keep the accepted QDOS Image Based Assessment principal default even when
   extraction finds a repairer address. Staff may choose repairer location only
   through the existing address-resolution decision with expected version and
   a required reason; method and location remain independent.

**Tests and expected outputs:** empty-current-body EREF subject plus duplicate
PDF/DOC triage letters yields one Triage and no Case/PO; replay returns the same
T reference. Quoted-only triage text does not match. Plain plus combined
distinct notification evidence is ambiguous. Formal instruction after Triage
allocates one normal Case and links it without consuming/reusing the T number.
Repairer-address extraction leaves effective location `Image Based Assessment`
until a reasoned staff override; overriding does not assert Physical inspection.

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~QdosMailClassificationPolicyTests|FullyQualifiedName~DefinitiveIntakeCaseTypeTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~QdosTriageIntegrationTests|FullyQualifiedName~TriageFromIntakeIntegrationTests"
```

Stop if any pre-case path allocates a Case/PO, if T references can reset/reuse,
or if an address changes inspection method implicitly.

## C05 - extract third-party reports as source evidence

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements.

Use the [report extraction review](../../reports/third-party-report-extraction.md),
[source inventory](../../reports/third-party-source-inventory.json) and
[candidate checks](../../reports/third-party-candidate-extractions.json). The
29 PDFs are design and regression evidence, not operator-accepted Case facts.

**Production callers:** intake retention and source reader identify document
role; the new Core-owned issuer/profile selector emits source candidates; the
existing Case provenance/chip projection displays them. Stream B owns commands
that accept estimate, valuation, assessment or report facts and owns the final
Pegasus report snapshot.

**Change and family cases:**

- Connexus: preserve amendment/base relationship, header date versus later
  comment date, initial labour £2,394.25 and agreed labour £3,351.95; reconcile
  £5,119.92 net/£1,023.98 VAT/£6,143.90 gross without conflating retail, trade,
  mid and reserve.
- Exclusive EREHR: preserve issuer and reference roles; reconcile page-one
  excluding-VAT with page-two breakdown/gross rather than flagging duplicates.
- EVA bodyshop: classify by issuer/report evidence, not generic EVA layout;
  preserve `Supp1`, repeated text and appended image-page relationships.
- Laird: the Supplementary heading controls; link the base report and do not
  fill omitted fields from an unrelated document.
- Montgomery: preserve the printed 26.2 x £90 contradiction with printed
  labour £1,582.20 while retaining the net/VAT/gross values that reconcile;
  keep model and odometer conflicts distinct.
- sPrint: keep ordinary zero totals and £8,250 contract repair as different
  amount roles; neither is selected by position.
- John R Bell: OCR only affected pages and preserve agreed/revised amounts and
  scan locators; human verification is required for critical low-quality OCR.
- GG/Audatex, MotorCheck, TonBridge invoice and EVA image-only PDFs are negative
  report cases. Route them respectively to the existing estimate, vehicle
  history, invoice evidence and image evidence paths; emit no invented report
  verdict.

The typed C-B02 projection is a strict superset of C-B01. It enumerates issuer;
engineer name/qualifications; report and claim references with roles; report
date; revision/amendment/supplement and related base report; VRM,
make/model/variant, VIN, mileage/unit; claimant; accident and inspection dates;
repairer/location; current outcome, repairability, roadworthiness/reason,
damage zones, severity/narrative, prior damage, tyres, restraints and airbags;
labour hours/rate/amount, paint/materials, parts, special/additional charges,
discounts, net, VAT rate/amount and gross, each with initial/claimed,
assessed/agreed, revised/supplement and contract-repair roles; valuation
guide/date, trade/retail/mid/PAV, mileage/condition adjustments and final value;
salvage category/value/bid, excess, reserve, cash-in-lieu and deductions; repair
duration/range; requested and observed inspection method; comments, supplement
reason and declaration/signatory; and photo/diagram roles.

Every item carries logical source document/version, immutable SHA-256,
occurrence, page/region/table-cell/form-field locator, raw text/value/unit/
currency, normalized candidate, field/party/version role, reader/profile
version and usable/missing/ambiguous/conflicting validation. Arithmetic or
cross-field reconciliation is a separate finding and never changes source
text/value. C does not convert a source candidate into a CE conclusion; B's
existing command makes any accepted Engineer decision.

**Tests and expected outputs:** all 29 originals classify to the recorded family
or explicit negative role; unknown/multiple issuers are ambiguous; supplements
link only to a proved base; replay is deterministic; confirmed CE facts remain
unchanged. End-to-end upload/mail retention -> profile -> candidates -> source
chips works. B's acceptance command receives a frozen source candidate, while
the final report remains blocked/stale according to B's own rules.

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ThirdPartyReport"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ThirdPartyReport|FullyQualifiedName~MultiFormatGenuineCorpusWebTests"
```

Stop if issuer is inferred from principal/folder, a negative file becomes a
report, source arithmetic is silently repaired, or C writes an Engineer value.

## C06 - current principal, organization and address directory

**Owner/model:** Fable 5.1 orchestrates; Sonnet 5 implements after A freezes the
schema and DI hooks.

**Production callers:** existing Organization/Principal administration Core
queries/stores and `/Administration/Organizations` and
`/Administration/Principals`; intake route selection reads the existing
provider reference catalog; `/Administration/ClaimSources` maintains the local
directory; the Case location picker consumes C's
`IInspectionAddressSuggestionQueries` through Stream B; accepted choices use
the existing address policy and resolution store. All mutations require
Administrator authorization, expected version, actor/time/reason and
idempotency.

**Change:**

1. Seed the current top-15 principal identities and only locally accepted route
   evidence. C-F05 freezes these exact codes and principal IDs before the clean
   target migration: QDOS/C001, PCH/C002, AX/C003, FW/C004, QCL/C005, OAK/C006,
   SBL/C007, BLACK/C008, RJS/C009, DFD/C00A, KBS/C00B, MP/C00C, YML/C00D,
   ALS/C00E and BC/C00F, where `Cnnn` abbreviates the full GUIDs stated in the
   handoff JSON. The migration/bootstrap inserts each once and a fresh-database
   test checks exact ID/code uniqueness. Preserve HDUK as document issuer for
   YML. Document-profile evidence is separately versioned and does not activate
   a sender route. Do not import historical
   cases, the 528 EVA contact rows wholesale, 412 `FAO The Court` addressees as
   organizations, inferred G-J columns, or an unaccepted sender domain.
2. Extend the existing organization/principal administration owner with the
   minimum current directory records used by intake and address suggestions:
   stable organization/contact/location identity, role, active state, version
   and source. Keep incoming domain, intermediary, report addressee, repairer,
   outgoing recipient and principal role separate. Define Claim Source as its
   own linked record with stable ID, name, contact, telephone, email, notes,
   active flag, version and audit/provenance; its locations, routes and
   principal identity remain separate. B copies the selected Claim Source ID,
   values and version into the Case, and later directory edits do not rewrite
   that snapshot.
3. Add `InspectionAddressSuggestionQuery`,
   `InspectionAddressSuggestionResult` and
   `IInspectionAddressSuggestionQueries` beside the existing address-choice
   contracts in `InspectionAddressResolution.cs`. Its one method is
   `SearchAsync(Guid caseId, string prefix, CancellationToken)`; require at
   least two normalized characters, and return no suggestions for a shorter
   prefix. The 20-row cap is internal, not caller-configurable. Extend the
   existing `InspectionAddressChoicesQueries.cs` adapter to search the union of
   Case's current claimant, repairer and storage addresses, the principal's
   prior accepted inspection locations, and active locations maintained by
   `OrganizationDirectory.cs`/`EfOrganizationDirectory.cs`.
4. Match a trimmed, collapsed-whitespace, case-insensitive name prefix or an
   uppercase, whitespace-free postcode prefix. Use no fuzzy/geographic
   inference. Deduplicate by stable location ID, order exact normalized matches
   before prefix matches and then by normalized name, postcode and stable ID,
   and return at most 20.
5. Each address result returns stable location ID, display/business name, full
   address, postcode, role, source kind, source record ID and source version.
   No network call, new package or nationwide address-provider integration is
   part of v1. Stream B copies the chosen address and its source/version into
   the Case; later directory edits do not rewrite that snapshot.
6. Manage one principal default inspection-location choice in place: `Image
   Based Assessment` or one sourced/manual physical address. QDOS seeds `Image
   Based Assessment`. No Principal page seeds or offers a physical-attendance
   CE method; all CE assessments remain desktop. A third party's requested or
   observed method is retained as raw evidence only. Later extraction of a
   repairer address creates an address choice and provenance; it does not
   replace the default. A staff address override requires a reason and keeps
   both facts, and selecting an address never changes B's separate CE assessment
   method.
7. Retain only the optional manual EVA setting and explicit staff action. Remove
   `EvaAutomaticSubmission` from C's administration contracts, forms, summaries
   and store methods. A removes its column, automatic store/DI/runtime wiring;
   B keeps EVA as an explicit optional downstream route. Principal-method docs,
   source evidence and seed data never silently enable manual EVA.
8. Present v3 Principal, organization and Claim Sources CRUD using the named
   admin pages, authorization and concurrency errors. Claim Source list/edit/
   create/disable operations retain name, contact, telephone, email and notes;
   writes require Administrator actor, expected version, reason and idempotent
   operation key. No password or
   mailbox controls enter these pages. Directory notes do not create workflow,
   recipients, packages or chasers.

**Tests and expected outputs:** all 15 principals appear once with stable codes;
YML/HDUK roles remain distinct; only accepted domains route; duplicate active
codes/domains and stale writes fail; non-admin writes forbid; disable preserves
referenced history; QDOS defaults to Image Based Assessment; reasoned repairer
override changes location but not CE assessment method and remains source-linked.
Claim Source CRUD round-trips all six data fields, keeps route/location/
principal separate, and a changed directory record does not rewrite a Case.
Address
search returns no more than 20 deterministic prefix matches from the four local
sources, with source/version; non-prefix and inactive entries are absent, and
selecting then editing the directory leaves the Case snapshot unchanged. No
test installs or calls an external address service. Automatic EVA has no schema,
setting, DI registration, runtime work item or UI control; manual EVA remains an
explicit optional action. The fresh database contains no imported historical
Case/email rows.

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Organization|FullyQualifiedName~ProviderInspectionMode"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OrganizationAdministration|FullyQualifiedName~ProviderDomainReference|FullyQualifiedName~InspectionAddress"
```

Stop if seed data assumes a spreadsheet workflow, merges roles, requires a
second principal catalog, calls an external address provider, or reintroduces
automatic EVA.

## C07 - Image Intake, Triage and PR 671 preservation

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements. A supplies Image
Intake/Triage schema and migrations; C owns Core behavior, store methods and
C-owned pages. Coordinate any Case-list projection with Stream B.

**Production callers:** mailbox/manual image registration uses
`IImageIntakeStore`; `/VehicleImages/{id:guid}`, `/Triage`,
`/Triage/{id:guid}`, Search and Work Centre query the records; the
formal-instruction association path creates the normal Case and link.

**Change:**

1. Re-apply the useful PR 671 behavior against the integration base at recorded
   tip `743311a0f4ac68794672510e596abd7d89ae47bb`: nullable
   known principal on Image Intake, staff set/replace/clear with expected
   version, inactive-principal rejection, `Not known` display and unchanged
   Awaiting-instruction query count. Do not import its migration/snapshot or
   stale Test UI snapshot; A and C reproduce behavior in owned files. Record a
   hunk/behavior disposition table with retained, superseded or rejected reason;
   preserve the branch/evidence and leave closure to the root workflow.
2. Give every pre-case Triage one immutable global T reference. Image evidence
   may create/link a Triage when instruction identity is incomplete; it does not
   allocate a normal Case/PO. Preserve grouped images, registration evidence,
   principal candidate, receipt and all source provenance.
3. Formal instruction creates the normal Case through the existing allocator,
   links the Triage/Image Intake and retains both pre-case references. Wrong or
   ambiguous principal remains staff-resolvable before allocation.
4. Render v3 pre-case queue/detail behavior without merging Triage, Unidentified
   and Image Intake identities or adding a global Images page.
5. Reuse `IntakeEnvelopeLimits` as the single channel-limit owner. Set the
   manual/public per-file cap to exactly `100 * 1024 * 1024` bytes, pin the
   app-wide multipart body budget to exactly
   `(200 * 1024 * 1024) + 64 * 1024` bytes instead of deriving 20 x 100 MB, and
   retain 20 as the staff batch file-count cap. Provider API remains bounded by
   its existing 30 MB decoded envelope, with its per-file effective bound no
   higher than that envelope. F applies matching host/ingress and accepted
   `DocumentRequests` settings only after measuring the 2 GiB Web container;
   configuration may tighten but never raise Core limits.
6. Extend the existing `RequestUploadPolicy` and `EfDocumentRequestStore` with a
   fixed public submission session. Link generation still records the current
   limits version. The first file whose content and custody are accepted starts
   one non-sliding 15-minute window; failed pre-success attempts do not start it.
   Additions and explicit replacements addressed by server-issued occurrence ID
   are allowed until explicit replay-safe finalization or expiry. Finalized or
   expired sessions refuse later bytes without Case disclosure. A limits-version
   mismatch returns typed `LimitsVersionMismatch` plus `MayReissue=true`; B's
   Case-side handler may create a new link only on explicit staff action. It
   never silently migrates or auto-reissues an outstanding link.
7. Implement `RetainIncomingArtifact` for every received, Unidentified, Triage,
   Image Intake and public-upload occurrence. It invokes A's one
   `ICaseArtifactCustody` contract with Case or holding destination, immutable
   occurrence, operation key, media identity, proposed safe original name,
   size/hash and bounded stream. Persist and render `Pending`, `Confirmed`,
   `Failed` or `Unknown`. Pending/failed/unknown never renders upload success,
   never consumes finalization and never ages out from staging. A confirmed
   replay returns the same logical document/version; equal filenames never
   overwrite occurrences. Re-evaluation reads that exact logical version
   through A04 after staging cleanup.
8. Append Triage staff notes through the existing attributed, versioned,
   replay-safe history and use the existing eligible-engineer query for
   assignment. Do not add `Assign to me` or mutable note replacement.

**Tests and expected outputs:** first two independent triages are `T-00001` and
`T-00002`; concurrent allocation is gap-tolerant but unique and never reuses a
committed number. Replay returns the same reference. Image principal can be
set/replaced/cleared only by staff with the expected version; inactive and stale
writes fail. Adding a principal does not change Awaiting counts/query growth.
Formal instruction yields one Case plus retained T/Image links; ambiguous input
yields no Case/PO. Limit tests assert 100 MiB per file, 200 MiB + 64 KiB global
multipart, 20 files and 30 MiB Provider API without a derived 2 GiB budget.
Public-session tests assert the first confirmed file starts exactly 15 minutes,
failed pre-start attempts do not, later successes do not extend expiry,
add/replace/finalize replay safely, expiry/finalization refuse bytes, and version
mismatch yields refusal/reissue. Custody tests cover duplicate occurrences,
same filename/different bytes, partial and uncertain failure, restart/retry,
authorization and exact source-version reads with zero live Box calls.

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntake|FullyQualifiedName~Triage|FullyQualifiedName~IntakeEnvelopeLimits|FullyQualifiedName~RequestUpload|FullyQualifiedName~RetainIncomingArtifact"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakePersistenceTests|FullyQualifiedName~ImageIntakeWebTests|FullyQualifiedName~TriageQueuesWebTests|FullyQualifiedName~TriageFromIntakeIntegrationTests|FullyQualifiedName~PublicUploadSessionTests|FullyQualifiedName~IncomingArtifactCustodyTests|FullyQualifiedName~ProviderApiSubmissionTests"
```

Stop if Triage and normal Case share an allocator/reference, a principal is
required to register images, a query-per-row regression is introduced, an
accepted upload lacks confirmed custody, or a host limit exceeds Core policy.

## C08 - v3 Inbox, Search, Work Centre, shell and shared Web assets

**Owner/model:** Fable 5.1 orchestrates; Sonnet 5 implements after C02/C06/C07
contracts and A's Graph query contract are stable.

**Production callers and files:** extend `/Inbox` and `/Inbox/{id}` over
`ListRetainedMail`, `GetRetainedMail`, freshness and deleted-mail search;
`/Search/Index` over `ISearchCases`, Image Intake and the new retained-material
query; `/Index` over `OperationsSnapshot`; shared layout/dialog/evidence/status
partials plus `site.css`, `site.js` and the existing icon sprite.

**Complete v3 route and ownership matrix:**

| v3 surface | Production route and PageModel | Owner and implementation result |
| --- | --- | --- |
| Sign-in/account | `/Account/SignIn`, `/Account/PasswordChange`, `/Account/SignOut`, `/Account/AccessDenied` | A-owned identity pages; consume C's external/auth layout styles only. |
| Outer shell | Every authenticated page through `Pages/Shared/_Layout.cshtml`; Ctrl+K handled by `_ShellDialogs.cshtml`/`site.js` | C single owner. One Ctrl+K command palette provides route commands and submits text to server route `/Search?query=...`; there is no second browser-side search engine or alternative global-search control. |
| Work Centre | `/` -> `Pages/Index.cshtml(.cs)` -> `OperationsSnapshot` | C-owned. Preserve five queried metrics, typed attention rows and real target links. |
| Inbox | `/Inbox` -> `Pages/Mail/Index.cshtml(.cs)`; `/Inbox/{id:guid}` -> `Pages/Mail/Message.cshtml(.cs)` | C-owned retained-mail presentation/query. A owns Graph polling/storage. |
| Staff upload | `/Upload`, `/Upload/Status/{id:guid}`, `/Upload/Group/{id:guid}` -> the existing root PageModels | C owns the v3 staff intake presentation; A owns retained-byte storage and queue runtime. Preserve the six typed per-file outcomes. |
| Intak

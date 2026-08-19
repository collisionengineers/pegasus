# Research — INTK-007: replace Needs sorting with referenced Unidentified work

## Question

How must Pegasus replace the broad `Needs sorting` outcome with a durable Unidentified queue whose items have immutable `U<n>` references, required reasons, history, search, and resolution—without collapsing Triage, Blocked intake, Audit, Image Intake, or Case/PO semantics?

## Binding product requirement

- Any safely retained document, image, message, attachment, or inseparable source group that Pegasus cannot read, identify, own, or route becomes Unidentified.
- Allocate the next internal reference `U1`, `U2`, … with no fixed-width padding and no practical five-digit ceiling.
- The U-reference is tracking identity only. It is never a Case/PO, Audit reference, Image Intake reference, principal identity, or evidence that case gates passed.
- Store a required canonical reason and safe explanatory detail.
- Preserve original bytes/custody, filename, source identity, receipt identity, and group membership.
- Resolution links/moves the material to a supported destination while the original U-reference and full history remain immutable.
- This is a wide semantic replacement of the old broad `Needs sorting` concept, not a label-only rename.

## Current semantic footprint

A repository-wide search found 66 files containing `NeedsSorting`, `Needs sorting`, or `needs_sorting` across governing docs, Core, Infrastructure, Web/MCP, design-system artifacts, and tests.

### Governing docs currently conflict with the new requirement

- `docs/operator-notes.md` says “Needs sorting” refers to unmatched e-mail and also uses it for Triage material missing a registration. This protected meaning must be reconciled, not silently overwritten.
- `docs/prd/pegasus-product.md` defines Needs sorting as retained evidence that cannot yet be routed.
- FRD-01/02/03/08/09/12 and design docs use Needs sorting for several distinct conditions: unidentified source, route abstention, ambiguous Case match, incomplete Audit evidence, and missing Triage VRM.
- The repository product invariant says Audit, Triage, Needs sorting, and Blocked intake have distinct settled meanings. Replacing it requires an explicit operator-truth and governing-doc update before code.
- ADR-0006 contains historical architectural terminology. ADRs are append-only: do not rewrite an accepted historical decision merely to remove wording. If its technical decision changes, supersede with a new ADR; otherwise leave historical text and ensure current docs/UI use Unidentified.

### Current Core owners

- `IntakeDecision.NeedsSorting` in `src/Pegasus.Core/Intake/IntakeContracts.cs` is the principal persisted processing decision.
- `ProcessIntake.cs` produces it for unreadable/unsupported/ambiguous/incomplete paths.
- `IntakeDecisionPolicy.cs` controls which decisions may proceed to manual Case creation.
- `MailOperationalDestinationPolicy.cs` and `QdosMailRoutePolicy.cs` have separate NeedsSorting enum members for classification/route abstention.
- Image automation currently expects image receipts to begin in NeedsSorting.
- Triage creation/index filtering uses NeedsSorting as a precondition/fallback.
- Existing reason text is free-form and distributed. There is no canonical Unidentified reason enum/record, U-reference, Unidentified aggregate, or sequence.

### Current persistence and query owners

- `EfIntakeReceiptStore.cs` stores the intake decision/reason and provides receipt list/detail/mutation.
- `EfRetainedMailboxMessageStore.cs` projects retained mail processing outcomes.
- `EfDashboardQueries.cs` and `EfOperationsStore.cs` count/filter NeedsSorting.
- `PegasusDbContext.cs` has intake, image, sequence, history, and association entities but no Unidentified entity/sequence.
- Existing Image Intake reference allocation demonstrates a useful local convention: a dedicated sequence row/table, unique formatted reference, idempotent origin uniqueness, and EF transaction/replay. Reuse the convention, not the Image Intake sequence or prefix.
- Case/Audit reference sequences are separate and must remain separate.
- Existing migrations demonstrate Azure SQL runtime-role grants; a new Unidentified store must grant only actual callers.

### Current operator surfaces and integrations

- Dashboard `Index.cshtml`, Operations, Intake list/detail, Mail message detail, Triage pages, and shared status chip display/filter Needs sorting.
- `OperatorLabels.cs` is the single intended presentation map; raw enum/string values must not reach markup.
- `IntakeMcpTools.cs` exposes intake decisions/queries to automation callers and must use the new vocabulary/reference/reason without accepting U-reference as Case identity.
- Search currently covers Case/PO and Image Intake Reference; it must add exact U-reference search and display.
- Design-system Markdown/React artifacts contain example Needs sorting labels. They are documentation/prototypes, but user-facing examples must be reconciled after canonical design changes.

## Required canonical model

### Aggregate identity

- Create one `UnidentifiedItem` aggregate for one source occurrence or one inseparable INTK-005 group.
- Store: GUID id, positive allocated sequence, formatted `U<n>` reference, origin kind (single receipt/group), origin id, canonical reason, safe detail, state, created/resolved timestamps, actor/operation identity, version, and resolution target metadata.
- Enforce exactly one origin kind/id, one Unidentified item per origin, unique sequence, and unique reference.
- For an INTK-005 group, allocate one U-reference for the group and list all member receipts. Never allocate one U per file in the same inseparable group.
- Existing pre-migration NeedsSorting receipts need deterministic grouping: grouped receipts use their group; ungrouped receipts each become a one-source Unidentified item.

### Reference allocation

- Format is exactly uppercase `U` followed by invariant-culture positive decimal digits; no padding.
- Allocate atomically and monotonically under concurrent transactions.
- Never reuse a sequence after rollback ambiguity, deletion (deletion is forbidden), resolution, migration, or replay.
- Use a dedicated Unidentified sequence owner. Do not compute `MAX + 1`.
- Database uniqueness is the final collision guard; retry follows existing serializable/idempotent allocation patterns.
- Parsing/search accepts canonical exact `U<n>` only as an Unidentified reference and never routes it into Case/Audit parsing.

### Canonical reason taxonomy

One Core-owned enum/table of codes, used by every producer and renderer:

1. `UnreadableOrCorruptContent` — bytes retained but content cannot be decoded/read.
2. `UnsupportedContent` — safely retained type/shape has no accepted reader.
3. `NoUsableIdentification` — content readable but no accepted identity/routing evidence exists.
4. `ConflictingIdentification` — two or more accepted identity signals conflict.
5. `AmbiguousOwnershipOrDestination` — evidence supports multiple owners/destinations or no unique eligible target.
6. `TechnicalProcessingFailure` — custody succeeded but processing exhausted retry or a non-content technical failure became terminal.

- Safe detail is required, bounded, operator-facing, and must not contain stack traces/secrets.
- The taxonomy lives in Core once. UI labels, persistence converters, MCP serialization, and tests map to it; no second string list.
- Image-specific detector/reader outcomes remain diagnostic evidence and map to the appropriate canonical reason only when the whole source/group becomes Unidentified.

### State and resolution

- Minimum states: `Open` and `Resolved`. Do not invent a generic workflow engine.
- Resolution requires authorized actor, reason, operation key, expected version, and exactly one supported destination/link result defined by updated FRDs.
- Resolution never changes U-reference or origin. It appends permanent history with previous/new state, target type/id/reference, actor, time, and reason.
- Replay of the same operation returns the existing result; conflicting operation reuse fails closed.
- No delete/reopen shortcut. If reopening is required by governing docs, it needs a reason and history; otherwise omit it.
- Existing receipt decision may become `Unidentified` or retain a lower-level processing outcome plus an Unidentified link. The governing docs must choose one canonical projection; code must not maintain two divergent “is unidentified” flags.

## Migration classification requirement

Every old NeedsSorting producer/consumer must be assigned, not blindly renamed:

- Truly unreadable/unidentified/ambiguous retained material → Unidentified with mapped canonical reason.
- Missing-VRM Triage request → follow updated FRD-03; do not accidentally turn Triage workflow identity into generic Unidentified if the operator wants a distinct pre-Triage rule.
- Incomplete Audit evidence → follow updated FRD-01; preserve Audit fail-closed semantics and no Audit reference.
- Vehicle image groups qualifying for INTK-006 → associate or create Image-Only Case, not Unidentified.
- Retryable processing → remains processing/retry, not Unidentified.
- Terminal technical failure after custody → Unidentified/TechnicalProcessingFailure.
- Reasoned policy refusal → Blocked intake, not Unidentified.
- Pre-Case Image Intake with usable registration → remains Image Intake unless updated docs explicitly change it.
- Mail route/classification abstention → Unidentified only when it represents unresolved received material; classification enum naming may remain internal if it is not the durable destination, but operator/MCP vocabulary must be canonical.

## Verified premises

- Read EPIC-007 context and linked INTK-005/006 requirements.
- Searched all 66 current repository files containing the old term.
- Inspected Core decision/policy owners, EF stores/mappings, sequence conventions, dashboard/operations/search/intake/Triage/MCP surfaces, migration conventions, and governing documents.
- Confirmed no existing U-reference or Unidentified aggregate exists.

## Implications

This is a cross-cutting behavior and data migration, not a textual replacement. The implementation must first update protected/governing docs, then introduce one Core aggregate/taxonomy/reference allocator, migrate each old producer by meaning, backfill existing records idempotently, and update every query/surface/test.

## Open questions

None. The requested reference format, grouping, required reason, persistence, queue, and wide replacement are binding. The governing-doc step must explicitly classify each old semantic use before implementation; the plan supplies the required classification table and a stop condition for omissions.

\# Inbox `Unidentified` scope keeps a message whose Unidentified item has already resolved



\## Context



Operator link: `/Inbox?queue=unidentified\&selected=c4fc84de-72e1-4dcd-9447-e1a9800864ca`.

The row shows a Case link (`a.QDOS26009`, chip "Case created") but still sits in the

Inbox \*\*Unidentified\*\* scope.



\### Live prod evidence (read-only SQL, 2026-09-16)



| Row | Value |

|---|---|

| `RetainedMailboxMessages` c4fc84de… | `Fw: (EREF10) RTA on 09/09/2026 : Mr George Robertson (Our Ref: SCL/ND/48450/1)`, received 10:45:17Z, not dismissed |

| `IntakeReceipts` 6b564cd8… | `Decision = needs\_sorting` — "The current instruction does not identify one accepted work type." |

| `IntakeMailClassificationDecisions` | `Outcome = unclassified` (fails closed for staff review) |

| `IntakeMailRouteDecisions` | `accepted`, QDOS direct\_provider |

| `IntakeCaseMatchDecisions` | `unique\_match` → a.QDOS26009 on claim token `48450/1` |

| `IntakeManualAssociations` | active link to a.QDOS26009 by `system-worker:intake-processing`, "Automatic association from the recorded case-match decision" (10:45:19Z) |

| `UnidentifiedItems` U51 (Origin = this receipt) | `State = Resolved` at 10:45:20Z → InstructionCase a.QDOS26009, "the Unidentified item is superseded" |

| `CaseIntakeLinks` | none |



Sibling receipt ceab82c1… (message acb61071…, 10:44:45Z) is the first forward of the same

instruction; it classified cleanly and \*\*created\*\* a.QDOS26009. This second forward failed

classification, opened U51, was auto-associated to the existing Case by the case-match path

(`src/Pegasus.Core/Intake/DurableIntake.cs:1159-1187`), and U51 was resolved by

`ReconcileUnidentifiedDestinations`. Only this one message is affected in the live estate today.



The Unidentified tab on `/Cases?tab=unidentified` and the dashboard Unidentified count already

agree the item is resolved (they read `UnidentifiedItems.State`). The Inbox is the outlier.



\## Diagnosis



The Inbox `unidentified` scope is decided solely by the mail classification outcome

(`src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs:1011-1017`):



```csharp

query.IncludesUnidentified

&#x20;   ? receipt.MailClassificationDecision.Outcome != classified

&#x20;   : ...

```



Nothing on the read side consults the receipt's Unidentified item, so once that item resolves

(auto case match, staff Link to Case, Create case, Register images, Triage, or Close with reason)

the message stays in the scope forever. The classification decision itself is immutable by design

(corrections are new history rows), and the automatic association correctly does not rewrite it —

so the scope predicate is the thing that is wrong.



FRD-08 is explicit that this scope is a projection of the one Unidentified owner, not a second

queue (`docs/frd/frd-08-email-mailbox-and-background-processing.md:5-9`):

> Mail projections link to the same Unidentified item rather than synthesising a second queue row.



A second in-memory copy of the scope rule lives in `Mail/Message.cshtml.cs:1850-1863`

(`MatchesQueue`, driving the "no longer in the view you opened it from" notice) and must move with it.



\## Recommended change



Rule: a retained message is in the Inbox Unidentified scope while its classification is not

`classified` \*\*and\*\* its receipt's Unidentified item has not resolved. An unclassified message

with no Unidentified item yet (deferred registration window) stays in scope — that fails toward

staff review, matching the abstention semantics. Resolution of any kind (Case, Image intake,

Triage, Closed with reason) leaves the scope; the message keeps its classification record and

Case association and remains in All incoming and on the Case.



No data repair is needed: the query change alone drops this message from the scope.



\### 1. Persistence predicate — `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`



In `ApplyClassificationFilter` (≈ line 1015), extend the `IncludesUnidentified` branch:



```csharp

var resolved = UnidentifiedState.Resolved.ToString();

var receiptOrigin = UnidentifiedOriginKind.Receipt.ToString();

...

? receipt.MailClassificationDecision.Outcome != classified

&#x20;   \&\& !context.Set<UnidentifiedItemEntity>().Any(item =>

&#x20;       item.OriginKind == receiptOrigin

&#x20;       \&\& item.OriginId == receipt.Id

&#x20;       \&\& item.State == resolved)

```



Both the list and the scope-rail counts (`CountAsync`/`CountManyAsync`) flow through

`BuildMatches`, so they stay in step. Follow the existing string-state pattern used in

`EfUnidentifiedStore.cs:443-451`.



\### 2. Summary projection — `src/Pegasus.Core/Intake/RetainedMail.cs` + store `MapSummariesAsync`



Add one fact to `RetainedMailSummary` (init property beside `DismissedAtUtc`, line 80-81):



```csharp

/// <summary>True once the receipt's Unidentified item has resolved; the message has left the Inbox Unidentified scope.</summary>

public bool UnidentifiedResolved { get; init; }

```



Populate it in `MapSummariesAsync` (`EfRetainedMailboxMessageStore.cs:794-941`) with one extra

page-level lookup keyed by `receiptIds` (same shape as the `allocationStates` lookup at 835-846):

the set of receipt ids that have a `Resolved` Unidentified item with `OriginKind == Receipt`.

Keep the "lookups for the whole page, never one per row" comment honest (update the count).



\### 3. Message page notice — `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`



`MatchesQueue` (1850-1863) takes the summary too and applies the same rule for the Unidentified

destination:



```csharp

if (DestinationFilter is { } destination)

{

&#x20;   var matches = MailOperationalDestinationPolicy.Map(dossier.Current).Destination == destination;

&#x20;   return destination == MailOperationalDestination.Unidentified

&#x20;       ? matches \&\& !summary.UnidentifiedResolved

&#x20;       : matches;

}

```



`IsOutsideListScope` (1805-1814) already has `detail.Summary`; pass it through.



\### 4. Documentation — `docs/frd/frd-08-email-mailbox-and-background-processing.md`



In the "Unidentified mail destination" paragraph (lines 5-9), add one sentence after the

"Mail projections…" sentence stating the rule above: the Inbox Unidentified scope lists retained

mail whose Unidentified item is still open; once the item resolves the message leaves that scope

while keeping its classification record and Case association. No new file, so the placement gate

does not apply; FRD-12's Inbox paragraph only names the scopes and needs no change.



\### 5. Tests



\- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` — new fact beside

&#x20; `OperationalAndDetailedViewsUseTheCurrentClassificationProjection` (477): retain two

&#x20; unclassified messages, register a Unidentified item for each receipt via

&#x20; `IRegisterUnidentified` (`UnidentifiedOrigin.Receipt(receiptId)`, helper pattern in

&#x20; `UnidentifiedPersistenceTests.cs:192-203`), resolve one via `IUnidentifiedStore.ResolveAsync`

&#x20; with `UnidentifiedResolutionTargetKind.ExternalReference` (pattern at 70-80, no Case needed).

&#x20; Assert: Unidentified scope lists/counts only the open one; All incoming still lists both; the

&#x20; resolved summary has `UnidentifiedResolved == true`.

\- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — extend the existing

&#x20; "no longer in the view" coverage (≈ 1700-1712, 1900-1917): `/Inbox/{id}?queue=unidentified`

&#x20; for the resolved message shows the notice; for the open one it does not.

\- Existing `CountManyMatchesEachInboxRailScope…` (540) and the projection test must still pass

&#x20; unchanged (their unclassified fixtures have no Unidentified item, so they stay in scope).



\## Branching and verification



\- Do \*\*not\*\* build this on the current checkout (`codex/u50-pdf-image-intake` is another

&#x20; ticket's branch). Cut `task/inbox-unidentified-resolved` from `origin/dev` in

&#x20; `../pegasus-worktrees/inbox-unidentified-resolved`, unset upstream after branching.

\- This session takes the host slot for the focused run; peer sessions share LocalDB so run one

&#x20; suite at a time.

\- `dotnet build Pegasus.sln` (gate tests on the build exit code), then

&#x20; `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName\~RetainedMailPersistenceTests|FullyQualifiedName\~MailWorkspaceWebTests"`

&#x20; and `dotnet test tests/Pegasus.Core.Tests --filter "FullyQualifiedName\~MailOperationalDestinationPolicyTests"`.

\- Full solution checks come from the PR's CI run; reuse that exact-head evidence rather than

&#x20; re-running the \~28-minute integration suite locally.

\- Live confirmation after release: the prod message c4fc84de… is absent from

&#x20; `/Inbox?queue=unidentified`, present in All incoming with the `a.QDOS26009` link, and the

&#x20; scope-rail Unidentified count drops by one. No SQL write is involved.



\## Out of scope (observed, not changed)



\- Why the second forward classified `unclassified` while the first classified as an instruction

&#x20; is a separate classification question; the row will still read "Unclassified · Unidentified"

&#x20; in its meta line beside the Case link, which is the classification-level fact and stays.

\- The receipt's `Decision = needs\_sorting` is the immutable processing decision and is not rewritten.




# Post-implementation report

**Branch** `task/docs-012-evidence-files` · **PR** [#532](https://github.com/collisionengineers/pegasus/pull/532) → `dev`
**Commits** `67d7900b` (the change) · `ff02edae` (the round-trip test)

## What the panel is now

`File | Type | From | Size | Added`, plus a per-row action cell in edit mode.
Nothing else.

| Gone | Why |
| --- | --- |
| Per-version `Custody` column | Internal vocabulary. That a file is listed **is** the statement that it is stored |
| `Revision state` column | Superseded by the notes rule |
| `EVA eligibility` cell | *"Eligible unless staff confirms third-party vehicle evidence"* — a how-it-works sentence in a table cell, banned outright |
| Selective export by version | The whole-case Export on the header is a different action and stays |
| `Retain document` form | The operator's reasoning is exact: the file is already stored |
| The semantic-role selector | It lived inside that form |

The Box folder is a button; unavailable and pending states are still shown rather
than implying success.

## Two things found on the way

**The table rendered a cartesian product.** The old loop nested documents ×
occurrences × versions, so a document with two occurrences and three versions
produced six rows. Rows are now one per occurrence, joined on the occurrence's
own version. The filter is the evidence gallery's own — current, not logically
removed, custody confirmed — which is the operator's *"if they show here, they
should be on box"*, read literally.

**`OperatorLabels.DocumentRole` and `DocumentOrigin` already existed with zero
callers.** The old cell printed the raw enum pair (`OriginalSource / Intake`) —
precisely the "dev speak leaking into UI" being complained about. Those two
helpers now have their first caller.

## The note, and the trap under it

A removal writes a case note **in the same transaction**, so a file is never
removed without its note or noted without its removal. The removal reason is the
note body — already required, already bounded, already the thing a person typed
to explain themselves. The actor is the member of staff who pressed the control;
"created by the system" describes who *writes* the note, not who *acted*.

It goes to **`CaseWorkflowEvents`**, which is what the Notes tab reads — **not
`CaseHistory`**, which nothing operator-facing reads and which is where the
neighbouring `custody_confirmed`/`custody_failed` writes wrongly go, invisibly,
today. Sending a note to the wrong table already reached production in Release
22: the page reported success against a timeline that stayed at zero.

So the test asserts the **round trip** through `CaseDetails.History` — the same
read the page makes — not the presence of a row. A row-count assertion would have
passed for the Release 22 defect too. It also pins that a replayed removal adds no
second note; `(CaseId, AfterVersion)` is unique, and the note carries the version
`CaseMutationGuard.Complete` has just claimed.

## Kept, against this ticket's original reading

The **third-party vehicle evidence confirmation stays.** I had mapped *"semantic
role shouldn't be user configurable"* onto it and written it down as removed.
That was wrong: it sets no semantic role. It sets
`ThirdPartyVehicleConfirmedAtUtc`, the only thing keeping a third-party vehicle's
photograph out of the EVA bundle, on a path the whole-case Export still uses, and
nothing sets it automatically. Removing it would have made the exclusion
permanently unreachable. Operator agreed to keep it, 2026-08-24.

## Governing documents amended, on the operator's authority

- The **Evidence/document panel contract row**, which currently *requires* the
  version, logical-removal and Box state this change removes. Operator truth
  outranks the design authority, but the row is rewritten here rather than left
  contradicting the code.
- The **Lucide glyph registry**, sixteen glyphs to seventeen. The registry is a
  closed, checksummed set and hand-drawn or substitute glyphs are banned, so the
  authentic v0.344.0 `trash-2` vector was fetched from the tagged release. The
  per-glyph checksum scheme is undocumented, so it was **determined empirically
  against four known glyphs** (SHA-256 of the `<g>` element, UTF-8, uppercase)
  rather than guessed. Both checksums recomputed from the committed bytes, and
  the recorded sprite checksum was verified accurate **before** this change as
  well as after.

## Handler retirement — what went and what stayed

| Handler | Disposition |
| --- | --- |
| `OnPostUploadDocumentAsync` | Gone. `IAddCaseDocument` **stays** — estimate import and the MCP `pegasus_document_add` tool still call it |
| Selective-export selection | Gone from the page. `IExportCaseDocuments` **stays** — the MCP `pegasus_document_export` tool still calls it |
| `OnPostRemoveDocumentAsync` | Stays, now reached from the trash control |
| `OnPostConfirmThirdPartyVehicleEvidenceAsync` | Stays — see above |

An architecture assertion pinned `IAddCaseDocument` to this page model's
constructor and failed. That is the assertion doing its job — it exists to catch
a port injected with no live handler — so the assertion was removed rather than
the parameter kept.

## Not in scope, and verified so

`_CaseDocuments.cshtml`'s public upload-request section belongs to [[CASE-022]].
**Zero lines of it appear in the diff** — checked with a diff filter, not
assumed. [[DOCS-011]]'s preview work is untouched; this ticket decides which
surface survives and stops there.

## Simplification pass

Not yet run as a separate independent lens over this branch's diff. The plan's
Verification section requires it before merge, and the PR should not merge until
it has run and its dispositions are recorded here. Stating that plainly rather
than implying the step is complete.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` | green, **0 warnings** |
| `dotnet test tests/Pegasus.Core.Tests` | **937 passed** |
| `dotnet test tests/Pegasus.ArchitectureTests` | **99 passed** |
| Integration suite | not yet run locally on this branch |
| CI on the pushed SHA | pending |

The removal-note round-trip test is the highest-value assertion here and has not
yet been executed against a database — CI's shards on the pushed SHA are what will
prove it.

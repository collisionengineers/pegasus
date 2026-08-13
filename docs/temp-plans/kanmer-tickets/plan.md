# Kanmer ticket reconciliation

## Summary

- Current board baseline: 224 tickets, all `To Do`; none taken, overdue,
  documented, or previously completed; zero file warnings.
- Complete 15 already-delivered work tickets using the one-off proof text
  `Operator confirmed`.
- Consolidate 16 proof-only tickets into owning work tickets and archive them.
- Rework six proof-oriented tickets that lack a suitable owner into
  outcome-focused work tickets.
- Preserve all live-system limitations. `Operator confirmed` is Kanmer closure
  evidence, not invented live verification.

## Complete with minimal proof

Write `proof.md` containing exactly `Operator confirmed`, then move these
tickets to `done`:

### Local tooling and data

- `TICK-002` — **OPS-22: Genuine-corpus local evaluation harness**.
- `TICK-003` — **EVAL-01: Local development-only EML categorisation
  evaluator over a read-only local working copy**.
- `TICK-005` — **EVAL-03: Let the reviewer create an `Other` category with a
  name and reasoning**.
- `TICK-006` — **EVAL-04: Copy reviewed EML files into the local evaluation
  tree and append the human result to the JSONL adjudication log**.
- `TICK-030` — **DATA-01: Publish immutable cumulative provider-domain
  reference snapshots from approved spreadsheets**.

### Image intake

- `TICK-011` — **INT-17: Read vehicle registrations automatically from
  ordinary vehicle images**.
- `TICK-042` — **INT-28: Match image-led and instruction-led records
  automatically**.
- `TICK-065` — **INT-32: Retain separate age/chase state for instruction and
  image halves and show when definitive pairing makes the job ready**.

### Locally caller-proved EVA and Box work

- `TICK-015` — **CASE-21: Record the first successful manual EVA bundle as
  the once-per-case `First sent to Engineer` handoff proxy**.
- `TICK-016` — **CASE-30: Track the QDOS-alpha inspection/report stage and EVA
  handoff without replacing EVA engineering work**.
- `TICK-017` — **DOC-01: Create the Box case folder automatically using the
  Case/PO name**.
- `TICK-019` — **DOC-03: Retain document versions**.

### Implemented Automation Actor actions

- `TICK-024` — **MCP-02: Expose Automation Actor Case actions through the same
  Core use cases as the staff app**.
- `TICK-025` — **MCP-03: Expose Automation Actor intake-queue actions through
  the same Core use cases as the QDOS-alpha staff app**.
- `TICK-026` — **MCP-04: Expose Automation Actor document actions through the
  same Core use cases as the staff app**.

### Explicitly keep open

Keep these open because their named behavior, caller proof, live evidence, or
known correctness gaps remain unresolved:

- `TICK-004` — **EVAL-02: Select from the detailed Received/Sent/Reply taxonomy
  and record required reasoning**.
- `TICK-007` — **EVAL-05: Display the rule-generated category and evidence
  beside the human review**.
- `TICK-009` — **MAIL-21: Deliver the shared Core classification foundation,
  acceptance cohort, deployment, and remaining verification**.
- `TICK-012` — **INT-25: Create cases automatically from definitive authorised
  intake**.
- `TICK-018` — **DOC-02: Store source emails, instruction documents, images,
  correspondence, and reports in Box**.
- `TICK-022` — **EXT-03: Produce the operator-approved deterministic EVA
  handoff containing the 13-key JSON, eligible images, and SHA-256 manifest**.
- `TICK-023` — **MCP-01: Provide controlled MCP ingress for the named
  vendor-neutral Automation Actor through Pegasus Core use cases**.
- `TICK-027` — **MCP-06: Provide Automation Actor assessment actions with
  direct-write logging parity through staff-equivalent Core guards**.
- `TICK-102` — **AI-09: Complete the durable Send-to-AI work-request and
  direct-writing Automation Actor round trip**.

## Consolidate proof-only tickets

For each row, append a `Migrated validation — [[ID]]` section to the owner's
`checklist.md`, preserving the original acceptance checks and live-approval
warnings. Append a migration note to the old ticket and archive it; never
delete it. Actual results later belong in the owner's `proof.md`.

| Proof-only ticket(s) to archive | Owning work ticket |
| --- | --- |
| `TICK-008` — Run live provider-specific categorisation against `.eml` files in the local evaluator | `TICK-007` — Display rule-generated category and evidence beside human review |
| `TICK-031` — Operator acceptance of the real end-to-end workflow; `TICK-032` — management approval before production release; `TICK-108` — recover the post-release-8 immutable manifest and migration transcript; `TICK-218` — record operator acceptance of the QDOS production workflow; `TICK-219` — record management approval before QDOS production release | `TICK-001` — Complete the QDOS alpha production release |
| `TICK-115` — Verify the scheduled predecessor Key Vault purge by fresh approved inventory | `TICK-110` — Reconcile local `azd` state against the observed production estate |
| `TICK-116` — Prove a genuine QDOS mailbox-to-Case/PO production journey; `TICK-117` — prove production Box custody for a real accepted case | `BUG-001` — Restore case creation and Box-folder provisioning when an email is received |
| `TICK-119` — Prove operator EVA drag-and-drop handoff from a live case | `TICK-022` — Deliver the deterministic EVA JSON, image, and manifest handoff |
| `TICK-190` — Prove template-database backup and restore against external SQL Server; `TICK-191` — observe abandoned LocalDB and backup-file reclamation | `TICK-028` — Establish database backup, restore, RPO, and RTO capability |
| `TICK-192` — Record tier-5 external-client Automation Actor evidence | `TICK-023` — Provide controlled Automation Actor MCP ingress |
| `TICK-209` — Prove the renderer container build and first Noble render baseline; `TICK-210` — prove the report-renderer MCPB bundle under .NET 10 | `SIMPLI-014` — Make the report renderer standalone (replacement for archived `TICK-221`) |
| `TICK-217` — Accept per-field extraction thresholds with zero false case creation | `TICK-186` — Assemble the extraction cohort and untouched holdout |

## Rework tickets without a suitable owner

Rewrite these as genuine outcome/work tickets, preserving their source
references and constraints:

- `TICK-001` → **Complete the QDOS alpha production release**.
- `TICK-028` → **Establish database backup, restore, RPO, and RTO capability**.
- `TICK-118` → **Activate live completeness and Review, Not ready, and Held
  queues**.
- `TICK-120` → **Activate production due-by and seven-day chasing**.
- `TICK-199` → **Retire `.infisical.json` or document its active owner**.
- `TICK-201` → **Correct canonical documentation claims against source
  evidence**.

Each body describes the intended outcome and implementation/recovery work.
Validation becomes checklist and eventual proof material rather than the
ticket's sole purpose.

## Verification

- Re-read every ticket immediately before mutation; skip any concurrently
  changed or taken ticket.
- Confirm the 15 proposed completions have `proof.md` and status `done`.
- Confirm all 16 consolidated tickets are archived and their requirements
  appear under the mapped owners.
- Confirm reworked tickets remain open and outcome-focused.
- Finish with `get_status`, `list_items include_archived: true`, and activity
  review; require zero warnings and no lost ticket content.
- Do not alter these new active work tickets beyond the explicitly listed
  proof migration into their documents:
  - `BUG-001` — **Case creation and Box folder are absent on email receipt**.
  - `SIMPLI-001` — **Make AI Centre a standalone repository**.
  - `SIMPLI-002` — **Rewrite `AGENTS.md`**.
  - `SIMPLI-013` — **Make the document extractor a standalone .NET package**
    (replacement for archived `TICK-220`).
  - `SIMPLI-014` — **Make the report renderer standalone** (replacement for
    archived `TICK-221`).

# Research — KANMER-001: Retarget Kanmer tickets citing the retired NOW.md / requirements.md

Date: 2026-08-14. Method: full grep sweep of the board tree (`.kanmer/areas/**` — ticket bodies, pipeline docs, and board machinery) for `NOW.md` and `requirements.md` citation variants, cross-checked against the current `docs/prd/` + `docs/frd/` tree at `origin/dev` (c99a7c1a). Performed by a dedicated explore agent; findings verified against SIMPLI-004/SIMPLI-006 proof records.

## Summary counts

**157 tickets** cite NOW.md and/or requirements.md in their main body (excluding KANMER-001 itself and the SIMPLI-* tickets that performed the retirement). Every one falls into exactly one of two mutually exclusive patterns — there are **zero merely-incidental body citations**:

- **(a) NOW.md authority/queue citation** — 113 tickets (105 mechanical boilerplate + 8 with unique substantive content)
- **(b) requirements.md canonical-owner link** (`Canonical owner: [..](requirements.md#anchor)`) — 44 tickets

By stage:

| Stage | (a) NOW.md authority | (b) requirements.md owner | Total |
|---|---|---|---|
| todo | 113 (100 active + 13 archived) | 33 (30 active + 3 archived) | 146 |
| in-progress | 0 | 3 | 3 |
| review | 0 | 1 | 1 |
| done | 0 | 7 | 7 |
| **Total** | **113** | **44** | **157** |

Reconciliation with the ticket's estimate (~131 todo + ~5 in-progress): actual is **130 active todo + 3 in-progress**, plus previously-uncounted hits in 1 review ticket, 7 done tickets, and 16 already-`archived:true` tickets.

## Complete affected-ID inventory

### In-progress (3) — category (b)
- TICK-015 (engineering-eva-export-handoff) → `#focused-eva-manual-handoff`
- TICK-016 (engineering-eva-export-handoff) → `#focused-eva-manual-handoff`
- TICK-033 (intake-manual-upload-source-intake) → `#request-scoped-upload-links`

### Review (1) — category (b)
- TICK-012 (intake-manual-upload-source-intake) → `#matching-conflicts-and-reversible-association`; **also has citations inside pipeline docs** (see below)

### Done (7) — category (b)
- TICK-002, TICK-003, TICK-005, TICK-006 (evaluation-local-email-evaluator) → `#qdos-alpha-evaluation-boundary`
- TICK-017 (files-staging-custody-box) → `#documents-extraction-and-custody`; also pipeline-doc citation
- TICK-019 (files-staging-custody-box) → `#documents-extraction-and-custody`
- TICK-030 (data-provider-principal-repairer-reference) → `#provider-api-principal-and-contract-boundary`

### Todo, category (b) canonical-owner (33; * = already archived)
TICK-004, TICK-007, TICK-008*, TICK-009, TICK-010, TICK-011, TICK-013, TICK-014, TICK-018, TICK-020, TICK-021, TICK-022, TICK-023, TICK-024, TICK-025, TICK-026, TICK-027, TICK-029, TICK-031*, TICK-032*, TICK-034, TICK-035, TICK-036, TICK-037, TICK-038, TICK-039, TICK-040, TICK-041, TICK-042, TICK-043, TICK-044, TICK-045, TICK-046

### Todo, category (a) boilerplate, active — the 95 archival candidates
TICK-109, TICK-110, TICK-111, TICK-112, TICK-113, TICK-114, TICK-121, TICK-122, TICK-123, TICK-124, TICK-125, TICK-126, TICK-127, TICK-128, TICK-129, TICK-130, TICK-131, TICK-132, TICK-133, TICK-134, TICK-135, TICK-136, TICK-137, TICK-138, TICK-139, TICK-140, TICK-141, TICK-142, TICK-143, TICK-144, TICK-145, TICK-146, TICK-147, TICK-148, TICK-149, TICK-150, TICK-151, TICK-152, TICK-153, TICK-154, TICK-155, TICK-156, TICK-157, TICK-158, TICK-159, TICK-160, TICK-161, TICK-162, TICK-163, TICK-164, TICK-165, TICK-166, TICK-167, TICK-168, TICK-169, TICK-171, TICK-172, TICK-173, TICK-174, TICK-175, TICK-176, TICK-177, TICK-178, TICK-179, TICK-180, TICK-181, TICK-182, TICK-183, TICK-184, TICK-185, TICK-186, TICK-187, TICK-188, TICK-189, TICK-193, TICK-194, TICK-195, TICK-196, TICK-197, TICK-198, TICK-200, TICK-202, TICK-203, TICK-204, TICK-205, TICK-206, TICK-207, TICK-208, TICK-211, TICK-212, TICK-213, TICK-214, TICK-215, TICK-216

### Todo, category (a) with unique substantive content — retarget, do NOT archive (5)
TICK-001, TICK-118, TICK-120, TICK-199, TICK-201

### Already `archived:true` with citations (16) — scope decision needed
- Boilerplate, archived (10): TICK-108, TICK-115, TICK-116, TICK-117, TICK-119, TICK-190, TICK-191, TICK-192, TICK-209, TICK-210
- Unique content, archived (3): TICK-217, TICK-218, TICK-219 — each carries an explicit `## Migrated validation` section (consolidated into TICK-186 / TICK-001 / TICK-218 respectively); intentionally-superseded stubs kept for history.
- Plus TICK-008, TICK-031, TICK-032 (category (b), counted in the todo-33 list above).

## Anchor → new-home mapping (zero gaps)

All 16 distinct `requirements.md` anchors cited by board tickets resolve to a live FRD heading — consistent with SIMPLI-006's proof ("51 heading slugs preserved, 301 references retargeted, 0 unmapped"):

| Anchor | Cites | New home |
|---|---|---|
| `#qdos-alpha-evaluation-boundary` | 7 | frd-08-email-mailbox-and-background-processing.md |
| `#intake-and-source-identity` | 7 | frd-02-intake-and-source-identity.md |
| `#email-mailbox-and-background-processing` | 5 | frd-08-email-mailbox-and-background-processing.md |
| `#mcp-automation-and-actor-boundary` | 4 | frd-10-mcp-automation-and-actor-boundary.md |
| `#operator-experience` | 3 | frd-12-operator-experience.md |
| `#focused-eva-manual-handoff` | 3 | frd-07-eva-and-external-engineering-handoff.md |
| `#documents-extraction-and-custody` | 3 | frd-05-documents-extraction-and-custody.md |
| `#vehicle-data-and-mot-enrichment` | 2 | frd-06-vehicle-and-engineering-evidence.md |
| `#outbound-correspondence-evidence` | 2 | frd-08-email-mailbox-and-background-processing.md |
| `#matching-conflicts-and-reversible-association` | 2 | frd-02-intake-and-source-identity.md |
| `#targeted-sending-and-reviewed-ai-proposals` | 1 | frd-11-reports-correspondence-and-reviewed-proposals.md |
| `#settled-mailbox-taxonomy-and-correction` | 1 | frd-08-email-mailbox-and-background-processing.md |
| `#request-scoped-upload-links` | 1 | frd-02-intake-and-source-identity.md |
| `#provider-api-principal-and-contract-boundary` | 1 | frd-09-provider-and-intermediary-routes.md |
| `#ordinary-image-vrm-and-image-analysis` | 1 | frd-06-vehicle-and-engineering-evidence.md |
| `#inspection-address` | 1 | frd-06-vehicle-and-engineering-evidence.md |

Link-text fix required alongside retargeting: 19 of the 44 category-(b) tickets use the bare label `[Requirements](requirements.md#anchor)`, which becomes ambiguous once split across 12 FRD files — the link text should name the owning FRD (e.g. "FRD-08 — Email, mailbox, and background processing").

## Archival-candidate rationale (the 95)

All share one root cause: the body is 100% mechanically generated from a NOW.md queue line — identical Why ("This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken."), identical generic 2-bullet Approach and 2-item Verification; only the title and `Source: NOW.md — …` note vary. Nothing is independently actionable. Representative clusters: TICK-144–169 (send-to-AI queue bullets), TICK-203–216 (renderer capability/relocation bullets), TICK-121–124 (Path-7 cutover alert bullets), TICK-139–143/177/182 (Triage reserved-meaning / pager sweep / identifier-clock debt bullets). Disposal options per candidate: archive with the underlying idea preserved (open-decisions.md line or board note), or a real research pass to make it actionable.

## Pipeline-doc citations (doc edits, not just body edits)

- **TICK-012** (review): `research.md` quotes `docs/requirements.md` "Matching conflicts and reversible association" verbatim and references NOW.md's live-caller warning; `plan.md` and `proof.md` also cite "NOW.md's own warning." Evidentiary/historical citations — retarget to the FRD heading + current operations/open-decisions docs, or annotate as historical-as-of-date.
- **TICK-017** (done): `research.md` quotes `docs/requirements.md` "Staging and custody" (:405-409) with a line-range citation. Same treatment.

No other non-meta ticket has pipeline-doc citations.

## Other findings

- Board machinery (`data/board.yml`, `counters.json`, `activity.jsonl`, `version.json`) is clean — zero citations.
- SIMPLI-001/002/004/006 mention the files as the subject of the retirement itself — excluded from the work inventory.
- Two distinct authority phrasings exist in category (a): the 105-ticket exact boilerplate, and 8 tickets with unique prose still naming NOW.md as source. A retarget pass that pattern-matches only the boilerplate string misses those 8.
- The ticket body's hold condition ("a Kanmer update is coming — do not mass-edit before then") appears satisfied: the board is now format 2 with the full doc pipeline. Confirm with the operator before the mass edit (see impact.md).

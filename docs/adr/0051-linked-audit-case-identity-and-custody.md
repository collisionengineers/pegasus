---
id: ADR-0051
status: accepted
date: 2026-09-16
supersedes: [ADR-0002]
superseded_by: []
related_capabilities: []
related_frd: [frd-01, frd-05]
tags: [identity, custody, audit, box]
---

# ADR-0051: Linked Audit Case identity and custody

## Status

Accepted, recording the operator's 13 September 2026 decision (v26 planning,
Create audit) that Stage 2 implemented. Partially supersedes ADR-0002's
Case reference allocation clause only where it describes the `a.`
reference as a secondary reference on the Inspection + Audit Case. The
sequence allocation rule, the single numbering implementation and every
other ADR-0002 clause are unchanged. FRD-01 owns the behaviour.

## Context

ADR-0002 allocated one principal/year sequence per Case and derived `a.` from
the same base reference "including the secondary Audit reference
created by an Inspection + Audit case": one Case carrying two references, with
the Engineer creating the Audit subfolder in Box by hand (FRD-01 as it stood).

The v26 planning round found that an Audit is worked, reported and listed as
its own piece of work: it starts in Review after the Inspection report exists,
has its own lifecycle, appears on the Work Centre as a new Case and needs its
own working-set tab, report and Box folder. A second reference on one record
could carry none of that.

## Decision

1. **Create audit creates a Case.** An Inspection + Audit Case, once a report
   has been generated on it, offers Create audit inside an edit session. The
   action creates one linked Audit Case of type Audit, starting in Review with
   the original's Engineer, carrying the original's Case data, assessment,
   figures, files and estimate forward.
2. **One sequence, two records.** The Audit Case keeps the Principal and the
   original's sequence number; no second sequence is consumed. Its reference
   is its own `a.{Case/PO}` for every assessment outcome. The assessment is
   recorded on the Audit Case rather than encoded in its identity. The operator
   is never asked.
   Neither reference changes or is reused, a second Create audit is refused,
   and the two Cases link to each other permanently.
3. **Shared bytes, nested custody root.** The Audit Case's files are the
   original's stored bytes by reference through the existing logical
   occurrence/version boundary; nothing is copied. Its Box custody root is the
   `a.` subfolder under the original Case's folder, created by Pegasus
   through the custody port when the Audit Case is created. The parent is the
   persisted relationship, never inferred from the reference prefix, and the
   Audit Case's own root is that subfolder from then on.
4. **Intake routes are unchanged.** A standalone Audit instruction still
   allocates its own Case/PO under FRD-01; this decision covers only the Audit
   created from an existing Inspection + Audit Case.

## Consequences

- ADR-0002's "secondary Audit reference" wording no longer describes the
  product; an Audit reference is a Case reference.
- The custody port gains one operation (create the linked Audit root) and one
  rule (resolve an Audit root through its original). Adapters without a folder
  hierarchy refuse it rather than flatten it.
- The Engineer's hand-made Box subfolder in the earlier FRD-01 wording is
  replaced by Pegasus creating it; existing flat Case folders are untouched.
- Case lists, Search, the Work Centre and the working set treat the Audit
  Case as any other Case; FRD-12 owns how it is shown.

## Links

- [FRD-01 — Case identity and lifecycle](../frd/frd-01-case-identity-and-lifecycle.md)
- [FRD-05 — Documents, extraction and custody](../frd/frd-05-documents-extraction-and-custody.md#custody-and-derived-reads)
- [ADR-0002](0002-dotnet-modular-monolith-on-azure.md)
- [ADR-0045](0045-document-custody-and-derived-caches.md)
- Provenance: [v26 planning, Create audit](../../design/planning-and-old-designs/v26_planning/pages/cases/case-record/dialogs/create-audit/how-it-should-work.md)

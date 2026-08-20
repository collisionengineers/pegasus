# Proof — PLAT-010

## Merge

PR #431, merge commit `dd99884723b152743ed7ffaaf215e92aef420d5e` on `dev`/`main`.

## Deployment

Shipped in **release 13** (`2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`,
deployed 2026-08-20 ~01:10–01:20Z). `dd998847` is a verified ancestor of
`2325ed4a`. See [[DELIV-012]] proof (Appendix — Release 13) for the
deployment readbacks.

## Production evidence

Production DOM probes are clean per [[DELIV-012]] proof and this ticket's
own verified assertions: `hasReceiptCopy=false, hasIntake=false` on the
Upload page (the specific INTK-010 handoff), and this ticket's own
132/132 Web tests plus 37/37 Browser/AccessibilityTests green after the
estate-wide copy strip (29 Razor pages plus 3 code-behind files brought
into `docs/design/README.md:160-161` compliance).

## Honest qualification

- **Scope carve-outs mid-task**: `Pages/Unidentified/{Index,Details}.cshtml`
  and `Pages/{Upload,UploadStatus}.cshtml`/`site.js` were carved out to
  [[INTK-009]] and [[INTK-010]] respectively (both release 13, same night);
  edits already made to those files were reverted before this ticket's
  commit, so this ticket's own diff does not touch them.
- **Two raw-GUID leaks found but NOT fixed**, reported rather than silently
  dropped: `Administration/Automation/Activity.cshtml:65` (`@record.SubjectId`)
  and `Cases/Shared/_CaseSummary.cshtml:208` (`@approval.ApprovedBy.SubjectId`)
  — both need a query/handler-layer change outside this copy-only ticket's
  scope; flagged as follow-up findings, not claimed fixed.
- `custody` terminology and "AI"/"Send to Claude" control names were
  deliberately left unchanged as established, approved vocabulary.

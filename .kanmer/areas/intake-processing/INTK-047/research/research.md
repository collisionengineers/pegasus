# Research — INTK-047

Wave 2 lane G of [[EPIC-011]]. Base `55e23b02` (`origin/dev`), worktree
`../pegasus-worktrees/intk-047-upload-pages`.

## Premises verified by read-only check

| Premise | Check | Result |
| --- | --- | --- |
| [[INTK-001]] is already under me | `git merge-base --is-ancestor 6c648c59 HEAD` | ancestor — PR #620 is on my base |
| `QueuedIntakeStatus` carries `RetryDueAtUtc`, not `CaseId` | `DurableIntake.cs:87-100` | confirmed |
| The design-system Upload block already exists | `site.css:519-546` | `.dropzone`, `.file-list`, `.file-row`, `.spinner`, `.progress`, `progress.progress`, `.upload-outcome`, `.accepted-list` all shipped by PLAT-029 |
| Those classes have no caller today | `grep -rn "file-list\|\"file-row\|progress" --include=*.cshtml Pages/` and site.js | zero — PLAT-029 shipped the vocabulary for this lane to consume |
| The upload pages are unported | `grep -rl page-heading` | `.page-heading` is not defined anywhere in `site.css`; the four upload surfaces still use it |
| The classes they use are wave-5 legacy | `awk 'NR<851'` split of `site.css` at the `/* ==== LEGACY (wave 5 deletes) ==== */` marker | `form-column`, `primary-action`, `secondary-action`, `detail-list`, `status-card`, `icon--lg`, `icon--spin`, `dropzone__file-row*` are legacy-only |
| No in-flight lane owns my files | `git diff --name-only origin/dev...<branch>` for PLAT-025/026/027, PLAT-049, ENG-027, DELIV-034, CASE-027 | only `Presentation/OperatorLabels.cs` is shared; nothing touches `site.js`, `site.css`, `Upload*`, `Pages/Uploads/**` |
| No branch or worktree exists for TICK-223 | `git branch --list 'task/*'`, `git worktree list` | none — `site.js` has no in-flight claimant |
| The real upload limits | `Core/Intake/IntakeContracts.cs:13,42` | `MaximumContentLength = 10 MB`, `MaximumBatchFileCount = 20` — **not** the prototype's "25 MB / 10 files" |
| The public page's Core view withholds identity | `RequestUploadPolicy.cs:306-308` | `RequestUploadPublicView(AllowedMediaTypes, MaximumFileBytes)` — no reference, no expiry |
| The prototype's effective final layer | `Pegasus_UI_Assessment_Refined.html:1486-1487` (last monkey-patch pass) | Upload = header + one panel (no section heading, no "what happens next"); public = auth card, "Secure file request", dropzone, Submit files |

## Assumed, not verified

- That the wave-5 lane (UIIMP-009) will promote the four legacy rules named in
  `files.md` rather than delete them. I report it; I cannot prove it.
- Visual fidelity at 1580/1100/760. The lane does not run browser walks
  (`context.md`: subagents do not run browser tests); the orchestrator's wave
  loop owns that proof.

## What the contract requires (context.md §1.10, design README §Upload)

Upload: header only; dropzone ("Drag files here or choose files" · type/limit
line · "Choose files" dark); file rows (status chip, progress, per-file
outcome); Upload (primary) + Clear. Public: external shell, company logo,
"Secure file request", heading, request ref + expiry, dropzone, Submit files.

## Three places the drawn contract cannot be ported verbatim

1. **"up to 25 MB each · 10 files"** is prototype fixture data. The real limits
   are 10 MB and 20 files, and the ticket's own verification condition is
   "upload limits unchanged". The line is rendered from
   `IntakeEnvelopeLimits` through `OperatorLabels.FileSize`.
2. **"request ref + expiry" on the public page.** FRD-02 §Request-scoped
   upload links: *"The public page exposes only the bound request's upload
   fields and its immediate structured success or failure. It exposes no case
   or reference identity, request/history state..."*, and the design README's
   own §Access table repeats it. `RequestUploadPublicView` deliberately
   carries neither. The FRD is the behavioural authority and the design README
   is downstream of it (`CLAUDE.md` §Documentation model), so the reference and
   expiry are **not** ported. Recorded as a reviewed divergence.
3. **"Submit files"** (plural) on the public page. `IUploadToRequest` accepts
   one file per attempt and returns one `RequestUploadDecision`; multi-file
   public upload is a Core capability change, not a port. The control is
   labelled for the arity the product actually has.

## What INTK-001 changed under me

`QueuedIntakeStatus` lost `CaseId` and gained `RetryDueAtUtc`;
`Presentation/UploadStatusRefresh.cs` became the one owner of the reload
cadence; `UploadStatus.cshtml` lost its lede paragraphs and renders the
duplicate fact as a labelled value; `site.js`'s auto-refresh timer is
visibility-aware. All of that is behaviour I keep untouched — this lane is the
restyle INTK-001 deliberately did not do ("No page was ported to the design
system. That is INTK-047's scope").

## Where the drawn `<progress>` has to live

The rows are built client-side from `input.files`, in the `[data-dropzone]`
block of `wwwroot/js/site.js` (PLAT-029's file, merged, no in-flight
claimant). There is no server-side seam for them. FRD-12 §Upload forbids a
finer per-file signal than the response actually proves, so the honest form of
the drawn progress bar is an **indeterminate** native `<progress>` — no
fabricated percentage, exactly what the element means.

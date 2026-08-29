## 2026-08-29 — post-merge defect found by the release local gate

`pwsh ./scripts/Test-UiCatalogue.ps1` **fails on merged `dev`** with exit code 1:

```
Routed Razor source is not classified:
src/Pegasus.Web/Pages/Administration/Accounts/Confirm.cshtml
```

PLAT-027 added that page in `f9dffa48` ("make staff actions safe and
script-free") and did not add its entry to `docs/design/test-ui/catalogue.json`.
It is a genuinely visual page — `@page "{operation}/{staffId:guid}"` rendering a
`<section class="panel">` with the disable-consequence notice and a reason form —
so it needs `classification: "visual"` with at least one captured state, not a
`protocol`/`redirect`/`download` reason.

### Why no gate caught it

**`Test-UiCatalogue.ps1` is not referenced anywhere in
`.github/workflows/ci.yml`.** It is a local-gate-only script today, so a routed
page can be added without classification and CI stays green. That is precisely
the hole [[UIIMP-005]] exists to close by putting the Test UI snapshot gate into
CI — which also means this failure would turn PR #609 red the moment it lands if
it is not fixed first.

A second, smaller trap worth recording: the script emits `Write-Error` and then
`throw`s, so it *does* exit 1 — but a shell that pipes it (`... | tail`) reads
the pipe's exit status instead and reports 0. "Verify with exit codes" only works
if the exit code you read is the script's.

### Disposition

**Fix in the [[UIIMP-005]] lane, not here.** The catalogue gate requires each
`visual` state's `file` to exist as a real prototype under
`docs/design/test-ui/pages/`, so the entry cannot be added without capturing the
snapshot, and snapshot capture and regeneration is exactly what UIIMP-005 owns.
That lane already has to regenerate the whole corpus against final `dev` before
#609 merges; this page joins that regeneration and gets its catalogue entry in
the same change.

Recorded here so the omission is attributed to the ticket that made it, and
tracked on [[DELIV-037]] as a blocking item for the release local gate.

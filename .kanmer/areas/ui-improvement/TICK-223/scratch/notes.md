- 2026-08-28 CASE-026 review disposition: this ticket also owns the
  site.js copy-by-delegation fix — `[data-copy-target]` binds once at load
  (site.js:51-68), so a Copy button inside a script-swapped preview never
  binds and the copyable reference lags the previewed facts after a
  client-side row swap (observed on the Search page's selected-case pane).
  Same file and root-cause family as the dialog-trigger delegation fix.

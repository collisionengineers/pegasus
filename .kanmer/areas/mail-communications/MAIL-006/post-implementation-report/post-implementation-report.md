# Post-implementation report — MAIL-006

Delivered as planned on `task/mail-006-inbox-message-page` (PR #498, with
[[MAIL-008]] and [[PLAT-019]]). Deviations and finds against the plan:

- **Two defects found during the visual pass and fixed:** the legacy
  `.mail-body` pre-wrap rule leaked into the paragraph body (scoped to
  `pre.mail-body`), and the receipt-less excerpt fallback showed the raw
  forwarded-header block (now skipped via the same split).
- **CSP find:** the deployed `default-src 'self'` policy discards inline
  scripts — `_ReasonDialog`'s binding (and the dev association dialogs) was
  dead in Production; binding moved to `site.js`. Recorded on [[PLAT-019]].
- **Folder-move contract preserved exactly:** expected versions travel in the
  action URL and only the reason posts ("WithoutPostingTransportIdentity"
  held without change).
- **Case tab deep-links:** a case query or picked target auto-selects the tab;
  association handlers land back on it.
- Body checklist status: all verification items green — artboard match at
  1280×800 with everything above the fold (screenshots in ticket scratch);
  no policy key/version/predicate/reason prose in response HTML (asserted in
  `MailWorkspaceWebTests`); `Open case` gone, `Filed to` links; all six POST
  handlers exercised end to end incl. optimistic concurrency and the
  uncertain-move replay; no inline `style` (AccessibilityTests); Release
  build 0/0; mail suites 96/96, Core 869/869, browser lane green (one
  parallel-run flake passing in isolation).
- **Held for the operator:** MAIL-008 label wording sign-off — the PR does
  not merge until the wording is confirmed or corrected.

Self-reviewed; subagents barred by operator directive (deviation noted).

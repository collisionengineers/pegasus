# Research — rebuild /Inbox/{id} on the record container

The approved design (`docs/design/references/mockups/inbox-message-page/`, 8
artboards + previews, drawn against origin/dev) is the exact target; this
research verified the mechanics it sits on. Premises checked read-only against
origin/dev and the live estate:

- **Verified:** all six POST handlers (`PrepareLinkCase`, `PrepareUnlinkCase`,
  `LinkCase`, `UnlinkCase`, `CorrectClassification`,
  `MoveToRecommendedFolder`) live in `Message.cshtml.cs` and are view-agnostic;
  the redesign is a view restructure plus redirect targets.
- **Verified:** `.record`, `.tabs`, `.status-chip`, `.queue-list`,
  `.reason-dialog`, `.prov`, `.facts` all exist in `site.css`; only
  `.decision`, the `.mail-*` letter set, `.form-column`, and the facts
  one-column pin are new (as the design README states).
- **Verified (defect found):** the deployed CSP is `default-src 'self'` —
  inline scripts are discarded in Production. `_ReasonDialog.cshtml` carried
  its dialog binding as an inline script, so the dev mail association dialogs
  would have shipped dead. The binding moves to `site.js` (single owner).
- **Verified:** `MailWorkspaceWebTests` pins the old markup in ~18 sites; the
  association helpers are handler-driven regexes over
  `<form method="post" action=…>` — attribute adjacency matters, and the
  folder-move contract carries expected versions in the action URL with only
  the reason in the posted body ("WithoutPostingTransportIdentity").
- **Verified:** effective-sender data is sound end to end (live route
  decisions carry the original QDOS sender for every retained forward); the
  From-cell desk subline and the raw-wrapper excerpt are display-only defects.
- **Assumed:** MAIL-008's label wordings ("New instruction · Inspection"
  format) are the design's proposals, implemented as proposed and flagged for
  operator settlement at review.

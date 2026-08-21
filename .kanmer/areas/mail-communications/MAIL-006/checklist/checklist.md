# Checklist — MAIL-006

- [x] Core: `SplitForwardedHeader` + facts
- [x] MAIL-008: `OperatorLabels.MailClassification` + selection/decision label reuse
- [x] PLAT-019: `_ReasonDialog` copy removal; dialog binding + Other toggle into site.js (CSP fix)
- [x] `MailBodyPresentation` (quoted header, run-on paragraphs)
- [x] `Message.cshtml` rebuilt: record head + chip, four tabs, split-main, Decision/Corrections cards, Case tab, dialogs
- [x] Association redirects land on the Case tab; case query auto-selects it
- [x] Move dialog: URL-carried expectations, reason-only body (contract preserved)
- [x] Index From cell subline removed; excerpt from cleaned original body
- [x] site.css/site.js additions; `node --check` green
- [x] `MailWorkspaceWebTests` + `RetainedMailPersistenceTests` reconciled (68/68 after fixes)
- [ ] Full-solution Release build 0/0 + Core tests + AccessibilityTests + Browser mail lane
- [ ] Visual QA against artboards at 1280 (local DevelopmentOffline run)
- [ ] PR to dev; merge on green CI; tickets → review → verifying

## Progress notes

2026-08-21: implementation complete on the branch; last four failures were the
folder-move contract (expected versions belong in the action URL, not the
posted body) — restored, 4/4 green in isolation.

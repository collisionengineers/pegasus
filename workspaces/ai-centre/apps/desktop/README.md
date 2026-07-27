# Windows desktop workstation

This folder will contain the full case-centred Windows application: navigation, case workspace,
evidence viewer, agent activity, review/approval surfaces, report preview, accessibility, secure
local state, diagnostics, updates, and connector consent.

No framework has been selected yet. Complete the spike and decision in
`docs/adr/0002-windows-desktop-stack.md` before creating the application project. The selection must
test Windows deployment/update, WebView or native rendering, PDF/image viewing, drag-and-drop,
secure delegated authentication, accessibility, offline recovery, and automated UI testing.

Business logic belongs in shared packages and agent/skill contracts, not in view code.

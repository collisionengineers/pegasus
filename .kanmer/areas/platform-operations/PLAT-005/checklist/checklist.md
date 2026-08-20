# Checklist — PLAT-005

- [ ] Start a documented Offline run and record the repository revision, run identity, base URL, and successful Status/Smoke checks.
- [ ] Create the capture manifest with the route matrix and fixed viewport settings.
- [ ] Capture the authenticated rail at 1280×720 and constrained 1024×768 / 512×768 views.
- [ ] Capture Dashboard, Inbox, Queues, Cases, Case Details, Assessment, Administration, and Upload as real local rendered routes.
- [ ] Inspect every screenshot for rail/navigation, H1, marks beside text, broken-image indicators, overflow/clipping, and non-colour state labels; record honest unavailable/empty states.
- [ ] Review screenshots and manifest for credentials, document text, personal data, and other unnecessary sensitive material before retention.
- [ ] Run the Browser-tagged integration lane, or record its exact prerequisite failure.
- [ ] Stop the local stack and write the post-implementation report/proof with artifacts, routes, commands, findings, and any linked follow-up.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)

- [ ] **Blocked 2026-08-20:** the supported Offline Start lifecycle is unable to classify a missing Windows LocalDB instance because this LocalDB build exits 0 while reporting “doesn't exist”. Start therefore fails its ownership guard before the web application launches; Status/Smoke and browser capture cannot proceed. [[PLAT-014]] owns the fail-closed detection fix. Re-run this checklist from the first step after that ticket is verified.

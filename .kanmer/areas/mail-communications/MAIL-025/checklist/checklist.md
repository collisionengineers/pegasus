# Checklist — MAIL-025

- [ ] Core: scope `UnreadOnly`/`OldestFirst`, summary attachment names, `CountAsync` on port and use case
- [ ] EF: `ApplyScope`, `CountAsync`, sort, attachment names; Core test fake compiles
- [ ] Inbox list page on the pane vocabulary with scope counts, sort, bounded pagination, `?selected=` preview + `_Preview`
- [ ] Message page: header, record head, tablist, decision card, timeline, attachments Preview, thread, case tab, `[data-dialog]` dialogs
- [ ] Tests retargeted at equal strength (web + browser)
- [ ] `catalogue.json` branch text; `Test-UiCatalogue.ps1` passes
- [ ] `dotnet build ./Pegasus.slnx --configuration Release` clean
- [ ] Merge origin/dev; simplification pass recorded; post-implementation report; PR to dev

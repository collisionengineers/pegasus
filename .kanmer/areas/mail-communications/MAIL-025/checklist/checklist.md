# Checklist — MAIL-025

- [x] Core: `UnreadOnly`/`OldestFirst`/`CountAsync` kept, unused
      `AttachmentFileNames` removed; `ListRetainedMail.CountAsync` reuses
      Normalize + authorization.
- [x] Infrastructure: `CountAsync` implemented on the shared filter builder;
      unread + oldest-first applied in `ListAsync`.
- [x] Inbox list: header "Inbox"/"Retained mail", filter bar (Mailbox,
      Folder, Queue, search, Search dark, `data-auto-submit`), scope rail
      with 7 scopes (icon well + count), messages pane (sort toggle, unread
      dot, sender, date/time, subject, excerpt, outcome chip, caseRef/queue ·
      attachments, bounded pagination), server-rendered preview pane
      (subject, route, chip, excerpt, attachment chips, fact grid, Open full
      message, Open linked Case).
- [x] Message page: header subject/"Inbox message"/Back to Inbox; record
      head; tabs Message / Attachments (n) / Thread / Case; decision card;
      corrections timeline; attachments table; case tab summary + association
      machinery.
- [x] Record bar Reply/Forward/Compose/Flag/Delete omitted (wave 4, no
      handlers); attachment Preview column omitted (no handler, not a D7
      seam).
- [x] Existing handlers keep antiforgery, version and reason behaviour.
- [x] No new CSS file, no inline styles, no page-specific classes beyond the
      PLAT-029 vocabulary.
- [x] Tests updated to ported markup; behaviour assertions preserved; Core
      fake implements `CountAsync`; new list-surface test added.
- [x] `dotnet restore --locked-mode` + Release build green (no test run).
- [ ] No clipped text/overflow at 1580/1100/760 (pane classes carry the
      breakpoints; browser walk belongs to the orchestrator).
- [x] Simplification pass recorded under dated heading in plan.
- [x] PR open to `dev`; ticket moved to Review with post-implementation
      report.

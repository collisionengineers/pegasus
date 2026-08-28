## Review fix — 2026-08-28 (PR #597 MINOR)

`unread` and `sort` now survive the whole message round-trip, not only the
two named links: MessageModel binds both (strict parse in
`TryParseListContext`, reusing `IndexModel.TryParseUnread`/`TryParseSort`,
so forged values 404 before any mutation like the other list-context
params), and every context-carrying surface re-emits them — Back to Inbox,
all four section tabs, thread rows, case-search form/rows, the
Prepare/Confirm link and unlink forms and their `Url.Page` dialog actions,
the correction and move dialog forms (including the uncertain-move recovery
form and the move dialog's C# action), the preview pane's "Open full
message" link on the list, and all four `RedirectToPage` targets in the
handlers. `ScopingAndPagingCarryTheMailboxFolderAndPageForward` extended:
list with `unread=true&sort=oldest&pageNumber=2` carries the pair into the
message, and Back and the Attachments tab carry them back. Build green
(0 warnings); tests not run per lane rules.

## CI fixes — 2026-08-28 (PR #597, commit 40a1920e)

- **Sort-arrow assertion**: the page rendered `Received &#x2191;`, not the
  raw arrow. Root cause: Razor writes markup text literally but runs
  expressions through the HTML encoder, and the toggle's arrow is an
  expression (`@(Model.OldestFirst ? "↑" : "↓")`), same encoder behaviour
  the passing `&#xB7;` assertion already proves. Markup is correct (the
  operator sees the glyph); the test now asserts the encoded form —
  `>Received &#x2193;` on the default page, `>Received &#x2191;` and no
  `&#x2193;` on the flipped page, so the flip itself is still proven.
- **axe aria-allowed-attr**: the violation was `aria-selected` on the
  roleless message-row divs (the scope rail was already real
  `<button type="submit">` + GET forms, so its `aria-pressed` is legal).
  Rows dropped `aria-selected`; the selected row's subject link now carries
  `aria-current="true"` (CASE-025 / PR #596's lane solution for anchors).
  Swept both ported pages: `aria-pressed` only on the scope submit button,
  `aria-current` only on links, `aria-expanded` only on the subject link
  (legal for role=link; the shipped site.js preview pattern). No CSS was
  touched — the server-rendered selected-row highlight keyed on
  `[aria-selected]` is lost with the attribute; selection is conveyed by
  the link's aria-current and the preview pane (follow-up for the wave-5
  design-system pass if the highlight must return).

## CI fixes round 2 — 2026-08-28 (PR #597, commit 979fc771)

- **Sort-arrow bytes, read from the artifact instead of theory**: the branch's
  captured `docs/design/test-ui/pages/inbox--*.html` are pre-port renders
  (stale; regenerated once per merge), so the actual bytes were read from the
  compiled view literals in `Pegasus.Web.dll` (UTF-16 user-string heap). The
  toggle's literal chunk is `                    Received ` (indent + text +
  trailing space) followed by the encoded expression — so the page renders
  `>\r\n                    Received &#x2191;\r\n                </a>`. Both
  earlier failures shared one cause: the assertions assumed `>` abuts
  "Received", which never happens. The entity is hex, as the same-pipeline
  passing `&#xB7;` assertion already proved. Test now asserts
  `Received &#x2191;` (flipped, plus `DoesNotContain("&#x2193;")`) and
  `Received &#x2193;` (default) — flip still proven both states.
- **Browser :55** turned out to be the desktop side-by-side layout check:
  `[data-mail-preview]` top-aligned with the messages pane. In the ported
  markup the preview article sits inside the third pane below its drawn
  "Message preview" pane-head, so it starts ~46px lower — the old assertion
  pinned the preview container as a grid child, a surface the port
  deliberately replaced with the pane wrapper. Not a layout regression (the
  port renders as drawn); the check was retargeted to the intended
  equivalence — pane 2 vs pane 3 top-aligned and side-by-side — and
  strengthened with `preview.left >= messages.right` for the inner article.
  The mobile stacking check was retargeted the same way (pane 3 below
  pane 2). No page markup changed in this commit.

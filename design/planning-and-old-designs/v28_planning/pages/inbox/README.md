# Inbox

- **Mockup route:** `pegasus_mail_upload_v28.html` (`mu-area=mail`) in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Mail/Index.cshtml`, `Message.cshtml`, `Compose.cshtml`, `Shared/_ComposeForm.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s31-mail-inbox-1580.png](../../current/v28-shots/s31-mail-inbox-1580.png) · [1440](../../current/v28-shots/s31-mail-inbox-1440.png) · [760](../../current/v28-shots/s31-mail-inbox-760.png)
- [s32-mail-message-1580.png](../../current/v28-shots/s32-mail-message-1580.png) · [1440](../../current/v28-shots/s32-mail-message-1440.png) · [760](../../current/v28-shots/s32-mail-message-760.png)
- [s33-mail-compose-1580.png](../../current/v28-shots/s33-mail-compose-1580.png) · [1440](../../current/v28-shots/s33-mail-compose-1440.png) · [760](../../current/v28-shots/s33-mail-compose-760.png)

## Notes

- The scope rail (`All incoming`, `Receiving work`, `Case updates`,
  `Pre-instructions`, `Unidentified`, `Sent Items`, `Dismissed`) is
  transcribed once with `All incoming` pressed. The live rail's
  `aria-pressed` state tracks the actual Folder/Category/Dismissed
  combination in the URL; reproducing every scope's pressed state would mean
  duplicating the rail seven times for no additional content, so this capture
  shows only the default and documents the mechanism in prose instead.
- Deleted Items is reached live only through the Folder filter, and its
  three notice states (unavailable, truncated-at-100, no match) only ever
  render once a search term has been submitted — the folder has no
  un-searched "browse" state. The mockup's `mu-list-scope=deleted` branch
  always shows the already-searched state for this reason.
- The mail Category filter's "Categories" optgroup enumerates
  `MailClassificationSelection`'s confirmed subtypes live, filtered to the
  ones whose destination is `DetailedClassification`; that destination
  partition was not independently re-derived from
  `MailOperationalDestinationPolicy` for this capture, so the mockup shows a
  representative subset (General, Billing, Not client related, Internal CC)
  rather than the exact policy-filtered list.
- The attachments-tab table shows two representative outcome rows (`Case
  created`, `Could not be read`) rather than all five outcome words the
  source comment names (`Case created`, `Unidentified`, `Vehicle images`,
  `Could not be read`, `Processing failed`) — the row shape is identical for
  each, so the other three are text-only variants, not additional markup.
- `Reply`/`Reply all`/`Forward` open an inline panel on the Message record
  itself (`Message.cshtml`'s own `Reply`/`ReplyAll`/`Forward` handlers) — a
  distinct mechanism from the separate `/Mail/Compose` dialog reached from
  the Inbox list's Compose button. Both are captured, as two different
  `mu-*` states, because they are two different live forms.
- No public/external upload link exists on any of these three pages, live —
  see [`../upload/how-it-works.md`](../upload/how-it-works.md) for the
  PR #789 removal this confirms.

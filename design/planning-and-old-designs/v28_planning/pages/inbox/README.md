# Inbox

- **Live source:** `src/Pegasus.Web/Pages/Mail/Index.cshtml`, `src/Pegasus.Web/Pages/Mail/Message.cshtml`, `src/Pegasus.Web/Pages/Mail/Compose.cshtml`, `src/Pegasus.Web/Pages/Mail/Shared/_ComposeForm.cshtml`, `src/Pegasus.Web/wwwroot/css/inbox.css`
- [**How it works**](how-it-works.md)

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Inbox, all incoming | `/Inbox` | [frame](../../current/pegasus_mail_upload_v28.html#inbox) · [page](../../current/states/inbox.html) | [1580](../../current/v28-shots/s33-inbox-1580.png) · [1440](../../current/v28-shots/s33-inbox-1440.png) · [760](../../current/v28-shots/s33-inbox-760.png) |
| Inbox, message selected | `/Inbox?mailbox=49f47eb9-c5b0-464f-b8f0-8c90ba061728&selected=0c6ff7c1-b0c0-48c1-803b-dcc531f91f6e` | [frame](../../current/pegasus_mail_upload_v28.html#inbox-selected) · [page](../../current/states/inbox-selected.html) | [1580](../../current/v28-shots/s34-inbox-selected-1580.png) · [1440](../../current/v28-shots/s34-inbox-selected-1440.png) · [760](../../current/v28-shots/s34-inbox-selected-760.png) |
| Inbox, Receiving work | `/Inbox?queue=receiving-work` | [frame](../../current/pegasus_mail_upload_v28.html#inbox-receiving-work) · [page](../../current/states/inbox-receiving-work.html) | [1580](../../current/v28-shots/s35-inbox-receiving-work-1580.png) · [1440](../../current/v28-shots/s35-inbox-receiving-work-1440.png) · [760](../../current/v28-shots/s35-inbox-receiving-work-760.png) |
| Inbox, oldest first | `/Inbox?sort=oldest` | [frame](../../current/pegasus_mail_upload_v28.html#inbox-oldest) · [page](../../current/states/inbox-oldest.html) | [1580](../../current/v28-shots/s36-inbox-oldest-1580.png) · [1440](../../current/v28-shots/s36-inbox-oldest-1440.png) · [760](../../current/v28-shots/s36-inbox-oldest-760.png) |
| Message record | `/Inbox/0c6ff7c1-b0c0-48c1-803b-dcc531f91f6e?mailbox=49f47eb9-c5b0-464f-b8f0-8c90ba061728` | [frame](../../current/pegasus_mail_upload_v28.html#mail-message) · [page](../../current/states/mail-message.html) | [1580](../../current/v28-shots/s37-mail-message-1580.png) · [1440](../../current/v28-shots/s37-mail-message-1440.png) · [760](../../current/v28-shots/s37-mail-message-760.png) |
| Compose | `/Inbox/Compose` | [frame](../../current/pegasus_mail_upload_v28.html#mail-compose) · [page](../../current/states/mail-compose.html) | [1580](../../current/v28-shots/s38-mail-compose-1580.png) · [1440](../../current/v28-shots/s38-mail-compose-1440.png) · [760](../../current/v28-shots/s38-mail-compose-760.png) |
| Compose for a Case | `/Inbox/Compose?caseReference=QDOS31001` | [frame](../../current/pegasus_mail_upload_v28.html#mail-compose-for-case) · [page](../../current/states/mail-compose-for-case.html) | [1580](../../current/v28-shots/s39-mail-compose-for-case-1580.png) · [1440](../../current/v28-shots/s39-mail-compose-for-case-1440.png) · [760](../../current/v28-shots/s39-mail-compose-for-case-760.png) |

## Not captured

- A processed message with a classification, a linked Case, attachments with outcomes, a thread, and the move-folder, link and unlink dialogs. The fixture holds one unprocessed message.
- Sent Items, Dismissed and Deleted Items with content, and the Submitted and Unknown send states.

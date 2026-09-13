# Inbox — how it should work

Decided with the operator on 13 September 2026. Inbox's own planning beyond these points is still to come.

- **Category, not Queue.** The filter the mockup labels "Queue" is the mail category (the vocabulary administered under Administration › Mail categories). It is labelled **Category**, and its empty option reads "All categories".
- **No Unread scope.** Read state is not a queue. The scope list is: All incoming, Receiving work, Case updates, Pre-instructions, Unidentified, Sent Items. Unread messages are still shown bold; there is no separate list of them.
- **Dismiss.** A message row and the message record gain a Dismiss action (an icon on the row, a button on the record). Dismissing removes the message from the incoming list without classifying or linking it. Dismissed messages are reachable under a **Dismissed** scope and can be restored from there. Nothing is deleted: Dismiss is the retained mail moving to a Dismissed logical folder, the same mechanism as "Move to recommended folder", and it is logged.

Open:
- Whether Dismiss on a message that has an open Unidentified item is allowed, or the item must be closed first.

# Administration — wireframe

Grouped card index, one-line job-focused copy. 1280px+.

## Main state

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus  Dashboard Inbox Upload Queues Cases Administration* |
|                                                     alex · Change password · Sign out
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Administration                                                                    |
|                                                                                    |
|  PEOPLE AND ACCESS                                                                 |
|  +----------------------+  +----------------------+  +----------------------+      |
|  | [i] Staff accounts   |  | [i] Staff roles      |  | [i] Access review    |      |
|  | Add staff, disable   |  | Set who is an        |  | Check who has access |      |
|  | access, and reset    |  | Administrator,       |  | and record the       |      |
|  | first-sign-in        |  | Engineer, or User.   |  | review.              |      |
|  | passwords.           |  |                      |  |                      |      |
|  +----------------------+  +----------------------+  +----------------------+      |
|                                                                                    |
|  ORGANISATIONS AND PRINCIPALS                                                      |
|  +----------------------+  +----------------------+                                |
|  | [i] Organizations    |  | [i] Principals       |                                |
|  | Manage work providers|  | Add principals and   |                                |
|  | and instruction      |  | replace them when    |                                |
|  | intermediaries.      |  | they change.         |                                |
|  +----------------------+  +----------------------+                                |
|                                                                                    |
|  SYSTEM                                                                            |
|  +----------------------+  +----------------------+  +----------------------+      |
|  | [i] Workflow         |  | [i] Approved         |  | [i] Automation       |      |
|  |     configuration    |  |     mailboxes        |  | See what runs        |      |
|  | Set the checks a case|  | Choose the mailbox   |  | automatically and    |      |
|  | must pass before it  |  | addresses Pegasus    |  | switch it on or off. |      |
|  | goes to an Engineer. |  | accepts e-mail from  |  |                      |      |
|  |                      |  | and sends from.      |  |                      |      |
|  +----------------------+  +----------------------+  +----------------------+      |
+------------------------------------------------------------------------------------+
```

## Non-default state — restricted account (no administration rights)

Accounts without administration rights never see the nav item or this page; there is no
"disabled card" state (standards §4.9 — disabled is never visible). A direct URL visit
renders the styled access-denied page:

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues Cases                |
|                                                     alex · Change password · Sign out
+------------------------------------------------------------------------------------+
|                                                                                    |
|            You do not have access to this page.                                    |
|            [ Back to Dashboard ]                                                   |
|                                                                                    |
+------------------------------------------------------------------------------------+
```

## Legend

- `*` — active nav item.
- `UPPERCASE` — group heading (H2 rendered as section label), three groups:
  People and access / Organisations and principals / System.
- `[i]` — card icon. The card title is the link; the whole card is the click target.
- Every description is one job-focused sentence; guard-rail detail lives on the destination
  pages at the point of action.

---
category: Record
---

A two-column definition list (`.detail-list`): a 10rem muted term column, bold values, and a hairline between rows (none after the last). Use it for a block of read-only details about one thing — a received message, a case's identity on the assessment screen, a rate card in use — where the values need weight and there is no provenance or action per row (that is `DataRow`).

**Rules**

- Four to eight term/value pairs; longer lists become a table.
- A value that does not exist is an em-dash with visually-hidden text (`<span aria-hidden="true">—</span><span className="vh">Not assigned</span>`), never an empty `<dd>`.
- Terms are short (`From`, `Mailbox`, `Received`, `Attachments`); values are the settled business value or office time.
- Constrain the width in a panel or record body (it fills its container); do not use it for identity facts inside a record head — that is `Facts`.

**Examples**

```tsx
<DetailList
  items={[
    { term: 'From', value: 'claims.engineering@axa-insurance.co.uk' },
    { term: 'Mailbox', value: 'Inbox' },
    { term: 'Received', value: '12 Aug 09:14' },
    { term: 'Attachments', value: '3 (2 PDF, 1 image)' },
  ]}
/>
```

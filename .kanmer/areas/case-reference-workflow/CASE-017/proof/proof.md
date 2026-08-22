# Proof

**Shipped:** PR #506 (`5414997d`) and PR #513 (`336b28f9`) ·
**Deployed:** Release 22, `191ddf33`, smoke-asserted source SHA, revision
`pegasus-prod-web-252ow37gij--191ddf334208` at 100% traffic.

## Verified in rendered output

Run locally under `DevelopmentOffline` against LocalDB — the permitted
verification route, with no Outlook or Box involvement and no production data.
The case page and its Notes tab were rendered and read back as HTML.

**The tab is named Notes, not History:**

```html
<a href="/Cases/{id}?tab=history"> Notes <span class="count">1</span> </a>
<h2 id="case-notes-title" class="section-label">Notes</h2>
```

**An operator can add a note, and it appears on the timeline:**

```
POST /Cases/{id}/Tasks?handler=AddNote  ->  302

Notes 1
When                 Event  Actor                              Detail
22 Aug 2026 07:36    Note   development-offline-administrator  Chased the third-party engineer for the original report.
```

Four things that only a rendered check proves:

- the event renders through the operator-label map as **Note**, never the raw
  `operator_note` event code;
- the time is Europe/London formatted, not a raw `DateTimeOffset`;
- the actor is attributed, so a note is distinguishable from a system entry —
  the ticket's own requirement;
- the note is one row on the same timeline as system entries, not a second list.

**The form obeys the design rules:** a label and a control, no hint sentence, no
format guidance; `required` and `maxlength="2000"` carried as attributes rather
than prose.

**Banned vocabulary:** none. The rendered case page and Notes tab were scanned
for `intake`, `bounded`, `projection`, `lease`, `opaque`, `ingress`, `composed`,
`artifact`, `durable`, `aggregate`, `caller`, `bytes` and `Immutable` — zero
occurrences. The same scan over the rendered Dashboard, Cases, Inbox, Triage,
Search, Operations and Administration pages is also clean, which closes the
rendered half of [[CASE-016]].

## The defect this found

The first implementation wrote notes to a table the tab does not read. It was
invisible to CI and to source review; only running the page exposed it. Full
account in the second post-implementation report and in scratch. Fixed and
re-verified on the same route before this proof was written.

## Append-only

Notes cannot be edited or deleted afterwards: `CaseWorkflowEvents` carries no
update or delete path, and both runtime roles are denied `DELETE` on it by the
least-privilege baseline.

## Evidence tier

**Observed in rendered output** for every claim above. The deployed revision is
smoke-asserted at the exact SHA; the rendering was verified locally at the same
source, because the production case workspace needs an authenticated staff
sign-in I must not perform.

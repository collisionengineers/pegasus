# Proof

**Shipped:** PR #490, merge `4baae5f0`, fix commit `6b7c62a4` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

Registration, claimant and stage were rendered **outside** the Case-result anchor, so they
were excluded from keyboard focus and from the accessible name. A screen-reader user
tabbing the results heard the reference alone and had nothing to choose between.

## Verified in the shipped markup

`src/Pegasus.Web/Pages/Mail/Message.cshtml:472-481`, on the deployed revision — one anchor
per result, with every fact inside it:

```html
<a asp-page="/Mail/Message" … asp-route-targetCaseId="@candidate.CaseId"
   aria-current="@(Model.TargetCase?.Summary.CaseId == candidate.CaseId ? "page" : null)">
    <span><strong>@candidate.Reference</strong></span>
    <span>@(candidate.Registration ?? "Not recorded") · @(candidate.Claimant ?? "Not recorded")</span>
    <span class="queue-list__end">@candidate.Stage</span>
</a>
```

Checked against the three bullets:

- **Exactly one selection target per result** — the `<a>` is the only focusable element in
  the row; the three `<span>`s are not interactive.
- **Its accessible name includes reference, registration, claimant and stage** — all four
  are text content *inside* the anchor, so they are concatenated into its accessible name.
  Absent values render `Not recorded` rather than collapsing, so the name never becomes
  ambiguous.
- **Existing search, reviewed summary and return context remain intact** — every route
  value (`mailbox`, `folder`, `pageNumber`, `search`, `queue`, `section`, `caseQuery`) is
  carried on the link, so selecting a result preserves where the operator came from.

`aria-current="page"` marks the selected result, so current selection is exposed
non-visually. No second component and no client-side framework were introduced — the
finding's constraint held.

## Not claimed

This is verified by reading the deployed markup. It has **not** been through the recorded
screen-reader, forced-colours and 200%-zoom inspection that `docs/design/README.md` requires
of a UI capability at acceptance. That inspection is a separate obligation and is not
claimed here.

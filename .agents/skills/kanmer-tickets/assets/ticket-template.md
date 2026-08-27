# Ticket body template

*The ticket body. Not a plan — this states the **problem and its shape**; how it will be solved belongs in the plan document.*

Use this structure for `create_item` bodies with `type: "ticket"`. Keep
sections that apply; drop ones that don'''t. Frontmatter fields (title, status,
area, profile, groups, labels, links, refs) are tool parameters, not body
content.

Pick the `profile` deliberately — it is what decides how much evidence this
ticket will owe. A two-line fix filed as a `feature` owes six documents nobody
needs.

---

## What

One or two sentences: the concrete change or outcome this ticket delivers.

## Why

The problem or need driving it. Reference other tickets inline with
`[[API-003]]`-style wiki-links. The deeper material — findings, file survey,
plan, checklist, proof — lives in this ticket's own documents
(`set_ticket_doc`), not in the body.

## Approach

- Bullet steps or key decisions. Short — the ticket is a work item, not a design doc.

## Verification

- [ ] How to check this is done (command, test, observable behaviour).

## Outcome

Filled at closeout: PR link, merge date, follow-up ticket ids, anything that
shipped differently than planned. (In-flight notes go in checklist.md's
Progress notes, not here.)

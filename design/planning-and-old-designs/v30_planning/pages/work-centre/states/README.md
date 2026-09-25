# Work Centre: states

## Three-design pass — 25 September 2026

The separate A/B/C files support `?state=default`, `mine`, `filtered`,
`empty`, `stale`, `partial`, `unavailable`, `assign`, `conflict`,
`new-cases`, `ai-jobs` and `quiet`. The review strip selects each state.

The filtered fixture enables Held + Review. Stale retains the last-good
data; partial makes AI jobs unavailable while the other two sections stay
current; unavailable suppresses the unread attention counts. Empty uses
real zero counts and omits all empty sections. Quiet has no Overdue items,
New cases or AI jobs; only populated due groups remain. Assignment conflict
changes no fixture.

See the [proposals](../../../current/work-centre-design-proposals.md#how-to-try-them).

| State | Query | Shows |
| --- | --- | --- |
| Default | `?state=default` | Office scope, nine items in three groups, the Unassigned item selected |
| Mine | `?state=mine` | The Mine scope: the signed-in person's items and the unowned ones they can take |
| Filtered | `?state=filtered` | Held and Review chips on, **All kinds** offered |
| Empty | `?state=empty` | "Nothing needs attention", no new Case, no AI job |
| Unavailable | `?state=unavailable` | The attention section's warning notice; New cases and AI jobs still current |
| Assign | `?state=assign` | The Assign Engineer dialog open over the default state |

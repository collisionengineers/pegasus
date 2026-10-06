# v34 discussion log

Chronological. Not design authority.

## 6 October 2026: the round is asked for

While walking PR 1015's decision points, the operator was asked how the three screens the PR adds (the Report sending panel, the Tasks section and the delivery form) should be accepted, since none had been through a mockup round. The operator chose "Mockup round for all three: A full Stage 1 round before any of the three screens is accepted; the PR stays held until then."

The same day's rulings (recorded in the PR description and the FRDs on the branch) reshaped the screens before the round began: delivery became one step (PR 1053), every Principal has rules, 'Instruction mentions' is searched rather than asked, removals win, only a rule's Stop takes an override reason, Tasks gained Assign, open tasks became the visible blocker, and the Work Centre gained an Open tasks row.

## 6 October 2026: what was built

The branch was rebuilt to the rulings and CI went green on `4803fa7c4`. The mockups were then captured from the server's own renders through temporary hooks in the integration tests (`captured/capture-test.patch`), so each state is the page as the as-built Razor draws it. The build inlines the live shell's assets; the self-check passed with no console error on 53 checks.

Points that needed a choice to build, and that the operator had not ruled on, were taken as plainly as possible and put on the sign-off list rather than decided: the Work Centre labels (Q), which task is first (R), the Assign control (J), the bare Override reason box when a Stop is only possible (C), and the two sentences reworded from "prepare" to "send" (D). Two as-built defects seen in the captures were listed for the operator rather than silently fixed (A, B).

Open: every lettered item in `v34-notes.md` section 6.

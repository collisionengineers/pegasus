# Captured frames

The two Case page frames are the v36 round's captures of the running synthetic visual host on 9 October 2026 (`origin/dev` 37c4b96f5): `frame-engineer-read.html` and `frame-engineer-edit.html` are `document.documentElement.outerHTML` after `site.js` and `case-workspace.js` ran, copied from [`../../../v36_planning/current/captured/`](../../../v36_planning/current/captured/README.md). `images/` holds the four synthetic tile images the host served, inlined by the build.

The frames supply the shell, ribbon, aside and every section but one. The Repair Spec section is not read from them: the build and the runtime render it from the fixture in [`../lib/repair-spec-v38-model.mjs`](../lib/repair-spec-v38-model.mjs) with the classes and label strings of `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml` at `origin/dev` 6d47cd3cb (12 October 2026). The runtime also removes from the frames what the tip no longer draws: the Scroll/Tabs switch (PR 1145) and the Decisions metric strip (v36 item I). What else the frames still show as of 9 October (no Queries section, the pre-v36 Decisions radios, the blocker "how" line) is listed in the notes' known limits.

Synthetic fixtures only; no customer data.

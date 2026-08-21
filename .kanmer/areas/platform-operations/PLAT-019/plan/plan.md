# Plan — PLAT-019

Delivered on `task/mail-006-inbox-message-page` with [[MAIL-006]] (the only
current caller of the partial is the page that rebuild touches; landing them
apart would ship the dead copy or an unused mechanism in between).

1. Remove the `DialogConsequence` default, variable and render from
   `_ReasonDialog`; remove the `Required.` hint and the placeholder. Required
   state stays visual (`required` + marker), the reason label stays.
2. Sweep `DialogConsequence` from every call site (all four were on
   `/Inbox/{id}` and fell with the MAIL-006 rebuild); dialog titles carry the
   target ("Link to …", "Unlink from …", "Move to …").
3. Move the partial's inline binding script to `site.js` — CSP-safe single
   owner; behaviour (initial focus, containment, Escape, backdrop click,
   focus return) unchanged.
4. Verify: grep 0; accessibility suite; mail workspace suite green.

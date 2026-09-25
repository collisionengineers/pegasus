# Sign in: how it should work

Stage 1 proposals, **not yet decided**. The lettered items are in
[v30-notes.md](../../current/v30-notes.md#sign-off-list) (items H to K).

1. The card keeps its geometry: 440px, the red top border, one h1, two
   fields and one primary button. Nothing is added below the button.
2. The brand row is offered two ways: as today (96px mark, PEGASUS) or as
   the rail lockup (the mark at 64px, PEGASUS and "Case management"), so
   the sign-in card and the rail read as one product.
3. The h1 is offered as "Sign in to Pegasus" (today) or "Sign in", because
   the lockup already names the product.
4. The password field may carry a show-password control (the `eye` glyph as
   a 32px icon button inside the field). It changes no policy.
5. The error state shows the summary once, above the form, and the field
   errors under their fields, as today. No new sentence.
6. The forced-change card drops its explanatory paragraph (page economy);
   the h1 and the two fields are the whole card.

Open: H (brand row), I (title), J (show password), K (forced-change
paragraph).

## Initial login alternatives — 25 September 2026

The operator requested three complete aesthetic alternatives. The
[comparison](../../current/pegasus_signin_designs_v30.html) presents an
open light form, a split brand/form layout and a refined dark-surround card.
These are additional proposals; nothing has been decided with the operator.

Open: H2 selects the overall composition and refines H. I and J continue
to govern the shorter heading and password visibility. H3 covers the
Collision Engineers caption. See the
[proposal decisions](../../current/signin-design-proposals.md#review-decisions).

All three retain the same initial-login fields and exact validation
messages. A selected layout must be reconciled with the other navless
pages before a shared auth-frame implementation begins.


## Decided — 25 September 2026: B, whole navless family

Operator: design B (Brand split), "slight visual improvement/aesthetic, show
password button"; the frame applies to every `_LayoutAuth` page.

1. `_LayoutAuth` is two planes on a common centre line: the charcoal
   identity panel (3px red stripe, 96px refined mark, PEGASUS, Case
   management, Collision Engineers) at 42%, the white panel at 58% holding
   the page's card (360px; 720px for the consent screen). Below 980px the
   identity panel is a strip above the card with the 64px mark.
2. The heading is "Sign in" (I). Fields, labels, autocomplete, the exact
   required and refusal messages and the one primary button are unchanged;
   the refusal reads as the shared danger notice above the form.
3. The password field carries Show / Hide (J), shipped hidden and revealed
   by `site.js`; without script it is a password field.
4. Signed out, forced password change, access denied, the error family and
   the consent screen render in the same frame with their markup unchanged
   (K stays open: the forced-change paragraph remains).

Implemented in the Stage 2 PR from this branch; FRD-12 and the design
README carry the frame.
